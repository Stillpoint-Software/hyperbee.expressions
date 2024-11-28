using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class FinalTransition : Transition
{
    internal override NodeExpression FallThroughNode => null;

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
    }

    protected override List<Expression> ReduceTransition( NodeExpression node )
    {
        return [Empty()];
    }

    protected override void AssignResult( NodeExpression node, List<Expression> expressions )
    {
        var resultField = node.StateMachineSource.ResultField;
        var returnValue = node.StateMachineSource.ReturnValue;

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
            Assign( resultField, node.ResultValue ?? Constant( null, typeof( IVoidResult ) ) )
        );
    }
}
