using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class InliningOptimizerTests
{
    [TestMethod]
    public void Inlining_ShouldInlineSimpleLambda()
    {
        // Arrange
        var x = Expression.Parameter( typeof( int ), "x" );
        var addLambda = Expression.Lambda<Func<int, int>>( Expression.Add( x, Expression.Constant( 5 ) ), x );
        var referenceExpr = Expression.Lambda<Func<int>>( Expression.Invoke( addLambda, Expression.Constant( 3 ) ) );

        var optimizedExpr = new InliningOptimizer().Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 8, result );
        Assert.AreEqual( 8, comparand );
    }


    [TestMethod]
    public void Inlining_ShouldShortCircuitBoolean()
    {
        // Arrange
        var referenceExpr = Expression.Lambda<Func<bool>>(
            Expression.AndAlso( Expression.Constant( true ), Expression.Constant( false ) )
        );

        var optimizedExpr = new InliningOptimizer().Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( false, result );
        Assert.AreEqual( false, comparand );
    }
}
