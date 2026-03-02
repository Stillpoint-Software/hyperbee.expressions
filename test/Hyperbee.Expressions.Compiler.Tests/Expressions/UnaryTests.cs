using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class UnaryTests
{
    // --- Negate (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( -1, fn( 1 ) );
        Assert.AreEqual( 1, fn( -1 ) );
        Assert.AreEqual( -42, fn( 42 ) );
        Assert.AreEqual( -int.MaxValue, fn( int.MaxValue ) );
    }

    // --- Negate (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var lambda = Expression.Lambda<Func<long, long>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0L ) );
        Assert.AreEqual( -1L, fn( 1L ) );
        Assert.AreEqual( 1L, fn( -1L ) );
        Assert.AreEqual( -long.MaxValue, fn( long.MaxValue ) );
    }

    // --- Negate (double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var lambda = Expression.Lambda<Func<double, double>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0.0, fn( 0.0 ) );
        Assert.AreEqual( -1.0, fn( 1.0 ) );
        Assert.AreEqual( 1.0, fn( -1.0 ) );
        Assert.AreEqual( double.NegativeInfinity, fn( double.PositiveInfinity ) );
        Assert.AreEqual( double.PositiveInfinity, fn( double.NegativeInfinity ) );
        Assert.IsTrue( double.IsNaN( fn( double.NaN ) ) );
    }

    // --- Negate (float) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_Float( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var lambda = Expression.Lambda<Func<float, float>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0f, fn( 0f ) );
        Assert.AreEqual( -1f, fn( 1f ) );
        Assert.AreEqual( 1f, fn( -1f ) );
        Assert.IsTrue( float.IsNaN( fn( float.NaN ) ) );
    }

    // --- NegateChecked (int) — validates Phase 6 SubChecked fix ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NegateChecked_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.NegateChecked( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( -1, fn( 1 ) );
        Assert.AreEqual( 1, fn( -1 ) );
        Assert.AreEqual( -42, fn( 42 ) );
        Assert.AreEqual( -int.MaxValue, fn( int.MaxValue ) );
    }

    // --- NegateChecked (int) — MinValue overflow ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NegateChecked_Int_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.NegateChecked( a ), a );
        var fn = lambda.Compile( compilerType );

        // Negating int.MinValue overflows because |int.MinValue| > int.MaxValue
        // FEC BUG: FEC uses bare `neg` instead of `sub.ovf` so does not throw OverflowException.
        var threw = false;
        try { fn( int.MinValue ); }
        catch ( OverflowException ) { threw = true; }

        if ( compilerType == CompilerType.Fast )
        {
            // FEC known bug: does not detect overflow for NegateChecked
            Assert.IsFalse( threw, "FEC is not expected to throw — remove this assertion when FEC fixes NegateChecked." );
        }
        else
        {
            Assert.IsTrue( threw, "Expected OverflowException from NegateChecked(int.MinValue)." );
        }
    }

    // --- NegateChecked (long) — MinValue overflow ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NegateChecked_Long_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var lambda = Expression.Lambda<Func<long, long>>( Expression.NegateChecked( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1L, fn( 1L ) );
        Assert.AreEqual( 1L, fn( -1L ) );

        // FEC BUG: FEC uses bare `neg` instead of `sub.ovf` so does not throw OverflowException.
        var threw = false;
        try { fn( long.MinValue ); }
        catch ( OverflowException ) { threw = true; }

        if ( compilerType == CompilerType.Fast )
        {
            // FEC known bug: does not detect overflow for NegateChecked
            Assert.IsFalse( threw, "FEC is not expected to throw — remove this assertion when FEC fixes NegateChecked." );
        }
        else
        {
            Assert.IsTrue( threw, "Expected OverflowException from NegateChecked(long.MinValue)." );
        }
    }

    // --- Not (bool) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Not_Bool( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var lambda = Expression.Lambda<Func<bool, bool>>( Expression.Not( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( true ) );
        Assert.IsTrue( fn( false ) );
    }

    // --- Not (int) — bitwise complement ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Not_Int_BitwiseComplement( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.Not( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( ~0, fn( 0 ) );
        Assert.AreEqual( ~1, fn( 1 ) );
        Assert.AreEqual( ~(-1), fn( -1 ) );
        Assert.AreEqual( ~int.MaxValue, fn( int.MaxValue ) );
        Assert.AreEqual( ~int.MinValue, fn( int.MinValue ) );
    }

    // --- OnesComplement (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OnesComplement_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.OnesComplement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( ~0, fn( 0 ) );
        Assert.AreEqual( ~1, fn( 1 ) );
        Assert.AreEqual( ~0xFF, fn( 0xFF ) );
    }

    // --- UnaryPlus (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void UnaryPlus_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.UnaryPlus( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( -42, fn( -42 ) );
    }

    // --- UnaryPlus (double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void UnaryPlus_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var lambda = Expression.Lambda<Func<double, double>>( Expression.UnaryPlus( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0.0, fn( 0.0 ) );
        Assert.AreEqual( 1.5, fn( 1.5 ) );
        Assert.AreEqual( -1.5, fn( -1.5 ) );
    }

    // --- Negate (decimal) — operator overload ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_Decimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var node = Expression.Negate( a );
        Assert.IsNotNull( node.Method, "Expected decimal Negate to use operator overload." );

        var lambda = Expression.Lambda<Func<decimal, decimal>>( node, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0.0m, fn( 0.0m ) );
        Assert.AreEqual( -1.5m, fn( 1.5m ) );
        Assert.AreEqual( 1.5m, fn( -1.5m ) );
    }

    // --- Increment (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Increment_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.Increment( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( 0 ) );
        Assert.AreEqual( 0, fn( -1 ) );
        Assert.AreEqual( 43, fn( 42 ) );
    }

    // --- Decrement (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Decrement_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.Decrement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1, fn( 0 ) );
        Assert.AreEqual( 0, fn( 1 ) );
        Assert.AreEqual( 41, fn( 42 ) );
    }

    // --- Increment (double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Increment_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var lambda = Expression.Lambda<Func<double, double>>( Expression.Increment( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.0, fn( 0.0 ) );
        Assert.AreEqual( 2.5, fn( 1.5 ) );
    }

    // --- Decrement (double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Decrement_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var lambda = Expression.Lambda<Func<double, double>>( Expression.Decrement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1.0, fn( 0.0 ) );
        Assert.AreEqual( 0.5, fn( 1.5 ) );
    }

    // --- Increment (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Increment_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var lambda = Expression.Lambda<Func<long, long>>( Expression.Increment( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1L, fn( 0L ) );
        Assert.AreEqual( 0L, fn( -1L ) );
        Assert.AreEqual( 43L, fn( 42L ) );
    }

    // --- Decrement (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Decrement_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var lambda = Expression.Lambda<Func<long, long>>( Expression.Decrement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1L, fn( 0L ) );
        Assert.AreEqual( 0L, fn( 1L ) );
        Assert.AreEqual( 41L, fn( 42L ) );
    }

    // --- Increment (float) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Increment_Float( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var lambda = Expression.Lambda<Func<float, float>>( Expression.Increment( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.0f, fn( 0.0f ) );
        Assert.AreEqual( 2.5f, fn( 1.5f ) );
    }

    // --- Increment (uint) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Increment_UInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var lambda = Expression.Lambda<Func<uint, uint>>( Expression.Increment( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1u, fn( 0u ) );
        Assert.AreEqual( 43u, fn( 42u ) );
    }

    // --- Increment (ulong) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Increment_ULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var lambda = Expression.Lambda<Func<ulong, ulong>>( Expression.Increment( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1UL, fn( 0UL ) );
        Assert.AreEqual( 43UL, fn( 42UL ) );
    }

    // --- Decrement (uint) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Decrement_UInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var lambda = Expression.Lambda<Func<uint, uint>>( Expression.Decrement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0u, fn( 1u ) );
        Assert.AreEqual( 41u, fn( 42u ) );
    }

    // --- OnesComplement (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OnesComplement_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var lambda = Expression.Lambda<Func<long, long>>( Expression.OnesComplement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( ~0L, fn( 0L ) );
        Assert.AreEqual( ~1L, fn( 1L ) );
        Assert.AreEqual( ~long.MaxValue, fn( long.MaxValue ) );
    }

    // --- OnesComplement (uint) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OnesComplement_UInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint), "a" );
        var lambda = Expression.Lambda<Func<uint, uint>>( Expression.OnesComplement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( ~0u, fn( 0u ) );
        Assert.AreEqual( ~1u, fn( 1u ) );
        Assert.AreEqual( ~uint.MaxValue, fn( uint.MaxValue ) );
    }

    // --- OnesComplement (ulong) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OnesComplement_ULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var lambda = Expression.Lambda<Func<ulong, ulong>>( Expression.OnesComplement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( ~0UL, fn( 0UL ) );
        Assert.AreEqual( ~1UL, fn( 1UL ) );
    }

    // --- UnaryPlus (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void UnaryPlus_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var lambda = Expression.Lambda<Func<long, long>>( Expression.UnaryPlus( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn( 0L ) );
        Assert.AreEqual( 42L, fn( 42L ) );
        Assert.AreEqual( -42L, fn( -42L ) );
    }

    // --- UnaryPlus (float) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void UnaryPlus_Float( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var lambda = Expression.Lambda<Func<float, float>>( Expression.UnaryPlus( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0.0f, fn( 0.0f ) );
        Assert.AreEqual( 1.5f, fn( 1.5f ) );
        Assert.AreEqual( -1.5f, fn( -1.5f ) );
    }

    // --- UnaryPlus (decimal) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void UnaryPlus_Decimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var lambda = Expression.Lambda<Func<decimal, decimal>>( Expression.UnaryPlus( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0m, fn( 0m ) );
        Assert.AreEqual( 3.14m, fn( 3.14m ) );
        Assert.AreEqual( -3.14m, fn( -3.14m ) );
    }

    // --- IsTrue (bool) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IsTrue_Bool( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var lambda = Expression.Lambda<Func<bool, bool>>( Expression.IsTrue( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( true ) );
        Assert.IsFalse( fn( false ) );
    }

    // --- IsFalse (bool) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IsFalse_Bool( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var lambda = Expression.Lambda<Func<bool, bool>>( Expression.IsFalse( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( true ) );
        Assert.IsTrue( fn( false ) );
    }

    // --- Negate (float) — special values ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_Float_SpecialValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var lambda = Expression.Lambda<Func<float, float>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( float.NegativeInfinity, fn( float.PositiveInfinity ) );
        Assert.AreEqual( float.PositiveInfinity, fn( float.NegativeInfinity ) );
    }

    // --- PostIncrementAssign (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void PostIncrementAssign_Long( CompilerType compilerType )
    {
        var i = Expression.Variable( typeof(long), "i" );
        var body = Expression.Block(
            new[] { i },
            Expression.Assign( i, Expression.Constant( 10L ) ),
            Expression.PostIncrementAssign( i ),
            i );
        var lambda = Expression.Lambda<Func<long>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 11L, fn() );
    }

    // --- PostDecrementAssign (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void PostDecrementAssign_Long( CompilerType compilerType )
    {
        var i = Expression.Variable( typeof(long), "i" );
        var body = Expression.Block(
            new[] { i },
            Expression.Assign( i, Expression.Constant( 10L ) ),
            Expression.PostDecrementAssign( i ),
            i );
        var lambda = Expression.Lambda<Func<long>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 9L, fn() );
    }
}
