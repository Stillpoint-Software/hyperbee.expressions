using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for <c>BlockEnumerable</c> when the state machine MoveNext is
/// compiled by HEC.
/// </summary>
/// <remarks>
/// The async block had integration coverage here from the start; the enumerable block did
/// not, and was broken under HEC for every shape. A coroutine construct needs coverage
/// under every compiler the library supports, not just the one it was written against.
/// </remarks>
[TestClass]
public class BlockYieldTests
{
    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void BlockYield_SingleYield( CompilerType compiler )
    {
        // Arrange
        var block = BlockEnumerable( YieldReturn( Constant( 1 ) ) );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert
        CollectionAssert.AreEqual( new[] { 1 }, compiled().ToArray() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void BlockYield_SequentialYields( CompilerType compiler )
    {
        // Arrange
        var local = Variable( typeof( int ), "local" );

        var block = BlockEnumerable(
            new[] { local },
            new Expression[]
            {
                Assign( local, Constant( 7 ) ),
                YieldReturn( local ),
                YieldReturn( Add( local, Constant( 1 ) ) )
            } );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert
        CollectionAssert.AreEqual( new[] { 7, 8 }, compiled().ToArray() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void BlockYield_YieldInsideLoop( CompilerType compiler )
    {
        // Arrange
        var index = Variable( typeof( int ), "index" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockEnumerable(
            new[] { index },
            new Expression[]
            {
                Assign( index, Constant( 0 ) ),
                Loop(
                    Block(
                        IfThen( GreaterThanOrEqual( index, Constant( 3 ) ), Break( breakLabel ) ),
                        Assign( index, Add( index, Constant( 1 ) ) ),
                        YieldReturn( index ) ),
                    breakLabel,
                    null )
            } );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert
        CollectionAssert.AreEqual( new[] { 1, 2, 3 }, compiled().ToArray() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void BlockYield_YieldInsideTryCatch( CompilerType compiler )
    {
        // Arrange
        var exceptionParam = Parameter( typeof( Exception ), "ex" );
        var message = Variable( typeof( string ), "message" );

        var block = BlockEnumerable(
            new[] { message },
            new Expression[]
            {
                TryCatch(
                    Block(
                        typeof( void ),
                        YieldReturn( Constant( "start" ) ),
                        Throw( Constant( new InvalidOperationException( "Boom" ) ) ) ),
                    Catch( exceptionParam,
                        Block( typeof( void ),
                            Assign( message, Property( exceptionParam, nameof( Exception.Message ) ) ) ) ) ),
                YieldReturn( message )
            } );

        var lambda = Lambda<Func<IEnumerable<string>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert
        CollectionAssert.AreEqual( new[] { "start", "Boom" }, compiled().ToArray() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void BlockYield_TryCatchInsideLoop( CompilerType compiler )
    {
        // Arrange
        var index = Variable( typeof( int ), "index" );
        var breakLabel = Label( "breakLabel" );

        var block = BlockEnumerable(
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
                                Assign( index, Add( index, Constant( 1 ) ) ),
                                YieldReturn( index ) ),
                            Catch( typeof( Exception ), Empty() ) ) ),
                    breakLabel,
                    null )
            } );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert
        CollectionAssert.AreEqual( new[] { 1, 2, 3 }, compiled().ToArray() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void BlockYield_DoesNotReExecutePrologue_WithYieldOnlyInsideTry( CompilerType compiler )
    {
        // Arrange
        var count = Variable( typeof( int ), "count" );

        var block = BlockEnumerable(
            new[] { count },
            new Expression[]
            {
                Assign( count, Add( count, Constant( 1 ) ) ),
                TryCatch(
                    Block( typeof( void ), YieldReturn( count ), YieldReturn( count ) ),
                    Catch( typeof( Exception ), Empty() ) )
            } );

        var lambda = Lambda<Func<IEnumerable<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act & Assert
        CollectionAssert.AreEqual( new[] { 1, 1 }, compiled().ToArray() );
    }
}
