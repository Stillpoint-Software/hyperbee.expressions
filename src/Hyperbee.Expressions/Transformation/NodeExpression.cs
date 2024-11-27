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

    public List<ParameterExpression> Variables { get; set; } = [];

    public Expression ResultVariable { get; set; } // Left-hand side of the result assignment
    public Expression ResultValue { get; set; } // Right-hand side of the result assignment

    public LabelTarget NodeLabel { get; set; }
    public List<Expression> Expressions { get; set; } = new( 8 );
    public Transition Transition { get; set; }

    public bool IsFinal => Transition == null;

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
    public override Type Type => typeof( void );
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

        var expressions = !IsFinal
            ? ReduceTransition()
            : ReduceFinal();

        return Block(
            Variables,
            expressions
        );
    }

    private List<Expression> ReduceTransition()
    {
        var expressions = new List<Expression> { Label( NodeLabel ) };
        expressions.AddRange( Expressions );

        AssignResult( expressions, ResultValue, ResultVariable );
        AddTransition( expressions, StateOrder, this );

        return expressions;

        // Helpers

        static void AssignResult( List<Expression> expressions, Expression resultValue, Expression resultVariable )
        {
            if ( resultValue != null && resultVariable != null && resultValue.Type == resultVariable.Type )
            {
                expressions.Add( Assign( resultVariable, resultValue ) );
            }
            else if ( resultVariable != null && expressions.Count > 0 && resultVariable.Type == expressions[^1].Type )
            {
                expressions[^1] = Assign( resultVariable, expressions[^1] );
            }
        }

        static void AddTransition( List<Expression> expressions, int stateOrder, NodeExpression node )
        {
            var resolverSource = node._stateMachineSource;
            var transition = node.Transition;

            var transitionExpression = transition.Reduce( stateOrder, node, resolverSource );
            expressions.Add( transitionExpression );
        }
    }

    private List<Expression> ReduceFinal()
    {
        var (_, _, stateIdField, builderField, resultField, returnValue) = _stateMachineSource;

        var expressions = new List<Expression> { Label( NodeLabel ) };
        expressions.AddRange( Expressions );

        AssignFinalResult( expressions, returnValue, resultField, ResultValue );
        AssignBuilderResult( expressions, stateIdField, builderField, resultField );

        return expressions;

        // Helpers

        static void AssignFinalResult( List<Expression> expressions, ParameterExpression returnValue, MemberExpression resultField, Expression resultValue )
        {
            if ( returnValue != null )
            {
                expressions.Add( Assign( resultField, returnValue ) );
                return;
            }

            if ( expressions.Count > 1 ) // Check if expressions (besides the label) exist
            {
                var lastExpression = expressions[^1];

                if ( lastExpression.Type == typeof( void ) )
                {
                    expressions[^1] = Block(
                        Assign( resultField, Constant( null, typeof( IVoidResult ) ) ),
                        lastExpression
                    );
                }
                else
                {
                    expressions[^1] = Assign( resultField, lastExpression );
                }

                return;
            }

            expressions.Add(
                Assign( resultField, resultValue ?? Constant( null, typeof( IVoidResult ) ) )
            );
        }

        static void AssignBuilderResult( List<Expression> expressions, MemberExpression stateIdField, MemberExpression builderField, MemberExpression resultField )
        {
            expressions.Add( Assign( stateIdField, Constant( -2 ) ) );

            expressions.Add( Call(
                builderField,
                "SetResult",
                null,
                resultField.Type != typeof( IVoidResult )
                    ? resultField
                    : Constant( null, resultField.Type ) // No result for IVoidResult
            ) );
        }
    }
}
