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

    // ================================================================
    // Decrement — float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Decrement_Float( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var lambda = Expression.Lambda<Func<float, float>>( Expression.Decrement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 4.5f, fn( 5.5f ), 1e-6f );
        Assert.AreEqual( -1.0f, fn( 0.0f ), 1e-6f );
    }

    // ================================================================
    // Increment — byte (promoted through int)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Increment_Short( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( short ), "a" );
        var lambda = Expression.Lambda<Func<short, short>>( Expression.Increment( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (short) 6, fn( 5 ) );
        Assert.AreEqual( (short) 0, fn( -1 ) );
    }

    // ================================================================
    // Decrement — short
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Decrement_Short( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( short ), "a" );
        var lambda = Expression.Lambda<Func<short, short>>( Expression.Decrement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (short) 4, fn( 5 ) );
        Assert.AreEqual( (short) -1, fn( 0 ) );
    }

    // ================================================================
    // Decrement — ulong
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Decrement_ULong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( ulong ), "a" );
        var lambda = Expression.Lambda<Func<ulong, ulong>>( Expression.Decrement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( ulong.MaxValue - 1, fn( ulong.MaxValue ) );
        Assert.AreEqual( 0UL, fn( 1UL ) );
    }

    // ================================================================
    // PostIncrementAssign — double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void PostIncrementAssign_Double( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC PostIncrementAssign on double returns pre-increment value instead of post-increment." );

        var i = Expression.Variable( typeof( double ), "i" );
        var body = Expression.Block(
            new[] { i },
            Expression.Assign( i, Expression.Constant( 1.5 ) ),
            Expression.PostIncrementAssign( i ),
            i );
        var lambda = Expression.Lambda<Func<double>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2.5, fn(), 1e-9 );
    }

    // ================================================================
    // PreDecrementAssign — long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void PreDecrementAssign_Long( CompilerType compilerType )
    {
        var i = Expression.Variable( typeof( long ), "i" );
        var body = Expression.Block(
            new[] { i },
            Expression.Assign( i, Expression.Constant( 10L ) ),
            Expression.PreDecrementAssign( i ),
            i );
        var lambda = Expression.Lambda<Func<long>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 9L, fn() );
    }

    // ================================================================
    // Negate — short
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_Short( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( short ), "a" );
        var lambda = Expression.Lambda<Func<short, short>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (short) -5, fn( 5 ) );
        Assert.AreEqual( (short) 10, fn( -10 ) );
    }

    // ================================================================
    // UnaryPlus — short
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void UnaryPlus_Short( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( short ), "a" );
        var lambda = Expression.Lambda<Func<short, short>>( Expression.UnaryPlus( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (short) 5, fn( 5 ) );
        Assert.AreEqual( (short) -3, fn( -3 ) );
    }

    // ================================================================
    // Negate — byte (int-widened)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_SByte( CompilerType compilerType )
    {
        // Expression.Negate is not defined for sbyte directly; widen to int, negate, narrow back
        var a = Expression.Parameter( typeof( sbyte ), "a" );
        var negated = Expression.Convert(
            Expression.Negate( Expression.Convert( a, typeof( int ) ) ),
            typeof( sbyte ) );
        var lambda = Expression.Lambda<Func<sbyte, sbyte>>( negated, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (sbyte) -10, fn( 10 ) );
        Assert.AreEqual( (sbyte) 1, fn( -1 ) );
    }

    // ================================================================
    // IsFalse — nullable bool (FEC known bug)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IsFalse_NullableBool( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC Not(bool?) generates invalid IL. See FecKnownIssues.Pattern21." );

        // IsFalse(bool?) returns bool? (lifted null semantics): null→null, false→true, true→false
        var a = Expression.Parameter( typeof( bool? ), "a" );
        var lambda = Expression.Lambda<Func<bool?, bool?>>( Expression.IsFalse( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (bool?) true, fn( false ) );
        Assert.AreEqual( (bool?) false, fn( true ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // IsTrue — nullable bool (FEC known bug)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IsTrue_NullableBool( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC Not(bool?) generates invalid IL. See FecKnownIssues.Pattern21." );

        // IsTrue(bool?) returns bool? (lifted null semantics): null→null, true→true, false→false
        var a = Expression.Parameter( typeof( bool? ), "a" );
        var lambda = Expression.Lambda<Func<bool?, bool?>>( Expression.IsTrue( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (bool?) true, fn( true ) );
        Assert.AreEqual( (bool?) false, fn( false ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // OnesComplement — byte
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OnesComplement_Byte( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( byte ), "a" );
        var result = Expression.Convert( Expression.OnesComplement( a ), typeof( byte ) );
        var lambda = Expression.Lambda<Func<byte, byte>>( result, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 0xFF, fn( 0 ) );
        Assert.AreEqual( (byte) 0x00, fn( 0xFF ) );
        Assert.AreEqual( (byte) 0xF0, fn( 0x0F ) );
    }
}
