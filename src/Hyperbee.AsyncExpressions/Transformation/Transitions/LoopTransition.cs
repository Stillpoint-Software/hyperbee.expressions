using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class LoopTransition : Transition
{
    public NodeExpression TargetNode { get; set; }
    public NodeExpression BodyNode { get; set; }
    public Expression ContinueGoto { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        return ContinueGoto;
    }

    internal override NodeExpression LogicalNextNode => BodyNode;
}