using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Transformation;

internal static class AwaitBinderFactory
{
    private static readonly ConcurrentDictionary<Type, AwaitBinder> Cache = new();

    const string GetResultName = "GetResult";
    const string GetAwaiterName = "GetAwaiter";
    const string ConfigureAwaitName = "ConfigureAwait";

    // Pre-cached MethodInfo

    private static MethodInfo AwaitMethod;
    private static MethodInfo AwaitResultMethod;

    private static MethodInfo GetAwaiterTaskMethod;
    private static MethodInfo GetAwaiterTaskResultMethod;
    private static MethodInfo GetAwaiterValueTaskMethod;
    private static MethodInfo GetAwaiterValueTaskResultMethod;
    private static MethodInfo GetAwaiterCustomMethod;

    private static MethodInfo GetResultTaskMethod;
    private static MethodInfo GetResultTaskResultMethod;
    private static MethodInfo GetResultValueTaskMethod;
    private static MethodInfo GetResultValueTaskResultMethod;
    private static MethodInfo GetResultCustomMethod;
    private static MethodInfo GetResultCustomResultMethod;

    private static MethodInfo CreateGetAwaiterImplDelegateMethod;
    private static MethodInfo CreateGetResultImplDelegateMethod;

    static AwaitBinderFactory()
    {
        // Pre-cache MethodInfo to reduce reflection overhead
        PreCacheMethodInfo();
    }

    public static void Clear() => Cache.Clear();

    public static AwaitBinder GetOrCreate( Type targetType ) => Cache.GetOrAdd( targetType, Create );

    public static bool TryGetOrCreate( Type targetType, out AwaitBinder awaitBinder )
    {
        try
        {
            awaitBinder = Cache.GetOrAdd( targetType, Create );
            return true;
        }
        catch
        {
            awaitBinder = null;
            return false;
        }
    }

    private static AwaitBinder Create( Type awaitableType )
    {
        if ( awaitableType.IsGenericType )
        {
            var awaitableTypeDefinition = awaitableType.GetGenericTypeDefinition();

            if ( Reflection.OpenGenericIsOrInherits( typeof( Task<> ), awaitableTypeDefinition ) )
                return CreateGenericTaskAwaitBinder( awaitableType );

            if ( Reflection.OpenGenericIsOrInherits( typeof( ValueTask<> ), awaitableTypeDefinition ) )
                return CreateGenericValueTaskAwaitBinder( awaitableType );
        }
        else
        {
            if ( awaitableType == typeof( Task ) || awaitableType.IsSubclassOf( typeof( Task ) ) )
                return CreateTaskAwaitBinder( awaitableType );

            if ( awaitableType == typeof( ValueTask ) )
                return CreateValueTaskAwaitBinder( awaitableType );
        }

        return CreateAwaitableTypeAwaitBinder( awaitableType );
    }

    // Binder creation methods

    private static AwaitBinder CreateGenericTaskAwaitBinder( Type awaitableType )
    {
        var awaiterResultType = awaitableType.GetGenericArguments()[0];
        var awaiterType = typeof( ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter ).MakeGenericType( awaiterResultType );

        return new AwaitBinder(
            AwaitResultMethod.MakeGenericMethod( awaitableType, awaiterType, awaiterResultType ),
            GetAwaiterTaskResultMethod.MakeGenericMethod( awaiterResultType ),
            GetResultTaskResultMethod.MakeGenericMethod( awaiterResultType ) );
    }

    private static AwaitBinder CreateGenericValueTaskAwaitBinder( Type awaitableType )
    {
        var awaiterResultType = awaitableType.GetGenericArguments()[0];
        var awaiterType = typeof( ConfiguredValueTaskAwaitable<>.ConfiguredValueTaskAwaiter ).MakeGenericType( awaiterResultType );

        return new AwaitBinder(
            AwaitResultMethod.MakeGenericMethod( awaitableType, awaiterType, awaiterResultType ),
            GetAwaiterValueTaskResultMethod.MakeGenericMethod( awaiterResultType ),
            GetResultValueTaskResultMethod.MakeGenericMethod( awaiterResultType ) );
    }

    private static AwaitBinder CreateTaskAwaitBinder( Type awaitableType )
    {
        var awaiterType = typeof( ConfiguredTaskAwaitable.ConfiguredTaskAwaiter );

        return new AwaitBinder(
            AwaitMethod.MakeGenericMethod( awaitableType, awaiterType ),
            GetAwaiterTaskMethod,
            GetResultTaskMethod );
    }

    private static AwaitBinder CreateValueTaskAwaitBinder( Type awaitableType )
    {
        var awaiterType = typeof( ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter );

        return new AwaitBinder(
            AwaitMethod.MakeGenericMethod( awaitableType, awaiterType ),
            GetAwaiterValueTaskMethod,
            GetResultValueTaskMethod );
    }

