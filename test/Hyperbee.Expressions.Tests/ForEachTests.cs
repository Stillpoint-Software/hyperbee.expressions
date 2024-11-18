using System.Linq.Expressions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ForEachExpressionTests
{
    [TestMethod]
    public void ForEachExpression_ShouldIterateOverCollection()
    {
        // Arrange
        var list = Expression.Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Expression.Variable( typeof( int ), "element" );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] );
        var body = Expression.Call( writeLineMethod!, element );

        var forEachExpr = ExpressionExtensions.ForEach( list, element, body );

        // Act
        var lambda = Expression.Lambda<Action>( forEachExpr );
        var compiledLambda = lambda.Compile();

        compiledLambda();

        // Assert: No assertion needed
    }

    [TestMethod]
    public void ForEachExpression_ShouldBreakOnCondition()
    {
        // Arrange
        var list = Expression.Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Expression.Variable( typeof( int ), "element" );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] )!;

        var forEachExpr = ExpressionExtensions.ForEach( list, element, ( breakLabel, continueLabel ) =>
            Expression.IfThenElse(
                Expression.Equal( element, Expression.Constant( 3 ) ),
                Expression.Break( breakLabel ),
                Expression.Call( writeLineMethod, element )
        ) );

        // Act
        var lambda = Expression.Lambda<Action>( forEachExpr );
        var compiledLambda = lambda.Compile();

        compiledLambda();

        // Assert: No assertion needed
    }

    [TestMethod]
    public void ForEachExpression_ShouldUseCustomBreakAndContinueLabels()
    {
        // Arrange
        var list = Expression.Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Expression.Variable( typeof( int ), "element" );

        var customBreakLabel = Expression.Label( "customBreak" );
        var customContinueLabel = Expression.Label( "customContinue" );

        var breakCondition = Expression.Equal( element, Expression.Constant( 4 ) );
        var continueCondition = Expression.Equal( element, Expression.Constant( 2 ) );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] )!;

        var body = Expression.Block(
            Expression.IfThen( continueCondition, Expression.Continue( customContinueLabel ) ),
            Expression.IfThenElse(
                breakCondition,
                Expression.Break( customBreakLabel ),
                Expression.Call( writeLineMethod, element )
            )
        );

        var forEachExpr = ExpressionExtensions.ForEach( list, element, body, customBreakLabel, customContinueLabel );

        // Act
        var lambda = Expression.Lambda<Action>( forEachExpr );
        var compiledLambda = lambda.Compile();

        compiledLambda();

        // Assert: No assert needed
    }
}
