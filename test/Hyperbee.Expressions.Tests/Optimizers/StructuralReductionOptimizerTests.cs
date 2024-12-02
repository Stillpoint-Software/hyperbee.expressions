using System.Linq.Expressions;
using Hyperbee.Expressions.Optimizers;

namespace Hyperbee.Expressions.Tests.Optimizers;

[TestClass]
public class StructuralReductionOptimizerTests
{
    [TestMethod]
    public void StructuralReduction_ShouldCompile()
    {
        // Arrange

        var labelTarget = Expression.Label( typeof(int) );
        var paramX = Expression.Parameter( typeof(int), "x" );
        var counter = Expression.Parameter( typeof(int), "counter" );

        var targetBlock = Expression.Block(
            [paramX, counter], // Declare 'counter' here to ensure it is scoped correctly
            Expression.Block(
                Expression.Block(
                    Expression.IfThen(
                        Expression.Constant( true ),
                        Expression.Assign(
                            counter,
                            Expression.Add( counter, Expression.Constant( 30 + 12 ) )
                        )
                    ),
                    Expression.Goto( labelTarget, Expression.Constant( 0 ) ) // Redundant Goto
                )
            ),
            Expression.TryCatch(
                Expression.Empty(),
                Expression.Catch( Expression.Parameter( typeof(Exception), "ex" ), Expression.Empty() )
            )
        );

        var block = Expression.Block(
            [paramX, counter], // Declare variables in the outer block
            Expression.Assign( paramX, Expression.Constant( 10 ) ),
            Expression.Assign( counter, Expression.Constant( 0 ) ),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan( counter, Expression.Constant( 1000 ) ),
                    targetBlock,
                    Expression.Break( labelTarget, Expression.Constant( 0 ) )
                ),
                labelTarget
            )
        );

        var optimizer = new StructuralReductionOptimizer();

        // Act
        var optimized = optimizer.Optimize( block );

        // Assert
        var compiled = Expression.Lambda<Func<int>>( optimized ).Compile();
    }

    [TestMethod]
    public void StructuralReduction_ShouldRemoveUnreachableCode()
    {
        // Before: .Block(.Constant(1), .Constant(2))
        // After:  .Constant(1)

        // Arrange
        var block = Expression.Block( Expression.Constant( 1 ), Expression.Constant( 2 ) );
        var optimizer = new StructuralReductionOptimizer();

        // Act
        var result = optimizer.Optimize( block );
        var value = ((ConstantExpression) ((BlockExpression) result).Expressions[0]).Value;

        // Assert
        Assert.AreEqual( 1, value );
    }

    [TestMethod]
    public void StructuralReduction_ShouldSimplifyEmptyTryCatch()
    {
        // Before: .TryCatch(.Empty(), .Catch(...))
        // After:  .Empty()

        // Arrange
        var tryCatch = Expression.TryCatch(
            Expression.Empty(),
            Expression.Catch( Expression.Parameter( typeof( Exception ) ), Expression.Empty() )
        );
        var optimizer = new StructuralReductionOptimizer();

        // Act
        var result = optimizer.Optimize( tryCatch );

        // Assert
        Assert.IsInstanceOfType( result, typeof( DefaultExpression ) );
    }

    [TestMethod]
    public void StructuralReduction_ShouldRemoveInfiniteLoop()
    {
        // Before: .Loop(.Constant(1))
        // After:  .Empty()

        // Arrange
        var loop = Expression.Loop( Expression.Constant( 1 ) );
        var optimizer = new StructuralReductionOptimizer();

        // Act
        var result = optimizer.Optimize( loop );

        // Assert
        Assert.IsInstanceOfType( result, typeof( DefaultExpression ) );
    }

    [TestMethod]
    public void StructuralReduction_ShouldSimplifyNestedConditionalExpression()
    {
        // Before: .Block(.IfThenElse(.Constant(true), .IfThenElse(.Constant(false), .Break(), .Constant("B"))))
        // After:  .Constant("B")

        // Arrange
        var innerCondition = Expression.IfThenElse(
            Expression.Constant( false ),
            Expression.Break( Expression.Label() ),
            Expression.Constant( "B" )
        );
        var outerCondition = Expression.IfThenElse(
            Expression.Constant( true ),
            innerCondition,
            Expression.Constant( "C" )
        );
        var block = Expression.Block( outerCondition );
        var optimizer = new StructuralReductionOptimizer();

        // Act
        var result = optimizer.Optimize( block );
        var value = ((ConstantExpression) result).Value;

        // Assert
        Assert.AreEqual( "B", value );
    }

    [TestMethod]
    public void StructuralReduction_ShouldSimplifyLoopWithComplexCondition()
    {
        // Before: .Loop(.IfThenElse(.Constant(false), .Break(), .Constant(1)))
        // After:  .Empty()

        // Arrange
        var loopCondition = Expression.IfThenElse(
            Expression.Constant( false ),
            Expression.Break( Expression.Label() ),
            Expression.Constant( 1 )
        );
        var loop = Expression.Loop( loopCondition );
        var optimizer = new StructuralReductionOptimizer();

        // Act
        var result = optimizer.Optimize( loop );

        // Assert
        Assert.IsInstanceOfType( result, typeof( DefaultExpression ) );
    }
}
