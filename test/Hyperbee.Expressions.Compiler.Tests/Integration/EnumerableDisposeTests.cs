using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// A <c>finally</c> around a yield has to run whether the sequence is exhausted or
/// abandoned, because abandoning is what <c>foreach</c> does on break and it disposes the
/// enumerator on the way out.
/// </summary>
/// <remarks>
/// Abandoning is the ordinary case, not the exotic one: First, Any and Take all do it. A
/// body that holds a resource across a yield -- which is most of the reason to write a lazy
/// sequence -- leaks it when the finally does not run.
/// </remarks>
[TestClass]
public class EnumerableDisposeTests
{
    public sealed class Log
    {
        public List<string> Entries { get; } = [];

        public void Add( string entry ) => Entries.Add( entry );
    }

    public sealed class Resource( Log log, string name ) : IDisposable
    {
        public void Dispose() => log.Add( name );
    }

    private static Expression Record( Expression log, string entry ) =>
        Call( log, typeof( Log ).GetMethod( nameof( Log.Add ) )!, Constant( entry ) );

    // yields 1 and 2 inside a finally that records when it runs

    private static (Expression<Func<IEnumerable<int>>> Lambda, Log Log) YieldsInsideFinally()
    {
        var log = new Log();
        var logConstant = Constant( log );

        var lambda = Lambda<Func<IEnumerable<int>>>(
            BlockEnumerable(
                TryFinally(
                    Block(
                        YieldReturn( Constant( 1 ) ),
                        YieldReturn( Constant( 2 ) ) ),
                    Record( logConstant, "finally" ) ) ) );

        return (lambda, log);
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void FinallyRunsWhenEnumerationCompletes( CompilerType compiler )
    {
        // Arrange
        var (lambda, log) = YieldsInsideFinally();

        // Act
        var values = lambda.Compile( compiler )().ToArray();

        // Assert
        CollectionAssert.AreEqual( new[] { 1, 2 }, values );
        CollectionAssert.AreEqual( new[] { "finally" }, log.Entries );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void FinallyRunsWhenEnumerationIsAbandoned( CompilerType compiler )
    {
        // Arrange
        var (lambda, log) = YieldsInsideFinally();

        // Act: stop after the first element, which disposes the enumerator
        foreach ( var value in lambda.Compile( compiler )() )
        {
            Assert.AreEqual( 1, value );
            break;
        }

        // Assert
        CollectionAssert.AreEqual( new[] { "finally" }, log.Entries );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void FinallyRunsOnceWhenDisposedTwice( CompilerType compiler )
    {
        // Arrange
        var (lambda, log) = YieldsInsideFinally();

        var enumerator = lambda.Compile( compiler )().GetEnumerator();

        // Act
        enumerator.MoveNext();
        enumerator.Dispose();
        enumerator.Dispose();

        // Assert
        CollectionAssert.AreEqual( new[] { "finally" }, log.Entries );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void FinallyDoesNotRunAgainAfterCompletion( CompilerType compiler )
    {
        // Arrange
        var (lambda, log) = YieldsInsideFinally();

        var enumerator = lambda.Compile( compiler )().GetEnumerator();

        // Act: run to the end, which already ran the finally, then dispose
        while ( enumerator.MoveNext() )
        {
        }

        enumerator.Dispose();

        // Assert
        CollectionAssert.AreEqual( new[] { "finally" }, log.Entries );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void FinallyDoesNotRunWhenNeverStarted( CompilerType compiler )
    {
        // Arrange: the try was never entered, so it owes nothing
        var (lambda, log) = YieldsInsideFinally();

        var enumerator = lambda.Compile( compiler )().GetEnumerator();

        // Act
        enumerator.Dispose();

        // Assert
        Assert.AreEqual( 0, log.Entries.Count );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void NestedFinallysRunInnermostFirst( CompilerType compiler )
    {
        // Arrange
        var log = new Log();
        var logConstant = Constant( log );

        var lambda = Lambda<Func<IEnumerable<int>>>(
            BlockEnumerable(
                TryFinally(
                    Block(
                        TryFinally(
                            Block(
                                YieldReturn( Constant( 1 ) ),
                                YieldReturn( Constant( 2 ) ) ),
                            Record( logConstant, "inner" ) ),
                        YieldReturn( Constant( 3 ) ) ),
                    Record( logConstant, "outer" ) ) ) );

        // Act: abandon while suspended inside both
        foreach ( var value in lambda.Compile( compiler )() )
        {
            Assert.AreEqual( 1, value );
            break;
        }

        // Assert
        CollectionAssert.AreEqual( new[] { "inner", "outer" }, log.Entries );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void OnlyTheEnteredFinallyRuns( CompilerType compiler )
    {
        // Arrange: suspended inside the outer try but before the inner one is entered
        var log = new Log();
        var logConstant = Constant( log );

        var lambda = Lambda<Func<IEnumerable<int>>>(
            BlockEnumerable(
                TryFinally(
                    Block(
                        YieldReturn( Constant( 1 ) ),
                        TryFinally(
                            YieldReturn( Constant( 2 ) ),
                            Record( logConstant, "inner" ) ) ),
                    Record( logConstant, "outer" ) ) ) );

        // Act
        foreach ( var value in lambda.Compile( compiler )() )
        {
            Assert.AreEqual( 1, value );
            break;
        }

        // Assert
        CollectionAssert.AreEqual( new[] { "outer" }, log.Entries );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void FinallyRunsWhenAbandonedInsideALoop( CompilerType compiler )
    {
        // Arrange
        var log = new Log();
        var logConstant = Constant( log );
        var index = Variable( typeof( int ), "index" );
        var breakLabel = Label( "break" );

        var lambda = Lambda<Func<IEnumerable<int>>>(
            BlockEnumerable(
                new[] { index },
                new Expression[]
                {
                    TryFinally(
                        Loop(
                            IfThenElse(
                                LessThan( index, Constant( 5 ) ),
                                Block(
                                    YieldReturn( index ),
                                    PostIncrementAssign( index ) ),
                                Break( breakLabel ) ),
                            breakLabel ),
                        Record( logConstant, "finally" ) )
                } ) );

        // Act: take two of five
        var taken = lambda.Compile( compiler )().Take( 2 ).ToArray();

        // Assert
        CollectionAssert.AreEqual( new[] { 0, 1 }, taken );
        CollectionAssert.AreEqual( new[] { "finally" }, log.Entries );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void UsingDisposesWhenAbandoned( CompilerType compiler )
    {
        // Arrange: the shape this is all for -- a resource held across a yield
        var log = new Log();
        var resource = Variable( typeof( Resource ), "resource" );

        var lambda = Lambda<Func<IEnumerable<int>>>(
            BlockEnumerable(
                new Expression[]
                {
                    Using(
                        resource,
                        New(
                            typeof( Resource ).GetConstructor( [typeof( Log ), typeof( string )] )!,
                            Constant( log ),
                            Constant( "disposed" ) ),
                        Block(
                            YieldReturn( Constant( 1 ) ),
                            YieldReturn( Constant( 2 ) ) ) )
                } ) );

        // Act
        var first = lambda.Compile( compiler )().First();

        // Assert
        Assert.AreEqual( 1, first );
        CollectionAssert.AreEqual( new[] { "disposed" }, log.Entries );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void CatchDoesNotRunWhenAbandoned( CompilerType compiler )
    {
        // Arrange: abandoning is not an exception, so only the finally should run
        var log = new Log();
        var logConstant = Constant( log );

        var lambda = Lambda<Func<IEnumerable<int>>>(
            BlockEnumerable(
                TryCatchFinally(
                    Block(
                        typeof( void ),
                        YieldReturn( Constant( 1 ) ),
                        YieldReturn( Constant( 2 ) ) ),
                    Record( logConstant, "finally" ),
                    Catch( typeof( Exception ), Record( logConstant, "catch" ) ) ) ) );

        // Act
        foreach ( var value in lambda.Compile( compiler )() )
        {
            Assert.AreEqual( 1, value );
            break;
        }

        // Assert
        CollectionAssert.AreEqual( new[] { "finally" }, log.Entries );
    }
}
