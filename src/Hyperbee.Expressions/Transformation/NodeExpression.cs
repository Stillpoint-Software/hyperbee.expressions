using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation.Transitions;

namespace Hyperbee.Expressions.Transformation;

[DebuggerDisplay( "State = {NodeLabel?.Name,nq}, Transition = {Transition?.GetType().Name,nq}" )]
public class NodeExpression : Expression
{
    public int StateId { get; }
    public int ScopeId { get; }

    internal int MachineOrder { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public Expression ResultValue { get; set; }

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
        if ( ResultValue != null && ResultVariable != null &&
             ResultValue.Type == ResultVariable.Type )
        {
            Expressions.Add( Assign( ResultVariable, ResultValue ) );
        }
        else if ( ResultVariable != null && Expressions.Count > 0 && ResultVariable.Type == Expressions[^1].Type )
        {
            // TODO: This feels like a hack that should be moved somewhere else
            // This might be related to go tos and Joins?
            Expressions[^1] = Assign( ResultVariable, Expressions[^1] );
        }

        Expressions.Add( Transition.Reduce( MachineOrder, this, _resolverSource ) );

        // Add the label to the beginning of the block
        Expressions.Insert( 0, Label( NodeLabel ) );

        return Block( Expressions );
    }

    private BlockExpression ReduceFinalBlock()
    {
        var (stateMachine, _, stateIdField, builderField, resultField, returnValue) = _resolverSource;

        Expression blockBody;

        if ( Expressions.Count > 0 )
            blockBody = Block( Expressions );
        else if ( ResultValue != null )
            blockBody = Block( ResultValue );
        else
            blockBody = Empty();

        // TODO: see if this can be improved earlier in the process
        var finalResult = returnValue != null
            ? Assign( resultField, returnValue )
            : (blockBody.Type == typeof( void ))
                ? Assign( resultField, Constant( null, typeof( IVoidResult ) ) )
                : Assign( resultField, blockBody );

        return Block(
            Label( NodeLabel ),
            finalResult,
            Assign( stateIdField, Constant( -2 ) ),
            Call(
                builderField,
                "SetResult",
                null,
                stateMachine.Type != typeof( IVoidResult )
                    ? resultField
                    : Constant( null, stateMachine.Type ) // No result for IVoidResult
            )
        );
    }
}
