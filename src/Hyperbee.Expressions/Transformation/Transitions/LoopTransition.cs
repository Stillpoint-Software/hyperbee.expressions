using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class LoopTransition : Transition
{
    public NodeExpression BodyNode { get; set; }
    public LabelTarget BreakLabel { get; set; }
    public LabelTarget ContinueLabel { get; set; }

    internal override NodeExpression FallThroughNode => BodyNode;

    protected override List<Expression> GetBody()
    {
        return EmptyBody;
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        references.Add( BodyNode.NodeLabel );

        if ( BreakLabel != null )
            references.Add( BreakLabel );

        if ( ContinueLabel != null )
            references.Add( ContinueLabel );
    }
}
