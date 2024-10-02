using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class AwaitResultTransition : Transition
{
    public ParameterExpression AwaiterVariable { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        Expression getResult = ResultVariable == null
            ? Expression.Call( AwaiterVariable, "GetResult", Type.EmptyTypes )
            : Expression.Assign( ResultVariable, Expression.Call( AwaiterVariable, "GetResult", Type.EmptyTypes ) );

        return Expression.Block(
            getResult,
            Expression.Goto( TargetNode.NodeLabel )
        );
    }

    internal override NodeExpression LogicalNextNode => TargetNode;
}