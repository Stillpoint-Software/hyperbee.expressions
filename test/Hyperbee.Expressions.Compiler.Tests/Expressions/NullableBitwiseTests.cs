using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class NullableBitwiseTests
{
    // ================================================================
    // And — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void And_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.And( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0xFF & 0x0F, fn( 0xFF, 0x0F ) );
        Assert.AreEqual( 0, fn( 0, 0xFF ) );
        Assert.IsNull( fn( 0xFF, null ) );
        Assert.IsNull( fn( null, 0x0F ) );
        Assert.IsNull( fn( null, null ) );
    }

    // ================================================================
    // And — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void And_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.And( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0xFFL & 0x0FL, fn( 0xFFL, 0x0FL ) );
        Assert.IsNull( fn( 0xFFL, null ) );
        Assert.IsNull( fn( null, 0x0FL ) );
    }

    // ================================================================
    // And — nullable uint
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void And_NullableUInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(uint?), "a" );
        var b = Expression.Parameter( typeof(uint?), "b" );
        var lambda = Expression.Lambda<Func<uint?, uint?, uint?>>( Expression.And( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (uint)0xF0 & (uint)0x0F, fn( 0xF0u, 0x0Fu ) );
        Assert.IsNull( fn( 0xF0u, null ) );
        Assert.IsNull( fn( null, 0x0Fu ) );
    }

    // ================================================================
    // Or — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Or_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.Or( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0xF0 | 0x0F, fn( 0xF0, 0x0F ) );
        Assert.AreEqual( 0xFF, fn( 0xFF, 0 ) );
        Assert.IsNull( fn( 0xF0, null ) );
        Assert.IsNull( fn( null, 0x0F ) );
        Assert.IsNull( fn( null, null ) );
    }

    // ================================================================
    // Or — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Or_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.Or( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0xF0L | 0x0FL, fn( 0xF0L, 0x0FL ) );
        Assert.IsNull( fn( 0xF0L, null ) );
        Assert.IsNull( fn( null, 0x0FL ) );
    }

    // ================================================================
    // Xor — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Xor_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.ExclusiveOr( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0xFF ^ 0x0F, fn( 0xFF, 0x0F ) );
        Assert.AreEqual( 0, fn( 42, 42 ) );
        Assert.IsNull( fn( 0xFF, null ) );
        Assert.IsNull( fn( null, 0x0F ) );
        Assert.IsNull( fn( null, null ) );
    }

    // ================================================================
    // Xor — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Xor_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.ExclusiveOr( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0xFFL ^ 0x0FL, fn( 0xFFL, 0x0FL ) );
        Assert.IsNull( fn( 0xFFL, null ) );
        Assert.IsNull( fn( null, 0x0FL ) );
    }

    // ================================================================
    // LeftShift — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LeftShift_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.LeftShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1 << 3, fn( 1, 3 ) );
        Assert.AreEqual( 16, fn( 2, 3 ) );
        Assert.IsNull( fn( 1, null ) );
        Assert.IsNull( fn( null, 3 ) );
        Assert.IsNull( fn( null, null ) );
    }

    // ================================================================
    // LeftShift — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LeftShift_NullableLong( CompilerType compilerType )
    {
        // LeftShift(long?, int?) — shift count must be int (not long)
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<long?, int?, long?>>( Expression.LeftShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1L << 4, fn( 1L, 4 ) );
        Assert.IsNull( fn( 1L, null ) );
        Assert.IsNull( fn( null, 4 ) );
    }

    // ================================================================
    // RightShift — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void RightShift_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.RightShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 16 >> 2, fn( 16, 2 ) );
        Assert.AreEqual( 0, fn( 1, 4 ) );
        Assert.IsNull( fn( 16, null ) );
        Assert.IsNull( fn( null, 2 ) );
        Assert.IsNull( fn( null, null ) );
    }

    // ================================================================
    // RightShift — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void RightShift_NullableLong( CompilerType compilerType )
    {
        // RightShift(long?, int?) — shift count must be int (not long)
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<long?, int?, long?>>( Expression.RightShift( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 64L >> 3, fn( 64L, 3 ) );
        Assert.IsNull( fn( 64L, null ) );
        Assert.IsNull( fn( null, 3 ) );
    }

    // ================================================================
    // OnesComplement — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OnesComplement_NullableInt( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC crashes (AccessViolationException) on OnesComplement(int?)." );

        var a = Expression.Parameter( typeof(int?), "a" );
        var lambda = Expression.Lambda<Func<int?, int?>>( Expression.OnesComplement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( ~0, fn( 0 ) );
        Assert.AreEqual( ~42, fn( 42 ) );
        Assert.AreEqual( ~int.MaxValue, fn( int.MaxValue ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // OnesComplement — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OnesComplement_NullableLong( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC crashes (InvalidProgramException) on OnesComplement(long?)." );

        var a = Expression.Parameter( typeof(long?), "a" );
        var lambda = Expression.Lambda<Func<long?, long?>>( Expression.OnesComplement( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( ~0L, fn( 0L ) );
        Assert.AreEqual( ~1L, fn( 1L ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // Negate — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var lambda = Expression.Lambda<Func<long?, long?>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -42L, fn( 42L ) );
        Assert.AreEqual( 42L, fn( -42L ) );
        Assert.AreEqual( 0L, fn( 0L ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // Negate — nullable float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_NullableFloat( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float?), "a" );
        var lambda = Expression.Lambda<Func<float?, float?>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1.5f, fn( 1.5f ) );
        Assert.AreEqual( 1.5f, fn( -1.5f ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // Negate — nullable decimal
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_NullableDecimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal?), "a" );
        var lambda = Expression.Lambda<Func<decimal?, decimal?>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -3.14m, fn( 3.14m ) );
        Assert.AreEqual( 3.14m, fn( -3.14m ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // UnaryPlus — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void UnaryPlus_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var lambda = Expression.Lambda<Func<int?, int?>>( Expression.UnaryPlus( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( -42, fn( -42 ) );
        Assert.AreEqual( 0, fn( 0 ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // UnaryPlus — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void UnaryPlus_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var lambda = Expression.Lambda<Func<double?, double?>>( Expression.UnaryPlus( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.14, fn( 3.14 ) );
        Assert.AreEqual( -2.5, fn( -2.5 ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // NegateChecked — nullable int (in-range)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NegateChecked_NullableInt_InRange( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var lambda = Expression.Lambda<Func<int?, int?>>( Expression.NegateChecked( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -42, fn( 42 ) );
        Assert.AreEqual( 42, fn( -42 ) );
        Assert.AreEqual( 0, fn( 0 ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // NegateChecked — nullable int (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NegateChecked_NullableInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var lambda = Expression.Lambda<Func<int?, int?>>( Expression.NegateChecked( a ), a );
        var fn = lambda.Compile( compilerType );

        var threw = false;
        try { fn( int.MinValue ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from NegateChecked(int?.MinValue)." );
    }

    // ================================================================
    // Not (bool?) — via lifted unary
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Not_NullableBool_LiftedNullCheck( CompilerType compilerType )
    {
        // Tests the core lifted null-check behavior: null? -> null?
        var a = Expression.Parameter( typeof(bool?), "a" );
        var lambda = Expression.Lambda<Func<bool?, bool?>>( Expression.Not( a ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( false, fn( true ) );
        Assert.AreEqual( true, fn( false ) );
        Assert.IsNull( fn( null ) );
    }
}
