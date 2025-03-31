using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockYieldTryCatchTests
{
    [DataTestMethod]
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
        Assert.AreEqual( 1, result.Length );
        Assert.AreEqual( 10, result[0] );
    }

    [DataTestMethod]
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
        Assert.AreEqual( 1, result.Length );
        Assert.AreEqual( 99, result[0] );
    }

    [DataTestMethod]
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
        Assert.AreEqual( 2, result.Length );
        Assert.AreEqual( 30, result[0] );
        Assert.AreEqual( 50, result[1] );
    }

    [DataTestMethod]
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
        Assert.AreEqual( 3, result.Length );
        Assert.AreEqual( 15, result[0] );
        Assert.AreEqual( 25, result[1] );
        Assert.AreEqual( 5, result[2] );
    }

    [DataTestMethod]
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
        Assert.AreEqual( 4, result.Length );
        Assert.AreEqual( 10, result[0] );
        Assert.AreEqual( 20, result[1] );
        Assert.AreEqual( 30, result[2] );
        Assert.AreEqual( 40, result[3] );
    }

    [DataTestMethod]
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
        Assert.AreEqual( 0, result.Length );
    }

    [DataTestMethod]
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
        Assert.AreEqual( 2, result.Length );
        Assert.AreEqual( 20, result[0] );
        Assert.AreEqual( 50, result[1] );
    }

    [DataTestMethod]
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
        Assert.AreEqual( 8, result.Length );
        Assert.AreEqual( 0, result[0] );
        Assert.AreEqual( 10, result[1] );
        Assert.AreEqual( 20, result[2] );
        Assert.AreEqual( 30, result[3] );
        Assert.AreEqual( 40, result[4] );
        Assert.AreEqual( 50, result[5] );
        Assert.AreEqual( 60, result[6] );
        Assert.AreEqual( 70, result[7] );
    }
}
