using System.Linq.Expressions;
using System.Text;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

/// <summary>
/// The custom expression types this library adds, compiled by HEC as well as by the System
/// compiler, with the two required to agree.
/// </summary>
/// <remarks>
/// These types are tested thoroughly in Hyperbee.Expressions.Tests, which cannot reference
/// the compiler and so runs them under the System, Fast and Interpret compilers only. The
/// result was that For, ForEach, While, Using and StringFormat -- the surface a consumer
/// actually emits -- had no HEC coverage at all, in a project whose entire purpose is
/// compiling this library's expressions. Correctness for these types lives in that project;
/// what lives here is that HEC produces the same answer.
/// </remarks>
[TestClass]
public class ExtensionExpressionParityTests
{
    public sealed class Resource( StringBuilder log, string name ) : IDisposable
    {
        public void Dispose() => log.Append( name );
    }

    private static void AssertSameResult<TDelegate, TResult>(
        Expression<TDelegate> lambda,
        Func<TDelegate, TResult> invoke )
        where TDelegate : Delegate
    {
        var system = invoke( lambda.Compile( CompilerType.System ) );
        var hyperbee = invoke( lambda.Compile( CompilerType.Hyperbee ) );

        Assert.AreEqual( system, hyperbee, $"{typeof( TDelegate ).Name}: compilers disagree" );
    }

    [TestMethod]
    public void For_SumsTheSameWay()
    {
        // Arrange
        var limit = Parameter( typeof( int ), "limit" );
        var index = Variable( typeof( int ), "index" );
        var total = Variable( typeof( int ), "total" );

        var lambda = Lambda<Func<int, int>>(
            Block(
                new[] { index, total },
                For(
                    Assign( index, Constant( 0 ) ),
                    LessThan( index, limit ),
                    PostIncrementAssign( index ),
                    AddAssign( total, index ) ),
                total ),
            limit );

        // Act & Assert
        AssertSameResult( lambda, compiled => compiled( 5 ) );
    }

    [TestMethod]
    public void ForEach_SumsTheSameWay()
    {
        // Arrange
        var source = Parameter( typeof( int[] ), "source" );
        var element = Variable( typeof( int ), "element" );
        var total = Variable( typeof( int ), "total" );

        var lambda = Lambda<Func<int[], int>>(
            Block(
                new[] { total },
                ForEach( source, element, AddAssign( total, element ) ),
                total ),
            source );

        // Act & Assert
        AssertSameResult( lambda, compiled => compiled( [1, 2, 3, 4] ) );
    }

    [TestMethod]
    public void While_SumsTheSameWay()
    {
        // Arrange
        var limit = Parameter( typeof( int ), "limit" );
        var index = Variable( typeof( int ), "index" );

        var lambda = Lambda<Func<int, int>>(
            Block(
                new[] { index },
                While(
                    LessThan( index, limit ),
                    PostIncrementAssign( index ) ),
                index ),
            limit );

        // Act & Assert
        AssertSameResult( lambda, compiled => compiled( 7 ) );
    }

    [TestMethod]
    public void Using_DisposesTheSameWay()
    {
        // Arrange
        var log = Parameter( typeof( StringBuilder ), "log" );
        var resource = Variable( typeof( Resource ), "resource" );

        var lambda = Lambda<Func<StringBuilder, string>>(
            Block(
                Using(
                    resource,
                    New(
                        typeof( Resource ).GetConstructor( [typeof( StringBuilder ), typeof( string )] )!,
                        log,
                        Constant( "disposed" ) ),
                    Call( log, typeof( StringBuilder ).GetMethod( nameof( StringBuilder.Append ), [typeof( string )] )!, Constant( "body" ) ) ),
                Call( log, typeof( object ).GetMethod( nameof( ToString ) )! ) ),
            log );

        // Act & Assert
        AssertSameResult( lambda, compiled => compiled( new StringBuilder() ) );
    }

    [TestMethod]
    public void Using_DisposesWhenTheBodyThrows()
    {
        // Arrange
        var log = Parameter( typeof( StringBuilder ), "log" );
        var resource = Variable( typeof( Resource ), "resource" );

        var lambda = Lambda<Func<StringBuilder, string>>(
            Block(
                TryCatch(
                    Using(
                        resource,
                        New(
                            typeof( Resource ).GetConstructor( [typeof( StringBuilder ), typeof( string )] )!,
                            log,
                            Constant( "disposed" ) ),
                        Throw( New( typeof( InvalidOperationException ) ), typeof( void ) ) ),
                    Catch( typeof( InvalidOperationException ), Empty() ) ),
                Call( log, typeof( object ).GetMethod( nameof( ToString ) )! ) ),
            log );

        // Act & Assert
        AssertSameResult( lambda, compiled => compiled( new StringBuilder() ) );
    }

    [TestMethod]
    public void StringFormat_FormatsTheSameWay()
    {
        // Arrange
        var value = Parameter( typeof( int ), "value" );

        var lambda = Lambda<Func<int, string>>(
            StringFormat(
                Constant( "value is {0} and twice is {1}" ),
                [Convert( value, typeof( object ) ), Convert( Multiply( value, Constant( 2 ) ), typeof( object ) )] ),
            value );

        // Act & Assert
        AssertSameResult( lambda, compiled => compiled( 21 ) );
    }

    [TestMethod]
    public void For_WithBreak_StopsTheSameWay()
    {
        // Arrange
        var index = Variable( typeof( int ), "index" );
        var total = Variable( typeof( int ), "total" );
        var breakLabel = Label( "break" );
        var continueLabel = Label( "continue" );

        var lambda = Lambda<Func<int>>(
            Block(
                new[] { index, total },
                For(
                    Assign( index, Constant( 0 ) ),
                    LessThan( index, Constant( 100 ) ),
                    PostIncrementAssign( index ),
                    IfThenElse(
                        GreaterThan( index, Constant( 4 ) ),
                        Break( breakLabel ),
                        AddAssign( total, index ) ),
                    breakLabel,
                    continueLabel ),
                total ) );

        // Act & Assert
        AssertSameResult( lambda, compiled => compiled() );
    }

    [TestMethod]
    public void ForEach_OverAnEnumerable_SumsTheSameWay()
    {
        // Arrange: a sequence rather than an array, so the enumerator path runs
        var source = Parameter( typeof( IEnumerable<int> ), "source" );
        var element = Variable( typeof( int ), "element" );
        var total = Variable( typeof( int ), "total" );

        var lambda = Lambda<Func<IEnumerable<int>, int>>(
            Block(
                new[] { total },
                ForEach( source, element, AddAssign( total, element ) ),
                total ),
            source );

        // Act & Assert
        AssertSameResult( lambda, compiled => compiled( Enumerable.Range( 1, 4 ) ) );
    }
}
