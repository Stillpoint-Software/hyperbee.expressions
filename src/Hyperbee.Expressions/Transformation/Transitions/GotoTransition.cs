using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }
    internal override NodeExpression FallThroughNode => TargetNode;

    protected override void SetBody( List<Expression> expressions, NodeExpression parent )
    {
        expressions.Add( GotoOrFallThrough( parent.StateOrder, TargetNode ) );
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
