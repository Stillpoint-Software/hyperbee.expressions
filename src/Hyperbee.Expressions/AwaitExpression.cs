using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation;

namespace Hyperbee.Expressions;

[DebuggerDisplay( "Await {Target?.ToString(),nq}" )]
[DebuggerTypeProxy( typeof( AwaitExpressionDebuggerProxy ) )]
public class AwaitExpression : Expression
{
    private Type _resultType;

    internal AwaitExpression( Expression asyncExpression, bool configureAwait, bool shouldReduce )
    {
        Target = asyncExpression ?? throw new ArgumentNullException( nameof( asyncExpression ) );
        ShouldReduce = shouldReduce;
        ConfigureAwait = configureAwait;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;
    public override Type Type => _resultType ??= ResultType( Target.Type );

    public Expression Target { get; }

    public bool ConfigureAwait { get; }
    public bool ShouldReduce { get; }

    public AwaitBinder GetAwaitBinder() => AwaitBinderFactory.GetOrCreate( Target.Type );

    public override Expression Reduce()
    {
        if ( !ShouldReduce )
            return this;

        var awaitableType = Target.Type;
        var awaitableInfo = AwaitBinderFactory.GetOrCreate( awaitableType );

        return Call( Constant( awaitableInfo ), awaitableInfo.AwaitMethod, Target, Constant( ConfigureAwait ) );
    }

    private static Type ResultType( Type awaitableType )
    {
        if ( awaitableType.IsGenericType )
        {
            if ( awaitableType == typeof( Task<IVoidResult> ) || awaitableType == typeof( ValueTask<IVoidResult> ) )
                return typeof( void );

            var genericTypeDef = awaitableType.GetGenericTypeDefinition();

            if ( genericTypeDef.IsSubclassOf( typeof( Task ) ) ||
                 genericTypeDef.IsSubclassOf( typeof( ValueTask ) ) )
            {
                return awaitableType.GetGenericArguments()[0];
            }
        }

        if ( awaitableType == typeof( Task ) || awaitableType == typeof( ValueTask ) )
            return typeof( void );

        var awaiterInfo = AwaitBinderFactory.GetOrCreate( awaitableType );
        return awaiterInfo.GetResultMethod.ReturnType;
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newTarget = visitor.Visit( Target );

        return newTarget == Target
            ? this
            : new AwaitExpression( newTarget, ConfigureAwait, ShouldReduce );
    }

    internal static bool IsAwaitable( Type type )
    {
        return typeof( Task ).IsAssignableFrom( type ) || typeof( ValueTask ).IsAssignableFrom( type ) || AwaitBinderFactory.TryGetOrCreate( type, out _ );
    }

    private class AwaitExpressionDebuggerProxy( AwaitExpression node )
    {
        public Expression Target => node.Target;
        public Type Type => node.Type;
    }
}

public static partial class ExpressionExtensions
{
    public static AwaitExpression GetAwaiterResult( Expression expression, bool configureAwait = false )
    {
        if ( !AwaitExpression.IsAwaitable( expression.Type ) )
            throw new ArgumentException( "Expression must be awaitable.", nameof( expression ) );

        return new AwaitExpression( expression, configureAwait, shouldReduce: true );
    }

    public static AwaitExpression Await( Expression expression, bool configureAwait = false )
    {
        if ( !AwaitExpression.IsAwaitable( expression.Type ) )
            throw new ArgumentException( "Expression must be awaitable.", nameof( expression ) );

        return new AwaitExpression( expression, configureAwait, shouldReduce: false );
    }
}
