﻿using System.Linq.Expressions;
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
        if ( context.StateMachineInfo is not EnumerableStateMachineInfo stateMachineInfo )
            throw new ArgumentException( "Invalid State Machine" );

        if ( Value == null )
        {
            // Yield Break
            expressions.Add(
                Block(
                    Assign( stateMachineInfo.Success, Constant( true ) ),
                    Return( stateMachineInfo.ExitLabel, Constant( false ), typeof( bool ) )
                )
            );
            return;
        }

        // Yield Return
        expressions.Add(
            Block(
                Assign( stateMachineInfo.StateField, Constant( TargetNode.StateId ) ),
                Assign( stateMachineInfo.CurrentField, Value ),
                Assign( stateMachineInfo.Success, Constant( true ) ),
                Return( stateMachineInfo.ExitLabel, Constant( true ), typeof( bool ) )
            )
        );
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        // Because both break and return always exit the state machine they cannot fallthrough
    }
}
