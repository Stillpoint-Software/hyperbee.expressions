using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class ConditionalTransition : Transition
{
    public Expression Test { get; set; }
    public NodeExpression IfTrue { get; set; }
    public NodeExpression IfFalse { get; set; }

    internal override NodeExpression FallThroughNode => IfFalse;

    protected override List<Expression> GetBody(NodeExpression parent )
    {
        return [GetExpression()];

        Expression GetExpression()
        {
            var fallThrough = GotoOrFallThrough( parent.StateOrder, IfFalse, true );

            if ( fallThrough == null )
                return IfThen( Test, Goto( IfTrue.NodeLabel ) );

            return IfThenElse(
                Test,
                Goto( IfTrue.NodeLabel ),
                fallThrough
            );
        }
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        IfTrue = OptimizeGotos( IfTrue );
        IfFalse = OptimizeGotos( IfFalse );

        references.Add( IfTrue.NodeLabel );
        references.Add( IfFalse.NodeLabel );
    }
}
