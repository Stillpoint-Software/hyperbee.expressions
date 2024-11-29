using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }
    internal override NodeExpression FallThroughNode => TargetNode;

    protected override List<Expression> GetBody()
    {
        return [GotoOrFallThrough( Parent.StateOrder, TargetNode )];
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
