using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public static partial class ExpressionExtensions
{
    public static TResult Compile<TResult>(
        this Expression<TResult> expression,
        IServiceProvider serviceProvider,
        bool preferInterpretation = false )
    {
        ArgumentNullException.ThrowIfNull( serviceProvider, nameof( serviceProvider ) );

        var setter = new ServiceSetter( serviceProvider );

        return setter.Visit( expression ) is Expression<TResult> replacedExpression
            ? replacedExpression.Compile( preferInterpretation )
            : throw new InvalidOperationException( "Failed to compile expression." );
    }

    public static Delegate Compile(
        this LambdaExpression expression,
        IServiceProvider serviceProvider,
        bool preferInterpretation = false )
    {
        ArgumentNullException.ThrowIfNull( serviceProvider, nameof( serviceProvider ) );

        var setter = new ServiceSetter( serviceProvider );

        return setter.Visit( expression ) is LambdaExpression replacedExpression
            ? replacedExpression.Compile( preferInterpretation )
            : throw new InvalidOperationException( "Failed to compile expression." );
    }

    private class ServiceSetter( IServiceProvider serviceProvider ) : ExpressionVisitor
    {
        protected override Expression VisitExtension( Expression node )
        {
            switch ( node )
            {
                case IDependencyInjectionExpression injectExpression:
                    injectExpression.SetServiceProvider( serviceProvider );
                    return node;
                default:
                    return base.VisitExtension( node );
            }
        }
    }
}