    private static AwaitBinder CreateAwaitableTypeAwaitBinder( Type awaitableType )
    {
        const BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Find GetAwaiter method

        var getAwaiterImplMethod = awaitableType.GetMethod( GetAwaiterName, bindingAttr )
            ?? Reflection.FindExtensionMethod( awaitableType, GetAwaiterName )
            ?? throw new InvalidOperationException( $"The type {awaitableType} is not awaitable." );

        // Find GetResult method

        var getResultImplMethod = getAwaiterImplMethod.ReturnType.GetMethod( GetResultName, bindingAttr )
            ?? throw new InvalidOperationException( $"The awaiter for {awaitableType} does not have a {GetResultName} method." );

        //  IL-generated delegates

        var awaiterType = getAwaiterImplMethod.ReturnType;
        var configureAwaitImplMethod = getAwaiterImplMethod.ReturnType.GetMethod( ConfigureAwaitName, bindingAttr, [typeof( bool )] );

        var getAwaiterImplDelegate = CreateGetAwaiterImplDelegate( awaitableType, getAwaiterImplMethod, configureAwaitImplMethod );
        var getResultImplDelegate = CreateGetResultImplDelegate( awaiterType, getResultImplMethod );

        // Get the AwaitBinder methods

        MethodInfo awaitMethod;
        MethodInfo getAwaiterMethod;
        MethodInfo getResultMethod;

        if ( awaiterType.IsGenericType )
        {
            var awaiterResultType = awaiterType.GetGenericArguments()[0];

            awaitMethod = AwaitResultMethod.MakeGenericMethod( awaitableType, awaiterType, awaiterResultType );
            getAwaiterMethod = GetAwaiterCustomMethod.MakeGenericMethod( awaitableType, awaiterType );
            getResultMethod = GetResultCustomResultMethod.MakeGenericMethod( awaiterType, awaiterResultType );
        }
        else
        {
            awaitMethod = AwaitMethod.MakeGenericMethod( awaitableType, awaiterType );
            getAwaiterMethod = GetAwaiterCustomMethod.MakeGenericMethod( awaitableType, awaiterType );
            getResultMethod = GetResultCustomMethod.MakeGenericMethod( awaiterType );
        }

        // Return the AwaitBinder

        return new AwaitBinder(
            awaitMethod,
            getAwaiterMethod,
            getResultMethod,
            getAwaiterImplDelegate,
            getResultImplDelegate );
    }

    // Delegate creation methods

    private static Delegate CreateGetAwaiterImplDelegate( Type awaitableType, MethodInfo getAwaiterImplMethod, MethodInfo configureAwaitImplMethod )
    {
        var awaiterType = getAwaiterImplMethod.ReturnType;

        var getAwaiterImplDelegate = CreateGetAwaiterImplDelegateMethod
            .MakeGenericMethod( awaitableType, awaiterType )
            .Invoke( null, [getAwaiterImplMethod, configureAwaitImplMethod] ) as Delegate;

        return getAwaiterImplDelegate;
    }

    private static Delegate CreateGetAwaiterImplDelegate<TAwaitable, TAwaiter>( MethodInfo getAwaiterImplMethod, MethodInfo configureAwaitImplMethod )
    {
        var dynamicMethod = new DynamicMethod(
            name: getAwaiterImplMethod.Name,
            returnType: typeof( TAwaiter ),
            parameterTypes: [typeof( TAwaitable ).MakeByRefType(), typeof( bool )],
            typeof( AwaitBinder ).Module,
            skipVisibility: true );

        var il = dynamicMethod.GetILGenerator();

        // Call ConfigureAwait

        if ( configureAwaitImplMethod != null )
        {
            var lblSkipConfigureAwait = il.DefineLabel();

            il.Emit( OpCodes.Ldarg_1 );
            il.Emit( OpCodes.Brtrue_S, lblSkipConfigureAwait );

            if ( !configureAwaitImplMethod.IsStatic )
            {
                il.Emit( OpCodes.Ldarg_0 );
                il.Emit( OpCodes.Castclass, configureAwaitImplMethod.DeclaringType! );
            }

            il.Emit( OpCodes.Ldc_I4_0 ); // Load constant false

            if ( configureAwaitImplMethod.IsStatic )
                il.Emit( OpCodes.Call, configureAwaitImplMethod );
            else
                il.Emit( OpCodes.Callvirt, configureAwaitImplMethod );

            il.MarkLabel( lblSkipConfigureAwait );
        }

        // Call GetAwaiter()

        il.Emit( OpCodes.Ldarg_0 );
        il.Emit( OpCodes.Ldind_Ref );

        if ( getAwaiterImplMethod.IsStatic )
        {
            il.Emit( OpCodes.Call, getAwaiterImplMethod );
        }
        else
        {
            il.Emit( OpCodes.Castclass, getAwaiterImplMethod.DeclaringType! ); //BF Test Instance Type.
            il.Emit( OpCodes.Callvirt, getAwaiterImplMethod );
        }

        il.Emit( OpCodes.Ret );

        return dynamicMethod.CreateDelegate( typeof( AwaitBinderGetAwaiterDelegate<TAwaitable, TAwaiter> ) );
    }

