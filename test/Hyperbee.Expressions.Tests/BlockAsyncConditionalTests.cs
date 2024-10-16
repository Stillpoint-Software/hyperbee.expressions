using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.AsyncExpression;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncConditionalTests
{
    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithIfThenCondition()
    {
        // Arrange: Condition depends on awaited value
        var condition = Await( Constant( Task.FromResult( true ) ) );
        var block = BlockAsync(
            IfThen( condition, Constant( 1 ) )
        );
        var lambda = Lambda<Func<Task>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        await compiledLambda();

        // Assert
        Assert.IsTrue( true ); // No exception means condition and block executed successfully
    }

    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithIfThenElseTrueBranch()
    {
        // Arrange: IfTrue branch contains an awaited task
        var condition = Constant( true );
        var block = BlockAsync(
            Condition( condition,
                Await( Constant( Task.FromResult( 1 ) ) ),
                Constant( 0 )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
    }

    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithIfThenElseFalseBranch()
    {
        // Arrange: IfFalse branch contains an awaited task
        var condition = Constant( false );
        var block = BlockAsync(
            Condition( condition,
                Constant( 0 ),
                Await( Constant( Task.FromResult( 2 ) ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result );
    }

    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithConditionalInTest()
    {
        // Arrange: Test depends on awaited value
        var test = Await( Constant( Task.FromResult( true ) ) );
        var block = BlockAsync(
            Condition( test, Constant( 1 ), Constant( 0 ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
    }

    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInTrueAndFalseBranches()
    {
        // Arrange: Both branches return values from awaited tasks
        var condition = Constant( true );
        var block = BlockAsync(
            Condition( condition,
                Await( Constant( Task.FromResult( 10 ) ) ),
                Await( Constant( Task.FromResult( 20 ) ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result ); // Condition is true, so the true branch should be awaited
    }

    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitBeforeAndAfterConditional()
    {
        // Arrange: Await a task before and after a conditional expression
        var block = BlockAsync(
            Await( Constant( Task.FromResult( 5 ) ) ),
            Condition( Constant( true ), Constant( 10 ), Constant( 0 ) ),
            Await( Constant( Task.FromResult( 15 ) ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result ); // Last awaited value should be 15
    }

    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithFalseCondition()
    {
        // Arrange: False condition should lead to the false branch being executed
        var condition = Constant( false );
        var block = BlockAsync(
            Condition( condition,
                Constant( 10 ),
                Await( Constant( Task.FromResult( 20 ) ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 20, result ); // False branch should be awaited and return 20
    }

    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithComplexConditionalLogic()
    {
        // Arrange: Two conditionals where both branches return awaited values
        var block = BlockAsync(
            Condition( Constant( true ),
                Await( Constant( Task.FromResult( 10 ) ) ),
                Await( Constant( Task.FromResult( 5 ) ) )
            ),
            Condition( Constant( false ),
                Await( Constant( Task.FromResult( 1 ) ) ),
                Await( Constant( Task.FromResult( 2 ) ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Second condition is false, so the false branch returns 2
    }

    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithNestedConditionals()
    {
        // Arrange: Conditionals nested inside each other
        var block = BlockAsync(
            Condition(
                Constant( true ),
                Condition(
                    Constant( false ),
                    Await( Constant( Task.FromResult( 5 ) ) ),
                    Await( Constant( Task.FromResult( 10 ) ) )
                ),
                Constant( 0 )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result ); // The true branch contains another conditional, false branch executed
    }

    [TestMethod]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithConditionalReturningTask()
    {
        // Arrange: The result of the conditional is an awaited Task
        var block = BlockAsync(
            Await(
                Condition(
                    Constant( true ),
                    Constant( Task.FromResult( 15 ) ),
                    Constant( Task.FromResult( 20 ) )
                )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result ); // True branch task should be awaited and return 15
    }

    [TestMethod]
    [ExpectedException( typeof( NullReferenceException ) )]
    public async Task AsyncBlock_ShouldThrowException_WithNullTaskInConditional()
    {
        // Arrange: One of the branches returns a null task, leading to exception
        var block = BlockAsync(
            Condition(
                Constant( true ),
                Await( Constant( null, typeof( Task<string> ) ) ),
                Constant( "false" )
            )
        );
        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile();

        // Act & Assert
        await compiledLambda();
    }

}
