using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices.Transitions;

internal class YieldTransition : Transition
{
    public StateNode TargetNode { get; set; }
    public Expression Value { get; internal set; }
    public int StateId { get; internal set; }

    internal override StateNode FallThroughNode => TargetNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        if ( context.StateMachineInfo is not YieldStateMachineInfo stateMachineInfo )
            throw new ArgumentException( "Invalid State Machine" );

        if ( Value == null )
        {
            // Yield Break
            expressions.Add(
                Block(
                    Return( stateMachineInfo.ExitLabel, Constant( false ) )
                )
            );
            return;
        }

        // Yield Return
        expressions.Add(
            Block(
                Assign( stateMachineInfo.StateField, Constant( TargetNode.StateId ) ),
                Assign( stateMachineInfo.CurrentField, Value ),
                Return( stateMachineInfo.ExitLabel, Constant( true ) )
            )
        );
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        // Because both break and return always exit the state machine they cannot fallthrough
    }
}
