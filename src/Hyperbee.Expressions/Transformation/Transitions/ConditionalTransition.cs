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
        var ifTrueTarget = OptimizeTransition( IfTrue );
        var ifFalseTarget = OptimizeTransition( IfFalse );

        var fallThrough = GotoOrFallThrough( order, ifFalseTarget, true );

        if ( fallThrough == null )
            return IfThen( Test, Goto( ifTrueTarget.NodeLabel ) );

        return IfThenElse(
            Test,
            Goto( ifTrueTarget.NodeLabel ),
            fallThrough
        );
    }

    internal override NodeExpression FallThroughNode => IfFalse;
}
