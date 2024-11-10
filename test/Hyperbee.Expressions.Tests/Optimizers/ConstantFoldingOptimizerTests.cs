using Hyperbee.Expressions.Optimizers;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class ConstantFoldingOptimizerTests
{
    [TestMethod]
    public void ConstantSimplification_ShouldEvaluateConstants()
    {
        // Arrange
        var referenceExpr = Lambda<Func<int>>(
            Add( Constant( 2 ), Constant( 3 ) )
        );

        var optimizedExpr = new ConstantFoldingOptimizer()
            .Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 5, result );
        Assert.AreEqual( 5, comparand );
    }

    [TestMethod]
    public void ConstantSimplification_ShouldPropagateConstants()
    {
        // Arrange
        var constant = Constant( 10 );
        var referenceExpr = Lambda<Func<int>>(
            Multiply( constant, Constant( 2 ) )
        );

        var optimizedExpr = new ConstantFoldingOptimizer()
            .Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 20, result );
        Assert.AreEqual( 20, comparand );
    }
}
