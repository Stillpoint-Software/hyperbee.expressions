﻿using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.AsyncExpression;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncBasicTests
{
    [TestMethod]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithCompletedTask()
    {
        // Arrange
        var block = BlockAsync( Await( Constant( Task.FromResult( 1 ) ) ) );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        //Assert
        Assert.AreEqual( 1, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithDelayedTask()
    {
        // Arrange
        var delayedTask = Task.Delay( 100 ).ContinueWith( _ => 5 );
        var block = BlockAsync( Await( Constant( delayedTask ) ) );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 5, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitMultipleTasks_WithDifferentResults()
    {
        // Arrange
        var block = BlockAsync(
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( Constant( Task.FromResult( 2 ) ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Last awaited result should be returned
    }

    [TestMethod]
    [ExpectedException( typeof( InvalidOperationException ) )]
    public async Task BlockAsync_ShouldThrowException_WithFaultedTask()
    {
        // Arrange
        var block = BlockAsync(
            Await( Constant( Task.FromException<int>( new InvalidOperationException( "Test Exception" ) ) ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act & Assert
        await compiledLambda();
    }

    [TestMethod]
    [ExpectedException( typeof( TaskCanceledException ) )]
    public async Task BlockAsync_ShouldHandleCanceledTask_WithCancellation()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var canceledTask = Task.FromCanceled<int>( cancellationTokenSource.Token );
        var block = BlockAsync( Await( Constant( canceledTask ) ) );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act & Assert
        await compiledLambda();
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitNestedBlockAsync_WithNestedTasks()
    {
        // Arrange
        var innerBlock = BlockAsync(
            Await( Constant( Task.FromResult( 2 ) ) )
        );
        var block = BlockAsync(
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( innerBlock )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Result from the nested block
    }

    [TestMethod]
    public async Task BlockAsync_ShouldHandleSyncAndAsync_WithMixedOperations()
    {
        // Arrange
        var block = BlockAsync(
            Constant( 10 ),
            Await( Constant( Task.FromResult( 20 ) ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 20, result ); // The last operation is asynchronous and returns 20
    }

    [TestMethod]
    public async Task BlockAsync_ShouldAwaitSuccessfully_WithTask()
    {
        // Arrange
        var block = BlockAsync( Await( Constant( Task.CompletedTask, typeof(Task) ) ) );
        var lambda = Lambda<Func<Task>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        await compiledLambda(); // Should complete without exception

        // Assert
        Assert.IsTrue( true ); // If no exception, the test is successful
    }
}