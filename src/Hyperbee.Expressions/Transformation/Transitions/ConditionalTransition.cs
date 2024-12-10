using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class ConditionalTransition : Transition
{
    public Expression Test { get; set; }
    public StateNode IfTrue { get; set; }
    public StateNode IfFalse { get; set; }

    internal override StateNode FallThroughNode => IfFalse;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        base.AddExpressions( expressions, context );
        expressions.Add( Expression() );
        return;

        Expression Expression()
        {
            var fallThrough = GotoOrFallThrough( context.StateNode.StateOrder, IfFalse, true );

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
