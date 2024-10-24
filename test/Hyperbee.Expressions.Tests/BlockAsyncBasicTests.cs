using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

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
        var block = BlockAsync( Await( Constant( Task.CompletedTask, typeof( Task ) ) ) );
        var lambda = Lambda<Func<Task>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        await compiledLambda(); // Should complete without exception

        // Assert
        Assert.IsTrue( true ); // If no exception, the test is successful
    }

    [TestMethod]
    public async Task BlockAsync_ShouldPreserveVariableAssignment_BeforeAndAfterAwait()
    {
        // Arrange
        var resultValue = Parameter( typeof( int ), "result" );

        var block = BlockAsync(
            [resultValue],
            Assign( resultValue, Constant( 5 ) ),
            Await( Constant( Task.Delay( 100 ) ) ),
            Assign( resultValue, Add( resultValue, Constant( 10 ) ) ),
            resultValue // Return the result
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldPreserveVariablesInNestedBlock_WithAwaits()
    {
        // Arrange
        // NOTE: this test is also verifying the hoisting visitor and reuse of names
        var outerValue = Parameter( typeof(int), "value" );
        var innerValue = Parameter( typeof(string), "value" );
        var otherValue1 = Variable( typeof(int), "value" );
        var otherValue2 = Variable( typeof(string), "value" );
        var otherValue3 = Variable( typeof(string), "value" );

        Expression<Func<string, int>> test = s => int.Parse( s );

        var innerBlock = Lambda<Func<string, Task<string>>>(
            BlockAsync(
                [otherValue1, otherValue2],
                Block(
                    Assign( otherValue1, Constant( 100 ) ),
                    Assign( otherValue2, Constant( "200" ) ),
                    Await( Constant( Task.Delay( 100 ) ) ),
                    innerValue )
            ),
            parameters: [innerValue]
        );

        var block = BlockAsync(
            [otherValue3],
            Assign( otherValue3, Constant( "300" ) ),
            Assign( outerValue,
                Add( outerValue,
                    Invoke( test, Await( Invoke( innerBlock, Constant( "50" ) ) ) ) ) ),
            outerValue
        );

        var lambda = Lambda<Func<int, Task<int>>>( block, [outerValue] );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda( 5 );

        // Assert
        Assert.AreEqual( 55, result );
    }

    [TestMethod]
    public async Task BlockAsync_ShouldPreserveVariables_WithNestedAwaits()
    {
        Expression<Func<Task<int>>> initVariableAsync = () => Task.FromResult( 5 );
        Expression<Func<Task<bool>>> isTrueAsync = () => Task.FromResult( true );
        Expression<Func<int, int, Task<int>>> addAsync = ( a, b ) => Task.FromResult( a + b );

        var variable = Variable( typeof( int ), "variable" );

        var asyncBlock =
            BlockAsync(
                [variable],
                Assign( variable, Await( Invoke( initVariableAsync ) ) ),
                IfThen( Await( Invoke( isTrueAsync ) ),
                    Assign( variable,
                        Await( Invoke( addAsync, variable, variable ) ) ) ),
                variable );

        var lambda = (Lambda<Func<Task<int>>>(asyncBlock ).Reduce() as Expression<Func<Task<int>>>)!;
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result );
    }
}
