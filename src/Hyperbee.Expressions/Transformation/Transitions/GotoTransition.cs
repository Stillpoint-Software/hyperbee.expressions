using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( int order, int scopeId, NodeExpression expression, StateMachineSource resolverSource )
    {
        return GotoOrFallThrough(
            order,
            scopeId,
            TargetNode
        );
    }

    internal override NodeExpression FallThroughNode => TargetNode;

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeTransition( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
