using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncLoopTests
{
    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitBeforeBreak( bool immediately )
    {
        // Arrange: Await before break in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );
        var block = BlockAsync(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Await( AsyncHelper.Completable(
                        Constant( immediately ),
                        Constant( 1 )
                    ) ), // Await before break
                    IfThen(
                        Equal( loopCount, Constant( 1 ) ),
                        Break( breakLabel )
                    ),
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) )
                ),
                breakLabel,
                null // No continue label
            ),
            loopCount
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result ); // Loop should break after 1 iteration
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitAfterLoop( bool immediately )
    {
        // Arrange: Await after break in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockAsync(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    IfThen(
                        Equal( loopCount, Constant( 2 ) ),
                        Break( breakLabel )
                    )
                ),
                breakLabel,
                null
            ),
            Await( AsyncHelper.Completable(
                        Constant( immediately ),
                        Constant( 5 )
                    ) ), // Await after loop ends
            loopCount
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Loop breaks after 2 iterations
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitBeforeContinue( bool immediately )
    {
        // Arrange: Await before continue in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var continueLabel = Label( "continueLabel" );
        var breakLabel = Label( "breakLabel" );
        var block = BlockAsync(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    Await( AsyncHelper.Completable( Constant( immediately ), Constant( 1 ) ) ),
                    IfThen(
                        LessThan( loopCount, Constant( 2 ) ),
                        Continue( continueLabel )
                    ),
                    Break( breakLabel )
                ),
                breakLabel,
                continueLabel
            ),
            loopCount
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Loop continues past the first iteration
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitAfterContinue( bool immediately )
    {
        // Arrange: Await after continue in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var continueLabel = Label( "continueLabel" );
        var breakLabel = Label( "breakLabel" );
        var block = BlockAsync(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    IfThen(
                        Equal( loopCount, Constant( 1 ) ),
                        Continue( continueLabel )
                    ),
                    Await( AsyncHelper.Completable( Constant( immediately ), Constant( 3 ) ) ),
                    Break( breakLabel )
                ),
                breakLabel,
                continueLabel
            ),
            loopCount
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 2, result ); // Loop processes all iterations
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithMultipleAwaitsInLoop( bool immediately )
    {
        // Arrange: Multiple awaits in a loop
        var loopCount = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );
        var block = BlockAsync(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Await( AsyncHelper.Completable( Constant( immediately ), Constant( 1 ) ) ),
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    Await( AsyncHelper.Completable( Constant( immediately ), Constant( 3 ) ) ),
                    Break( breakLabel )
                ),
                breakLabel,
                null // No continue label
            ),
            loopCount
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result ); // Loop processes all iterations
    }

    [DataTestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithBreakAndContinueLabels( bool immediately )
    {
        // Arrange: Use both breakLabel and continueLabel in the loop
        var loopCount = Variable( typeof( int ), "count" );
        var breakLabel = Label( "breakLabel" );
        var continueLabel = Label( "continueLabel" );

        var block = BlockAsync(
            [loopCount],
            Assign( loopCount, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( loopCount, Add( loopCount, Constant( 1 ) ) ),
                    Await( AsyncHelper.Completable( Constant( immediately ), Constant( 1 ) ) ),
                    IfThen(
                        Equal( loopCount, Constant( 3 ) ),
                        Break( breakLabel )
                    ),
                    IfThen(
                        LessThan( loopCount, Constant( 5 ) ),
                        Continue( continueLabel )
                    )
                ),
                breakLabel,
                continueLabel
            ),
            loopCount
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 3, result ); // Loop breaks after reaching 3
    }
}
