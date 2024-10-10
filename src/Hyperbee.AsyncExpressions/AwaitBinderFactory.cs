using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hyperbee.AsyncExpressions.Transformation;

namespace Hyperbee.AsyncExpressions;

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

    static AwaitBinderFactory()
    {
        // Pre-cache methods
        AwaitMethod = GetMethod( nameof(AwaitBinder.Await) );
        AwaitResultMethod = GetMethod( nameof(AwaitBinder.AwaitResult) );

        GetAwaiterTaskMethod = GetMethod( nameof(AwaitBinder.GetAwaiter), [typeof(Task)] );
        GetAwaiterTaskResultMethod = GetMethod( nameof(AwaitBinder.GetAwaiter), typeof(Task<>) );
        GetAwaiterValueTaskMethod = GetMethod( nameof(AwaitBinder.GetAwaiter), [typeof(ValueTask)] );
        GetAwaiterValueTaskResultMethod = GetMethod( nameof(AwaitBinder.GetAwaiter), typeof(ValueTask<>) );

        GetResultTaskMethod = GetMethod( nameof(AwaitBinder.GetResult), [typeof(TaskAwaiter)] );
        GetResultTaskResultMethod = GetMethod( nameof(AwaitBinder.GetResult), typeof(TaskAwaiter<>) );
        GetResultValueTaskMethod = GetMethod( nameof(AwaitBinder.GetResult), [typeof(ValueTaskAwaiter)] );
        GetResultValueTaskResultMethod = GetMethod( nameof(AwaitBinder.GetResult), typeof(ValueTaskAwaiter<>) );
    }

    private static MethodInfo GetMethod( string name ) => 
        typeof(AwaitBinder).GetMethod( name, BindingFlags.Instance | BindingFlags.NonPublic );

    private static MethodInfo GetMethod( string name, Type[] types ) => 
        typeof(AwaitBinder).GetMethod( name, BindingFlags.Static | BindingFlags.NonPublic, types );

    private static MethodInfo GetMethod( string name, Type genericType )
    {
        return typeof( AwaitBinder )
            .GetMethods( BindingFlags.Static | BindingFlags.NonPublic )
            .FirstOrDefault( m => m.Name == name && m.IsGenericMethodDefinition &&
                                  m.GetGenericArguments().Length == 1 &&
                                  m.GetParameters().Any( p => p.ParameterType.IsGenericType &&
                                                              p.ParameterType.GetGenericTypeDefinition() == genericType ) );
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
        // Task or ValueTask

        if ( targetType.IsGenericType )
        {
            var targetTypeDefinition = targetType.GetGenericTypeDefinition();

            if ( targetTypeDefinition == typeof(Task<>) || targetTypeDefinition.IsSubclassOf( typeof(Task) ) )
            {
                var typeArgument = targetType.GetGenericArguments()[0];

                return new AwaitBinder(
                    AwaitResultMethod.MakeGenericMethod( targetType, typeArgument ),
                    GetAwaiterTaskResultMethod,
                    GetResultTaskResultMethod
                );
            }

            if ( targetTypeDefinition == typeof( ValueTask<> ) || targetTypeDefinition.IsSubclassOf( typeof(ValueTask) ) )
            {
                var typeArgument = targetType.GetGenericArguments()[0];

                return new AwaitBinder(
                    AwaitResultMethod.MakeGenericMethod( targetType, typeArgument ),
                    GetAwaiterValueTaskResultMethod,
                    GetResultValueTaskResultMethod
                );
            }
        }
        else
        {
            if ( targetType == typeof( Task ) || targetType.IsSubclassOf( typeof( Task ) ) )
            {
                return new AwaitBinder(
                    AwaitMethod.MakeGenericMethod( targetType ),
                    GetAwaiterTaskMethod,
                    GetResultTaskMethod
                );
            }

            if ( targetType == typeof( ValueTask ) || targetType.IsSubclassOf( typeof( ValueTask ) ) )
            {
                return new AwaitBinder(
                    AwaitMethod.MakeGenericMethod( targetType ),
                    GetAwaiterValueTaskMethod,
                    GetResultValueTaskMethod
                );
            }
        }

        // Awaitable Type

        var getAwaiterMethod = targetType
            .GetMethod( GetAwaiterName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
            ?? FindExtensionMethod( targetType, GetAwaiterName );

        if ( getAwaiterMethod == null )
            throw new InvalidOperationException( $"The type {targetType} is not awaitable." );

        var awaiterType = getAwaiterMethod.ReturnType;
        var getResultMethod = awaiterType.GetMethod( GetResultName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );

        if ( getResultMethod == null )
            throw new InvalidOperationException( $"The awaiter for {targetType} does not have a GetResult method." );

        var awaitMethod = getResultMethod.ReturnType == typeof(void) || getResultMethod.ReturnType == typeof(IVoidTaskResult)
            ? AwaitMethod.MakeGenericMethod( targetType ) 
            : AwaitResultMethod.MakeGenericMethod( targetType, getResultMethod.ReturnType ); 

        return new AwaitBinder(
            awaitMethod,
            getAwaiterMethod,
            getResultMethod
        );
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
