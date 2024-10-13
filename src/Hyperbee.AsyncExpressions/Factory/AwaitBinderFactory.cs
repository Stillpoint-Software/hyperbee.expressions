using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Hyperbee.AsyncExpressions.Transformation;

namespace Hyperbee.AsyncExpressions.Factory;

internal static class AwaitBinderFactory
{
    private static readonly ConcurrentDictionary<Type, AwaitBinder> Cache = new();

    const string GetResultName = "GetResult";
    const string GetAwaiterName = "GetAwaiter";
    const string ConfigureAwaitName = "ConfigureAwait";

    // Pre-cached MethodInfo

    private static readonly MethodInfo AwaitMethod;
    private static readonly MethodInfo AwaitResultMethod;

    private static readonly MethodInfo GetAwaiterTaskMethod;
    private static readonly MethodInfo GetAwaiterTaskResultMethod;
    private static readonly MethodInfo GetAwaiterValueTaskMethod;
    private static readonly MethodInfo GetAwaiterValueTaskResultMethod;

    private static readonly MethodInfo GetResultTaskMethod;
    private static readonly MethodInfo GetResultTaskResultMethod;
    private static readonly MethodInfo GetResultValueTaskMethod;
    private static readonly MethodInfo GetResultValueTaskResultMethod;

    private static readonly MethodInfo CreateGetAwaiterImplDelegateMethod;
    private static readonly MethodInfo CreateGetResultImplDelegateMethod;

