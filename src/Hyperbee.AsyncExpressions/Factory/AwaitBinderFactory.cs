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
    //private static readonly MethodInfo AwaitAwaitableMethod;
    //private static readonly MethodInfo AwaitAwaitableResultMethod;

    private static readonly MethodInfo GetAwaiterTaskMethod;
    private static readonly MethodInfo GetAwaiterTaskResultMethod;
    private static readonly MethodInfo GetAwaiterValueTaskMethod;
    private static readonly MethodInfo GetAwaiterValueTaskResultMethod;
    private static readonly MethodInfo GetResultTaskMethod;
    private static readonly MethodInfo GetResultTaskResultMethod;
    private static readonly MethodInfo GetResultValueTaskMethod;
    private static readonly MethodInfo GetResultValueTaskResultMethod;

    static AwaitBinderFactory()
    {
        // Pre-cache binder methods

        // Await methods

        //AwaitMethod = Reflection.GetOpenGenericMethod(
        //    typeof( AwaitBinder ),
        //    nameof(AwaitBinder.Await), 
        //    argCount: 1, 
        //    parameterTypes: [null, typeof( bool )],
        //    BindingFlags.Instance | BindingFlags.NonPublic
        //);

        //AwaitResultMethod = Reflection.GetOpenGenericMethod(
        //    typeof(AwaitBinder),
        //    nameof(AwaitBinder.AwaitResult),
        //    argCount: 2,
        //    parameterTypes: [null, typeof(bool)],
        //    BindingFlags.Instance | BindingFlags.NonPublic
        //);

        //AwaitAwaitableMethod = Reflection.GetOpenGenericMethod(
        AwaitMethod = Reflection.GetOpenGenericMethod(
            typeof(AwaitBinder),
            nameof(AwaitBinder.Await),
            argCount: 2,
            parameterTypes: [null, typeof(bool)],
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        //AwaitAwaitableResultMethod = Reflection.GetOpenGenericMethod(
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
    }

    public static void ClearCache()
    {
        Cache.Clear();
    }

    public static AwaitBinder GetOrCreate( Type targetType )
    {
        return Cache.GetOrAdd( targetType, Create );
    }

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
            AwaitResultMethod.MakeGenericMethod( awaitableType, awaiterType ),
            GetAwaiterTaskMethod.MakeGenericMethod(),
            GetResultTaskMethod.MakeGenericMethod() );
    }

    private static AwaitBinder CreateValueTaskAwaitBinder( Type awaitableType )
    {
        var awaiterType = typeof( ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter );

        return new AwaitBinder(
            AwaitResultMethod.MakeGenericMethod( awaitableType, awaiterType ),
            GetAwaiterValueTaskMethod.MakeGenericMethod(),
            GetResultValueTaskMethod.MakeGenericMethod() );
    }

    private static AwaitBinder CreateAwaitableTypeAwaitBinder( Type awaitableType )
    {
        const BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Find GetAwaiter method

        var getAwaiterImplMethod = awaitableType
            .GetMethod( GetAwaiterName, bindingAttr )
            ?? Reflection.FindExtensionMethod( awaitableType, GetAwaiterName );

        if ( getAwaiterImplMethod == null )
            throw new InvalidOperationException( $"The type {awaitableType} is not awaitable." );

        // Find GetResult method

        var getResultImplMethod = getAwaiterImplMethod.ReturnType.GetMethod( GetResultName, bindingAttr );

        if ( getResultImplMethod == null )
            throw new InvalidOperationException( $"The awaiter for {awaitableType} does not have a GetResult method." );

        // Find ConfigureAwait method

        var configureAwaitImplMethod = getAwaiterImplMethod.ReturnType.GetMethod( ConfigureAwaitName, bindingAttr, [typeof(bool)] );

        // Create the IL-generated GetAwaiter delegate

        var awaiterType = getAwaiterImplMethod.ReturnType;

        var getAwaiterImplDelegate = typeof(AwaitBinderFactory)
            .GetMethod( nameof(CreateGetAwaiterDelegate), BindingFlags.NonPublic | BindingFlags.Static )
            ?.MakeGenericMethod( awaitableType, awaiterType )
            .Invoke( null, [getAwaiterImplMethod, configureAwaitImplMethod] ) as Delegate;

        // Create the IL-generated GetResult delegate

        var resultImplType = getResultImplMethod.ReturnType == typeof(void)
            ? typeof(IVoidTaskResult)
            : getResultImplMethod.ReturnType;

        var getResultImplDelegate = typeof(AwaitBinderFactory)
            .GetMethod( nameof(CreateGetResultDelegate), BindingFlags.NonPublic | BindingFlags.Static )
            ?.MakeGenericMethod( awaiterType, resultImplType )
            .Invoke( null, [getResultImplMethod] ) as Delegate;

        // Get the AwaitBinder Await method

        var binderAwaitMethod = awaiterType.IsGenericType
            ? AwaitResultMethod.MakeGenericMethod( awaitableType, awaiterType, awaiterType.GetGenericArguments()[0] )
            : AwaitMethod.MakeGenericMethod( awaitableType, awaiterType );

        return new AwaitBinder(
            binderAwaitMethod,
            getAwaiterImplMethod, //BF fix - should be AwaitBinder.GetAwaiter
            getResultImplMethod, //BF fix - should be AwaitBinder.GetResult
            getAwaiterImplDelegate,
            getResultImplDelegate );
    }

    private static Delegate CreateGetAwaiterDelegate<TAwaitable, TAwaiter>( MethodInfo getAwaiterMethod, MethodInfo configureAwaitMethod )
    {
        var dynamicMethod = new DynamicMethod(
            name: getAwaiterMethod.Name,
            returnType: typeof(TAwaiter),
            parameterTypes: [typeof(TAwaitable), typeof(bool)],
            typeof(AwaitBinder).Module,
            skipVisibility: true );

        var il = dynamicMethod.GetILGenerator();

        // Call ConfigureAwait

        if ( configureAwaitMethod != null )
        {
            var lblSkipConfigureAwait = il.DefineLabel();

            il.Emit( OpCodes.Ldarg_1 );
            il.Emit( OpCodes.Brtrue_S, lblSkipConfigureAwait ); 

            if ( !configureAwaitMethod.IsStatic )
            {
                il.Emit( OpCodes.Ldarg_0 );
                il.Emit( OpCodes.Castclass, configureAwaitMethod.DeclaringType! );
            }

            il.Emit( OpCodes.Ldc_I4_0 ); // Load constant false
            il.Emit( configureAwaitMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, configureAwaitMethod );

            il.MarkLabel( lblSkipConfigureAwait );
        }

        // Call GetAwaiter()

        il.Emit( OpCodes.Ldarg_0 ); 

        if ( getAwaiterMethod.IsStatic )
        {
            il.Emit( OpCodes.Castclass, getAwaiterMethod.GetParameters()[0].ParameterType );
            il.Emit( OpCodes.Call, getAwaiterMethod );
        }
        else
        {
            il.Emit( OpCodes.Castclass, getAwaiterMethod.DeclaringType! );
            il.Emit( OpCodes.Callvirt, getAwaiterMethod );
        }

        il.Emit( OpCodes.Ret );

        return dynamicMethod.CreateDelegate( typeof(AwaitBinderGetAwaiterDelegate<TAwaitable, TAwaiter>) );
    }

    private static Delegate CreateGetResultDelegate<TAwaiter, TResult>( MethodInfo getResultMethod )
    {
        var dynamicMethod = new DynamicMethod(
            name: getResultMethod.Name,
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

        il.Emit( OpCodes.Call, getResultMethod );

        il.DeclareLocal( typeof(TResult) ); 
        il.Emit( OpCodes.Stloc_0 ); 
        il.Emit( OpCodes.Ldloc_0 );

        il.Emit( OpCodes.Ret );

        return dynamicMethod.CreateDelegate( typeof(AwaitBinderGetResultDelegate<TAwaiter, TResult>) );
    }
}
