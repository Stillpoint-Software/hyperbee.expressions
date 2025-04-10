using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public class UsingExpression : Expression
{
    public ParameterExpression DisposeVariable { get; }
    public Expression Disposable { get; }
    public Expression Body { get; }

    internal UsingExpression( ParameterExpression variable, Expression disposable, Expression body )
    {
        if ( !typeof( IDisposable ).IsAssignableFrom( disposable.Type ) )
            throw new ArgumentException( $"The disposable expression must return an {nameof( IDisposable )}.", nameof( disposable ) );

        DisposeVariable = variable;
        Disposable = disposable;
        Body = body;
    }

    public override Type Type => Body.Type;
    public override ExpressionType NodeType => ExpressionType.Extension;

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        var disposableVariable = DisposeVariable ?? Variable( Disposable.Type );
        var disposableAssignment = Assign( disposableVariable, Disposable );

        var finallyBlock = IfThen(
            NotEqual( disposableVariable, Constant( null ) ),
            Call( disposableVariable, nameof( IDisposable.Dispose ), Type.EmptyTypes )
        );

        return Block(
            [disposableVariable],
            disposableAssignment,
            TryFinally( Body, finallyBlock )
        );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newDisposeVariable = DisposeVariable != null 
            ? visitor.VisitAndConvert( DisposeVariable, nameof( VisitChildren ) )
            : null;

        var newDisposable = visitor.Visit( Disposable );
        var newBody = visitor.Visit( Body );

        if ( newDisposeVariable == DisposeVariable && newDisposable == Disposable && newBody == Body )
            return this;

        return new UsingExpression( newDisposeVariable, newDisposable, newBody );

    }
}

public static partial class ExpressionExtensions
{
    public static UsingExpression Using( ParameterExpression variable, Expression disposable, Expression body )
    {
        return new UsingExpression( variable, disposable, body );
    }

    public static UsingExpression Using( Expression disposable, Expression body )
    {
        return new UsingExpression( null, disposable, body );
    }
}
