using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class AwaitResultTransition : Transition
{
    public Expression AwaiterVariable { get; set; }
    public Expression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }

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

    internal override Expression Reduce( int order, NodeExpression expression, StateMachineSource resolverSource )
    {
        var getResultMethod = AwaitBinder.GetResultMethod;

        var getResultCall = getResultMethod.IsStatic
            ? Call( getResultMethod, AwaiterVariable )
            : Call( Constant( AwaitBinder ), getResultMethod, AwaiterVariable );

        if ( ResultVariable == null )
        {
            var transition = GotoOrFallThrough( order, TargetNode );

            return transition == Empty()
                ? getResultCall
                : Block( getResultCall, transition );
        }

        var getResult = Assign( ResultVariable, getResultCall );

        return Block(
            getResult,
            GotoOrFallThrough( order, TargetNode )
        );
    }

    internal override NodeExpression FallThroughNode => TargetNode;

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeTransition( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
