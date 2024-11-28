using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }
    internal override NodeExpression FallThroughNode => TargetNode;

    protected override List<Expression> ReduceTransition( NodeExpression node )
    {
        return [GotoOrFallThrough( node.StateOrder, TargetNode )];
    }

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
