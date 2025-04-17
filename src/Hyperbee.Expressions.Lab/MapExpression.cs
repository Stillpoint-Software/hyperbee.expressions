using System.Linq.Expressions;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Lab;

public delegate Expression MapBody( ParameterExpression item );
public delegate Expression MapBodyIndex( ParameterExpression item, ParameterExpression index );
public delegate Expression MapBodyIndexSource( ParameterExpression item, ParameterExpression index, Expression source );

public class MapExpression : Expression
{
    public Expression Collection { get; }
    public Expression Body { get; }

    public ParameterExpression Item { get; }
    public ParameterExpression Index { get; }
    public ParameterExpression Source { get; }
    public ParameterExpression ResultList { get; }

    public Type ResultType { get; }

    private MapExpression( Expression collection, Type resultType )
    {
        ArgumentNullException.ThrowIfNull( collection );
        ArgumentNullException.ThrowIfNull( resultType );

        if ( !typeof( System.Collections.IEnumerable ).IsAssignableFrom( collection.Type ) )
            throw new ArgumentException( "Collection must be IEnumerable", nameof( collection ) );

        Collection = collection;
        ResultType = resultType;

        var enumerableType = collection.Type.GetInterfaces()
            .Concat( [collection.Type] )
            .FirstOrDefault( t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof( IEnumerable<> ) );

        var itemType = enumerableType?.GetGenericArguments()[0] ?? typeof( object );

        Item = Variable( itemType, "item" );
        ResultList = Variable( typeof( List<> ).MakeGenericType( resultType ), "result" );
    }

    public MapExpression( Expression collection, Type resultType, MapBody body ) : this( collection, resultType )
    {
        Body = body( Item );
    }

    public MapExpression( Expression collection, Type resultType, MapBodyIndex body ) : this( collection, resultType )
    {
        Index = Variable( typeof( int ), "index" );
        Body = body( Item, Index );
    }

    public MapExpression( Expression collection, Type resultType, MapBodyIndexSource body ) : this( collection, resultType )
    {
        Index = Variable( typeof( int ), "index" );
        Source = Variable( collection.Type, "source" );
        Body = body( Item, Index, Collection );
    }

    public MapExpression( Expression collection, Type resultType, Expression body ) : this( collection, resultType )
    {
        ArgumentNullException.ThrowIfNull( body );
        Body = body;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( List<> ).MakeGenericType( ResultType );
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        var addMethod = typeof( List<> )
            .MakeGenericType( ResultType )
            .GetMethod( "Add" )!;

        if ( Source != null && Index != null )
        {
            return Block(
                [Item, ResultList, Index, Source],
                Assign( ResultList, New( ResultList.Type ) ),
                Assign( Source, Collection ),
                Assign( Index, Constant( 0 ) ),
                ForEach( Source, Item,
                    Block(
                        Call( ResultList, addMethod, Body ),
                        PostIncrementAssign( Index )
                    )
                ),
                ResultList
            );
        }

        if ( Index != null )
        {
            return Block(
                [Item, ResultList, Index],
                Assign( ResultList, New( ResultList.Type ) ),
                Assign( Index, Constant( 0 ) ),
                ForEach( Collection, Item,
                    Block(
                        Call( ResultList, addMethod, Body ),
                        PostIncrementAssign( Index )
                    )
                ),
                ResultList
            );
        }

        return Block(
            [Item, ResultList],
            Assign( ResultList, New( ResultList.Type ) ),
            ForEach( Collection, Item,
                Call( ResultList, addMethod, Body )
            ),
            ResultList
        );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newCollection = visitor.Visit( Collection );
        var newBody = visitor.Visit( Body );

        if ( newCollection == Collection && newBody == Body )
            return this;

        return new MapExpression( newCollection, ResultType, newBody );
    }
}
public static partial class ExpressionExtensions
{
    public static MapExpression Map( Expression collection, Type resultType, MapBody body )
        => new( collection, resultType, body );

    public static MapExpression Map( Expression collection, MapBody body )
        => new( collection, GetGenericType( collection.Type ), body );

    public static MapExpression Map( Expression collection, Type resultType, MapBodyIndex body )
        => new( collection, resultType, body );

    public static MapExpression Map( Expression collection, MapBodyIndex body )
        => new( collection, GetGenericType( collection.Type ), body );

    public static MapExpression Map( Expression collection, Type resultType, MapBodyIndexSource body )
        => new( collection, resultType, body );

    public static MapExpression Map( Expression collection, MapBodyIndexSource body )
        => new( collection, GetGenericType( collection.Type ), body );

    private static Type GetGenericType( Type type )
    {
        var enumerableType = type == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces()
                .FirstOrDefault( t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>) );

        var enumerableGenericType = enumerableType?.GetGenericArguments()[0] ?? typeof( object );

        if( enumerableGenericType == null )
            throw new ArgumentException( "Collection must be IEnumerable", nameof( type ) );

        return enumerableGenericType;
    }

}
