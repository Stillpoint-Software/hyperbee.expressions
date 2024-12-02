using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }
    internal override NodeExpression FallThroughNode => TargetNode;

    protected override List<Expression> GetBody(NodeExpression parent )
    {
        return [GotoOrFallThrough( parent.StateOrder, TargetNode )];
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
