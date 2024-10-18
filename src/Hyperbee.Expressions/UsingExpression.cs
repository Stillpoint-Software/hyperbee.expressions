using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public class UsingExpression : Expression
{
    public Expression Disposable { get; }
    public Expression Body { get; }

    internal UsingExpression( Expression disposable, Expression body )
    {
        if ( !typeof(IDisposable).IsAssignableFrom( disposable.Type ) )
            throw new ArgumentException( "The disposable expression must return an IDisposable.", nameof(disposable) );

        Disposable = disposable;
        Body = body;
    }

    public override Type Type => Body.Type;
    public override ExpressionType NodeType => ExpressionType.Extension;

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        var disposableVar = Variable( Disposable.Type, "disposable" );
        var disposableAssignment = Assign( disposableVar, Disposable );

        var finallyBlock = IfThen(
            NotEqual( disposableVar, Constant( null ) ),
            Call( disposableVar, nameof(IDisposable.Dispose), Type.EmptyTypes )
        );

        return Block(
            [disposableVar],
            disposableAssignment,
            TryFinally( Body, finallyBlock )
        );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newDisposable = visitor.Visit( Disposable );
        var newBody = visitor.Visit( Body );

        if ( newDisposable != Disposable || newBody != Body )
            return new UsingExpression( newDisposable, newBody );

        return this;
    }
}

public static partial class ExpressionExtensions
{
    public static UsingExpression Using( Expression disposable, Expression body )
    {
        return new UsingExpression( disposable, body );
    }
}

