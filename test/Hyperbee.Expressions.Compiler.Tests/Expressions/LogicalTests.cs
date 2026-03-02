using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class LogicalTests
{
    // --- AndAlso (basic) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AndAlso_Basic( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var b = Expression.Parameter( typeof(bool), "b" );
        var lambda = Expression.Lambda<Func<bool, bool, bool>>( Expression.AndAlso( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( true, true ) );
        Assert.IsFalse( fn( true, false ) );
        Assert.IsFalse( fn( false, true ) );
        Assert.IsFalse( fn( false, false ) );
    }

    // --- OrElse (basic) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OrElse_Basic( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var b = Expression.Parameter( typeof(bool), "b" );
        var lambda = Expression.Lambda<Func<bool, bool, bool>>( Expression.OrElse( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( true, true ) );
        Assert.IsTrue( fn( true, false ) );
        Assert.IsTrue( fn( false, true ) );
        Assert.IsFalse( fn( false, false ) );
    }

    // --- AndAlso short-circuit: right side not evaluated when left is false ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AndAlso_ShortCircuit_LeftFalse( CompilerType compilerType )
    {
        // Use a static method to track whether the right side was evaluated.
        // Build: (a, counter) => a && IncrementAndReturnTrue(counter)
        // When a=false, IncrementAndReturnTrue should NOT be called.

        var a = Expression.Parameter( typeof(bool), "a" );
        var counter = Expression.Parameter( typeof(int[]), "counter" );

        var incrementMethod = typeof(LogicalTests).GetMethod( nameof(IncrementAndReturnTrue) )!;
        var rightSide = Expression.Call( incrementMethod, counter );

        var body = Expression.AndAlso( a, rightSide );
        var lambda = Expression.Lambda<Func<bool, int[], bool>>( body, a, counter );
        var fn = lambda.Compile( compilerType );

        var counts = new int[1];

        // Left is false — right should NOT be evaluated
        var result = fn( false, counts );
        Assert.IsFalse( result );
        Assert.AreEqual( 0, counts[0], "Right side of AndAlso should not be evaluated when left is false." );

        // Left is true — right SHOULD be evaluated
        result = fn( true, counts );
        Assert.IsTrue( result );
        Assert.AreEqual( 1, counts[0], "Right side of AndAlso should be evaluated when left is true." );
    }

    // --- OrElse short-circuit: right side not evaluated when left is true ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OrElse_ShortCircuit_LeftTrue( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var counter = Expression.Parameter( typeof(int[]), "counter" );

        var incrementMethod = typeof(LogicalTests).GetMethod( nameof(IncrementAndReturnTrue) )!;
        var rightSide = Expression.Call( incrementMethod, counter );

        var body = Expression.OrElse( a, rightSide );
        var lambda = Expression.Lambda<Func<bool, int[], bool>>( body, a, counter );
        var fn = lambda.Compile( compilerType );

        var counts = new int[1];

        // Left is true — right should NOT be evaluated
        var result = fn( true, counts );
        Assert.IsTrue( result );
        Assert.AreEqual( 0, counts[0], "Right side of OrElse should not be evaluated when left is true." );

        // Left is false — right SHOULD be evaluated
        result = fn( false, counts );
        Assert.IsTrue( result );
        Assert.AreEqual( 1, counts[0], "Right side of OrElse should be evaluated when left is false." );
    }

    // --- Nested AndAlso/OrElse ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Nested_AndAlso_OrElse( CompilerType compilerType )
    {
        // (a && b) || c
        var a = Expression.Parameter( typeof(bool), "a" );
        var b = Expression.Parameter( typeof(bool), "b" );
        var c = Expression.Parameter( typeof(bool), "c" );

        var body = Expression.OrElse(
            Expression.AndAlso( a, b ),
            c );
        var lambda = Expression.Lambda<Func<bool, bool, bool, bool>>( body, a, b, c );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( true, true, false ) );   // (T && T) || F = T
        Assert.IsFalse( fn( true, false, false ) );  // (T && F) || F = F
        Assert.IsTrue( fn( false, false, true ) );   // (F && _) || T = T
        Assert.IsFalse( fn( false, true, false ) );  // (F && _) || F = F
        Assert.IsTrue( fn( false, false, true ) );   // (F && _) || T = T
    }

    // --- And (bitwise, not short-circuit) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void And_Int_Bitwise( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.And( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( 0, fn( 0xFF, 0x00 ) );
        Assert.AreEqual( 0x0F, fn( 0xFF, 0x0F ) );
        Assert.AreEqual( 0xFF, fn( 0xFF, 0xFF ) );
    }

    // --- Or (bitwise) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Or_Int_Bitwise( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Or( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( 0xFF, fn( 0xFF, 0x00 ) );
        Assert.AreEqual( 0xFF, fn( 0xF0, 0x0F ) );
    }

    // --- ExclusiveOr (bitwise) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Xor_Int_Bitwise( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.ExclusiveOr( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 0 ) );
        Assert.AreEqual( 0xFF, fn( 0xFF, 0x00 ) );
        Assert.AreEqual( 0xFF, fn( 0xF0, 0x0F ) );
        Assert.AreEqual( 0, fn( 0xFF, 0xFF ) );
    }

    // --- LeftShift ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LeftShift_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.LeftShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 5 ) );
        Assert.AreEqual( 2, fn( 1, 1 ) );
        Assert.AreEqual( 8, fn( 1, 3 ) );
        Assert.AreEqual( 1024, fn( 1, 10 ) );
    }

    // --- RightShift ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void RightShift_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.RightShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0, 5 ) );
        Assert.AreEqual( 0, fn( 1, 1 ) );
        Assert.AreEqual( 4, fn( 8, 1 ) );
        Assert.AreEqual( 1, fn( 1024, 10 ) );
        Assert.AreEqual( -1, fn( -1, 1 ) ); // arithmetic right shift preserves sign
    }

    // Helper method for short-circuit tests
    public static bool IncrementAndReturnTrue( int[] counter )
    {
        counter[0]++;
        return true;
    }
}
