using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class DebugExpressionTests
{
    [TestMethod]
    public void DebugExpression_Should_Invoke_DebugDelegate_Unconditionally()
    {
        // Arrange
        var called = false;
        void DebugAction( string message ) => called = message == "Test Message";

        var message = Constant( "Test Message" );
        var debugExpr = Debug( DebugAction, [message] );

        // Act
        var lambda = Lambda<Action>( debugExpr ).Compile();
        lambda();

        // Assert
        Assert.IsTrue( called, "Debug delegate should have been invoked." );
    }

    [TestMethod]
    public void DebugExpression_Should_Invoke_DebugDelegate_Unconditionally1()
    {
        // Arrange
        var called = false;
        void DebugAction( string message1, string message2 ) => called = message1 == "Test Message 1" && message2 == "Test Message 2";

        var message1 = Constant( "Test Message 1" );
        var message2 = Constant( "Test Message 2" );
        var debugExpr = Debug( DebugAction, [message1, message2] );

        // Act
        var lambda = Lambda<Action>( debugExpr ).Compile();
        lambda();

        // Assert
        Assert.IsTrue( called, "Debug delegate should have been invoked." );
    }

    [TestMethod]
    public void DebugExpression_Should_Invoke_DebugDelegate_When_Condition_Is_True()
    {
        // Arrange
        var called = false;
        void DebugAction( int value ) => called = value == 42;

        var value = Constant( 42 );
        var condition = Constant( true );
        var debugExpr = Debug( DebugAction, condition, value );

        // Act
        var lambda = Lambda<Action>( debugExpr ).Compile();
        lambda();

        // Assert
        Assert.IsTrue( called, "Debug delegate should have been invoked when condition is true." );
    }

    [TestMethod]
    public void DebugExpression_Should_Not_Invoke_DebugDelegate_When_Condition_Is_False()
    {
        // Arrange
        var called = false;
        void DebugAction( int value ) => called = value == 42;

        var value = Constant( 42 );
        var condition = Constant( false );
        var debugExpr = Debug( DebugAction, condition, value );

        // Act
        var lambda = Lambda<Action>( debugExpr ).Compile();
        lambda();

        // Assert
        Assert.IsFalse( called, "Debug delegate should not have been invoked when condition is false." );
    }

    [TestMethod]
    [ExpectedException( typeof( ArgumentException ) )]
    public void DebugExpression_Should_Throw_If_Condition_Is_Not_Boolean()
    {
        // Arrange
        void DebugAction( int value )
        {
        }

        var invalidCondition = Constant( 42 );

        // Act
        _ = Debug( DebugAction, invalidCondition, Constant( 10 ) );

        // Assert: Exception is expected
    }

    [TestMethod]
    public void DebugExpression_Should_Work_Within_Complex_Block()
    {
        // Arrange
        var called = false;
        void DebugAction( int value ) => called = value == 15;

        var x = Parameter( typeof( int ), "x" );
        var y = Parameter( typeof( int ), "y" );
        var sum = Add( x, y );

        var debugExpr = Debug( DebugAction, sum );

        var block = Block( debugExpr, sum );

        // Act
        var lambda = Lambda<Func<int, int, int>>( block, x, y ).Compile();
        var result = lambda( 10, 5 );

        // Assert
        Assert.IsTrue( called, "Debug delegate should have been invoked within the block." );
        Assert.AreEqual( 15, result, "Block should correctly compute the sum." );
    }
}
