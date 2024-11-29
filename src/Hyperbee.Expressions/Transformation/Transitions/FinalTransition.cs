using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class FinalTransition : Transition
{
    internal override NodeExpression FallThroughNode => null;

    internal override void Optimize( HashSet<LabelTarget> references )
    {
    }

    protected override List<Expression> GetExpressions()
    {
        return [Empty()];
    }

    protected override void AssignResult( List<Expression> expressions )
    {
        var resultField = Parent.StateMachineSource.ResultField;
        var returnValue = Parent.StateMachineSource.ReturnValue;

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
            Assign( resultField, Parent.ResultValue ?? Constant( null, typeof( IVoidResult ) ) )
        );
    }
}
