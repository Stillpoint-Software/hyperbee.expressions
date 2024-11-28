using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class LoopTransition : Transition
{
    public NodeExpression BodyNode { get; set; }
    public LabelTarget BreakLabel { get; set; }
    public LabelTarget ContinueLabel { get; set; }

    internal override NodeExpression FallThroughNode => BodyNode;

    protected override List<Expression> ReduceTransition( NodeExpression node )
    {
        return [Empty()];
    }

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        references.Add( BodyNode.NodeLabel );

        if ( BreakLabel != null )
            references.Add( BreakLabel );

        if ( ContinueLabel != null )
            references.Add( ContinueLabel );
    }
}
