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

    private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags InstancePublicNonPublic = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;

    static AwaitBinderFactory()
    {
        // Pre-cache binder methods
        AwaitMethod = GetMethod( nameof(AwaitBinder.Await), InstanceNonPublic );
        AwaitResultMethod = GetMethod( nameof(AwaitBinder.AwaitResult), InstanceNonPublic );

        GetAwaiterTaskMethod = GetMethod( nameof(AwaitBinder.GetAwaiter), [typeof(Task)] );
        GetAwaiterTaskResultMethod = GetMethod( nameof(AwaitBinder.GetAwaiter), typeof(Task<>) );
        GetAwaiterValueTaskMethod = GetMethod( nameof(AwaitBinder.GetAwaiter), [typeof(ValueTask)] );
        GetAwaiterValueTaskResultMethod = GetMethod( nameof(AwaitBinder.GetAwaiter), typeof(ValueTask<>) );

        GetResultTaskMethod = GetMethod( nameof(AwaitBinder.GetResult), [typeof(TaskAwaiter)] );
        GetResultTaskResultMethod = GetMethod( nameof(AwaitBinder.GetResult), typeof(TaskAwaiter<>) );
        GetResultValueTaskMethod = GetMethod( nameof(AwaitBinder.GetResult), [typeof(ValueTaskAwaiter)] );
        GetResultValueTaskResultMethod = GetMethod( nameof(AwaitBinder.GetResult), typeof(ValueTaskAwaiter<>) );
    }

    private static MethodInfo GetMethod( string name, BindingFlags bindingAttr = StaticNonPublic )
    {
        return typeof(AwaitBinder).GetMethod( name, bindingAttr );
    }

    private static MethodInfo GetMethod( string name, Type[] types, BindingFlags bindingAttr = StaticNonPublic )
    {
        return typeof(AwaitBinder).GetMethod( name, bindingAttr, types );
    }

    private static MethodInfo GetMethod( string name, Type genericType, BindingFlags bindingAttr = StaticNonPublic )
    {
        var methods = typeof(AwaitBinder)
            .GetMethods( StaticNonPublic )
            .Where( method => method.Name == name && method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 1 );

        foreach ( var method in methods )
        {
            foreach ( var param in method.GetParameters() )
            {
                if ( param.ParameterType.IsGenericType && param.ParameterType.GetGenericTypeDefinition() == genericType ) 
                    return method;
            }
        }

        return null;
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

    public static void ClearCache()
    {
        Cache.Clear();
    }

    private static AwaitBinder Create( Type targetType )
    {
        // Task and ValueTask types

        if ( targetType.IsGenericType )
        {
            var targetTypeDefinition = targetType.GetGenericTypeDefinition();
            var typeArgument = targetType.GetGenericArguments()[0];

            if ( targetTypeDefinition == typeof(Task<>) )
            {
                return new AwaitBinder(
                    AwaitResultMethod.MakeGenericMethod( targetType, typeArgument ), 
                    GetAwaiterTaskResultMethod.MakeGenericMethod( typeArgument ),
                    GetResultTaskResultMethod.MakeGenericMethod( typeArgument ) );
            }

            if ( targetTypeDefinition == typeof(ValueTask<>) )
            {
                return new AwaitBinder(
                    AwaitResultMethod.MakeGenericMethod( targetType, typeArgument ), 
                    GetAwaiterValueTaskResultMethod.MakeGenericMethod( typeArgument ),
                    GetResultValueTaskResultMethod.MakeGenericMethod( typeArgument ) );
            }
        }
        else if ( targetType == typeof(Task) )
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

        var getAwaiterMethod = targetType
            .GetMethod( GetAwaiterName, InstancePublicNonPublic )
            ?? FindExtensionMethod( targetType, GetAwaiterName );

        if ( getAwaiterMethod == null )
            throw new InvalidOperationException( $"The type {targetType} is not awaitable." );

        var awaiterType = getAwaiterMethod.ReturnType;
        var getResultMethod = awaiterType.GetMethod( GetResultName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );

        if ( getResultMethod == null )
            throw new InvalidOperationException( $"The awaiter for {targetType} does not have a GetResult method." );

        // Create the dynamic delegates for GetAwaiter and GetResult
        var getAwaiterDelegate = CreateDynamicMethodDelegate( getAwaiterMethod, getAwaiterMethod.IsStatic );
        var getResultDelegate = CreateDynamicMethodDelegate( getResultMethod, getResultMethod.IsStatic );

        return new AwaitBinder(
            AwaitMethod.MakeGenericMethod( targetType ),
            getAwaiterMethod,
            getResultMethod,
            getAwaiterDelegate,
            getResultDelegate );
    }

    private static AwaitBinderDelegate CreateDynamicMethodDelegate( MethodInfo methodInfo, bool isStatic )
    {
        var dynamicMethod = new DynamicMethod(
            name: methodInfo.Name,
            returnType: typeof(object),
            parameterTypes: [typeof(object)],
            typeof(AwaitBinder).Module,
            skipVisibility: true );

        var il = dynamicMethod.GetILGenerator();

        if ( !isStatic )
        {
            il.Emit( OpCodes.Ldarg_0 );
            il.Emit( OpCodes.Castclass, methodInfo.DeclaringType! ); 
        }

        il.Emit( isStatic ? OpCodes.Call : OpCodes.Callvirt, methodInfo );

        if ( methodInfo.ReturnType.IsValueType ) // box value types
        {
            il.Emit( OpCodes.Box, methodInfo.ReturnType );
        }

        il.Emit( OpCodes.Ret );

        return (AwaitBinderDelegate) dynamicMethod.CreateDelegate( typeof(AwaitBinderDelegate) );
    }

    private static MethodInfo FindExtensionMethod( Type targetType, string methodName )
    {
        var callingAssembly = Assembly.GetCallingAssembly();
        var entryAssembly = Assembly.GetEntryAssembly();
        var targetAssembly = targetType.Assembly;

        // Search the calling assembly
        var method = FindMethodInAssembly( targetType, methodName, callingAssembly );

        if ( method != null )
            return method;

        // Search the entry assembly
        if ( entryAssembly != null && entryAssembly != callingAssembly )
        {
            method = FindMethodInAssembly( targetType, methodName, entryAssembly );

            if ( method != null )
                return method;
        }

        // Search the target assembly
        if ( targetAssembly != callingAssembly && targetAssembly != entryAssembly )
        {
            method = FindMethodInAssembly( targetType, methodName, targetAssembly );

            if ( method != null )
                return method;
        }

        // Search all other assemblies
        foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
        {
            if ( assembly == callingAssembly || assembly == entryAssembly || assembly == targetAssembly )
                continue;

            method = FindMethodInAssembly( targetType, methodName, assembly );
            
            if ( method != null ) 
                return method;
        }

        return null;
    }

    private static MethodInfo FindMethodInAssembly( Type targetType, string methodName, Assembly assembly )
    {
        var extensionMethods = assembly.GetTypes()
            .Where( t => t.IsSealed && !t.IsGenericType && !t.IsNested )
            .SelectMany( t => t.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic ) )
            .Where( m => m.Name == methodName && m.IsDefined( typeof(ExtensionAttribute), false ) );

        MethodInfo closedGenericMatch = null;
        MethodInfo openGenericMatch = null;

        foreach ( var method in extensionMethods )
        {
            var parameters = method.GetParameters();

            if ( parameters.Length == 0 )
                continue;

            var parameterType = parameters[0].ParameterType;

            if ( !parameterType.IsGenericType && parameterType == targetType )
                return method;

            if ( method.IsGenericMethodDefinition && parameterType.IsGenericType && targetType.IsGenericType )
            {
                var parameterGenericTypeDefinition = parameterType.GetGenericTypeDefinition();
                var targetGenericTypeDefinition = targetType.GetGenericTypeDefinition();

                if ( parameterGenericTypeDefinition == targetGenericTypeDefinition )
                {
                    var targetGenericArguments = targetType.GetGenericArguments();

                    try
                    {
                        openGenericMatch = method.MakeGenericMethod( targetGenericArguments );
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            if ( !parameterType.IsGenericType || !targetType.IsGenericType || parameterType != targetType )
                continue;

            closedGenericMatch = method;
            break;
        }

        return closedGenericMatch ?? openGenericMatch;
    }
}
