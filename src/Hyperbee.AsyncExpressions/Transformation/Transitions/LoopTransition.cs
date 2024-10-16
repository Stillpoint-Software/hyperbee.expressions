using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class LoopTransition : Transition
{
    public NodeExpression BodyNode { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        return Expression.Empty();
    }

    internal override NodeExpression FallThroughNode => BodyNode; // We won't reduce, but we need to provide a value for ordering
}
