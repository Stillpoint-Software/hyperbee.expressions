using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        //return Goto( TargetNode.NodeLabel );
        return order + 1 == TargetNode.Order //BF ugly but works - we can clean up :)
            ? Expression.Empty()
            : Expression.Goto( TargetNode.NodeLabel );
    }

    internal override NodeExpression LogicalNextNode => TargetNode;
}