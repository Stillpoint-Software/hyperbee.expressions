using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

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

    static AwaitBinderFactory()
    {
        // Pre-cache binder methods

        // Await methods
        const BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;

        AwaitMethod = GetBinderMethod( nameof(AwaitBinder.Await), bindingAttr );
        AwaitResultMethod = GetBinderMethod( nameof(AwaitBinder.AwaitResult), bindingAttr );

        // GetAwaiter methods
        GetAwaiterTaskMethod = GetBinderMethod( nameof(AwaitBinder.GetAwaiter), 
            [typeof(Task),typeof(bool)] 
        );

        GetAwaiterValueTaskMethod = GetBinderMethod( nameof(AwaitBinder.GetAwaiter), 
            [typeof(ValueTask), typeof(bool)] 
        );

        GetAwaiterTaskResultMethod = GetBinderGenericMethod( nameof(AwaitBinder.GetAwaiter), 
            [typeof(Task<>), typeof(bool)] 
        );

        GetAwaiterValueTaskResultMethod = GetBinderGenericMethod( nameof(AwaitBinder.GetAwaiter), 
            [typeof(ValueTask<>), typeof(bool)] 
        );

        // GetResult methods
        GetResultTaskMethod = GetBinderMethod( nameof(AwaitBinder.GetResult), 
            [typeof(ConfiguredTaskAwaitable.ConfiguredTaskAwaiter)] 
        );

        GetResultValueTaskMethod = GetBinderMethod( nameof(AwaitBinder.GetResult), 
            [typeof(ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter)] 
        );

        GetResultTaskResultMethod = GetBinderGenericMethod( nameof(AwaitBinder.GetResult), 
            [typeof(ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter)] 
        );

        GetResultValueTaskResultMethod = GetBinderGenericMethod( nameof(AwaitBinder.GetResult), 
            [typeof(ConfiguredValueTaskAwaitable<>.ConfiguredValueTaskAwaiter)] 
        );
    }

    private static MethodInfo GetBinderMethod( string name, BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.NonPublic )
    {
        return typeof(AwaitBinder).GetMethod( name, bindingAttr );
    }

    private static MethodInfo GetBinderMethod( string name, Type[] types, BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.NonPublic )
    {
        return typeof(AwaitBinder).GetMethod( name, bindingAttr, types );
    }

    private static MethodInfo GetBinderGenericMethod( string name, Type[] types, BindingFlags bindingAttr = BindingFlags.Static | BindingFlags.NonPublic )
    {
        return Reflection.GetGenericMethod( typeof( AwaitBinder ), name, types, bindingAttr );
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

    private static AwaitBinder Create( Type targetType )
    {
        // Task and ValueTask types

        if ( targetType.IsGenericType )
        {
            var targetTypeDefinition = targetType.GetGenericTypeDefinition();
            var typeArgument = targetType.GetGenericArguments()[0];

            if ( Reflection.IsOrInheritsFromGeneric( typeof(Task<>), targetTypeDefinition ) )
            {
                return new AwaitBinder(
                    AwaitResultMethod.MakeGenericMethod( targetType, typeArgument ), 
                    GetAwaiterTaskResultMethod.MakeGenericMethod( typeArgument ),
                    GetResultTaskResultMethod.MakeGenericMethod( typeArgument ) );
            }

            if ( Reflection.IsOrInheritsFromGeneric( typeof(ValueTask<>), targetTypeDefinition ) )
            {
                return new AwaitBinder(
                    AwaitResultMethod.MakeGenericMethod( targetType, typeArgument ), 
                    GetAwaiterValueTaskResultMethod.MakeGenericMethod( typeArgument ),
                    GetResultValueTaskResultMethod.MakeGenericMethod( typeArgument ) );
            }
        }
        else if ( targetType == typeof(Task) || targetType.IsSubclassOf( typeof(Task) ))
        {
            return new AwaitBinder(
                AwaitMethod.MakeGenericMethod( targetType ),
                GetAwaiterTaskMethod,
                GetResultTaskMethod );
        }
        else if ( targetType == typeof(ValueTask) )
        {
            return new AwaitBinder(
                AwaitMethod.MakeGenericMethod( targetType ),
                GetAwaiterValueTaskMethod,
                GetResultValueTaskMethod );
        }

        // other awaitable types

        // Create delegates for GetAwaiter and GetResult Implementations

        const BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var getAwaiterImplMethod = targetType
            .GetMethod( GetAwaiterName, bindingAttr )
            ?? Reflection.FindExtensionMethod( targetType, GetAwaiterName );

        if ( getAwaiterImplMethod == null )
            throw new InvalidOperationException( $"The type {targetType} is not awaitable." );

        var getResultImplMethod = getAwaiterImplMethod.ReturnType.GetMethod( GetResultName, bindingAttr );

        if ( getResultImplMethod == null )
            throw new InvalidOperationException( $"The awaiter for {targetType} does not have a GetResult method." );

        var configureAwaitImplMethod = getAwaiterImplMethod.ReturnType.GetMethod( ConfigureAwaitName, bindingAttr, [typeof(bool)] );

        var getAwaiterImpl = CreateGetAwaiterDelegate( getAwaiterImplMethod, configureAwaitImplMethod );
        var getResultImpl = CreateGetResultDelegate( getResultImplMethod );

        // Get the AwaitBinder Await method
        var binderAwaitMethod = targetType.IsGenericType
            ? AwaitResultMethod.MakeGenericMethod( targetType, targetType.GetGenericArguments()[0] )
            : AwaitMethod.MakeGenericMethod( targetType );

        return new AwaitBinder(
            binderAwaitMethod,
            getAwaiterImplMethod,
            getResultImplMethod,
            getAwaiterImpl,
            getResultImpl );
    }

    private static AwaitBinderGetAwaiterDelegate CreateGetAwaiterDelegate( MethodInfo getAwaiterMethod, MethodInfo configureAwaitMethod )
    {
        var dynamicMethod = new DynamicMethod(
            name: getAwaiterMethod.Name,
            returnType: typeof(object),
            parameterTypes: [typeof(object), typeof(bool)],
            typeof(AwaitBinder).Module,
            skipVisibility: true );

        var il = dynamicMethod.GetILGenerator();

        // Call ConfigureAwait (conditional)

        if ( configureAwaitMethod != null )
        {
            var lblSkipConfigureAwait = il.DefineLabel();

            // Test ConfigureAwait
            il.Emit( OpCodes.Ldarg_1 );
            il.Emit( OpCodes.Brtrue_S, lblSkipConfigureAwait ); 

            // Call ConfigureAwait(false)
            if ( !configureAwaitMethod.IsStatic )
            {
                // Load the instance (first argument)
                il.Emit( OpCodes.Ldarg_0 );
                il.Emit( OpCodes.Castclass, configureAwaitMethod.DeclaringType! );
            }

            il.Emit( OpCodes.Ldc_I4_0 ); // Load constant false
            il.Emit( configureAwaitMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, configureAwaitMethod );

            il.MarkLabel( lblSkipConfigureAwait );
        }

        // Call GetAwaiter()

        il.Emit( OpCodes.Ldarg_0 ); // Load the awaitable (for static extensions and instances)

        il.Emit( OpCodes.Castclass, getAwaiterMethod.IsStatic 
            ? getAwaiterMethod.GetParameters()[0].ParameterType 
            : getAwaiterMethod.DeclaringType! 
        );

        il.Emit( getAwaiterMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, getAwaiterMethod );

        if ( getAwaiterMethod.ReturnType.IsValueType )
        {
            il.Emit( OpCodes.Box, getAwaiterMethod.ReturnType );
        }

        il.Emit( OpCodes.Ret );

        return (AwaitBinderGetAwaiterDelegate) dynamicMethod.CreateDelegate( typeof(AwaitBinderGetAwaiterDelegate) );
    }

    private static AwaitBinderGetResultDelegate CreateGetResultDelegate( MethodInfo getResultMethod )
    {
        var dynamicMethod = new DynamicMethod(
            name: getResultMethod.Name,
            returnType: typeof(object),
            parameterTypes: [typeof(object)],
            typeof(AwaitBinder).Module,
            skipVisibility: true );

        var il = dynamicMethod.GetILGenerator();

        il.Emit( OpCodes.Ldarg_0 ); // Load the awaiter (for static extensions and instances)

        if ( getResultMethod.IsStatic )
        {
            var parameters = getResultMethod.GetParameters();
            var argType = parameters[0].ParameterType;

            il.Emit( argType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, argType );
            il.Emit( OpCodes.Call, getResultMethod );
        }
        else
        {
            var argType = getResultMethod.DeclaringType!;

            if ( argType.IsValueType )
            {
                var awaiter = il.DeclareLocal( getResultMethod.DeclaringType! );

                il.Emit( OpCodes.Unbox_Any, argType ); // Unbox the value type
                il.Emit( OpCodes.Stloc, awaiter ); // Store in local variable

                il.Emit( OpCodes.Ldloca_S, awaiter ); // Load address of local variable
                il.Emit( OpCodes.Call, getResultMethod ); // Call the method (non-virtual)
            }
            else
            {
                il.Emit( OpCodes.Castclass, argType ); // Cast to the reference type
                il.Emit( OpCodes.Callvirt, getResultMethod ); // Call the method (virtual)
            }
        }

        if ( getResultMethod.ReturnType.IsValueType )
        {
            il.Emit( OpCodes.Box, getResultMethod.ReturnType ); 
        }

        il.Emit( OpCodes.Ret );

        return (AwaitBinderGetResultDelegate) dynamicMethod.CreateDelegate( typeof(AwaitBinderGetResultDelegate) );
    }
}
