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
        Expressions.Add( Label( NodeLabel ) );
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
        if ( Expressions.Last() is GotoExpression )
            return Block( Expressions );

        if ( Transition == null )
        {
            return ReduceFinalNode();
        }

        // Temporary hack to handle final node
        
        if ( ResultValue != null && ResultVariable != null )
            Expressions.Add( Assign( ResultVariable, ResultValue ) );

        Expressions.Add( Transition.Reduce( MachineOrder, _resolverSource ) );


        return Block( Expressions );
        //
        // return Transition == null
        //     ? ReduceFinalNode()
        //     : Block( 
        //         Expressions.Concat( 
        //             [Transition.Reduce( MachineOrder, _resolverSource )] ) );
    }


    public static void WriteLine( string value )
    {
        Console.WriteLine( value );
    }


    private BlockExpression ReduceFinalNode()
    {
        var methodInfo = typeof( NodeExpression ).GetMethod( nameof( WriteLine ) );
        var log = Call( methodInfo, Constant( "Before SetResult" ) );



        var blockLabel = Expressions[0]; // Hack: move goto to the top
        Expression blockBody = (Expressions.Count > 1)
            ? Block(Expressions[1..].Concat( [ResultVariable] ))
            : ResultVariable;

        return Block(
            blockLabel,  
            _resolverSource.ReturnValue != null 
                ? Assign( _resolverSource.ResultField, _resolverSource.ReturnValue ) 
                : Assign( _resolverSource.ResultField, blockBody ),
            Assign( _resolverSource.StateIdField, Constant( -2 ) ),
            log,
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
