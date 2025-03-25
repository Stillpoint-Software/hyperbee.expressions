using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices.Transitions;

internal class YieldTransition : Transition
{
    public StateNode TargetNode { get; set; }
    public LabelTarget ResumeLabel { get; internal set; }
    public Expression? Value { get; internal set; }
    public int StateId { get; internal set; }

    internal override StateNode FallThroughNode => TargetNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        //base.AddExpressions( expressions, context );
        if ( Value == null )
        {
            expressions.Add(
                Block(
                    Return( context.StateMachineInfo.ExitLabel, Constant( false ) )
                )
            );
            return;
        }
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
        TargetNode = OptimizeGotos( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
