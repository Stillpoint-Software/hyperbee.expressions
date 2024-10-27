using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class ConditionalTransition : Transition
{
    public Expression Test { get; set; }
    public NodeExpression IfTrue { get; set; }
    public NodeExpression IfFalse { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        var fallThrough = GotoOrFallThrough( order, IfFalse, true );

        if ( fallThrough == null )
            return IfThen( Test, Goto( IfTrue.NodeLabel ) );

        return IfThenElse(
            Test,
            Goto( IfTrue.NodeLabel ),
            fallThrough
        );
    }

    internal override NodeExpression FallThroughNode => IfFalse;

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        IfTrue = OptimizeTransition( IfTrue );
        IfFalse = OptimizeTransition( IfFalse );

        references.Add( IfTrue.NodeLabel );
        references.Add( IfFalse.NodeLabel );
    }
}
