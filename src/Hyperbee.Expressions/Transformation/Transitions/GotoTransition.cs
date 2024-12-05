using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class GotoTransition : Transition
{
    public IStateNode TargetNode { get; set; }
    internal override IStateNode FallThroughNode => TargetNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        base.AddExpressions( expressions, context );
        expressions.Add( GotoOrFallThrough( context.StateNode.StateOrder, TargetNode ) );
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
