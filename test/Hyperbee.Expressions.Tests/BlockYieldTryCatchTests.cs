using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockYieldTryCatchTests
{
    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldInTryBlock( CompilerType compiler )
    {
        // Arrange: Yield in the try block
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var block = BlockEnumerable(
            TryCatch(
                YieldReturn( Constant( 10 ) ),
                Catch( exceptionParam, Constant( 0 ) )
            )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 1, result );
        Assert.AreEqual( 10, result[0] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldCatchExceptionSuccessfully_WithYieldInCatchBlock( CompilerType compiler )
    {
        // Arrange: Yield in the catch block
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockEnumerable(
            TryCatch(
                Block(
                    Throw( Constant( new Exception() ) ),
                    Constant( 1 )
                ),
                Catch(
                    exceptionParam,
                    YieldReturn( Constant( 99 ) )
                )
            )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 1, result );
        Assert.AreEqual( 99, result[0] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldHandleExceptionSuccessfully_WithTryCatchFinally( CompilerType compiler )
    {
        // Arrange: yield in both catch and finally blocks
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var block = BlockEnumerable(
            TryCatchFinally(
                Block(
                    Throw( Constant( new Exception() ) ),
                    Constant( 1 )
                ),
                YieldReturn( Constant( 50 ) ), // This Yield will still be executed after the exception
                Catch( exceptionParam,
                    YieldReturn( Constant( 30 ) )
                )
            )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 2, result );
        Assert.AreEqual( 30, result[0] );
        Assert.AreEqual( 50, result[1] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldInTryAndFinallyBlocks( CompilerType compiler )
    {
        // Arrange: Yield in both try and finally blocks
        var resultValue = Parameter( typeof( int ) );
        var block = BlockEnumerable(
            [resultValue],
            TryFinally(
                YieldReturn( Constant( 15 ) ), // Try block
                YieldReturn( Constant( 25 ) ) // Finally block
            ),
            YieldReturn( Constant( 5 ) )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 3, result );
        Assert.AreEqual( 15, result[0] );
        Assert.AreEqual( 25, result[1] );
        Assert.AreEqual( 5, result[2] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldInTryCatchAndFinallyBlocks( CompilerType compiler )
    {
        // Arrange: Yield in Try, Catch, and Finally blocks
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var block = BlockEnumerable(
            TryCatchFinally(
                Block( // Try block
                    YieldReturn( Constant( 10 ) ),
                    Throw( Constant( new Exception() ), typeof( int ) ) // throw must keep block return type
                ),
                YieldReturn( Constant( 30 ) ), // Finally block
                Catch( exceptionParam,
                    YieldReturn( Constant( 20 ) ) // Catch block
                )
            ),
            YieldReturn( Constant( 40 ) )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 4, result );
        Assert.AreEqual( 10, result[0] );
        Assert.AreEqual( 20, result[1] );
        Assert.AreEqual( 30, result[2] );
        Assert.AreEqual( 40, result[3] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithYieldAfterThrow( CompilerType compiler )
    {
        var resultValue = Parameter( typeof( int ) );
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockEnumerable(
            [resultValue],
            TryCatch(
                Block(
                    Throw( Constant( new Exception( "Exception" ) ) ),

                    // pointless code
                    YieldReturn( Constant( 20 ) )
                ),
                Catch( exceptionParam, Constant( 50 ) )
            ),
            Constant( 1 )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.IsEmpty( result );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldCatchMultipleExceptionsInNestedTryBlocks( CompilerType compiler )
    {
        // Arrange: Multiple exceptions in nested Try-Catch blocks
        var outerExceptionParam = Parameter( typeof( Exception ), "outerEx" );
        var innerExceptionParam = Parameter( typeof( Exception ), "innerEx" );

        var block = BlockEnumerable(
            TryCatch(
                Block(
                    TryCatch(
                        Block(
                            Throw( Constant( new Exception( "Inner Exception" ) ) ),
                            Constant( 0 )
                        ),
                        Catch( innerExceptionParam, YieldReturn( Constant( 20 ) ) )
                    ),
                    Throw( Constant( new Exception( "Outer Exception" ) ), typeof( int ) )
                ),
                Catch( outerExceptionParam, YieldReturn( Constant( 50 ) ) )
            ),
            Constant( 1 )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 2, result );
        Assert.AreEqual( 20, result[0] );
        Assert.AreEqual( 50, result[1] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldYieldSuccessfully_WithComplexNestedTryBlock( CompilerType compiler )
    {
        // Arrange: Yield in the try block
        var block = BlockEnumerable(
            YieldReturn( Constant( 0 ) ),
            TryCatch(
                Block(
                    YieldReturn( Constant( 10 ) ),
                    TryCatch(
                        Block(
                            YieldReturn( Constant( 20 ) ),
                            TryCatch(
                                YieldReturn( Constant( 30 ) ),
                                Catch( typeof( Exception ), YieldReturn( Constant( 1 ) ) )
                            ) ),
                        Catch( typeof( Exception ), YieldReturn( Constant( 2 ) ) )
                    ),
                    YieldReturn( Constant( 40 ) ),
                    TryCatch(
                        Block(
                            YieldReturn( Constant( 50 ) ),
                            TryCatch(
                                YieldReturn( Constant( 60 ) ),
                                Catch( typeof( Exception ), YieldReturn( Constant( 3 ) ) )
                            ) ),
                        Catch( typeof( Exception ), YieldReturn( Constant( 4 ) ) )
                    ) ),
                Catch( typeof( Exception ), YieldReturn( Constant( 6 ) ) )
            ),
            YieldReturn( Constant( 70 ) )
        );
        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 8, result );
        Assert.AreEqual( 0, result[0] );
        Assert.AreEqual( 10, result[1] );
        Assert.AreEqual( 20, result[2] );
        Assert.AreEqual( 30, result[3] );
        Assert.AreEqual( 40, result[4] );
        Assert.AreEqual( 50, result[5] );
        Assert.AreEqual( 60, result[6] );
        Assert.AreEqual( 70, result[7] );
    }

    // Catch variables
    //
    // Lowering moves a catch handler body into its own state, outside of the generated
    // try. The catch variable must be hoisted so the handler can still reference it.

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldBindCatchVariable_WithYieldInTryBlock( CompilerType compiler )
    {
        // Arrange
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var message = Variable( typeof( string ), "message" );

        var block = BlockEnumerable(
            [message],
            TryCatch(
                Block(
                    typeof( void ),
                    YieldReturn( Constant( "start" ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) )
                ),
                Catch( exceptionParam,
                    Block( typeof( void ),
                        Assign( message, Property( exceptionParam, nameof( Exception.Message ) ) ) ) )
            ),
            YieldReturn( message )
        );

        var lambda = Lambda<Func<IEnumerable<string>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 2, result );
        Assert.AreEqual( "start", result[0] );
        Assert.AreEqual( "Boom", result[1] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldNotCatchException_WhenCatchFilterDoesNotMatch( CompilerType compiler )
    {
        // Arrange
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = BlockEnumerable(
            TryCatch(
                Block(
                    typeof( void ),
                    YieldReturn( Constant( 1 ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) )
                ),
                MakeCatchBlock(
                    typeof( Exception ),
                    exceptionParam,
                    Empty(),
                    Equal( Property( exceptionParam, nameof( Exception.Message ) ), Constant( "Other" ) )
                )
            ),
            YieldReturn( Constant( 2 ) )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act & Assert
        Assert.ThrowsExactly<InvalidOperationException>( () => compiledLambda().ToArray() );
    }

    // Resumption
    //
    // A yield inside a try suspends into a nested state scope. The state machine must
    // resume into that scope, and must not re-run expressions that precede the try.

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldNotReExecutePrologue_WithYieldOnlyInsideTry( CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );

        var block = BlockEnumerable(
            [count],
            Assign( count, Add( count, Constant( 1 ) ) ),
            TryCatch(
                Block(
                    typeof( void ),
                    YieldReturn( count ),
                    YieldReturn( count )
                ),
                Catch( typeof( Exception ), Empty() )
            )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 2, result );
        Assert.AreEqual( 1, result[0] );
        Assert.AreEqual( 1, result[1] );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void YieldBlock_ShouldIterate_WithTryCatchInsideLoop( CompilerType compiler )
    {
        // Arrange
        var index = Variable( typeof( int ), "i" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockEnumerable(
            [index],
            Assign( index, Constant( 0 ) ),
            Loop(
                Block(
                    IfThen( GreaterThanOrEqual( index, Constant( 3 ) ), Break( breakLabel ) ),
                    TryCatch(
                        Block(
                            typeof( void ),
                            Assign( index, Add( index, Constant( 1 ) ) ),
                            YieldReturn( index )
                        ),
                        Catch( typeof( Exception ), Empty() )
                    )
                ),
                breakLabel,
                null
            )
        );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = compiledLambda().ToArray();

        // Assert
        Assert.HasCount( 3, result );
        Assert.AreEqual( 1, result[0] );
        Assert.AreEqual( 2, result[1] );
        Assert.AreEqual( 3, result[2] );
    }
}
