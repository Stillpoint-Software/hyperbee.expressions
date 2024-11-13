using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return this;
    }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        //return GotoOrFallThrough(
        //    order,
        //    TargetNode
        //);

        // TODO: Fix fall through order number
        return Goto( TargetNode.NodeLabel );
    }

    internal override NodeExpression FallThroughNode => TargetNode;

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeTransition( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
