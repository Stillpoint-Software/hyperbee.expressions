using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

[DebuggerDisplay( "State = {NodeLabel?.Name,nq}, Transition = {Transition?.GetType().Name,nq}" )]
public class NodeExpression : Expression
{
    public int StateId { get; }
    public int ScopeId { get; }

    internal int StateOrder { get; set; }
    public ParameterExpression ResultVariable { get; set; } // Left-hand side of the result assignment
    public Expression ResultValue { get; set; } // Right-hand side of the result assignment

    public LabelTarget NodeLabel { get; set; }
    public List<Expression> Expressions { get; set; } = new( 8 );
    public Transition Transition { get; set; }

    private Expression _expression;
    private IHoistingSource _resolverSource;

    public NodeExpression( int stateId, int scopeId )
    {
        StateId = stateId;
        ScopeId = scopeId;
        NodeLabel = Label( $"ST_{StateId:0000}" );
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => ResultValue?.Type ?? typeof( void );
    public override bool CanReduce => true;

    public bool IsNoOp => Expressions.Count == 0 && ResultVariable == null;

    internal void SetResolverSource( IHoistingSource resolverSource )
    {
        _resolverSource = resolverSource;
    }

    internal Expression Reduce( IHoistingSource resolverSource )
    {
        _resolverSource = resolverSource;
        return Reduce();
    }

    public override Expression Reduce()
    {
        if ( _resolverSource == null )
            throw new InvalidOperationException( $"Reduce requires an {nameof( IHoistingSource )} instance." );

        return _expression ??= ReduceTransition();
    }

    private BlockExpression ReduceTransition()
    {
        return Transition != null
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

        var transitionExpression = Transition.Reduce( StateOrder, this, _resolverSource );
        Expressions.Add( transitionExpression );

        // Add the label to the beginning of the block
        Expressions.Insert( 0, Label( NodeLabel ) );

        return Block( Expressions );
    }

    private BlockExpression ReduceFinalBlock()
    {
        var (_, _, stateIdField, builderField, resultField, returnValue) = _resolverSource;

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

        static BinaryExpression GetFinalResultExpression( ParameterExpression returnValue, MemberExpression resultField, Expression resultValue, List<Expression> expressions )
        {
            Expression blockBody;

            if ( expressions.Count > 0 )
                blockBody = Block( expressions );
            else if ( resultValue != null )
                blockBody = Block( resultValue );
            else
                blockBody = Empty();

            if ( returnValue != null )
                return Assign( resultField, returnValue );

            return blockBody.Type == typeof( void )
                ? Assign( resultField, Constant( null, typeof( IVoidResult ) ) )
                : Assign( resultField, blockBody );
        }
    }
}
