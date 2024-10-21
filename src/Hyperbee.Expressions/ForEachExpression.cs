using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.Expressions;

public class ForEachExpression : Expression
{
    private static readonly MethodInfo GetCurrentMethod;
    private static readonly MethodInfo MoveNextMethod;
    private static readonly MethodInfo GetEnumeratorMethod;

    public Expression Collection { get; }
    public ParameterExpression Element { get; }
    public Expression Body { get; }

    public LabelTarget BreakLabel { get; } = Label( "break" );
    public LabelTarget ContinueLabel { get; } = Label( "continue" );

    static ForEachExpression()
    {
        GetEnumeratorMethod = typeof( IEnumerable ).GetMethod( "GetEnumerator" );
        MoveNextMethod = typeof( IEnumerator ).GetMethod( "MoveNext" );
        GetCurrentMethod = typeof( IEnumerator ).GetProperty( "Current" )!.GetMethod;
    }

    internal ForEachExpression( Expression collection, ParameterExpression element, Expression body )
    {
        ThrowIfInvalid( collection, element, body );

        Collection = collection;
        Element = element;
        Body = body;
    }

    internal ForEachExpression( Expression collection, ParameterExpression element, LoopBody body )
    {
        ThrowIfInvalid( collection, element, body );

        Collection = collection;
        Element = element;
        Body = body( BreakLabel, ContinueLabel );
    }

    internal ForEachExpression( Expression collection, ParameterExpression element, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        ThrowIfInvalid( collection, element, body );

        ArgumentNullException.ThrowIfNull( breakLabel, nameof( breakLabel ) );
        ArgumentNullException.ThrowIfNull( continueLabel, nameof( continueLabel ) );

        Collection = collection;
        Element = element;
        Body = body;
        BreakLabel = breakLabel;
        ContinueLabel = continueLabel;
    }

    private static void ThrowIfInvalid( Expression collection, ParameterExpression element, object body )
    {
        ArgumentNullException.ThrowIfNull( collection, nameof( collection ) );
        ArgumentNullException.ThrowIfNull( element, nameof( element ) );
        ArgumentNullException.ThrowIfNull( body, nameof( body ) );

        if ( !typeof( IEnumerable ).IsAssignableFrom( collection.Type ) )
            throw new ArgumentException( "Collection must implement IEnumerable.", nameof( collection ) );
    }

    public override Type Type => typeof( void );
    public override ExpressionType NodeType => ExpressionType.Extension;

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        var enumerator = Variable( typeof( IEnumerator ), "enumerator" );

        return Block(
            [enumerator, Element],
            Assign( enumerator, Call( Collection, GetEnumeratorMethod ) ), // Initialize the enumerator
            Loop(
                IfThenElse(
                    Call( enumerator, MoveNextMethod! ),
                    Block(
                        Assign( Element, Convert( Call( enumerator, GetCurrentMethod! ), Element.Type ) ),
                        Body
                    ),
                    Break( BreakLabel )
                )
                ,
                BreakLabel,
                ContinueLabel
            )
        );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newCollection = visitor.Visit( Collection );
        var newBody = visitor.Visit( Body );

        if ( newCollection != Collection || newBody != Body )
        {
            return new ForEachExpression( newCollection, Element, newBody, BreakLabel, ContinueLabel );
        }

        return this;
    }
}

public static partial class ExpressionExtensions
{
    public static ForEachExpression ForEach( Expression collection, ParameterExpression element, Expression body )
    {
        return new ForEachExpression( collection, element, body );
    }

    public static ForEachExpression ForEach( Expression collection, ParameterExpression element, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        return new ForEachExpression( collection, element, body, breakLabel, continueLabel );
    }

    public static ForEachExpression ForEach( Expression collection, ParameterExpression element, LoopBody body )
    {
        return new ForEachExpression( collection, element, body );
    }
}
