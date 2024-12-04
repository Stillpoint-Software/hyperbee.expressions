using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

[DebuggerDisplay( "State = {NodeLabel?.Name,nq}, ScopeId = {ScopeId}, GroupId = {GroupId}, StateOrder = {StateOrder}, Transition = {Transition?.GetType().Name,nq}" )]
internal sealed class StateExpression : Expression, IStateNode //BF ME - NodeExpression to StateExpression ??
{
    public int StateId { get; set; }
    public int GroupId { get; set; }
    public int ScopeId { get; set; }

    public int StateOrder { get; set; }

    public StateResult Result { get; set; } = new();

    public LabelTarget NodeLabel { get; set; }
    public List<Expression> Expressions { get; set; } = new( 8 );

    public Transition Transition { get; set; }

    public StateExpression( int stateId, int scopeId, int groupId )
    {
        StateId = stateId;
        ScopeId = scopeId;
        GroupId = groupId;
        NodeLabel = Label( $"ST_{StateId:0000}" );
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );

    public override bool CanReduce => false; // This should NEVER be reduced
    public override Expression Reduce() => throw new NotSupportedException();

    public Expression GetExpression( StateMachineContext context )
    {
        ArgumentNullException.ThrowIfNull( context, nameof( context ) );

        var expressions = new List<Expression>( 8 ) { Label( NodeLabel ) };
        expressions.AddRange( Expressions );

        var prevNodeInfo = context.StateInfo;
        context.StateInfo = new StateInfo( StateOrder, Result );

        Transition.AddExpressions( expressions, context );

        context.StateInfo = prevNodeInfo;

        return expressions.Count == 1
            ? expressions[0]
            : Block( expressions );
    }
}
