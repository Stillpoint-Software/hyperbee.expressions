using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.AsyncExpressions.Transformation.Transitions;

namespace Hyperbee.AsyncExpressions.Transformation;

[DebuggerDisplay( "State = {NodeLabel?.Name,nq}, Transition = {Transition?.GetType().Name,nq}" )]
public class NodeExpression : Expression
{
    public int StateId { get; }
    internal int MachineOrder { get; set; }

    public LabelTarget NodeLabel { get; set; }
    public List<Expression> Expressions { get; } = new (8);
    public Transition Transition { get; set; }

    private Expression _expression;
    private IFieldResolverSource _resolverSource;

    public NodeExpression( int stateId )
    {
        StateId = stateId;

        NodeLabel = Label( $"ST_{StateId:0000}" );
        Expressions.Add( Label( NodeLabel ) );
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
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
        // Skip the transition if the last expression is a Goto
        if ( Expressions.Last() is GotoExpression )
            return Block( Expressions );

        return Transition == null
            ? ReduceFinalNode()
            : ReduceNode();
    }

    private BlockExpression ReduceNode()
    {
        return Block( 
            Expressions.Concat( [Transition.Reduce( MachineOrder, _resolverSource )] ) 
        );
    }

    private BlockExpression ReduceFinalNode()
    {
        var ( stateMachineType, _, _, stateIdField, builderField, resultField, returnValue ) = _resolverSource;

        return Block(
            Expressions[0],  // Hack: move goto to the top
            returnValue != null 
                ? Assign( resultField, returnValue ) 
                : Assign( resultField, Block( Expressions[1..] ) ),
            Assign( stateIdField, Constant( -2 ) ),
            Call(
                builderField,
                "SetResult",
                null,
                stateMachineType != typeof( IVoidTaskResult )
                    ? resultField
                    : Constant( null, stateMachineType ) // No result for IVoidTaskResult
            )
        );
    }
}
