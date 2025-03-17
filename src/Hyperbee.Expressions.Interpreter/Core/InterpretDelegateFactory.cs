using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace Hyperbee.Expressions.Interpreter.Core;

public static class InterpretDelegateFactory
{
    private static readonly ConcurrentDictionary<Type, DynamicMethod> CachedDynamicMethods = new();

    private static readonly MethodInfo InterpretFuncMethod =
        typeof( InterpretDelegateClosure )
            .GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
            .First( x => x.Name == nameof( InterpretDelegateClosure.Interpret ) && x.IsGenericMethodDefinition );

    private static readonly MethodInfo InterpretActionMethod =
        typeof( InterpretDelegateClosure )
            .GetMethods( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
            .First( x => x.Name == nameof( InterpretDelegateClosure.Interpret ) && !x.IsGenericMethodDefinition );

    public static TDelegate CreateDelegate<TDelegate>( XsInterpreter instance, LambdaExpression lambda )
        where TDelegate : Delegate
    {
        var dm = CachedDynamicMethods.GetOrAdd( typeof( TDelegate ), _ => CreateDynamicMethod<TDelegate>() );

        var closure = new InterpretDelegateClosure( instance.Context, lambda );
        return (TDelegate) dm.CreateDelegate( typeof( TDelegate ), closure );
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

        paramTypes[0] = typeof( InterpretDelegateClosure );

        for ( var i = 0; i < paramInfos.Length; i++ )
            paramTypes[i + 1] = paramInfos[i].ParameterType;

        // Create a dynamic method

        var dm = new DynamicMethod( string.Empty, returnType, paramTypes, typeof( InterpretDelegateFactory ).Module, true );
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

        il.Emit( OpCodes.Ldarg_0 );             // Load closure
        il.Emit( OpCodes.Ldloc, paramArray );   // Load parameters

        if ( returnType == typeof( void ) )
        {
            il.Emit( OpCodes.Callvirt, InterpretActionMethod );
        }
        else
        {
            var genericEvalMethod = InterpretFuncMethod.MakeGenericMethod( returnType );
            il.Emit( OpCodes.Callvirt, genericEvalMethod );
        }

        // Return from the dynamic method

        il.Emit( OpCodes.Ret );
        return dm;
    }
}
