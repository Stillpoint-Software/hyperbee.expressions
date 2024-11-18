using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncConditionalTests
{
    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithIfThenCondition( bool immediateFlag )
    {
        // Arrange: Condition depends on awaited value
        var condition = Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( true )
                    ) );
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

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithIfThenElseTrueBranch( bool immediateFlag )
    {
        // Arrange: IfTrue branch contains an awaited task
        var condition = Constant( true );
        var block = BlockAsync(
            Condition( condition,
                Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 1 )
                    ) ),
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

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithIfThenElseFalseBranch( bool immediateFlag )
    {
        // Arrange: IfFalse branch contains an awaited task
        var condition = Constant( false );
        var block = BlockAsync(
            Condition( condition,
                Constant( 0 ),
                Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 2 )
                    ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result );
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithConditionalInTest( bool immediateFlag )
    {
        // Arrange: Test depends on awaited value
        var test = Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( true )
                    ) );
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

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInTrueAndFalseBranches( bool immediateFlag )
    {
        // Arrange: Both branches return values from awaited tasks
        var condition = Constant( true );
        var block = BlockAsync(
            Condition( condition,
                Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 10 )
                    ) ),
               Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 20 )
                    ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result ); // Condition is true, so the true branch should be awaited
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitBeforeAndAfterConditional( bool immediateFlag )
    {
        // Arrange: Await a task before and after a conditional expression
        var block = BlockAsync(
            Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 5 )
                    ) ),
            Condition( Constant( true ), Constant( 10 ), Constant( 0 ) ),
            Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 15 )
                    ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result ); // Last awaited value should be 15
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithFalseCondition( bool immediateFlag )
    {
        // Arrange: False condition should lead to the false branch being executed
        var condition = Constant( false );
        var block = BlockAsync(
            Condition( condition,
                Constant( 10 ),
                Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 20 )
                    ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 20, result ); // False branch should be awaited and return 20
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithComplexConditionalLogic( bool immediateFlag )
    {
        // Arrange: Two conditionals where both branches return awaited values
        var block = BlockAsync(
            Condition( Constant( true ),
                Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 10 )
                    ) ),
                Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 5 )
                    ) )
            ),
            Condition( Constant( false ),
                Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 1 )
                    ) ),
                Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 2 )
                    ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Second condition is false, so the false branch returns 2
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithNestedConditionals( bool immediateFlag )
    {
        // Arrange: Conditionals nested inside each other
        var block = BlockAsync(
            Condition(
                Constant( true ),
                Condition(
                    Constant( false ),
                    Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 5 )
                    ) ),
                    Await( AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 10 )
                    ) )
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

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithConditionalReturningTask( bool immediateFlag )
    {
        // Arrange: The result of the conditional is an awaited Task
        var block = BlockAsync(
            Await(
                Condition(
                    Constant( true ),
                    AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 15 )
                    ),
                    AsyncHelper.Completable(
                        Constant( immediateFlag ),
                        Constant( 20 )
                    )
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
