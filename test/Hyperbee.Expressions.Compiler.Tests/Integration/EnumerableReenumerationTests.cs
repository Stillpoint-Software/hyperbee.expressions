using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// A <c>BlockEnumerable</c> handed out as an <see cref="IEnumerable{T}"/> may be enumerated
/// more than once, and the enumerations may overlap.
/// </summary>
/// <remarks>
/// The state machine is its own enumerator, so handing out <c>this</c> for every
/// GetEnumerator makes concurrent enumerations share one state field and one set of hoisted
/// locals. A C# iterator hands out <c>this</c> only for the first enumeration on the thread
/// that created it, and a fresh instance after that.
/// </remarks>
[TestClass]
public class EnumerableReenumerationTests
{
    private static Expression<Func<int, IEnumerable<int>>> Counter()
    {
        var input = Parameter( typeof( int ), "input" );
        var index = Variable( typeof( int ), "index" );
        var breakLabel = Label( "break" );

        return Lambda<Func<int, IEnumerable<int>>>(
            BlockEnumerable(
                new[] { index },
                new Expression[]
                {
                    Loop(
                        IfThenElse(
                            LessThan( index, input ),
                            Block(
                                YieldReturn( index ),
                                PostIncrementAssign( index ) ),
                            Break( breakLabel ) ),
                        breakLabel )
                } ),
            input );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void EnumeratesTwiceInSequence( CompilerType compiler )
    {
        // Arrange
        var sequence = Counter().Compile( compiler )( 3 );

        // Act
        var first = sequence.ToArray();
        var second = sequence.ToArray();

        // Assert: the locals restart, so the second pass matches the first
        CollectionAssert.AreEqual( new[] { 0, 1, 2 }, first );
        CollectionAssert.AreEqual( first, second );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void EnumeratesTwiceNested( CompilerType compiler )
    {
        // Arrange
        var sequence = Counter().Compile( compiler )( 3 );

        // Act: the inner loop runs to completion inside every step of the outer one
        var pairs = new List<(int Outer, int Inner)>();

        foreach ( var outer in sequence )
        {
            foreach ( var inner in sequence )
                pairs.Add( (outer, inner) );
        }

        // Assert
        CollectionAssert.AreEqual(
            new[]
            {
                (0, 0), (0, 1), (0, 2),
                (1, 0), (1, 1), (1, 2),
                (2, 0), (2, 1), (2, 2)
            },
            pairs );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void TwoEnumeratorsAdvanceIndependently( CompilerType compiler )
    {
        // Arrange
        var sequence = Counter().Compile( compiler )( 3 );

        using var left = sequence.GetEnumerator();
        using var right = sequence.GetEnumerator();

        // Act & Assert: stepping one must not move the other
        Assert.IsTrue( left.MoveNext() );
        Assert.AreEqual( 0, left.Current );

        Assert.IsTrue( right.MoveNext() );
        Assert.AreEqual( 0, right.Current );

        Assert.IsTrue( left.MoveNext() );
        Assert.AreEqual( 1, left.Current );
        Assert.AreEqual( 0, right.Current );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void CapturedValueSurvivesReenumeration( CompilerType compiler )
    {
        // Arrange: the body reads the enclosing parameter, which travels by field
        var input = Parameter( typeof( int ), "input" );

        var lambda = Lambda<Func<int, IEnumerable<int>>>(
            BlockEnumerable( YieldReturn( input ), YieldReturn( Add( input, Constant( 1 ) ) ) ),
            input );

        var sequence = lambda.Compile( compiler )( 7 );

        // Act
        var first = sequence.ToArray();
        var second = sequence.ToArray();

        // Assert
        CollectionAssert.AreEqual( new[] { 7, 8 }, first );
        CollectionAssert.AreEqual( first, second );
    }
}