    private static Delegate CreateGetResultImplDelegate( Type awaiterType, MethodInfo getResultImplMethod )
    {
        var resultImplType = getResultImplMethod.ReturnType == typeof( void )
            ? typeof( IVoidResult )
            : getResultImplMethod.ReturnType;

        var getResultImplDelegate = CreateGetResultImplDelegateMethod
            .MakeGenericMethod( awaiterType, resultImplType )
            .Invoke( null, [getResultImplMethod] ) as Delegate;

        return getResultImplDelegate;
    }

    private static Delegate CreateGetResultImplDelegate<TAwaiter, TResult>( MethodInfo getResultImplMethod )
    {
        var dynamicMethod = new DynamicMethod(
            name: getResultImplMethod.Name,
            returnType: typeof( TResult ),
            parameterTypes: [typeof( TAwaiter ).MakeByRefType()],
            typeof( AwaitBinder ).Module,
            skipVisibility: true
        );

        var il = dynamicMethod.GetILGenerator();

        il.Emit( OpCodes.Ldarg_0 );
        il.Emit( OpCodes.Call, getResultImplMethod );

        il.DeclareLocal( typeof( TResult ) );
        il.Emit( OpCodes.Stloc_0 );
        il.Emit( OpCodes.Ldloc_0 );

        il.Emit( OpCodes.Ret );

        return dynamicMethod.CreateDelegate( typeof( AwaitBinderGetResultDelegate<TAwaiter, TResult> ) );
    }

    // Pre-Cache factory MethodInfo

    private static void PreCacheMethodInfo()
    {
        // Await methods

        Reflection.GetMethods(
            typeof( AwaitBinder ),
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic,
            ( name, method, matches ) =>
            {
                switch ( name )
                {
                    case nameof( AwaitBinder.Await )
                        when matches( [null, typeof( bool )], argCount: 2 ):
                        AwaitMethod = method;
                        break;

                    case nameof( AwaitBinder.AwaitResult )
                        when matches( [null, typeof( bool )], argCount: 3 ):
                        AwaitResultMethod = method;
                        break;

                    case nameof( AwaitBinder.GetAwaiter )
                        when matches( [typeof( Task<> ).MakeByRefType(), typeof( bool )], argCount: 1 ):
                        GetAwaiterTaskResultMethod = method;
                        break;

                    case nameof( AwaitBinder.GetAwaiter )
                        when matches( [typeof( Task ).MakeByRefType(), typeof( bool )] ):
                        GetAwaiterTaskMethod = method;
                        break;

                    case nameof( AwaitBinder.GetAwaiter )
                        when matches( [typeof( ValueTask<> ).MakeByRefType(), typeof( bool )], argCount: 1 ):
                        GetAwaiterValueTaskResultMethod = method;
                        break;

                    case nameof( AwaitBinder.GetAwaiter )
                        when matches( [typeof( ValueTask ).MakeByRefType(), typeof( bool )] ):
                        GetAwaiterValueTaskMethod = method;
                        break;

                    case nameof( AwaitBinder.GetAwaiter ) // custom awaitable
                        when matches( [null, typeof( bool )], argCount: 2 ):
                        GetAwaiterCustomMethod = method;
                        break;

                    case nameof( AwaitBinder.GetResult )
                        when matches( [typeof( ConfiguredTaskAwaitable.ConfiguredTaskAwaiter ).MakeByRefType()] ):
                        GetResultTaskMethod = method;
                        break;

                    case nameof( AwaitBinder.GetResult )
                        when matches( [typeof( ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter ).MakeByRefType()] ):
                        GetResultValueTaskMethod = method;
                        break;

                    case nameof( AwaitBinder.GetResult )
                        when matches( [typeof( ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter ).MakeByRefType()], argCount: 1 ):
                        GetResultTaskResultMethod = method;
                        break;

                    case nameof( AwaitBinder.GetResult )
                        when matches( [typeof( ConfiguredValueTaskAwaitable<>.ConfiguredValueTaskAwaiter ).MakeByRefType()], argCount: 1 ):
                        GetResultValueTaskResultMethod = method;
                        break;

                    case nameof( AwaitBinder.GetResult ) // custom awaitable
                        when matches( [null], argCount: 1 ):
                        GetResultCustomMethod = method;
                        break;

                    case nameof( AwaitBinder.GetResult ) // custom awaitable
                        when matches( [null], argCount: 2 ):
                        GetResultCustomResultMethod = method;
                        break;
                }
            }
        );

        // Delegate creation methods

        Reflection.GetMethods(
            typeof( AwaitBinderFactory ),
            BindingFlags.Static | BindingFlags.NonPublic,
            ( name, method, matches ) =>
            {
                switch ( name )
                {
                    case nameof( CreateGetAwaiterImplDelegate )
                        when matches( [typeof( MethodInfo ), typeof( MethodInfo )], argCount: 2 ):
                        CreateGetAwaiterImplDelegateMethod = method;
                        break;

                    case nameof( CreateGetResultImplDelegate )
                        when matches( [typeof( MethodInfo )], argCount: 2 ):
                        CreateGetResultImplDelegateMethod = method;
                        break;
                }
            }
        );
    }
}
