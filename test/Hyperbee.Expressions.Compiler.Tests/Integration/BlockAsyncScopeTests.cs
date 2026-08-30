using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for variable hoisting and catch-variable binding inside BlockAsync
/// when the state machine MoveNext is compiled by HEC.
/// </summary>
[TestClass]
public class BlockAsyncScopeTests
{
    private static async Task<int> AddAsync( int value, int addend )
    {
        await Task.Yield();
        return value + addend;
    }

    private static Expression AddAsyncCall( Expression value, int addend )
    {
        return Call(
            typeof( BlockAsyncScopeTests ),
            nameof( AddAsync ),
            Type.EmptyTypes,
            value,
            Constant( addend )
        );
    }

    // -----------------------------------------------------------------------
    // Distinct ParameterExpression instances that share a name
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_DistinctVariablesWithSameName_AreNotConflated( CompilerType compiler )
    {
        // Arrange: two distinct variables in sibling blocks share the name "value"
        var first = Variable( typeof( int ), "value" );
        var second = Variable( typeof( int ), "value" );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                Block(
                    new[] { first },
                    Assign( first, Constant( 1 ) ),
                    Assign( result, Await( AddAsyncCall( first, 0 ) ) )
                ),
                Block(
                    new[] { second },
                    Assign( second, Constant( 10 ) ),
                    Assign( result, Await( AddAsyncCall( Add( result, second ), 0 ) ) )
                ),
                result
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 11, value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_OuterLambdaParameter_IsVisibleInsideAsyncBlock( CompilerType compiler )
    {
        // A state-machine body that reads an enclosing variable cannot be pre-compiled in
        // isolation, so it is emitted inline and the enclosing compiler shares the
        // variable. See ClosureCaptureTests for the full set of capture cases.

        // Arrange
        var input = Parameter( typeof( int ), "input" );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                Assign( result, Await( AddAsyncCall( input, 5 ) ) ),
                result
            }
        );

        var lambda = Lambda<Func<int, Task<int>>>( block, input );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled( 10 );

        // Assert
        Assert.AreEqual( 15, value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_UnnamedVariables_AreNotConflated( CompilerType compiler )
    {
        // Arrange
        var first = Variable( typeof( int ) );
        var second = Variable( typeof( int ) );
        var result = Variable( typeof( int ), "result" );

        var block = BlockAsync(
            new[] { result },
            new Expression[]
            {
                Block(
                    new[] { first, second },
                    Assign( first, Constant( 1 ) ),
                    Assign( second, Constant( 10 ) ),
                    Assign( result, Await( AddAsyncCall( Add( first, second ), 0 ) ) )
                ),
                result
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 11, value );
    }

    // -----------------------------------------------------------------------
    // Catch variables
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_CatchVariable_IsReadableInHandler( CompilerType compiler )
    {
        // Arrange
        var ex = Parameter( typeof( Exception ), "ex" );

        var block = BlockAsync(
            TryCatch(
                Block(
                    typeof( string ),
                    Await( AddAsyncCall( Constant( 1 ), 1 ) ),
                    Throw( Constant( new InvalidOperationException( "Boom" ) ) ),
                    Constant( "not-thrown" )
                ),
                Catch( ex, Property( ex, nameof( Exception.Message ) ) )
            )
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( "Boom", value );
    }

    // -----------------------------------------------------------------------
    // Try semantics
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_TryFinally_PropagatesUnhandledException( CompilerType compiler )
    {
        // Arrange
        var log = Variable( typeof( string ), "log" );

        var block = BlockAsync(
            new[] { log },
            new Expression[]
            {
                Assign( log, Constant( "" ) ),
                TryFinally(
                    Block(
                        typeof( void ),
                        Await( AddAsyncCall( Constant( 1 ), 1 ) ),
                        Throw( Constant( new InvalidOperationException( "Boom" ) ) )
                    ),
                    Assign( log, Constant( "F" ) )
                ),
                log
            }
        );

        var lambda = Lambda<Func<Task<string>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert
        await Assert.ThrowsExactlyAsync<InvalidOperationException>( async () => await compiled() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitOnlyInsideTry_DoesNotReExecutePrologue( CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );

        var block = BlockAsync(
            new[] { count },
            new Expression[]
            {
                Assign( count, Add( count, Constant( 1 ) ) ),
                TryCatch(
                    Block(
                        typeof( void ),
                        Await( AddAsyncCall( Constant( 1 ), 1 ) )
                    ),
                    Catch( typeof( Exception ), Empty() )
                ),
                count
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 1, value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_TryCatchInsideLoop_Iterates( CompilerType compiler )
    {
        // Arrange
        var index = Variable( typeof( int ), "i" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockAsync(
            new[] { index },
            new Expression[]
            {
                Assign( index, Constant( 0 ) ),
                Loop(
                    Block(
                        IfThen( GreaterThanOrEqual( index, Constant( 3 ) ), Break( breakLabel ) ),
                        TryCatch(
                            Block(
                                typeof( void ),
                                Assign( index, Await( AddAsyncCall( index, 1 ) ) )
                            ),
                            Catch( typeof( Exception ), Empty() )
                        )
                    ),
                    breakLabel,
                    null
                ),
                index
            }
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var value = await compiled();

        // Assert
        Assert.AreEqual( 3, value );
    }
}
