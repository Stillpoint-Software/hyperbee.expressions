using static System.Linq.Expressions.Expression;
using static Hyperbee.AsyncExpressions.AsyncExpression;

namespace Hyperbee.AsyncExpressions.Tests
{
    [TestClass]
    public class BlockAsyncTryCatchTests
    {
        [TestMethod]
        public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInTryBlock()
        {
            // Arrange: Await in the try block
            var exceptionParam = Parameter( typeof( Exception ), "ex" );
            var block = BlockAsync(
                TryCatch(
                    Await( Constant( Task.FromResult( 10 ) ) ),
                    Catch( exceptionParam, Constant( 0 ) )
                )
            );
            var lambda = Lambda<Func<Task<int>>>( block );
            var compiledLambda = lambda.Compile();

            // Act
            var result = await compiledLambda();

            // Assert
            Assert.AreEqual( 10, result );
        }

        [TestMethod]
        public async Task AsyncBlock_ShouldCatchExceptionSuccessfully_WithAwaitInCatchBlock()
        {
            // Arrange: Await in the catch block
            var exceptionParam = Parameter( typeof( Exception ), "ex" );
            var resultValue = Parameter( typeof( int ) );
            var block = BlockAsync(
                [resultValue],
                TryCatch(
                    Block(
                        Throw( Constant( new Exception() ) ),
                        Constant( 1 )
                    ),
                    Catch(
                        exceptionParam,
                        Assign( resultValue, Await( Constant( Task.FromResult( 99 ) ) ))
                    )
                ),
                resultValue
            );
            var lambda = Lambda<Func<Task<int>>>( block );
            var compiledLambda = lambda.Compile();
        
            // Act
            var result = await compiledLambda();
            
            // Assert
            Assert.AreEqual( 99, result );

        }
        /*
        [TestMethod]
        public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInFinallyBlock()
        {
            // Arrange: Await in the finally block
            var resultValue = Parameter( typeof(int) );
            var block = BlockAsync(
                [resultValue],
                TryFinally(
                    Assign( resultValue, Await( Constant( Task.FromResult( 10 ) ) ) ),
                    Assign( resultValue, Await( Constant( Task.FromResult( 20 ) ) ) )
                ),
                resultValue
            );
            var lambda = Lambda<Func<Task<int>>>( block );
            var compiledLambda = lambda.Compile();
        
            // Act
            var result = await compiledLambda();
            
            // Assert
            Assert.AreEqual( 20, result ); // Should return awaited value from finally block
        }
        */
        [TestMethod]
        public async Task AsyncBlock_ShouldHandleExceptionSuccessfully_WithTryCatchFinally()
        {
            // Arrange: Await in both catch and finally blocks
            var exceptionParam = Parameter( typeof( Exception ), "ex" );
            var resultValue = Parameter( typeof( int ) );
            var block = BlockAsync(
                [resultValue],
                TryCatchFinally(
                    Block(
                        Throw( Constant( new Exception() ) ),
                        Constant( 1 )
                    ),
                    Assign( resultValue,  Await( Constant( Task.FromResult( 50 ) ) ) ), // This Await will not be executed because of the exception
                    Catch( exceptionParam,
                        Assign( resultValue, Await( Constant( Task.FromResult( 30 ) ) ) ) ) // Catch block
                ),
                resultValue
            );
            var lambda = Lambda<Func<Task<int>>>( block );
            var compiledLambda = lambda.Compile();
        
            // Act
            var result = await compiledLambda();
        
            // Assert
            Assert.AreEqual( 30, result ); // Catch block handles the exception and returns 30
        }

        [TestMethod]
        public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInTryAndFinallyBlocks()
        {
            // Arrange: Await in both try and finally blocks
            var block = BlockAsync(
                TryFinally(
                    Await( Constant( Task.FromResult( 15 ) ) ), // Try block
                    Constant(25 ) // Finally block
                )
            );
            var lambda = Lambda<Func<Task<int>>>( block );
            var compiledLambda = lambda.Compile();

            // Act
            await compiledLambda();

            // Assert
            // Since no value is returned from Try, we assert that the last awaited value from Finally is executed.
            // Here we cannot capture the returned value, just ensure that the await completes without issues.
        }

        [TestMethod]
        public async Task AsyncBlock_ShouldAwaitSuccessfully_WithNestedTryBlock()
        {
            // Arrange: Await in the try block
            var resultValue = Parameter( typeof( int ) );
            var block = BlockAsync(
                [resultValue],
                Await( Constant( Task.FromResult( 0 ) ) ),
                TryCatchFinally(
                    Block(
                        Assign( resultValue, Await( Constant( Task.FromResult( 10 ) ) ) ),
                        TryCatch(
                            Block(
                                Assign( resultValue, Await( Constant( Task.FromResult( 20 ) ) ) ),
                                TryCatch(
                                    Assign( resultValue, Await( Constant( Task.FromResult( 30 ) ) ) ),
                                    Catch( typeof(Exception), Assign( resultValue, Constant( 1 ) ) )
                                ) ),
                            Catch( typeof(Exception), Assign( resultValue, Constant( 2 ) ) )
                        ),
                        Assign( resultValue, Await( Constant( Task.FromResult( 40 ) ) ) ),
                        TryCatch(
                            Block(
                                Assign( resultValue, Await( Constant( Task.FromResult( 50 ) ) ) ),
                                TryCatch(
                                    Assign( resultValue, Await( Constant( Task.FromResult( 60 ) ) ) ),
                                    Catch( typeof( Exception ), Assign( resultValue, Constant( 3 ) ) )
                                ) )
                            ,
                            Catch( typeof( Exception ), Assign( resultValue, Constant( 4 ) ) )

                        ) ),
                    TryCatch(
                        Assign( resultValue, Await( Constant( Task.FromResult( 40 ) ) ) ),
                        Catch( typeof(Exception), Assign( resultValue, Constant( 5 ) ) )
                    ),
                    Catch( typeof(Exception), Assign( resultValue, Constant( 6 ) ) )
                ),
                resultValue
            );
            var lambda = Lambda<Func<Task<int>>>( block );
            var compiledLambda = lambda.Compile();

            // Act
            var result = await compiledLambda();

            // Assert
            Assert.AreEqual( 60, result );
        }
    }
}
