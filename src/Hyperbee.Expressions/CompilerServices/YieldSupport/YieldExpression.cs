using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Transactions;

namespace Hyperbee.Expressions.CompilerServices.YieldSupport;

public class YieldExpression : Expression
{
    public Expression? Value { get; }
    public bool IsReturn { get; }

    public YieldExpression( Expression value ) : this( value, true )
    {
    }

    public YieldExpression() : this( null, false )
    {
    }

    private YieldExpression( Expression? value, bool isReturn )
    {
        if ( isReturn && value == null )
            throw new ArgumentNullException( nameof( value ), "Yield return must have a value." );

        Value = value;
        IsReturn = isReturn;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => IsReturn ? Value!.Type : typeof( void );
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        //if ( IsReturn )
        //{
        //    return Block(
        //        Assign( State, Constant( -1 ) ),
        //        Assign( Current, Value ),
        //        Return( Constant( true ) )
        //    );
        //}


        // TODO: Hack
        return Value ?? Default( Type );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newTarget = visitor.Visit( Value );

        return newTarget == Value
            ? this
            : new YieldExpression( newTarget, IsReturn );
    }
}

public static partial class ExpressionExtensions
{
    public static YieldExpression YieldReturn( Expression value )
    {
        return new YieldExpression( value );
    }

    public static YieldExpression YieldBreak()
    {
        return new YieldExpression();
    }

}
