using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.AsyncExpressions.Transformation.Transitions;

namespace Hyperbee.AsyncExpressions.Transformation;

[DebuggerDisplay( "State = {NodeLabel?.Name,nq}, Transition = {Transition?.GetType().Name,nq}" )]
public class NodeExpression : Expression
{
    public int StateId { get; }
    internal int Order { get; set; }

    public LabelTarget NodeLabel { get; set; }
    public List<Expression> Expressions { get; } = new (8);
    public Transition Transition { get; set; }

    private Expression _expression;
    private IFieldResolverSource _resolverSource;

    public NodeExpression( int stateId )
    {
        StateId = stateId;
        NodeLabel = Expression.Label( $"ST_{StateId:0000}" );
        Expressions.Add( Expression.Label( NodeLabel ) );
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
        return Transition == null
            ? ReduceFinal()
            : Block( Expressions.Concat( [Transition.Reduce( Order, _resolverSource )] ) );
    }

    private BlockExpression ReduceFinal()
    {        
        return Block(
            Expressions[0],  // Hack: move goto to the top
            _resolverSource.ReturnValue != null 
                ? Assign( _resolverSource.ResultField, _resolverSource.ReturnValue ) 
                : Assign( _resolverSource.ResultField, Block( Expressions[1..] ) ),
            Assign( _resolverSource.StateIdField, Constant( -2 ) ),
            Call(
                _resolverSource.BuilderField,
                "SetResult",
                null,
                _resolverSource.StateMachineType != typeof( IVoidTaskResult )
                    ? _resolverSource.ResultField
                    : Constant( null, _resolverSource.StateMachineType ) // No result for IVoidTaskResult
            )
        );
    }
}
