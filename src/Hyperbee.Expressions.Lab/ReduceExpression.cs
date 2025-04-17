using System.Linq.Expressions;

using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Lab;

public delegate Expression ReduceBody( ParameterExpression accumulator, ParameterExpression item );
public delegate Expression ReduceBodyIndex( ParameterExpression accumulator, ParameterExpression item, ParameterExpression index );
public delegate Expression ReduceBodyIndexSource( ParameterExpression accumulator, ParameterExpression item, ParameterExpression index, Expression source );

public class ReduceExpression : Expression
{
    public Expression Collection { get; }
    public Expression Seed { get; }
    public Expression Body { get; }

    public ParameterExpression Accumulator { get; }
    public ParameterExpression Item { get; }
    public ParameterExpression Index { get; }
    public ParameterExpression Source { get; }

    private ReduceExpression( Expression collection, Expression seed )
    {
        ArgumentNullException.ThrowIfNull( collection );
        ArgumentNullException.ThrowIfNull( seed );

        if ( !typeof( System.Collections.IEnumerable ).IsAssignableFrom( collection.Type ) )
            throw new ArgumentException( "Collection must be IEnumerable", nameof( collection ) );

        Collection = collection;
        Seed = seed;

        var enumerableType = collection.Type.GetInterfaces()
            .Concat( [collection.Type] )
            .FirstOrDefault( t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof( IEnumerable<> ) );

        var itemType = enumerableType?.GetGenericArguments()[0] ?? typeof( object );

        Accumulator = Variable( seed.Type, "acc" );
        Item = Variable( itemType, "item" );
    }

    public ReduceExpression( Expression collection, Expression seed, ReduceBody body ) : this( collection, seed )
    {
        ArgumentNullException.ThrowIfNull( body );
        Body = body( Accumulator, Item );
    }

    public ReduceExpression( Expression collection, Expression seed, ReduceBodyIndex body ) : this( collection, seed )
    {
        Index = Variable( typeof( int ), "index" );
        Body = body( Accumulator, Item, Index );
    }

    public ReduceExpression( Expression collection, Expression seed, ReduceBodyIndexSource body ) : this( collection, seed )
    {
        Index = Variable( typeof( int ), "index" );
        Source = Variable( collection.Type, "source" );
        Body = body( Accumulator, Item, Index, Collection );
    }

    public ReduceExpression( Expression collection, Expression seed, Expression body ) : this( collection, seed )
    {
        ArgumentNullException.ThrowIfNull( body );
        Body = body;
    }

    public override Type Type => Seed.Type;
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        if ( Source != null && Index != null )
        {
            return Block(
                [Accumulator, Item, Index, Source],
                Assign( Accumulator, Seed ),
                Assign( Source, Collection ),
                Assign( Index, Constant( 0 ) ),
                ForEach(
                    Source,
                    Item,
                    Block(
                        Assign( Accumulator, Body ),
                        PostIncrementAssign( Index )
                    )
                ),
                Accumulator
            );
        }
        
        if ( Index != null )
        {
            return Block(
                [Accumulator, Item, Index],
                Assign( Accumulator, Seed ),
                Assign( Index, Constant( 0 ) ),
                ForEach(
                    Collection,
                    Item,
                    Block(
                        Assign( Accumulator, Body ),
                        PostIncrementAssign( Index )
                    )
                ),
                Accumulator
            );
        }

        return Block(
            [Accumulator, Item],
            Assign( Accumulator, Seed ),
            ForEach(
                Collection,
                Item,
                Assign( Accumulator, Body )
            ),
            Accumulator
        );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newCollection = visitor.Visit( Collection );
        var newSeed = visitor.Visit( Seed );
        var newBody = visitor.Visit( Body );

        if ( newCollection == Collection && newSeed == Seed && newBody == Body )
            return this;

        return new ReduceExpression( newCollection, newSeed, newBody );
    }
}
public static partial class ExpressionExtensions
{
    public static ReduceExpression Reduce(
        Expression collection,
        Expression seed,
        ReduceBody body )
    {
        return new ReduceExpression( collection, seed, body );
    }

    public static ReduceExpression Reduce( 
        Expression collection, 
        Expression seed, 
        ReduceBodyIndex body )
    {
        return new ReduceExpression( collection, seed, body );
    }

    public static ReduceExpression Reduce( 
        Expression collection, 
        Expression seed, 
        ReduceBodyIndexSource body )
    {
        return new ReduceExpression( collection, seed, body );
    }
}
