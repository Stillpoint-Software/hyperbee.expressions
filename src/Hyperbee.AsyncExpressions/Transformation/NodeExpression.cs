using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.AsyncExpressions.Transformation.Transitions;

namespace Hyperbee.AsyncExpressions.Transformation;

[DebuggerDisplay( "State = {NodeLabel?.Name,nq}, Transition = {Transition?.GetType().Name,nq}" )]
public class NodeExpression : Expression
{
    public int StateId { get; }
    internal int MachineOrder { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public Expression ResultValue { get; set; }

    public LabelTarget NodeLabel { get; set; }
    public List<Expression> Expressions { get; } = new (8);
    public Transition Transition { get; set; }

    private Expression _expression;
    private IFieldResolverSource _resolverSource;

    public NodeExpression( int stateId )
    {
        StateId = stateId;
        NodeLabel = Label( $"ST_{StateId:0000}" );
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => ResultValue?.Type ?? typeof( void );
    public override bool CanReduce => true;

    internal Expression Reduce( IFieldResolverSource resolverSource )
    {
        _resolverSource = resolverSource;
        return Reduce();
    }

    public override Expression Reduce()
    {
        if ( _resolverSource == null )
            throw new InvalidOperationException( $"Reduce requires an {nameof( IFieldResolverSource )} instance." );

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

        Expressions.Add( Transition.Reduce( MachineOrder, this, _resolverSource ) );

        // Add the label to the beginning of the block
        Expressions.Insert( 0, Label( NodeLabel ) );

        return Block( Expressions );
    }

    private BlockExpression ReduceFinalBlock()
    {
        var (stateMachineType, _, _, stateIdField, builderField, resultField, returnValue) = _resolverSource;

        Expression blockBody;

        if ( Expressions.Count > 0 )
            blockBody = Block( Expressions );
        else if ( ResultValue != null )
            blockBody = Block( ResultValue );
        else
            blockBody = Empty();

        return Block(
            Label( NodeLabel ),
            returnValue != null
                ? Assign( resultField, returnValue )
                : Assign( resultField, blockBody ),
            Assign( stateIdField, Constant( -2 ) ),
            Call(
                builderField,
                "SetResult",
                null,
                stateMachineType != typeof(IVoidTaskResult)
                    ? resultField
                    : Constant( null, stateMachineType ) // No result for IVoidTaskResult
            )
        );
    }
}
