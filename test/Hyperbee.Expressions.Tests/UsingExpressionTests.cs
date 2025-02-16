﻿using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class UsingExpressionTests
{
    private class TestDisposableResource : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private bool _wasBodyExecuted;

    public void SetWasBodyExecuted()
    {
        _wasBodyExecuted = true;
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
        var disposableVariable = Variable( typeof( TestDisposableResource ) );
        var disposableExpression = Constant( resource, typeof( TestDisposableResource ) );

        var bodyExpression = Condition( Property( disposableVariable, nameof( TestDisposableResource.IsDisposed ) ), Constant( true ), Constant( false ) );

        // Act
        var usingExpression = Using( disposableVariable, disposableExpression, bodyExpression );

        var lambda = Lambda<Func<bool>>( usingExpression );
        var compiledLambda = lambda.Compile();

        var result = compiledLambda();

        // Assert
        Assert.IsFalse( result );
        Assert.IsTrue( resource.IsDisposed, "Resource should be disposed after using the expression." );
    }

    [TestMethod]
    public void UsingExpression_ShouldExecuteBodyExpression()
    {
        // Arrange
        var resource = new TestDisposableResource();
        var disposableExpression = Constant( resource, typeof( TestDisposableResource ) );

        // Create a body expression that sets 'wasBodyExecuted' to true
        var bodyExpression = Call(
            Constant( this ),
            typeof( UsingExpressionTests ).GetMethod( nameof( SetWasBodyExecuted ) )!
        );

        // Act
        var usingExpression = Using( disposableExpression, bodyExpression );

        var lambda = Lambda<Action>( usingExpression );
        var compiledLambda = lambda.Compile();

        compiledLambda();

        // Assert
        Assert.IsTrue( _wasBodyExecuted, "The body expression should be executed." );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task UsingExpression_ShouldExecuteAsyncExpression( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var resource = new TestDisposableResource();
        var disposableExpression = Constant( resource, typeof( TestDisposableResource ) );

        var bodyExpression = BlockAsync(
            Await( AsyncHelper.Completer( Constant( completer ), Constant( 10 ) ) )
        );

        // Act
        var usingExpression = Using( disposableExpression, bodyExpression );

        var lambda = Lambda<Func<Task<int>>>( usingExpression );
        var compiledLambda = lambda.Compile( compiler );

        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task UsingExpression_ShouldExecuteAsyncExpression_WithInnerUsing( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var resource = new TestDisposableResource();
        var disposableExpression = Constant( resource, typeof( TestDisposableResource ) );

        var bodyExpression = BlockAsync(
            Using(
                disposableExpression,
                Await( AsyncHelper.Completer( Constant( completer ), Constant( 10 ) ) )
            )
        );

        // Act
        var lambda = Lambda<Func<Task<int>>>( bodyExpression );
        var compiledLambda = lambda.Compile( compiler );

        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result );
    }

    [TestMethod]
    [ExpectedException( typeof( ArgumentException ) )]
    public void UsingExpression_ShouldThrowArgumentException_WhenNonDisposableUsed()
    {
        // Arrange
        var nonDisposableExpression = Constant( "non-disposable string" );

        // Act
        Using( nonDisposableExpression, Empty() );

        // Assert: Expect an ArgumentException due to non-disposable resource
        // The constructor should throw the exception, no need for further assertions
    }

    [TestMethod]
    public void UsingExpression_ShouldDisposeResource_EvenIfExceptionThrown()
    {
        // Arrange
        var resource = new TestDisposableResource();
        var disposableExpression = Constant( resource, typeof( TestDisposableResource ) );

        var bodyExpression = Throw( New( typeof( Exception ) ) );

        // Act
        var usingExpression = Using( disposableExpression, bodyExpression );

        var lambda = Lambda<Action>( usingExpression );
        var compiledLambda = lambda.Compile();

        // Assert: Execute the expression and catch the exception, check if the resource was disposed
        try
        {
            compiledLambda();
        }
        catch ( Exception )
        {
            // Expected exception
        }

        Assert.IsTrue( resource.IsDisposed, "Resource should be disposed even if an exception is thrown." );
    }
}
