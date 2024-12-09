using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class AwaitResultTransition : Transition
{
    public Expression AwaiterVariable { get; set; }
    public Expression ResultVariable { get; set; }
    public StateNode TargetNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }

    internal override StateNode FallThroughNode => TargetNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        base.AddExpressions( expressions, context );
        expressions.AddRange( Expressions() );
        return;

        List<Expression> Expressions()
        {
            var getResultMethod = AwaitBinder.GetResultMethod;

            var getResultCall = getResultMethod.IsStatic
                ? Call( getResultMethod, AwaiterVariable )
                : Call( Constant( AwaitBinder ), getResultMethod, AwaiterVariable );

            if ( ResultVariable == null )
            {
                var transition = GotoOrFallThrough( context.StateNode.StateOrder, TargetNode );

                return transition == Empty()
                    ? [getResultCall]
                    : [getResultCall, transition];
            }

            return
            [
                Assign( ResultVariable, getResultCall ),
                GotoOrFallThrough( context.StateNode.StateOrder, TargetNode )
            ];
        }
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
