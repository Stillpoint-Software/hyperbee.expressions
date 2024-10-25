using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class AwaitResultTransition : Transition
{
    public ParameterExpression AwaiterVariable { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        var getResultMethod = AwaitBinder.GetResultMethod;

        var getResultCall = getResultMethod.IsStatic
            ? Call( getResultMethod, AwaiterVariable )
            : Call( Constant( AwaitBinder ), getResultMethod, AwaiterVariable );

        if ( ResultVariable == null )
        {
            return Block(
                getResultCall, // Use the pre-determined call expression
                GotoOrFallThrough( order, TargetNode )
            );
        }

        var getResult = Assign( ResultVariable, getResultCall );

        return Block(
            getResult,
            GotoOrFallThrough( order, TargetNode )
        );
    }

    internal override NodeExpression FallThroughNode => TargetNode;
}
