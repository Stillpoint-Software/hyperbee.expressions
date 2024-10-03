using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class ConditionalTransition : Transition
{
    public Expression Test { get; set; }
    public NodeExpression IfTrue { get; set; }
    public NodeExpression IfFalse { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        return IfThenElse(
            Test,
            Goto( IfTrue.NodeLabel ),
            //Goto( IfFalse.NodeLabel )
            GotoOrFallThrough( order, IfFalse ) //BF
        );
    }

    internal override NodeExpression FallThroughNode => IfFalse;
}
