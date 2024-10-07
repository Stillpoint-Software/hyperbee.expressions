using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class AwaitResultTransition : Transition
{
    public ParameterExpression AwaiterVariable { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        if ( ResultVariable == null )
            return Block(
                Call( AwaiterVariable, "GetResult", Type.EmptyTypes ),
                GotoOrFallThrough( order, TargetNode )
            );

        var getResult = Assign( ResultVariable, Call( AwaiterVariable, "GetResult", Type.EmptyTypes ) );

        return Block(
            getResult,
            GotoOrFallThrough( order, TargetNode )
        );
    }

    internal override NodeExpression FallThroughNode => TargetNode;
}
