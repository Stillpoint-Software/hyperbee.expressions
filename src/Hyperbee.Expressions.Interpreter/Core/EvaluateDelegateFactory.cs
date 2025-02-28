using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.Expressions.Interpreter.Core;

public static class EvaluateDelegateFactory
{
    record Closure( XsInterpreter Interpreter, LambdaExpression Lambda );

    private static readonly ConcurrentDictionary<Type, DynamicMethod> CachedDynamicMethods = new();

    private static readonly MethodInfo EvaluateFuncMethod =
        typeof(XsInterpreter)
            .GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
            .First( x => x.Name == "Evaluate" && x.IsGenericMethodDefinition );

    private static readonly MethodInfo EvaluateActionMethod =
        typeof(XsInterpreter)
            .GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
            .First( x => x.Name == "Evaluate" && !x.IsGenericMethodDefinition );

    public static TDelegate CreateDelegate<TDelegate>( XsInterpreter instance, LambdaExpression lambda )
        where TDelegate : Delegate
    {
        var dm = CachedDynamicMethods.GetOrAdd( typeof(TDelegate), _ => CreateDynamicMethod<TDelegate>() );
        
        var closure = new Closure( instance, lambda );
        return (TDelegate) dm.CreateDelegate( typeof(TDelegate), closure );
    }

    // Create a dynamic method for the given delegate type
    private static DynamicMethod CreateDynamicMethod<TDelegate>() where TDelegate : Delegate
    {
        var invokeMethod = typeof( TDelegate ).GetMethod( "Invoke" );
        
        if ( invokeMethod is null )
            throw new InvalidOperationException( "Delegate type must have an Invoke method." );

        // Get delegate return and parameter types

        var returnType = invokeMethod.ReturnType;

        var paramInfos = invokeMethod.GetParameters();
        var paramTypes = new Type[paramInfos.Length + 1];
        
        paramTypes[0] = typeof( Closure );

        for ( var i = 0; i < paramInfos.Length; i++ )
            paramTypes[i + 1] = paramInfos[i].ParameterType;

        // Create a dynamic method
        
        var dm = new DynamicMethod( string.Empty, returnType, paramTypes, typeof( EvaluateDelegateFactory ).Module, true );
        var il = dm.GetILGenerator();

        var objArrayLocal = il.DeclareLocal( typeof( object[] ) );
        
        il.Emit( OpCodes.Ldc_I4, paramInfos.Length );
        il.Emit( OpCodes.Newarr, typeof( object ) );
        il.Emit( OpCodes.Stloc, objArrayLocal );

        // Load the arguments into an object array

        for ( var i = 0; i < paramInfos.Length; i++ )
        {
            il.Emit( OpCodes.Ldloc, objArrayLocal );
            il.Emit( OpCodes.Ldc_I4, i );
            il.Emit( OpCodes.Ldarg, i + 1 );
            if ( paramInfos[i].ParameterType.IsValueType )
                il.Emit( OpCodes.Box, paramInfos[i].ParameterType );
            il.Emit( OpCodes.Stelem_Ref );
        }

        // Load the closure instance
        
        il.Emit( OpCodes.Ldarg_0 );

        // Get the Interpreter property of the closure and call it
        
        var instanceProperty = typeof( Closure ).GetProperty( "Interpreter", BindingFlags.Instance | BindingFlags.Public );
        il.Emit( OpCodes.Callvirt, instanceProperty!.GetMethod! );
        il.Emit( OpCodes.Ldarg_0 );

        // Get the Lambda property of the closure and call it

        var lambdaProperty = typeof( Closure ).GetProperty( "Lambda", BindingFlags.Instance | BindingFlags.Public );
        il.Emit( OpCodes.Callvirt, lambdaProperty!.GetMethod! );
        il.Emit( OpCodes.Ldloc, objArrayLocal );

        // Call the appropriate Evaluate method based on the return type

        if ( returnType == typeof( void ) )
        {
            il.Emit( OpCodes.Callvirt, EvaluateActionMethod );
        }
        else
        {
            var genericEvalMethod = EvaluateFuncMethod.MakeGenericMethod( returnType );
            il.Emit( OpCodes.Callvirt, genericEvalMethod );
        }

        // Return from the dynamic method

        il.Emit( OpCodes.Ret );
        return dm;
    }
}

