using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class ConditionalTransition : Transition
{
    public Expression Test { get; set; }
    public NodeExpression IfTrue { get; set; }
    public NodeExpression IfFalse { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        return Expression.IfThenElse(
            Test,
            Expression.Goto( IfTrue.NodeLabel ),
            //Goto( IfFalse.NodeLabel )
            GotoOrFallThrough( order, IfFalse ) //BF
        );
    }

    internal override NodeExpression LogicalNextNode => IfFalse;
}
