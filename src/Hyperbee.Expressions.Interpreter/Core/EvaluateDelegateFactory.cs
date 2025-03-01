using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

// ReSharper disable NotAccessedPositionalProperty.Local

namespace Hyperbee.Expressions.Interpreter.Core;

public static class EvaluateDelegateFactory
{
    record DelegateClosure( XsInterpreter Interpreter, LambdaExpression Lambda );

    private static readonly ConcurrentDictionary<Type, DynamicMethod> CachedDynamicMethods = new();

    private static readonly MethodInfo EvaluateFuncMethod =
        typeof(XsInterpreter)
            .GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
            .First( x => x.Name == "Evaluate" && x.IsGenericMethodDefinition );

    private static readonly MethodInfo EvaluateActionMethod =
        typeof(XsInterpreter)
            .GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
            .First( x => x.Name == "Evaluate" && !x.IsGenericMethodDefinition );

    private static readonly PropertyInfo InterpreterProperty = typeof(DelegateClosure)
        .GetProperty( nameof(DelegateClosure.Interpreter), BindingFlags.Instance | BindingFlags.Public )!;

    private static readonly PropertyInfo LambdaProperty = typeof(DelegateClosure)
        .GetProperty( nameof(DelegateClosure.Lambda), BindingFlags.Instance | BindingFlags.Public )!;


    public static TDelegate CreateDelegate<TDelegate>( XsInterpreter instance, LambdaExpression lambda )
        where TDelegate : Delegate
    {
        var dm = CachedDynamicMethods.GetOrAdd( typeof(TDelegate), _ => CreateDynamicMethod<TDelegate>() );
        
        var closure = new DelegateClosure( instance, lambda );
        return (TDelegate) dm.CreateDelegate( typeof(TDelegate), closure );
    }

    private static DynamicMethod CreateDynamicMethod<TDelegate>() where TDelegate : Delegate
    {
        var invokeMethod = typeof( TDelegate ).GetMethod( "Invoke" );
        
        if ( invokeMethod is null )
            throw new InvalidOperationException( "Delegate type must have an Invoke method." );

        // Get delegate return and parameter types

        var returnType = invokeMethod.ReturnType;
        var paramInfos = invokeMethod.GetParameters();
        var paramTypes = new Type[paramInfos.Length + 1];
        
        paramTypes[0] = typeof( DelegateClosure );

        for ( var i = 0; i < paramInfos.Length; i++ )
            paramTypes[i + 1] = paramInfos[i].ParameterType;

        // Create a dynamic method
        
        var dm = new DynamicMethod( string.Empty, returnType, paramTypes, typeof( EvaluateDelegateFactory ).Module, true );
        var il = dm.GetILGenerator();

        // Map delegate parameters to an object[] array

        var paramArray = il.DeclareLocal( typeof( object[] ) );
        
        il.Emit( OpCodes.Ldc_I4, paramInfos.Length );
        il.Emit( OpCodes.Newarr, typeof( object ) );
        il.Emit( OpCodes.Stloc, paramArray );

        for ( var i = 0; i < paramInfos.Length; i++ )
        {
            il.Emit( OpCodes.Ldloc, paramArray );
            il.Emit( OpCodes.Ldc_I4, i );
            il.Emit( OpCodes.Ldarg, i + 1 );

            if ( paramInfos[i].ParameterType.IsByRef )
                il.Emit( OpCodes.Ldind_Ref );

            else if ( paramInfos[i].ParameterType.IsValueType )
                il.Emit( OpCodes.Box, paramInfos[i].ParameterType );

            il.Emit( OpCodes.Stelem_Ref );
        }

        // Load parameters and call Evaluate
        
        il.Emit( OpCodes.Ldarg_0 );
        il.Emit( OpCodes.Callvirt, InterpreterProperty.GetMethod! );
        il.Emit( OpCodes.Ldarg_0 );
        il.Emit( OpCodes.Callvirt, LambdaProperty.GetMethod! );
        il.Emit( OpCodes.Ldloc, paramArray );

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
