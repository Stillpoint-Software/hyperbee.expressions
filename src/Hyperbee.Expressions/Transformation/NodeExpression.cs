using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
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

    private Expression _expression;
    private StateMachineSource _stateMachineSource;

    internal NodeExpression() { }

    public NodeExpression( int stateId, int scopeId, int groupId )
    {
        StateId = stateId;
        ScopeId = scopeId;
        GroupId = groupId;
        NodeLabel = Label( $"ST_{StateId:0000}" );
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof(void);
    public override bool CanReduce => true;

    public bool IsNoOp => Expressions.Count == 0 && ResultVariable == null;

    internal void SetStateMachineSource( StateMachineSource stateMachineSource )
    {
        _stateMachineSource = stateMachineSource;
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
        if ( _stateMachineSource == null )
            throw new InvalidOperationException( $"Reduce requires an {nameof( StateMachineSource )} instance." );

        return _expression ??= Transition != null
            ? ReduceBlock()
            : ReduceFinalBlock();
    }

    private BlockExpression ReduceBlock()
    {
        if ( ResultValue != null && ResultVariable != null && ResultValue.Type == ResultVariable.Type )
        {
            Expressions.Add( Assign( ResultVariable, ResultValue ) );
        }
        else if ( ResultVariable != null && Expressions.Count > 0 && ResultVariable.Type == Expressions[^1].Type )
        {
            Expressions[^1] = Assign( ResultVariable, Expressions[^1] );
        }

        var transitionExpression = Transition.Reduce( StateOrder, this, _stateMachineSource );
        Expressions.Add( transitionExpression );

        // Add the label to the beginning of the block
        Expressions.Insert( 0, Label( NodeLabel ) );

        return Block( Expressions );
    }

    private BlockExpression ReduceFinalBlock()
    {
        var (_, _, stateIdField, builderField, resultField, returnValue) = _stateMachineSource;

        return Block(
            Label( NodeLabel ),
            GetFinalResultExpression( returnValue, resultField, ResultValue, Expressions ),
            Assign( stateIdField, Constant( -2 ) ),
            Call(
                builderField,
                "SetResult",
                null,
                resultField.Type != typeof( IVoidResult )
                    ? resultField
                    : Constant( null, resultField.Type ) // No result for IVoidResult
            )
        );

        static Expression GetFinalResultExpression( ParameterExpression returnValue, MemberExpression resultField, Expression resultValue, List<Expression> expressions )
        {
            var blockBody = expressions.Count > 0
                ? Block( expressions )
                : resultValue ?? Empty();

            if ( returnValue != null )
            {
                return Assign( resultField, returnValue );
            }

            if ( blockBody.Type == typeof( void ) )
            {
                return Block(
                    Assign( resultField, Constant( null, typeof( IVoidResult ) ) ),
                    blockBody
                );
            }

            return Assign( resultField, blockBody );
        }
    }
}
