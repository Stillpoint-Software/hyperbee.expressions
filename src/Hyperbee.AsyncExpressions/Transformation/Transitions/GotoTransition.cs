using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        //return Goto( TargetNode.NodeLabel );
        return GotoOrFallThrough( order, TargetNode ); //BF
    }

    internal override NodeExpression LogicalNextNode => TargetNode;
}
