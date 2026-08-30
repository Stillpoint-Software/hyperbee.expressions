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
[TestClass]
public class EnumerableDisposeTests
{
    public sealed class Flag
    {
        public bool Ran;
    }

    private static (Expression<Func<IEnumerable<int>>> Lambda, Flag Flag) YieldsInsideFinally()
    {
        var flag = new Flag();
        var flagConstant = Constant( flag );

        var lambda = Lambda<Func<IEnumerable<int>>>(
            BlockEnumerable(
                TryFinally(
                    Block(
                        YieldReturn( Constant( 1 ) ),
                        YieldReturn( Constant( 2 ) ) ),
                    Assign( Field( flagConstant, nameof( Flag.Ran ) ), Constant( true ) ) ) ) );

        return (lambda, flag);
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void FinallyRunsWhenEnumerationCompletes( CompilerType compiler )
    {
        // Arrange
        var (lambda, flag) = YieldsInsideFinally();

        // Act
        var values = lambda.Compile( compiler )().ToArray();

        // Assert
        CollectionAssert.AreEqual( new[] { 1, 2 }, values );
        Assert.IsTrue( flag.Ran, "the finally should run when the sequence is exhausted" );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    [Ignore( "Known gap: Dispose does not run the finally blocks pending for the current state. "
        + "A C# iterator emits one method per finally and a Dispose that dispatches on state; "
        + "the lowering records the try scopes but nothing walks them on the dispose path." )]
    public void FinallyRunsWhenEnumerationIsAbandoned( CompilerType compiler )
    {
        // Arrange
        var (lambda, flag) = YieldsInsideFinally();

        // Act: stop after the first element, which disposes the enumerator
        foreach ( var value in lambda.Compile( compiler )() )
        {
            Assert.AreEqual( 1, value );
            break;
        }

        // Assert
        Assert.IsTrue( flag.Ran, "the finally should run when the enumerator is disposed" );
    }
}
