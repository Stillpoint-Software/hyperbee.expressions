using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.AsyncExpressions.Transformation;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "Await {Target?.ToString(),nq}" )]
[DebuggerTypeProxy( typeof(AwaitExpressionDebuggerProxy) )]
public class AwaitExpression : Expression
{
    private readonly bool _configureAwait;
    private Type _resultType;

    internal AwaitExpression( Expression asyncExpression, bool configureAwait )
    {
        Target = asyncExpression ?? throw new ArgumentNullException( nameof(asyncExpression) );

        _configureAwait = configureAwait;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;
    public override Type Type => _resultType ??= ResultType( Target.Type );

    public Expression Target { get; }

    public override Expression Reduce()
    {
        var awaitableType = Target.Type;

        if ( !AwaiterCache.TryGetAwaiterInfo( awaitableType, out var awaitableInfo ) )
            throw new InvalidOperationException( $"Unable to resolve await method for type {awaitableType}." );

        return Call( awaitableInfo.GetAwait(), Target, Constant( _configureAwait ) );
    }

    private static Type ResultType( Type awaitableType )
    {
        if ( awaitableType.IsGenericType )
        {
            if ( awaitableType == typeof(Task<IVoidTaskResult>) || awaitableType == typeof(ValueTask<IVoidTaskResult>) )
                return typeof(void);

            var genericTypeDef = awaitableType.GetGenericTypeDefinition();

            if ( genericTypeDef == typeof(Task<>) || genericTypeDef == typeof(ValueTask<>) )
                return awaitableType.GetGenericArguments()[0];
        }

        if ( awaitableType == typeof(Task) || awaitableType == typeof(ValueTask) )
            return typeof(void);

        if ( AwaiterCache.TryGetAwaiterInfo( awaitableType, out AwaiterInfo awaitableInfo ) )
            return awaitableInfo.GetResult.ReturnType;

        throw new InvalidOperationException( $"Unsupported type in {nameof(AwaitExpression)}." );
    }

    internal static bool IsAwaitable( Type type )
    {
        return typeof(Task).IsAssignableFrom( type ) || typeof(ValueTask).IsAssignableFrom( type ) || AwaiterCache.TryGetAwaiterInfo( type, out _ );
    }

    private static void Await<TAwaitable>( TAwaitable awaitable, bool configureAwait )
    {
        switch ( awaitable )
        {
            case Task task:
                task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();
                return;

            case ValueTask valueTask:
                valueTask.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();
                return;

            default:
                var awaiter = AwaiterCache.GetAwaiter( awaitable, out var getResult );
                getResult.Invoke( awaiter, null );
                return;
        }
    }

    private static T AwaitResult<TAwaitable, T>( TAwaitable awaitable, bool configureAwait )
    {
        switch ( awaitable )
        {
            case Task<T> task:
                return task.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();

            case ValueTask<T> valueTask:
                return valueTask.ConfigureAwait( configureAwait ).GetAwaiter().GetResult();

            default:
                var awaiter = AwaiterCache.GetAwaiter( awaitable, out var getResult );
                return (T) getResult.Invoke( awaiter, null );
        }
    }

    private class AwaitExpressionDebuggerProxy( AwaitExpression node )
    {
        public Expression Target => node.Target;
        public Type Type => node.Type;
    }

    private record AwaiterInfo(
        MethodInfo GetAwaiter,
        MethodInfo GetResult,
        MethodInfo Await,
        MethodInfo AwaitResult
    )
    {
        public MethodInfo GetAwait() => Await ?? AwaitResult;
    }

    private static class AwaiterCache
    {
        private static readonly ConcurrentDictionary<Type, AwaiterInfo> Cache = new();

        private static readonly MethodInfo ExpressionAwaitMethod;
        private static readonly MethodInfo ExpressionAwaitResultMethod;

        const string GetAwaiterName = "GetAwaiter";
        const string GetResultName = "GetResult";

        static AwaiterCache()
        {
            ExpressionAwaitMethod = typeof(AwaitExpression)
                .GetMethod( nameof(Await), BindingFlags.NonPublic | BindingFlags.Static );

            ExpressionAwaitResultMethod = typeof(AwaitExpression)
                .GetMethod( nameof(AwaitResult), BindingFlags.NonPublic | BindingFlags.Static );
        }

        public static object GetAwaiter<TAwaitable>( TAwaitable awaitable, out MethodInfo getResult )
        {
            if ( !TryGetAwaiterInfo( typeof(TAwaitable), out AwaiterInfo awaitableInfo ) )
                throw new InvalidOperationException( $"The type {typeof(TAwaitable)} is not awaitable." );

            getResult = awaitableInfo.GetResult;

            switch ( awaitableInfo.GetAwaiter.IsStatic )
            {
                case true:
                    if ( awaitableInfo.GetAwaiter.GetParameters().Length != 1 )
                        throw new InvalidOperationException( "GetAwaiter static method has an invalid signature." );
                    return awaitableInfo.GetAwaiter.Invoke( null, [awaitable] );

                case false:
                    if ( awaitableInfo.GetAwaiter.GetParameters().Length != 0 )
                        throw new InvalidOperationException( "GetAwaiter instance method has an invalid signature." );
                    return awaitableInfo.GetAwaiter.Invoke( awaitable, null );
            }
        }

        public static bool TryGetAwaiterInfo( Type targetType, out AwaiterInfo awaitableInfo )
        {
            awaitableInfo = Cache.GetOrAdd( targetType, type =>
            {
                var getAwaiterMethod = type.GetMethod( GetAwaiterName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );

                if ( getAwaiterMethod == null )
                {
                    getAwaiterMethod = type.GetMethod( GetAwaiterName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic );
                }

                if ( getAwaiterMethod == null )
                {
                    getAwaiterMethod = FindExtensionMethod( type, GetAwaiterName );

                    if ( getAwaiterMethod == null )
                        return null;
                }

                var awaiterType = getAwaiterMethod.ReturnType;
                var getResultMethod = awaiterType.GetMethod( GetResultName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );

                if ( getResultMethod == null )
                    return null;

                MethodInfo awaitMethod = null;
                MethodInfo awaitResultMethod = null;

                if ( getResultMethod.ReturnType == typeof(void) || getResultMethod.ReturnType == typeof(IVoidTaskResult) )
                    awaitMethod = ExpressionAwaitMethod.MakeGenericMethod( type );
                else
                    awaitResultMethod = ExpressionAwaitResultMethod.MakeGenericMethod( type, getResultMethod.ReturnType );

                return new AwaiterInfo( getAwaiterMethod, getResultMethod, awaitMethod, awaitResultMethod );

            } );

            return awaitableInfo != null;
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
                if ( method != null ) return method;
            }

            return null;
        }

        private static MethodInfo FindMethodInAssembly( Type targetType, string methodName, Assembly assembly )
        {
            var extensionMethods = assembly.GetTypes()
                .Where( t => t.IsSealed && !t.IsGenericType && !t.IsNested )
                .SelectMany( t => t.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic ) )
                .Where( m => m.Name == methodName && m.IsDefined( typeof(System.Runtime.CompilerServices.ExtensionAttribute), false ) );

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
}

public static partial class AsyncExpression
{
    public static AwaitExpression Await( Expression expression, bool configureAwait = false )
    {
        if ( expression is AsyncBlockExpression )
            return new AwaitExpression( expression, configureAwait );

        if ( !AwaitExpression.IsAwaitable( expression.Type ) )
            throw new ArgumentException( "Expression must be of type Task.", nameof(expression) );

        return new AwaitExpression( expression, configureAwait );
    }
}


