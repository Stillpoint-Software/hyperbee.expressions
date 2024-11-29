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

    protected override List<Expression> GetBody()
    {
        return GetExpressions();

        List<Expression> GetExpressions()
        {
            var getResultMethod = AwaitBinder.GetResultMethod;

            var getResultCall = getResultMethod.IsStatic
                ? Call( getResultMethod, AwaiterVariable )
                : Call( Constant( AwaitBinder ), getResultMethod, AwaiterVariable );

            if ( ResultVariable == null )
            {
                var transition = GotoOrFallThrough( Parent.StateOrder, TargetNode );

                return transition == Empty()
                    ? [getResultCall]
                    : [getResultCall, transition];
            }

            return [
                Assign( ResultVariable, getResultCall ),
                GotoOrFallThrough( Parent.StateOrder, TargetNode )
            ];
        }
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
