using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        return GotoOrFallThrough(
            order,
            OptimizeTransition( TargetNode )
        );
    }

    internal override NodeExpression FallThroughNode => TargetNode;
}