    static AwaitBinderFactory()
    {
        // Pre-cache binder methods

        // Await methods

        AwaitMethod = Reflection.GetOpenGenericMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.Await),
            argCount: 2,
            parameterTypes: [null, typeof(bool)],
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        AwaitResultMethod = Reflection.GetOpenGenericMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.AwaitResult),
            argCount: 3,
            parameterTypes: [null, typeof(bool)],
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        // GetAwaiter methods

        GetAwaiterTaskMethod = Reflection.GetMethod( 
            typeof( AwaitBinder ),
            nameof(AwaitBinder.GetAwaiter),
            [typeof(Task), typeof(bool)],
            BindingFlags.Static | BindingFlags.NonPublic
        );

        GetAwaiterValueTaskMethod = Reflection.GetMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.GetAwaiter),
            [typeof(ValueTask), typeof(bool)],
            BindingFlags.Static | BindingFlags.NonPublic
        );

        GetAwaiterTaskResultMethod = Reflection.GetOpenGenericMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.GetAwaiter),
            argCount: 1,
            parameterTypes: [typeof(Task<>), typeof(bool)],
            BindingFlags.Static | BindingFlags.NonPublic
        );

        GetAwaiterValueTaskResultMethod = Reflection.GetOpenGenericMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.GetAwaiter),
            argCount: 1,
            parameterTypes: [typeof(ValueTask<>), typeof(bool)],
            BindingFlags.Static | BindingFlags.NonPublic
        );

        // GetResult methods

        GetResultTaskMethod = Reflection.GetMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.GetResult),
            [typeof(ConfiguredTaskAwaitable.ConfiguredTaskAwaiter)],
            BindingFlags.Static | BindingFlags.NonPublic
        );

        GetResultValueTaskMethod = Reflection.GetMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.GetResult),
            [typeof(ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter)],
            BindingFlags.Static | BindingFlags.NonPublic
        );

        GetResultTaskResultMethod = Reflection.GetOpenGenericMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.GetResult),
            argCount: 1,
            parameterTypes: [typeof(ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter)],
            BindingFlags.Static | BindingFlags.NonPublic
        );

        GetResultValueTaskResultMethod = Reflection.GetOpenGenericMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.GetResult),
            argCount: 1,
            parameterTypes: [typeof(ConfiguredValueTaskAwaitable<>.ConfiguredValueTaskAwaiter)],
            BindingFlags.Static | BindingFlags.NonPublic
        );

        // Delegate creation methods

        CreateGetAwaiterImplDelegateMethod = Reflection.GetOpenGenericMethod(
            typeof( AwaitBinderFactory ),
            nameof( CreateGetAwaiterImplDelegate ),
            argCount: 2,
            parameterTypes: [typeof( MethodInfo ), typeof( MethodInfo )],
            BindingFlags.NonPublic | BindingFlags.Static
        );

        CreateGetResultImplDelegateMethod = Reflection.GetOpenGenericMethod(
            typeof( AwaitBinderFactory ),
            nameof( CreateGetResultImplDelegate ),
            argCount: 2,
            parameterTypes: [typeof( MethodInfo )],
            BindingFlags.NonPublic | BindingFlags.Static
        );
    }

    public static void ClearCache() => Cache.Clear();

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

            if ( Reflection.IsOrInheritsFromGeneric( typeof(Task<>), awaitableTypeDefinition ) )
            {
                return CreateGenericTaskAwaitBinder( awaitableType );
            }

            if ( Reflection.IsOrInheritsFromGeneric( typeof(ValueTask<>), awaitableTypeDefinition ) )
            {
                return CreateGenericValueTaskAwaitBinder( awaitableType );
            }
        }
        else
        {
            if ( awaitableType == typeof(Task) || awaitableType.IsSubclassOf( typeof(Task) ) )
            {
                return CreateTaskAwaitBinder( awaitableType );
            }

            if ( awaitableType == typeof(ValueTask) )
            {
                return CreateValueTaskAwaitBinder( awaitableType );
            }
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
        var configureAwaitImplMethod = getAwaiterImplMethod.ReturnType.GetMethod( ConfigureAwaitName, bindingAttr, [typeof(bool)] );

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
            getAwaiterMethod = GetAwaiterTaskResultMethod.MakeGenericMethod( awaiterResultType );
            getResultMethod = GetResultTaskResultMethod.MakeGenericMethod( awaiterResultType );
        }
        else
        {
            awaitMethod = AwaitMethod.MakeGenericMethod( awaitableType, awaiterType );
            getAwaiterMethod = GetAwaiterTaskMethod.MakeGenericMethod();
            getResultMethod = GetResultTaskMethod.MakeGenericMethod();
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
            returnType: typeof(TAwaiter),
            parameterTypes: [typeof(TAwaitable), typeof(bool)],
            typeof(AwaitBinder).Module,
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

        if ( getAwaiterImplMethod.IsStatic )
        {
            il.Emit( OpCodes.Castclass, getAwaiterImplMethod.GetParameters()[0].ParameterType );
            il.Emit( OpCodes.Call, getAwaiterImplMethod );
        }
        else
        {
            il.Emit( OpCodes.Castclass, getAwaiterImplMethod.DeclaringType! );
            il.Emit( OpCodes.Callvirt, getAwaiterImplMethod );
        }

        il.Emit( OpCodes.Ret );

        return dynamicMethod.CreateDelegate( typeof(AwaitBinderGetAwaiterDelegate<TAwaitable, TAwaiter>) );
    }

    private static Delegate CreateGetResultImplDelegate( Type awaiterType, MethodInfo getResultImplMethod )
    {
        var resultImplType = getResultImplMethod.ReturnType == typeof(void)
            ? typeof(IVoidTaskResult)
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
            returnType: typeof(TResult),
            parameterTypes: [typeof(TAwaiter)],
            typeof(AwaitBinder).Module,
            skipVisibility: true
        );

        var il = dynamicMethod.GetILGenerator();

        if ( typeof(TAwaiter).IsValueType )
            il.Emit( OpCodes.Ldarga_S, 0 ); 
        else
            il.Emit( OpCodes.Ldarg_0 ); 

        il.Emit( OpCodes.Call, getResultImplMethod );

        il.DeclareLocal( typeof(TResult) ); 
        il.Emit( OpCodes.Stloc_0 ); 
        il.Emit( OpCodes.Ldloc_0 );

        il.Emit( OpCodes.Ret );

        return dynamicMethod.CreateDelegate( typeof(AwaitBinderGetResultDelegate<TAwaiter, TResult>) );
    }
}
