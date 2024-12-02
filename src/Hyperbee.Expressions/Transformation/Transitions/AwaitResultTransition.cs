using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class AwaitResultTransition : Transition
{
    public Expression AwaiterVariable { get; set; }
    public Expression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }

    internal override NodeExpression FallThroughNode => TargetNode;

    protected override List<Expression> GetBody(NodeExpression parent )
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
                var transition = GotoOrFallThrough( parent.StateOrder, TargetNode );

                return transition == Empty()
                    ? [getResultCall]
                    : [getResultCall, transition];
            }

            return [
                Assign( ResultVariable, getResultCall ),
                GotoOrFallThrough( parent.StateOrder, TargetNode )
            ];
        }
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
