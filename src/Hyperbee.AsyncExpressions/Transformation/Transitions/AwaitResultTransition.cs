using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class AwaitResultTransition : Transition
{
    public ParameterExpression AwaiterVariable { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        Expression getResult = ResultVariable == null
            ? Call( AwaiterVariable, "GetResult", Type.EmptyTypes )
            : Assign( ResultVariable, Call( AwaiterVariable, "GetResult", Type.EmptyTypes ) );

        return Block(
            getResult,
            //Expression.Goto( TargetNode.NodeLabel )
            GotoOrFallThrough( order, TargetNode ) //BF
        );
    }

    internal override NodeExpression LogicalNextNode => TargetNode;
}
