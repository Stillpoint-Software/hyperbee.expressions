using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class LoopTransition : Transition
{
    public IStateNode BodyNode { get; set; }
    public LabelTarget BreakLabel { get; set; }
    public LabelTarget ContinueLabel { get; set; }

    internal override IStateNode FallThroughNode => BodyNode;

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        references.Add( BodyNode.NodeLabel );

        if ( BreakLabel != null )
            references.Add( BreakLabel );

        if ( ContinueLabel != null )
            references.Add( ContinueLabel );
    }
}
