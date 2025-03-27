using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices.Transitions;

internal class YieldTransition : Transition
{
    public StateNode TargetNode { get; set; }
    public Expression? Value { get; internal set; }
    public int StateId { get; internal set; }

    internal override StateNode FallThroughNode => TargetNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        // Note: Base call seems to be pointless
        //base.AddExpressions( expressions, context );  

        if ( Value == null )
        {
            // Yield Break
            expressions.Add(
                Block(
                    Return( context.StateMachineInfo.ExitLabel, Constant( false ) )
                )
            );
            return;
        }

        // Yield Return
        expressions.Add(
            Block(
                Assign( context.StateMachineInfo.StateField, Constant( TargetNode.StateId ) ),
                Assign( context.StateMachineInfo.CurrentField, Value ),
                Return( context.StateMachineInfo.ExitLabel, Constant( true ) )
            )
        );
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        // Because both break and return always exit the state machine they cannot fallthrough
    }
}
