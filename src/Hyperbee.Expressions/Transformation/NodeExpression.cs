using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

[DebuggerDisplay( "State = {NodeLabel?.Name,nq}, ScopeId = {ScopeId}, GroupId = {GroupId}, StateOrder = {StateOrder}, Transition = {Transition?.GetType().Name,nq}" )]
public sealed class NodeExpression : Expression
{
    public int StateId { get; set; }
    public int GroupId { get; set; }
    public int ScopeId { get; set; }

    internal int StateOrder { get; set; }

    public Expression ResultVariable { get; set; } // Left-hand side of the result assignment
    public Expression ResultValue { get; set; } // Right-hand side of the result assignment

    public LabelTarget NodeLabel { get; set; }
    public List<Expression> Expressions { get; set; } = new( 8 );
    public Transition Transition { get; set; }

    internal StateMachineSource StateMachineSource { get; private set; }

    internal NodeExpression() { }

    public NodeExpression( int stateId, int scopeId, int groupId )
    {
        StateId = stateId;
        ScopeId = scopeId;
        GroupId = groupId;
        NodeLabel = Label( $"ST_{StateId:0000}" );
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    public bool IsNoOp => Expressions.Count == 0 && ResultVariable == null;

    internal void SetStateMachineSource( StateMachineSource stateMachineSource )
    {
        StateMachineSource = stateMachineSource;
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return Update(
            Expressions.Select( visitor.Visit ).ToList(),
            visitor.Visit( ResultValue ),
            visitor.Visit( ResultVariable ),
            (Transition) visitor.Visit( Transition )
        );
    }

    public Expression Update( List<Expression> expressions, Expression resultValue, Expression resultVariable, Transition transition )
    {
        Expressions = expressions;
        ResultValue = resultValue;
        ResultVariable = resultVariable;
        Transition = transition;

        return this;
    }

    public override Expression Reduce()
    {
        if ( StateMachineSource == null )
            throw new InvalidOperationException( $"Reduce requires an {nameof( Transformation.StateMachineSource )} instance." );

        return Block(
            Transition.Reduce( this )
        );
    }
}
