using System.Linq.Expressions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class UsingExpressionTests
{
    private bool _wasBodyExecuted;

    private class TestDisposableResource : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    [TestInitialize]
    public void Initialize()
    {
        _wasBodyExecuted = false;
    }

    [TestMethod]
    public void UsingExpression_ShouldDisposeResource_AfterUse()
    {
        // Arrange
        var resource = new TestDisposableResource();
        var disposableExpression = Expression.Constant( resource, typeof( TestDisposableResource ) );

        // Create a body expression that just writes to the console or does something simple
        var bodyExpression = Expression.Empty(); // No actual operation, just a placeholder

        // Act
        var usingExpression = new UsingExpression( disposableExpression, bodyExpression );
        var reducedExpression = usingExpression.Reduce();
        var action = Expression.Lambda<Action>( reducedExpression ).Compile();

        // Execute the expression (which should dispose the resource)
        action();

        // Assert
        Assert.IsTrue( resource.IsDisposed, "Resource should be disposed after using the expression." );
    }

    [TestMethod]
    public void UsingExpression_ShouldExecuteBodyExpression()
    {
        // Arrange
        var resource = new TestDisposableResource();
        var disposableExpression = Expression.Constant( resource, typeof( TestDisposableResource ) );

        // Create a body expression that sets 'wasBodyExecuted' to true
        var bodyExpression = Expression.Call(
            Expression.Constant( this ),
            typeof( UsingExpressionTests ).GetMethod( nameof( SetWasBodyExecuted ) )!
        );

        // Act
        var usingExpression = new UsingExpression( disposableExpression, bodyExpression );
        var reducedExpression = usingExpression.Reduce();
        var action = Expression.Lambda<Action>( reducedExpression ).Compile();

        action();

        // Assert
        Assert.IsTrue( _wasBodyExecuted, "The body expression should be executed." );
    }

    // Helper method used in the body expression
    public void SetWasBodyExecuted()
    {
        _wasBodyExecuted = true;
    }

    [TestMethod]
    [ExpectedException( typeof( ArgumentException ) )]
    public void UsingExpression_ShouldThrowArgumentException_WhenNonDisposableUsed()
    {
        // Arrange
        var nonDisposableExpression = Expression.Constant( "non-disposable string" );

        // Act
        _ = new UsingExpression( nonDisposableExpression, Expression.Empty() );

        // Assert: Expect an ArgumentException due to non-disposable resource
        // The constructor should throw the exception, no need for further assertions
    }

    [TestMethod]
    public void UsingExpression_ShouldDisposeResource_EvenIfExceptionThrown()
    {
        // Arrange
        var resource = new TestDisposableResource();
        var disposableExpression = Expression.Constant( resource, typeof( TestDisposableResource ) );

        // Create a body expression that throws an exception
        var bodyExpression = Expression.Throw( Expression.New( typeof( Exception ) ) );

        // Act
        var usingExpression = new UsingExpression( disposableExpression, bodyExpression );
        var reducedExpression = usingExpression.Reduce();
        var action = Expression.Lambda<Action>( reducedExpression ).Compile();

        // Assert: Execute the expression and catch the exception, check if the resource was disposed
        try
        {
            action();
        }
        catch ( Exception )
        {
            // Expected exception
        }

        // Assert that the resource was still disposed even though an exception was thrown
        Assert.IsTrue( resource.IsDisposed, "Resource should be disposed even if an exception is thrown." );
    }
}
