using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class LoopTransition : Transition
{
    public NodeExpression BodyNode { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        return Expression.Empty();
    }

    internal override NodeExpression FallThroughNode => BodyNode; // We won't reduce, but we need to provide a value for ordering
    public LabelTarget BreakLabel { get; set; }
    public LabelTarget ContinueLabel { get; set; }

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        references.Add( BodyNode.NodeLabel );

        if ( BreakLabel != null )
            references.Add( BreakLabel );

        if ( ContinueLabel != null )
            references.Add( ContinueLabel );
    }
}
