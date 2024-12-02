using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class FinalTransition : Transition
{
    internal override NodeExpression FallThroughNode => null;

    internal override void Optimize( HashSet<LabelTarget> references )
    {
    }

    protected override void SetBody( List<Expression> expressions, NodeExpression parent )
    {
    }

    protected override void SetResult( List<Expression> expressions, NodeExpression parent )
    {
        var resultField = parent.StateMachineSource.ResultField;
        var returnValue = parent.StateMachineSource.ReturnValue;

        if ( returnValue != null )
        {
            expressions.Add( Assign( resultField, returnValue ) );
            return;
        }

        if ( expressions.Count > 1 )
        {
            var lastExpression = expressions[^1];

            if ( lastExpression.Type == typeof( void ) )
            {
                expressions[^1] = Block(
                    Assign( resultField, Constant( null, typeof( IVoidResult ) ) ),
                    lastExpression
                );
            }
            else
            {
                expressions[^1] = Assign( resultField, lastExpression );
            }

            return;
        }

        expressions.Add(
            Assign( resultField, parent.ResultValue ?? Constant( null, typeof( IVoidResult ) ) )
        );
    }
}
