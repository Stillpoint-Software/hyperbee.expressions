using System.Linq.Expressions;

using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class GotoTransition : Transition
{
    public NodeExpression TargetNode { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        return Goto( TargetNode.NodeLabel );

        // TODO: causes infinite loop with nested try/catch
        // return GotoOrFallThrough( order, TargetNode );
    }

    internal override NodeExpression FallThroughNode => TargetNode;
}
