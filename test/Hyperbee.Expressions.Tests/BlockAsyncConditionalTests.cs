using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncConditionalTests
{
    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithIfThenCondition( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Condition depends on awaited value
        var condition = Await( AsyncHelper.Completer(
            Constant( completer ),
            Constant( true )
        ) );

        var block = BlockAsync(
            IfThen( condition, Constant( 1 ) )
        );

        var lambda = Lambda<Func<Task>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        await compiledLambda();

        // Assert
        Assert.IsTrue( true ); // No exception means condition and block executed successfully
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithConditionalAssignment( CompleterType completer, CompilerType compiler )
    {
        // Arrange: IfTrue branch contains an awaited task
        var var = Variable( typeof( int ), "var" );
        var condition = Constant( true );

        var block = BlockAsync(
            [var],
            Assign( var,
                Condition( condition,
                    Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 1 )
                    ) ),
                    Constant( 0 )
                )
            ),
            var
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithIfThenElseTrueBranch( CompleterType completer, CompilerType compiler )
    {
        // Arrange: IfTrue branch contains an awaited task
        var condition = Constant( true );
        var block = BlockAsync(
            Condition( condition,
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 1 )
                ) ),
                Constant( 0 )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithIfThenElseFalseBranch( CompleterType completer, CompilerType compiler )
    {
        // Arrange: IfFalse branch contains an awaited task
        var condition = Constant( false );
        var block = BlockAsync(
            Condition( condition,
                Constant( 0 ),
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 2 )
                ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithConditionalInTest( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Test depends on awaited value
        var test = Await( AsyncHelper.Completer(
            Constant( completer ),
            Constant( true )
        ) );

        var block = BlockAsync(
            Condition( test, Constant( 1 ), Constant( 0 ) )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInTrueAndFalseBranches( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Both branches return values from awaited tasks
        var condition = Constant( true );
        var block = BlockAsync(
            Condition( condition,
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 10 )
                ) ),
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 20 )
                ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result ); // Condition is true, so the true branch should be awaited
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitBeforeAndAfterConditional( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await a task before and after a conditional expression
        var block = BlockAsync(
            Await( AsyncHelper.Completer(
                Constant( completer ),
                Constant( 5 )
            ) ),
            Condition( Constant( true ), Constant( 10 ), Constant( 0 ) ),
            Await( AsyncHelper.Completer(
                Constant( completer ),
                Constant( 15 )
            ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result ); // Last awaited value should be 15
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithFalseCondition( CompleterType completer, CompilerType compiler )
    {
        // Arrange: False condition should lead to the false branch being executed
        var condition = Constant( false );
        var block = BlockAsync(
            Condition( condition,
                Constant( 10 ),
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 20 )
                ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 20, result ); // False branch should be awaited and return 20
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithComplexConditionalLogic( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Two conditionals where both branches return awaited values
        var block = BlockAsync(
            Condition( Constant( true ),
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 10 )
                ) ),
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 5 )
                ) )
            ),
            Condition( Constant( false ),
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 1 )
                ) ),
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 2 )
                ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Second condition is false, so the false branch returns 2
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithNestedConditionals( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Conditionals nested inside each other
        var block = BlockAsync(
            Condition(
                Constant( true ),
                Condition(
                    Constant( false ),
                    Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 5 )
                    ) ),
                    Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 10 )
                    ) )
                ),
                Constant( 0 )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result ); // The true branch contains another conditional, false branch executed
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithConditionalReturningTask( CompleterType completer, CompilerType compiler )
    {
        //Arrange: The result of the conditional is an awaited Task
        var block = BlockAsync(
           Await(
               Condition(
                   Constant( true ),
                   AsyncHelper.Completer(
                       Constant( completer ),
                       Constant( 15 )
                   ),
                   AsyncHelper.Completer(
                       Constant( completer ),
                       Constant( 20 )
                   )
               )
           )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

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
