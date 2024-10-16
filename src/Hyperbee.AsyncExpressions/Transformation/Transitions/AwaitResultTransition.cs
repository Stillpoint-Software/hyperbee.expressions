using System.Linq.Expressions;
using System.Reflection;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class AwaitResultTransition : Transition
{
    public ParameterExpression AwaiterVariable { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }
    public MethodInfo GetResultMethod { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        var getResultCall = GetResultMethod.IsStatic
            ? Call( GetResultMethod, AwaiterVariable )
            : Call( AwaiterVariable, GetResultMethod );

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
