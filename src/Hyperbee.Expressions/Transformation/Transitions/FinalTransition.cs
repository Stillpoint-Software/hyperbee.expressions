using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class FinalTransition : Transition
{
    internal override IStateNode FallThroughNode => null;

    internal override void Optimize( HashSet<LabelTarget> references )
    {
    }

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        if ( context.LoweringInfo.HasFinalResultVariable )
            return;

        var finalResultField = context.StateMachineInfo.FinalResultField;

        if ( expressions.Count > 1 )
        {
            var lastExpression = expressions[^1];

            if ( lastExpression.Type != typeof( void ) )
            {
                expressions[^1] = Assign( finalResultField, lastExpression );
                return;
            }
        }

        expressions.Add(
            Assign( finalResultField, context.StateNode.Result.Value ?? Constant( null, typeof( IVoidResult ) ) )
        );
    }
}
