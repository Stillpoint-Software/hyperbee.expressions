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
        //Check if the last expression is a Goto and skip the transition if so
        if ( Expressions.Count > 1 && Expressions[^1] is GotoExpression )
            return Block( Expressions );

        if ( Transition == null )
        {
            return ReduceFinalNode();
        }

        if ( ResultValue != null && ResultVariable != null )
        {
            Expressions.Add( Assign( ResultVariable, ResultValue ) );
            Expressions.Add( Transition.Reduce( MachineOrder, this, _resolverSource ) );
        }
        else if ( ResultVariable != null && ResultVariable.Type == Type )
        {
            Expressions.Add( Assign( ResultVariable, Transition.Reduce( MachineOrder, this, _resolverSource ) ) );
        }
        else
        {
            Expressions.Add( Transition.Reduce( MachineOrder, this, _resolverSource ) );
        }

        Expressions.Insert( 0, Label( NodeLabel ) );

        return Block( Expressions );
    }

    private BlockExpression ReduceFinalNode()
    {
        var blockBody = Expressions.Count switch
        {
            > 0 when ResultValue == null => Block( Expressions ),
            > 0 when ResultValue != null => Block( Expressions[1..].Concat( [ResultValue] ) ),
            _ => ResultValue ?? Empty()
        };

        return Block(
            Label( NodeLabel ),  
            _resolverSource.ReturnValue != null 
                ? Assign( _resolverSource.ResultField, _resolverSource.ReturnValue ) 
                : Assign( _resolverSource.ResultField, blockBody ),
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
