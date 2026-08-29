using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.CompilerServices;

internal static class AwaitBinderFactory
{
    private static readonly ConcurrentDictionary<Type, AwaitBinder> Cache = new();

    const string GetResultName = "GetResult";
    const string GetAwaiterName = "GetAwaiter";
    const string ConfigureAwaitName = "ConfigureAwait";

    // Cached reflection members

    private static MethodInfo WaitMethod;
    private static MethodInfo WaitResultMethod;

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

    private static FieldInfo VoidResultInstance;

    private sealed class VoidResult : IVoidResult
    {
        public static readonly VoidResult Instance = new();
    }

    static AwaitBinderFactory()
    {
        // cache reflection member to reduce overhead
        CacheReflectionMembers();
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

            if ( ReflectionHelper.OpenGenericIsOrInherits( typeof( Task<> ), awaitableTypeDefinition ) )
                return CreateGenericTaskAwaitBinder( awaitableType );

            if ( ReflectionHelper.OpenGenericIsOrInherits( typeof( ValueTask<> ), awaitableTypeDefinition ) )
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
            awaitableType,
            WaitResultMethod.MakeGenericMethod( awaitableType, awaiterType, awaiterResultType ),
            GetAwaiterTaskResultMethod.MakeGenericMethod( awaiterResultType ),
            GetResultTaskResultMethod.MakeGenericMethod( awaiterResultType ) );
    }

    private static AwaitBinder CreateGenericValueTaskAwaitBinder( Type awaitableType )
    {
        var awaiterResultType = awaitableType.GetGenericArguments()[0];
        var awaiterType = typeof( ConfiguredValueTaskAwaitable<>.ConfiguredValueTaskAwaiter ).MakeGenericType( awaiterResultType );

        return new AwaitBinder(
            awaitableType,
            WaitResultMethod.MakeGenericMethod( awaitableType, awaiterType, awaiterResultType ),
            GetAwaiterValueTaskResultMethod.MakeGenericMethod( awaiterResultType ),
            GetResultValueTaskResultMethod.MakeGenericMethod( awaiterResultType )
        );
    }

    private static AwaitBinder CreateTaskAwaitBinder( Type awaitableType )
    {
        var awaiterType = typeof( ConfiguredTaskAwaitable.ConfiguredTaskAwaiter );

        return new AwaitBinder(
            awaitableType,
            WaitMethod.MakeGenericMethod( awaitableType, awaiterType ),
            GetAwaiterTaskMethod,
            GetResultTaskMethod );
    }

    private static AwaitBinder CreateValueTaskAwaitBinder( Type awaitableType )
    {
        var awaiterType = typeof( ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter );

        return new AwaitBinder(
            awaitableType,
            WaitMethod.MakeGenericMethod( awaitableType, awaiterType ),
            GetAwaiterValueTaskMethod,
            GetResultValueTaskMethod );
    }

    private static AwaitBinder CreateAwaitableTypeAwaitBinder( Type awaitableType )
    {
        const BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Find GetAwaiter method

        var getAwaiterImplMethod = awaitableType.GetMethod( GetAwaiterName, bindingAttr )
            ?? ReflectionHelper.FindExtensionMethod( awaitableType, GetAwaiterName )
            ?? throw new InvalidOperationException( $"The type {awaitableType} is not awaitable." );

        // Find GetResult method

        var getResultImplMethod = getAwaiterImplMethod.ReturnType.GetMethod( GetResultName, bindingAttr )
            ?? throw new InvalidOperationException( $"The awaiter for {awaitableType} does not have a {GetResultName} method." );

        //  IL-generated delegates

        var awaiterType = getAwaiterImplMethod.ReturnType;
        // ConfigureAwait belongs to the awaitable, as Task.ConfigureAwait does -- not to the
        // awaiter, which is where this used to look and so never found one.

        var configureAwaitImplMethod = awaitableType.GetMethod( ConfigureAwaitName, bindingAttr, [typeof( bool )] );

        var getAwaiterImplDelegate = CreateGetAwaiterImplDelegate( awaitableType, getAwaiterImplMethod, configureAwaitImplMethod );
        var getResultImplDelegate = CreateGetResultImplDelegate( awaiterType, getResultImplMethod );

        // Get the AwaitBinder methods

        MethodInfo waitMethod;
        MethodInfo getAwaiterMethod;
        MethodInfo getResultMethod;

        // The awaited type is what GetResult returns. It was read from the awaiter's first
        // generic argument, which agrees for an awaiter like MyAwaiter<int> but calls every
        // non-generic awaiter void -- including one whose GetResult returns a value.

        var awaiterResultType = getResultImplMethod.ReturnType;

        if ( awaiterResultType != typeof( void ) )
        {
            waitMethod = WaitResultMethod.MakeGenericMethod( awaitableType, awaiterType, awaiterResultType );
            getAwaiterMethod = GetAwaiterCustomMethod.MakeGenericMethod( awaitableType, awaiterType );
            getResultMethod = GetResultCustomResultMethod.MakeGenericMethod( awaiterType, awaiterResultType );
        }
        else
        {
            waitMethod = WaitMethod.MakeGenericMethod( awaitableType, awaiterType );
            getAwaiterMethod = GetAwaiterCustomMethod.MakeGenericMethod( awaitableType, awaiterType );
            getResultMethod = GetResultCustomMethod.MakeGenericMethod( awaiterType );
        }

        // Return the AwaitBinder

        return new AwaitBinder(
            awaitableType,
            waitMethod,
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

        // ConfigureAwait( false ), when the awaitable offers it and the awaitable it returns
        // yields the same awaiter type. The awaiter type is fixed by this delegate's
        // signature, so an awaitable whose configured form has a different awaiter cannot be
        // expressed here, and configureAwait is ignored for it.

        var configuredGetAwaiterMethod = FindConfiguredGetAwaiter( configureAwaitImplMethod, typeof( TAwaiter ) );

        if ( configuredGetAwaiterMethod != null )
        {
            var lblDefault = il.DefineLabel();

            il.Emit( OpCodes.Ldarg_1 );
            il.Emit( OpCodes.Brtrue_S, lblDefault );

            EmitLoadTarget( il, typeof( TAwaitable ), configureAwaitImplMethod );
            il.Emit( OpCodes.Ldc_I4_0 );
            EmitCall( il, typeof( TAwaitable ), configureAwaitImplMethod );

            var configuredType = configureAwaitImplMethod.ReturnType;

            if ( configuredType.IsValueType )
            {
                // An instance method on a struct needs an address, and the configured
                // awaitable is on the stack by value.

                il.DeclareLocal( configuredType );
                il.Emit( OpCodes.Stloc_0 );
                il.Emit( OpCodes.Ldloca_S, (byte) 0 );
                il.Emit( OpCodes.Call, configuredGetAwaiterMethod );
            }
            else
            {
                il.Emit( OpCodes.Callvirt, configuredGetAwaiterMethod );
            }

            il.Emit( OpCodes.Ret );

            // Each path returns, so the two never merge at different stack depths.

            il.MarkLabel( lblDefault );
        }

        // Call GetAwaiter()
        //
        // Arg 0 is a managed pointer to the awaitable. An instance method on a struct is
        // called on that pointer directly; a reference has to be loaded out of it first.

        EmitLoadTarget( il, typeof( TAwaitable ), getAwaiterImplMethod );
        EmitCall( il, typeof( TAwaitable ), getAwaiterImplMethod );

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

        // Arg 0 is a managed pointer to the awaiter, which is a struct far more often than
        // not -- TaskAwaiter and YieldAwaitable's awaiter both are.

        EmitLoadTarget( il, typeof( TAwaiter ), getResultImplMethod );
        EmitCall( il, typeof( TAwaiter ), getResultImplMethod );

        if ( typeof( TResult ) == typeof( IVoidResult ) )
        {
            il.Emit( OpCodes.Ldsfld, VoidResultInstance );
        }
        else
        {
            il.DeclareLocal( typeof( TResult ) );
            il.Emit( OpCodes.Stloc_0 );
            il.Emit( OpCodes.Ldloc_0 );
        }

        il.Emit( OpCodes.Ret );

        return dynamicMethod.CreateDelegate( typeof( AwaitBinderGetResultDelegate<TAwaiter, TResult> ) );
    }

    // The GetAwaiter of whatever ConfigureAwait returns, when it produces the awaiter type
    // this thunk is committed to. Null means configureAwait cannot be honored.

    private static MethodInfo FindConfiguredGetAwaiter( MethodInfo configureAwaitImplMethod, Type awaiterType )
    {
        const BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var getAwaiterMethod = configureAwaitImplMethod?.ReturnType.GetMethod( GetAwaiterName, bindingAttr, [] );

        return getAwaiterMethod?.ReturnType == awaiterType ? getAwaiterMethod : null;
    }

    // Loads the target of an instance or extension call from arg 0, which is always a
    // managed pointer to the awaitable or awaiter.

    private static void EmitLoadTarget( ILGenerator il, Type targetType, MethodInfo method )
    {
        il.Emit( OpCodes.Ldarg_0 );

        if ( targetType.IsValueType )
        {
            // A struct instance method takes the pointer as-is. An extension method takes
            // the struct by value, so copy it out.

            if ( method.IsStatic )
                il.Emit( OpCodes.Ldobj, targetType );

            return;
        }

        // A reference lives behind the pointer, so read it out.

        il.Emit( OpCodes.Ldind_Ref );

        // An instance call has to prove the reference's type. An extension method declares
        // the parameter it accepts, and its declaring type is the static class holding it,
        // which the reference is not.

        if ( !method.IsStatic )
            il.Emit( OpCodes.Castclass, method.DeclaringType! );
    }

    private static void EmitCall( ILGenerator il, Type targetType, MethodInfo method )
    {
        // Callvirt on a value type is not valid, and there is nothing to dispatch on: the
        // exact type is known here.

        il.Emit( method.IsStatic || targetType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, method );
    }

    private static void CacheReflectionMembers()
    {
        // VoidResult

        VoidResultInstance = typeof( VoidResult ).GetField( nameof( VoidResult.Instance ) );

        // Await methods

        ReflectionHelper.GetMethods(
            typeof( AwaitBinder ),
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            ( name, method, matches ) =>
            {
                switch ( name )
                {
                    case nameof( AwaitBinder.Wait )
                        when matches( [null, typeof( bool )], argCount: 2 ):
                        WaitMethod = method;
                        break;

                    case nameof( AwaitBinder.WaitResult )
                        when matches( [null, typeof( bool )], argCount: 3 ):
                        WaitResultMethod = method;
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

        ReflectionHelper.GetMethods(
            typeof( AwaitBinderFactory ),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
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
