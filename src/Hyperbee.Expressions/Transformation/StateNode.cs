using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation.Transitions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation;

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

        Transition.AddExpressions( expressions, context );

        context.StateNode = prevState;

        return expressions.Count == 1
            ? expressions[0]
            : Block( expressions );
    }
}
