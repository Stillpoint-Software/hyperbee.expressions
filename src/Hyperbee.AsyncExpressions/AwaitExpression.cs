using System.Diagnostics;
using System.Linq.Expressions;
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

    public AwaiterInfo GetAwaiterInfo() => AwaiterInfoFactory.GetOrCreate( Target.Type );

    public override Expression Reduce()
    {
        var awaitableType = Target.Type;

        if ( !AwaiterInfoFactory.TryGetOrCreate( awaitableType, out var awaitableInfo ) )
            throw new InvalidOperationException( $"Unable to resolve await method for type {awaitableType}." );

        return Call( Constant( awaitableInfo ), awaitableInfo.AwaitMethod, Target, Constant( _configureAwait ) );
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

        if ( AwaiterInfoFactory.TryGetOrCreate( awaitableType, out var awaiterInfo ) )
            return awaiterInfo.GetResultMethod.ReturnType;

        throw new InvalidOperationException( $"Unsupported type in {nameof(AwaitExpression)}." );
    }

    internal static bool IsAwaitable( Type type )
    {
        return typeof(Task).IsAssignableFrom( type ) || typeof(ValueTask).IsAssignableFrom( type ) || AwaiterInfoFactory.TryGetOrCreate( type, out _ );
    }

    private class AwaitExpressionDebuggerProxy( AwaitExpression node )
    {
        public Expression Target => node.Target;
        public Type Type => node.Type;
    }
}

public static partial class AsyncExpression
{
    public static AwaitExpression Await( Expression expression, bool configureAwait = false )
    {
        if ( expression is AsyncBlockExpression )
            return new AwaitExpression( expression, configureAwait );

        if ( !AwaitExpression.IsAwaitable( expression.Type ) )
            throw new ArgumentException( "Expression must be awaitable.", nameof(expression) );

        return new AwaitExpression( expression, configureAwait );
    }
}
