using System.Linq.Expressions;
using Hyperbee.Expressions.Shared;

namespace Hyperbee.Expressions;

public class DebugExpression : Expression
{
    public Delegate DebugDelegate { get; }
    public IReadOnlyList<Expression> Arguments { get; }
    public Expression DebugCondition { get; }

    public DebugExpression( Delegate debugDelegate, Expression condition, Expression[] arguments )
    {
        ThrowIf.Null( debugDelegate, nameof( debugDelegate ) );
        ThrowIf.Null( arguments, nameof( arguments ) );

        if ( condition != null && condition.Type != typeof( bool ) )
            throw new ArgumentException( "Condition must be a boolean expression.", nameof( condition ) );

        DebugDelegate = debugDelegate;
        DebugCondition = condition;
        Arguments = [.. arguments];
    }

    public override Type Type => typeof( void );
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        var delegateExpression = Constant( DebugDelegate );

        if ( DebugCondition == null )
        {
            return Invoke( delegateExpression, Arguments );
        }

        return IfThen( DebugCondition, Invoke( delegateExpression, Arguments ) );
    }
}

public static partial class ExpressionExtensions
{
    public static DebugExpression Debug( Delegate debugDelegate, Expression argument )
    {
        return new DebugExpression( debugDelegate, null, [argument] );
    }

    public static DebugExpression Debug( Delegate debugDelegate, Expression[] arguments )
    {
        return new DebugExpression( debugDelegate, null, arguments );
    }

    public static DebugExpression Debug( Delegate debugDelegate, Expression condition, Expression argument )
    {
        return new DebugExpression( debugDelegate, condition, [argument] );
    }

    public static DebugExpression Debug( Delegate debugDelegate, Expression condition, Expression[] arguments )
    {
        return new DebugExpression( debugDelegate, condition, arguments );
    }
}
