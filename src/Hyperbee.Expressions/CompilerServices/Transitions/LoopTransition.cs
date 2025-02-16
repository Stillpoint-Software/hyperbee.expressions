﻿using System.Linq.Expressions;

namespace Hyperbee.Expressions.CompilerServices.Transitions;

internal class LoopTransition : Transition
{
    public StateNode BodyNode { get; set; }
    public LabelTarget BreakLabel { get; set; }
    public LabelTarget ContinueLabel { get; set; }

    internal override StateNode FallThroughNode => BodyNode;

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        references.Add( BodyNode.NodeLabel );

        if ( BreakLabel != null )
            references.Add( BreakLabel );

        if ( ContinueLabel != null )
            references.Add( ContinueLabel );
    }
}
