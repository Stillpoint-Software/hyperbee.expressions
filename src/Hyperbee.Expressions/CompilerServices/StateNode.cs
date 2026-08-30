using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.CompilerServices.Transitions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices;

[DebuggerDisplay( "State = {NodeLabel?.Name,nq}, ScopeId = {ScopeId}, GroupId = {GroupId}, StateOrder = {StateOrder}, Transition = {Transition?.GetType().Name,nq}" )]
internal sealed class StateNode
{
    public int StateId { get; }
    public int GroupId { get; }
    public int ScopeId { get; }

    public int StateOrder { get; set; }

    public StateResult Result { get; } = new();

    public LabelTarget NodeLabel { get; }
    public List<Expression> Expressions { get; } = new( 8 );

    public Transition Transition { get; set; }

    /// <summary>
    /// An expression emitted after this state's body and before its transition, built when
    /// the state machine is, so it can reach members the lowering does not have yet.
    /// </summary>
    /// <remarks>
    /// A finally state uses this to leave the machine when it is running to dispose rather
    /// than falling through to the code after the try.
    /// </remarks>
    public Func<StateMachineContext, Expression> Guard { get; set; }

    public StateNode( int stateId, int scopeId, int groupId )
    {
        StateId = stateId;
        ScopeId = scopeId;
        GroupId = groupId;
        NodeLabel = Label( $"ST_{StateId:0000}" );
    }

    public Expression GetExpression( StateMachineContext context )
    {
        ArgumentNullException.ThrowIfNull( context, nameof( context ) );

        var expressions = new List<Expression>( 8 ) { Label( NodeLabel ) };
        expressions.AddRange( Expressions );

        var prevState = context.StateNode;
        context.StateNode = this;

        if ( Guard != null )
            expressions.Add( Guard( context ) );

        Transition.AddExpressions( expressions, context );

        context.StateNode = prevState;

        return expressions.Count == 1
            ? expressions[0]
            : Block( expressions );
    }
}
