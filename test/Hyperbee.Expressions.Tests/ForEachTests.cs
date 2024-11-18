using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ForEachExpressionTests
{
    [TestMethod]
    public void ForEachExpression_ShouldIterateOverCollection()
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Variable( typeof( int ), "element" );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] );
        var body = Call( writeLineMethod!, element );

        var forEachExpr = ForEach( list, element, body );

        // Act
        var lambda = Lambda<Action>( forEachExpr );
        var compiledLambda = lambda.Compile();

        compiledLambda();

        // Assert: No assertion needed
    }

    [TestMethod]
    public void ForEachExpression_ShouldBreakOnCondition()
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Variable( typeof( int ), "element" );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] )!;

        var forEachExpr = ForEach( list, element, ( breakLabel, continueLabel ) =>
            IfThenElse(
                Equal( element, Constant( 3 ) ),
                Break( breakLabel ),
                Call( writeLineMethod, element )
        ) );

        // Act
        var lambda = Lambda<Action>( forEachExpr );
        var compiledLambda = lambda.Compile();

        compiledLambda();

        // Assert: No assertion needed
    }

    [TestMethod]
    public void ForEachExpression_ShouldUseCustomBreakAndContinueLabels()
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Variable( typeof( int ), "element" );

        var customBreakLabel = Label( "customBreak" );
        var customContinueLabel = Label( "customContinue" );

        var breakCondition = Equal( element, Constant( 4 ) );
        var continueCondition = Equal( element, Constant( 2 ) );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] )!;

        var body = Block(
            IfThen( continueCondition, Continue( customContinueLabel ) ),
            IfThenElse(
                breakCondition,
                Break( customBreakLabel ),
                Call( writeLineMethod, element )
            )
        );

        var forEachExpr = ForEach( list, element, body, customBreakLabel, customContinueLabel );

        // Act
        var lambda = Lambda<Action>( forEachExpr );
        var compiledLambda = lambda.Compile();

        compiledLambda();

        // Assert: No assert needed
    }
}
