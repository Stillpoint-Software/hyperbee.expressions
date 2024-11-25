using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public class DebugExpression : Expression
{
    public Delegate DebugDelegate { get; }
    public IReadOnlyList<Expression> Arguments { get; }
    public Expression DebugCondition { get; }

    public DebugExpression( Delegate debugDelegate, Expression condition, params Expression[] arguments )
    {
        ArgumentNullException.ThrowIfNull( debugDelegate, nameof( debugDelegate ) );
        ArgumentNullException.ThrowIfNull( arguments, nameof( arguments ) );

        if ( condition != null && condition.Type != typeof( bool ) )
            throw new ArgumentException( "Condition must be a boolean expression.", nameof( condition ) );

        DebugDelegate = debugDelegate;
        DebugCondition = condition;
        Arguments = arguments.ToList();
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
    public static DebugExpression Debug( Delegate debugDelegate, params Expression[] arguments )
    {
        return new DebugExpression( debugDelegate, null, arguments );
    }

    public static DebugExpression DebugCondition( Delegate debugDelegate, Expression condition, params Expression[] arguments )
    {
        return new DebugExpression( debugDelegate, condition, arguments );
    }
}
