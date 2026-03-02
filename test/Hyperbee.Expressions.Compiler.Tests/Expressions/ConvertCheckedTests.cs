using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ConvertCheckedTests
{
    private static void AssertOverflow<TFrom, TTo>(
        Expression<Func<TFrom, TTo>> lambda,
        CompilerType compilerType,
        TFrom overflowValue )
    {
        var fn = lambda.Compile( compilerType );
        var threw = false;
        try { fn( overflowValue ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, $"Expected OverflowException converting {overflowValue} from {typeof(TFrom).Name} to {typeof(TTo).Name}." );
    }

    // ================================================================
    // short -> byte (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_ShortToByte_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(short), "a" );
        var lambda = Expression.Lambda<Func<short, byte>>( Expression.ConvertChecked( a, typeof(byte) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 100, fn( 100 ) );
        Assert.AreEqual( (byte) 0, fn( 0 ) );

        var threw = false;
        try { fn( 256 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for 256 -> byte." );

        threw = false;
        try { fn( -1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for -1 -> byte." );
    }

    // ================================================================
    // int -> short (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_IntToShort_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, short>>( Expression.ConvertChecked( a, typeof(short) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (short) 1000, fn( 1000 ) );

        var threw = false;
        try { fn( (int) short.MaxValue + 1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for int > short.MaxValue -> short." );
    }

    // ================================================================
    // int -> sbyte (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_IntToSByte_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, sbyte>>( Expression.ConvertChecked( a, typeof(sbyte) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (sbyte) 42, fn( 42 ) );
        Assert.AreEqual( (sbyte) -1, fn( -1 ) );

        var threw = false;
        try { fn( 128 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for 128 -> sbyte." );
    }

    // ================================================================
    // int -> sbyte (in-range no overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_IntToSByte_InRange( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, sbyte>>( Expression.ConvertChecked( a, typeof(sbyte) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (sbyte) sbyte.MaxValue, fn( sbyte.MaxValue ) );
        Assert.AreEqual( (sbyte) sbyte.MinValue, fn( sbyte.MinValue ) );
        Assert.AreEqual( (sbyte) 0, fn( 0 ) );
    }

    // ================================================================
    // long -> uint (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_LongToUInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var lambda = Expression.Lambda<Func<long, uint>>( Expression.ConvertChecked( a, typeof(uint) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (uint) 42, fn( 42L ) );
        Assert.AreEqual( uint.MaxValue, fn( (long) uint.MaxValue ) );

        var threw = false;
        try { fn( (long) uint.MaxValue + 1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for long > uint.MaxValue -> uint." );

        threw = false;
        try { fn( -1L ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for -1 -> uint." );
    }

    // ================================================================
    // ulong -> int (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_ULongToInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(ulong), "a" );
        var lambda = Expression.Lambda<Func<ulong, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42UL ) );

        var threw = false;
        try { fn( (ulong) int.MaxValue + 1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for ulong > int.MaxValue -> int." );
    }

    // ================================================================
    // ulong -> long (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_ULongToLong_Overflow( CompilerType compilerType )
    {
        // FEC known bug: FEC uses conv.ovf.i8 (signed source) instead of conv.ovf.i8.un
        // (unsigned source) for ulong→long ConvertChecked, so overflow is not detected.
        // See FecKnownIssues.Pattern25.
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC uses wrong conv opcode for ulong→long ConvertChecked, missing overflow. See FecKnownIssues.Pattern25." );

        var a = Expression.Parameter( typeof(ulong), "a" );
        var lambda = Expression.Lambda<Func<ulong, long>>( Expression.ConvertChecked( a, typeof(long) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42L, fn( 42UL ) );
        Assert.AreEqual( long.MaxValue, fn( (ulong) long.MaxValue ) );

        var threw = false;
        try { fn( (ulong) long.MaxValue + 1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for ulong > long.MaxValue -> long." );
    }

    // ================================================================
    // int -> ushort (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_IntToUShort_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, ushort>>( Expression.ConvertChecked( a, typeof(ushort) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (ushort) 1000, fn( 1000 ) );

        var threw = false;
        try { fn( -1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for -1 -> ushort." );

        threw = false;
        try { fn( (int) ushort.MaxValue + 1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for int > ushort.MaxValue -> ushort." );
    }

    // ================================================================
    // double -> int (overflow — large value)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_DoubleToInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var lambda = Expression.Lambda<Func<double, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42.0 ) );

        var threw = false;
        try { fn( (double) int.MaxValue + 1.0 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for double > int.MaxValue -> int." );
    }

    // ================================================================
    // double -> int (NaN throws)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_DoubleToInt_NaN_Throws( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var lambda = Expression.Lambda<Func<double, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        var threw = false;
        try { fn( double.NaN ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException converting NaN to int." );
    }

    // ================================================================
    // double -> long (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_DoubleToLong_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var lambda = Expression.Lambda<Func<double, long>>( Expression.ConvertChecked( a, typeof(long) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 100L, fn( 100.0 ) );

        var threw = false;
        try { fn( double.MaxValue ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for double.MaxValue -> long." );
    }

    // ================================================================
    // float -> int (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_FloatToInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var lambda = Expression.Lambda<Func<float, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42.0f ) );

        var threw = false;
        try { fn( float.MaxValue ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for float.MaxValue -> int." );
    }

    // ================================================================
    // float -> int (NaN throws)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_FloatToInt_NaN_Throws( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var lambda = Expression.Lambda<Func<float, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        var threw = false;
        try { fn( float.NaN ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException converting NaN to int." );
    }

    // ================================================================
    // decimal -> int (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_DecimalToInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var lambda = Expression.Lambda<Func<decimal, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42m ) );

        var threw = false;
        try { fn( (decimal) int.MaxValue + 1m ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for decimal > int.MaxValue -> int." );
    }

    // ================================================================
    // decimal -> long (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_DecimalToLong_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var lambda = Expression.Lambda<Func<decimal, long>>( Expression.ConvertChecked( a, typeof(long) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42L, fn( 42m ) );

        var threw = false;
        try { fn( decimal.MaxValue ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for decimal.MaxValue -> long." );
    }

    // ================================================================
    // decimal -> byte (overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_DecimalToByte_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var lambda = Expression.Lambda<Func<decimal, byte>>( Expression.ConvertChecked( a, typeof(byte) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 42, fn( 42m ) );

        var threw = false;
        try { fn( 256m ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for 256m -> byte." );
    }

    // ================================================================
    // long -> int (in-range no overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_LongToInt_InRange( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var lambda = Expression.Lambda<Func<long, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( int.MaxValue, fn( (long) int.MaxValue ) );
        Assert.AreEqual( int.MinValue, fn( (long) int.MinValue ) );
        Assert.AreEqual( 0, fn( 0L ) );
    }

    // ================================================================
    // Nullable ConvertChecked: int? -> int (null throws InvalidOperationException)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_NullableIntToInt_ThrowsOnNull( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var lambda = Expression.Lambda<Func<int?, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );

        var threw = false;
        try { fn( null ); } catch ( InvalidOperationException ) { threw = true; }
        Assert.IsTrue( threw, "Expected InvalidOperationException unwrapping null int?." );
    }

    // ================================================================
    // Nullable ConvertChecked: int? -> int (with value, in-range)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_NullableIntToInt_WithValue( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var lambda = Expression.Lambda<Func<int?, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( -1, fn( -1 ) );
        Assert.AreEqual( int.MaxValue, fn( int.MaxValue ) );
        Assert.AreEqual( int.MinValue, fn( int.MinValue ) );
    }

    // ================================================================
    // Convert: int? -> long? (nullable widening, no checked needed — validates ConvertChecked path)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_NullableIntToNullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var lambda = Expression.Lambda<Func<int?, long?>>( Expression.ConvertChecked( a, typeof(long?) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42L, fn( 42 ) );
        Assert.AreEqual( -1L, fn( -1 ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // Convert: long? -> int? (nullable narrowing with overflow check)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_NullableLongToNullableInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var lambda = Expression.Lambda<Func<long?, int?>>( Expression.ConvertChecked( a, typeof(int?) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42L ) );
        Assert.IsNull( fn( null ) );

        var threw = false;
        try { fn( (long) int.MaxValue + 1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for long? > int.MaxValue -> int?." );
    }

    // ================================================================
    // double -> float (narrowing, precision loss but no overflow exception)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_DoubleToFloat_NoOverflow( CompilerType compilerType )
    {
        // double -> float does not throw OverflowException even for double.MaxValue
        // (it just saturates to Infinity in float)
        var a = Expression.Parameter( typeof(double), "a" );
        var lambda = Expression.Lambda<Func<double, float>>( Expression.ConvertChecked( a, typeof(float) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.5f, fn( 1.5 ) );
        Assert.IsTrue( float.IsInfinity( fn( double.MaxValue ) ), "Expected +Infinity for double.MaxValue -> float." );
    }

    // ================================================================
    // int -> uint (in-range: 0 and positive values)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_IntToUInt_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, uint>>( Expression.ConvertChecked( a, typeof(uint) ), a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (uint) 42, fn( 42 ) );
        Assert.AreEqual( (uint) int.MaxValue, fn( int.MaxValue ) );

        var threw = false;
        try { fn( -1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for -1 -> uint." );
    }

    // ================================================================
    // double -> Infinity path (Infinity stays Infinity, no overflow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ConvertChecked_DoubleToInt_Infinity_Throws( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var lambda = Expression.Lambda<Func<double, int>>( Expression.ConvertChecked( a, typeof(int) ), a );
        var fn = lambda.Compile( compilerType );

        var threw = false;
        try { fn( double.PositiveInfinity ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for +Infinity -> int." );
    }
}
