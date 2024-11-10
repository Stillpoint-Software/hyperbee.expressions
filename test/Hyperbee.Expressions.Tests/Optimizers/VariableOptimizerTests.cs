using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class VariableOptimizerTests
{
    [TestMethod]
    public void VariableOptimization_ShouldRemoveUnusedVariable()
    {
        // Arrange
        var unusedVariable = Expression.Variable( typeof( int ), "unused" );
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Block( [unusedVariable],
                Expression.Assign( unusedVariable, Expression.Constant( 3 ) ),
                Expression.Constant( 5 )
            )
        );

        var optimizedExpr = new VariableOptimizer().Optimize( referenceExpr );

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
    public void VariableOptimization_ShouldInlineSingleUseVariable()
    {
        // Arrange
        var variable = Expression.Variable( typeof( int ), "x" );
        var referenceExpr = Expression.Lambda<Func<int>>(
            Expression.Block( [variable],
                Expression.Assign( variable, Expression.Constant( 5 ) ),
                Expression.Add( variable, Expression.Constant( 3 ) )
            )
        );

        var optimizedExpr = new VariableOptimizer().Optimize( referenceExpr );

        // Act
        var reference = referenceExpr.Compile();
        var result = reference();

        var optimized = optimizedExpr.Compile();
        var comparand = optimized();

        // Assert
        Assert.AreEqual( 8, result );
        Assert.AreEqual( 8, comparand );
    }
}
