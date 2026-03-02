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

    // --- AndAlso — with method call side effect (short-circuit) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AndAlso_ShortCircuit_FalseFirstPreventsSecond( CompilerType compilerType )
    {
        // When first is false, second side effect should NOT run
        var counter = new int[1];
        var counterParam = Expression.Constant( counter );
        var incMethod = typeof( LogicalTests ).GetMethod( nameof( IncrementAndReturnTrue ) )!;
        var lambda = Expression.Lambda<Func<bool>>(
            Expression.AndAlso(
                Expression.Constant( false ),
                Expression.Call( incMethod, counterParam ) ) );
        var fn = lambda.Compile( compilerType );

        fn();
        Assert.AreEqual( 0, counter[0] );  // second never ran
        Assert.IsFalse( fn() );
        Assert.AreEqual( 0, counter[0] );
    }

    // --- OrElse — with method call side effect (short-circuit) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OrElse_ShortCircuit_TrueFirstPreventsSecond( CompilerType compilerType )
    {
        var counter = new int[1];
        var counterParam = Expression.Constant( counter );
        var incMethod = typeof( LogicalTests ).GetMethod( nameof( IncrementAndReturnTrue ) )!;
        var lambda = Expression.Lambda<Func<bool>>(
            Expression.OrElse(
                Expression.Constant( true ),
                Expression.Call( incMethod, counterParam ) ) );
        var fn = lambda.Compile( compilerType );

        fn();
        Assert.AreEqual( 0, counter[0] );  // second never ran
        Assert.IsTrue( fn() );
        Assert.AreEqual( 0, counter[0] );
    }

    // --- AndAlso — chained three conditions ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AndAlso_Chained_ThreeConditions( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( int ), "a" );
        var b = Expression.Parameter( typeof( int ), "b" );
        var c = Expression.Parameter( typeof( int ), "c" );
        // a > 0 && b > 0 && c > 0
        var lambda = Expression.Lambda<Func<int, int, int, bool>>(
            Expression.AndAlso(
                Expression.AndAlso(
                    Expression.GreaterThan( a, Expression.Constant( 0 ) ),
                    Expression.GreaterThan( b, Expression.Constant( 0 ) ) ),
                Expression.GreaterThan( c, Expression.Constant( 0 ) ) ),
            a, b, c );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1, 2, 3 ) );
        Assert.IsFalse( fn( 1, 0, 3 ) );
        Assert.IsFalse( fn( -1, 2, 3 ) );
    }

    // --- OrElse — chained three conditions ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OrElse_Chained_ThreeConditions( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( int ), "a" );
        var b = Expression.Parameter( typeof( int ), "b" );
        var c = Expression.Parameter( typeof( int ), "c" );
        // a < 0 || b < 0 || c < 0
        var lambda = Expression.Lambda<Func<int, int, int, bool>>(
            Expression.OrElse(
                Expression.OrElse(
                    Expression.LessThan( a, Expression.Constant( 0 ) ),
                    Expression.LessThan( b, Expression.Constant( 0 ) ) ),
                Expression.LessThan( c, Expression.Constant( 0 ) ) ),
            a, b, c );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( 1, 2, 3 ) );
        Assert.IsTrue( fn( 1, -1, 3 ) );
        Assert.IsTrue( fn( -1, 2, 3 ) );
        Assert.IsTrue( fn( 1, 2, -3 ) );
    }

    // --- Not (bool) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Not_Bool( CompilerType compilerType )
    {
        var b = Expression.Parameter( typeof( bool ), "b" );
        var lambda = Expression.Lambda<Func<bool, bool>>( Expression.Not( b ), b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( true ) );
        Assert.IsTrue( fn( false ) );
    }

    // --- AndAlso mixed with OrElse ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AndAlso_WithOrElse_ComplexCondition( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( int ), "a" );
        var b = Expression.Parameter( typeof( int ), "b" );
        // (a > 0 || b > 0) && a != b
        var lambda = Expression.Lambda<Func<int, int, bool>>(
            Expression.AndAlso(
                Expression.OrElse(
                    Expression.GreaterThan( a, Expression.Constant( 0 ) ),
                    Expression.GreaterThan( b, Expression.Constant( 0 ) ) ),
                Expression.NotEqual( a, b ) ),
            a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1, 0 ) );   // (1>0 || 0>0) && 1!=0
        Assert.IsTrue( fn( 0, 1 ) );   // (0>0 || 1>0) && 0!=1
        Assert.IsFalse( fn( 1, 1 ) );  // (1>0 || 1>0) && 1!=1 → false
        Assert.IsFalse( fn( 0, 0 ) );  // (0>0 || 0>0) → false
    }

    // --- AndAlso — result in ternary ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AndAlso_UsedInConditional_BothRequired( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof( int ), "x" );
        var y = Expression.Parameter( typeof( int ), "y" );
        // x > 0 && y > 0 ? x + y : 0
        var lambda = Expression.Lambda<Func<int, int, int>>(
            Expression.Condition(
                Expression.AndAlso(
                    Expression.GreaterThan( x, Expression.Constant( 0 ) ),
                    Expression.GreaterThan( y, Expression.Constant( 0 ) ) ),
                Expression.Add( x, y ),
                Expression.Constant( 0 ) ),
            x, y );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 7, fn( 3, 4 ) );
        Assert.AreEqual( 0, fn( -1, 4 ) );
        Assert.AreEqual( 0, fn( 3, -1 ) );
    }

    // --- LeftShift on long ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LeftShift_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( int ), "b" );
        var lambda = Expression.Lambda<Func<long, int, long>>( Expression.LeftShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1L << 32, fn( 1L, 32 ) );
        Assert.AreEqual( 0L, fn( 0L, 10 ) );
        Assert.AreEqual( 2L, fn( 1L, 1 ) );
    }

    // --- RightShift on long ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void RightShift_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( int ), "b" );
        var lambda = Expression.Lambda<Func<long, int, long>>( Expression.RightShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1L, fn( 1024L, 10 ) );
        Assert.AreEqual( -1L, fn( -1L, 1 ) );  // arithmetic shift preserves sign
        Assert.AreEqual( 0L, fn( 0L, 5 ) );
    }

    // --- And — bitwise on long ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void And_Long_Bitwise( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( long ), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.And( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0xFFL, 0x00L ) );
        Assert.AreEqual( 0x0FL, fn( 0xFFL, 0x0FL ) );
        Assert.AreEqual( long.MaxValue & (long) 0xFFFF, fn( long.MaxValue, 0xFFFFL ) );
    }

    // Helper method for short-circuit tests
    public static bool IncrementAndReturnTrue( int[] counter )
    {
        counter[0]++;
        return true;
    }
}
