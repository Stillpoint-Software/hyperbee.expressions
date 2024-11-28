using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class AwaitResultTransition : Transition
{
    public Expression AwaiterVariable { get; set; }
    public Expression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }

    internal override NodeExpression FallThroughNode => TargetNode;

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return Update(
            visitor.Visit( AwaiterVariable ),
            visitor.Visit( ResultVariable )
        );
    }

    internal AwaitResultTransition Update( Expression awaiterVariable, Expression resultVariable )
    {
        if ( awaiterVariable == AwaiterVariable && resultVariable == ResultVariable )
            return this;

        return new AwaitResultTransition
        {
            AwaiterVariable = awaiterVariable,
            ResultVariable = resultVariable,
            TargetNode = TargetNode,
            AwaitBinder = AwaitBinder
        };
    }

    protected override List<Expression> ReduceTransition( NodeExpression node )
    {
        return [GetExpression()];

        Expression GetExpression()
        {
            var getResultMethod = AwaitBinder.GetResultMethod;

            var getResultCall = getResultMethod.IsStatic
                ? Call( getResultMethod, AwaiterVariable )
                : Call( Constant( AwaitBinder ), getResultMethod, AwaiterVariable );

            if ( ResultVariable == null )
            {
                var transition = GotoOrFallThrough( node.StateOrder, TargetNode );

                return transition == Empty()
                    ? getResultCall
                    : Block( getResultCall, transition );
            }

            var getResult = Assign( ResultVariable, getResultCall );

            return Block(
                getResult,
                GotoOrFallThrough( node.StateOrder, TargetNode )
            );
        }
    }

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
