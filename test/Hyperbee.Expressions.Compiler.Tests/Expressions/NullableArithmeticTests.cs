using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class NullableArithmeticTests
{
    // ================================================================
    // Divide — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn( 6, 2 ) );
        Assert.AreEqual( 0, fn( 0, 5 ) );
        Assert.IsNull( fn( 6, null ) );
        Assert.IsNull( fn( null, 2 ) );
        Assert.IsNull( fn( null, null ) );
    }

    // ================================================================
    // Divide — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3L, fn( 9L, 3L ) );
        Assert.IsNull( fn( null, 3L ) );
        Assert.IsNull( fn( 9L, null ) );
    }

    // ================================================================
    // Divide — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var lambda = Expression.Lambda<Func<double?, double?, double?>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2.5, fn( 5.0, 2.0 ) );
        Assert.IsNull( fn( 5.0, null ) );
        Assert.IsNull( fn( null, 2.0 ) );
    }

    // ================================================================
    // Modulo — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( 7, 3 ) );
        Assert.AreEqual( 0, fn( 6, 3 ) );
        Assert.IsNull( fn( 7, null ) );
        Assert.IsNull( fn( null, 3 ) );
        Assert.IsNull( fn( null, null ) );
    }

    // ================================================================
    // Modulo — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1L, fn( 10L, 3L ) );
        Assert.IsNull( fn( null, 3L ) );
    }

    // ================================================================
    // Add — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10L, fn( 4L, 6L ) );
        Assert.IsNull( fn( 4L, null ) );
        Assert.IsNull( fn( null, 6L ) );
        Assert.IsNull( fn( null, null ) );
    }

    // ================================================================
    // Add — nullable float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_NullableFloat( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float?), "a" );
        var b = Expression.Parameter( typeof(float?), "b" );
        var lambda = Expression.Lambda<Func<float?, float?, float?>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.0f, fn( 1.0f, 2.0f ) );
        Assert.IsNull( fn( 1.0f, null ) );
        Assert.IsNull( fn( null, 2.0f ) );
    }

    // ================================================================
    // Add — nullable decimal (operator overload)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_NullableDecimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal?), "a" );
        var b = Expression.Parameter( typeof(decimal?), "b" );
        var lambda = Expression.Lambda<Func<decimal?, decimal?, decimal?>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.5m, fn( 1.0m, 2.5m ) );
        Assert.IsNull( fn( 1.0m, null ) );
        Assert.IsNull( fn( null, 2.5m ) );
    }

    // ================================================================
    // Subtract — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3L, fn( 10L, 7L ) );
        Assert.IsNull( fn( null, 7L ) );
        Assert.IsNull( fn( 10L, null ) );
    }

    // ================================================================
    // Subtract — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var lambda = Expression.Lambda<Func<double?, double?, double?>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2.5, fn( 5.0, 2.5 ) );
        Assert.IsNull( fn( null, 2.5 ) );
    }

    // ================================================================
    // Multiply — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 12L, fn( 3L, 4L ) );
        Assert.IsNull( fn( 3L, null ) );
        Assert.IsNull( fn( null, 4L ) );
    }

    // ================================================================
    // Multiply — nullable decimal
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_NullableDecimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal?), "a" );
        var b = Expression.Parameter( typeof(decimal?), "b" );
        var lambda = Expression.Lambda<Func<decimal?, decimal?, decimal?>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6.0m, fn( 2.0m, 3.0m ) );
        Assert.IsNull( fn( 2.0m, null ) );
    }

    // ================================================================
    // Power — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Power_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var lambda = Expression.Lambda<Func<double?, double?, double?>>( Expression.Power( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 8.0, fn( 2.0, 3.0 ) );
        Assert.AreEqual( 1.0, fn( 5.0, 0.0 ) );
        Assert.IsNull( fn( 2.0, null ) );
        Assert.IsNull( fn( null, 3.0 ) );
    }

    // ================================================================
    // AddChecked — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddChecked_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.AddChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn( 1, 2 ) );
        Assert.IsNull( fn( 1, null ) );
        Assert.IsNull( fn( null, 2 ) );

        var threw = false;
        try { fn( int.MaxValue, 1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from AddChecked overflow on int?." );
    }

    // ================================================================
    // MultiplyChecked — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void MultiplyChecked_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.MultiplyChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 2, 3 ) );
        Assert.IsNull( fn( 2, null ) );

        var threw = false;
        try { fn( int.MaxValue, 2 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from MultiplyChecked overflow on int?." );
    }

    // ================================================================
    // Comparison — Equal (nullable long)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5L, 5L ) );
        Assert.IsFalse( fn( 5L, 6L ) );
        Assert.IsFalse( fn( 5L, null ) );
        Assert.IsFalse( fn( null, 5L ) );
        Assert.IsTrue( fn( null, null ) );
    }

    // ================================================================
    // Comparison — Equal (nullable double)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var lambda = Expression.Lambda<Func<double?, double?, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.5, 1.5 ) );
        Assert.IsFalse( fn( 1.5, 2.5 ) );
        Assert.IsFalse( fn( 1.5, null ) );
        Assert.IsTrue( fn( null, null ) );
    }

    // ================================================================
    // Comparison — Equal (nullable bool)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_NullableBool( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool?), "a" );
        var b = Expression.Parameter( typeof(bool?), "b" );
        var lambda = Expression.Lambda<Func<bool?, bool?, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( true, true ) );
        Assert.IsTrue( fn( false, false ) );
        Assert.IsFalse( fn( true, false ) );
        Assert.IsFalse( fn( true, null ) );
        Assert.IsTrue( fn( null, null ) );
    }

    // ================================================================
    // Comparison — NotEqual (nullable int)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, bool>>( Expression.NotEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( 5L, 5L ) );
        Assert.IsTrue( fn( 5L, 6L ) );
        Assert.IsTrue( fn( 5L, null ) );
        Assert.IsTrue( fn( null, 5L ) );
        Assert.IsFalse( fn( null, null ) );
    }

    // ================================================================
    // Comparison — GreaterThan (nullable long)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5L, 3L ) );
        Assert.IsFalse( fn( 3L, 5L ) );
        Assert.IsFalse( fn( 5L, 5L ) );
        Assert.IsFalse( fn( null, 5L ) );
        Assert.IsFalse( fn( 5L, null ) );
        Assert.IsFalse( fn( null, null ) );
    }

    // ================================================================
    // Comparison — GreaterThan (nullable double)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var lambda = Expression.Lambda<Func<double?, double?, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5.0, 3.0 ) );
        Assert.IsFalse( fn( 3.0, 5.0 ) );
        Assert.IsFalse( fn( null, 3.0 ) );
        Assert.IsFalse( fn( 5.0, null ) );
    }

    // ================================================================
    // Comparison — LessThan (nullable long)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 3L, 5L ) );
        Assert.IsFalse( fn( 5L, 3L ) );
        Assert.IsFalse( fn( 5L, 5L ) );
        Assert.IsFalse( fn( null, 5L ) );
        Assert.IsFalse( fn( 5L, null ) );
    }

    // ================================================================
    // Comparison — LessThanOrEqual (nullable int)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 3, 5 ) );
        Assert.IsTrue( fn( 5, 5 ) );
        Assert.IsFalse( fn( 6, 5 ) );
        Assert.IsFalse( fn( null, 5 ) );
        Assert.IsFalse( fn( 5, null ) );
        Assert.IsFalse( fn( null, null ) );
    }

    // ================================================================
    // Comparison — LessThanOrEqual (nullable long)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 3L, 5L ) );
        Assert.IsTrue( fn( 5L, 5L ) );
        Assert.IsFalse( fn( 6L, 5L ) );
        Assert.IsFalse( fn( null, 5L ) );
    }

    // ================================================================
    // Comparison — GreaterThanOrEqual (nullable int)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5, 3 ) );
        Assert.IsTrue( fn( 5, 5 ) );
        Assert.IsFalse( fn( 3, 5 ) );
        Assert.IsFalse( fn( null, 5 ) );
        Assert.IsFalse( fn( 5, null ) );
        Assert.IsFalse( fn( null, null ) );
    }

    // ================================================================
    // Comparison — GreaterThanOrEqual (nullable double)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var lambda = Expression.Lambda<Func<double?, double?, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5.0, 3.0 ) );
        Assert.IsTrue( fn( 5.0, 5.0 ) );
        Assert.IsFalse( fn( 3.0, 5.0 ) );
        Assert.IsFalse( fn( null, 5.0 ) );
        Assert.IsFalse( fn( 5.0, null ) );
    }

    // ================================================================
    // SubtractChecked — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void SubtractChecked_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( Expression.SubtractChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn( 5, 2 ) );
        Assert.IsNull( fn( 5, null ) );
        Assert.IsNull( fn( null, 2 ) );

        var threw = false;
        try { fn( int.MinValue, 1 ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from SubtractChecked overflow on int?." );
    }

    // ================================================================
    // Add — nullable float and double mixed scenario
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_NullableFloat( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float?), "a" );
        var b = Expression.Parameter( typeof(float?), "b" );
        var lambda = Expression.Lambda<Func<float?, float?, float?>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1.5f, fn( 4.0f, 2.5f ) );
        Assert.IsNull( fn( 4.0f, null ) );
        Assert.IsNull( fn( null, 2.5f ) );
    }

    // ================================================================
    // Multiply — nullable float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_NullableFloat( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float?), "a" );
        var b = Expression.Parameter( typeof(float?), "b" );
        var lambda = Expression.Lambda<Func<float?, float?, float?>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6.0f, fn( 2.0f, 3.0f ) );
        Assert.IsNull( fn( 2.0f, null ) );
        Assert.IsNull( fn( null, 3.0f ) );
    }

    // ================================================================
    // Divide — nullable decimal
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_NullableDecimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal?), "a" );
        var b = Expression.Parameter( typeof(decimal?), "b" );
        var lambda = Expression.Lambda<Func<decimal?, decimal?, decimal?>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2.5m, fn( 5.0m, 2.0m ) );
        Assert.IsNull( fn( 5.0m, null ) );
        Assert.IsNull( fn( null, 2.0m ) );
    }

    // ================================================================
    // GetValueOrDefault — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GetValueOrDefault_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var getVal = Expression.Call( a, typeof(long?).GetMethod( "GetValueOrDefault", Type.EmptyTypes )! );
        var lambda = Expression.Lambda<Func<long?, long>>( getVal, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42L, fn( 42L ) );
        Assert.AreEqual( 0L, fn( null ) );
    }

    // ================================================================
    // HasValue — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void HasValue_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var hasValue = Expression.Property( a, "HasValue" );
        var lambda = Expression.Lambda<Func<double?, bool>>( hasValue, a );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 3.14 ) );
        Assert.IsFalse( fn( null ) );
    }

    // ================================================================
    // Coalesce — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var coalesce = Expression.Coalesce( a, Expression.Constant( -1L ) );
        var lambda = Expression.Lambda<Func<long?, long>>( coalesce, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42L, fn( 42L ) );
        Assert.AreEqual( -1L, fn( null ) );
    }

    // ================================================================
    // Coalesce — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var coalesce = Expression.Coalesce( a, Expression.Constant( 0.0 ) );
        var lambda = Expression.Lambda<Func<double?, double>>( coalesce, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.14, fn( 3.14 ) );
        Assert.AreEqual( 0.0, fn( null ) );
    }

    // ================================================================
    // Conditional with nullable (if a? b : c)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_NullableGuard_ReturnsValueOrDefault( CompilerType compilerType )
    {
        // a.HasValue ? a.Value * 2 : 0L
        var a = Expression.Parameter( typeof(long?), "a" );
        var body = Expression.Condition(
            Expression.Property( a, "HasValue" ),
            Expression.Multiply(
                Expression.Convert( a, typeof(long) ),
                Expression.Constant( 2L ) ),
            Expression.Constant( 0L ) );
        var lambda = Expression.Lambda<Func<long?, long>>( body, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10L, fn( 5L ) );
        Assert.AreEqual( 0L, fn( null ) );
    }

    // ================================================================
    // Convert: nullable int -> nullable long (nullable-to-nullable widening)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_NullableIntToNullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var convert = Expression.Convert( a, typeof(long?) );
        var lambda = Expression.Lambda<Func<int?, long?>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42L, fn( 42 ) );
        Assert.AreEqual( -1L, fn( -1 ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // Convert: nullable long -> nullable int (nullable-to-nullable narrowing)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_NullableLongToNullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var convert = Expression.Convert( a, typeof(int?) );
        var lambda = Expression.Lambda<Func<long?, int?>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42L ) );
        Assert.AreEqual( -1, fn( -1L ) );
        Assert.IsNull( fn( null ) );
    }

    // ================================================================
    // Divide — nullable float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_NullableFloat( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float?), "a" );
        var b = Expression.Parameter( typeof(float?), "b" );
        var lambda = Expression.Lambda<Func<float?, float?, float?>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2.0f, fn( 6.0f, 3.0f ) );
        Assert.IsNull( fn( 6.0f, null ) );
        Assert.IsNull( fn( null, 3.0f ) );
    }

    // ================================================================
    // Modulo — nullable decimal
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_NullableDecimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal?), "a" );
        var b = Expression.Parameter( typeof(decimal?), "b" );
        var lambda = Expression.Lambda<Func<decimal?, decimal?, decimal?>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1m, fn( 7m, 3m ) );
        Assert.AreEqual( 0m, fn( 6m, 3m ) );
        Assert.IsNull( fn( 7m, null ) );
        Assert.IsNull( fn( null, 3m ) );
    }

    // ================================================================
    // Multiply — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var lambda = Expression.Lambda<Func<double?, double?, double?>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6.0, fn( 2.0, 3.0 ) );
        Assert.IsNull( fn( 2.0, null ) );
        Assert.IsNull( fn( null, 3.0 ) );
    }

    // ================================================================
    // NotEqual — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var lambda = Expression.Lambda<Func<double?, double?, bool>>( Expression.NotEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( 1.5, 1.5 ) );
        Assert.IsTrue( fn( 1.5, 2.5 ) );
        Assert.IsTrue( fn( 1.5, null ) );
        Assert.IsTrue( fn( null, 1.5 ) );
        Assert.IsFalse( fn( null, null ) );
    }

    // ================================================================
    // NotEqual — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lambda = Expression.Lambda<Func<int?, int?, bool>>( Expression.NotEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( 5, 5 ) );
        Assert.IsTrue( fn( 5, 6 ) );
        Assert.IsTrue( fn( 5, null ) );
        Assert.IsTrue( fn( null, 5 ) );
        Assert.IsFalse( fn( null, null ) );
    }

    // ================================================================
    // LessThan — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var lambda = Expression.Lambda<Func<double?, double?, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.0, 2.0 ) );
        Assert.IsFalse( fn( 2.0, 1.0 ) );
        Assert.IsFalse( fn( 2.0, 2.0 ) );
        Assert.IsFalse( fn( null, 2.0 ) );
        Assert.IsFalse( fn( 1.0, null ) );
    }

    // ================================================================
    // Equal — nullable float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_NullableFloat( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float?), "a" );
        var b = Expression.Parameter( typeof(float?), "b" );
        var lambda = Expression.Lambda<Func<float?, float?, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.5f, 1.5f ) );
        Assert.IsFalse( fn( 1.5f, 2.5f ) );
        Assert.IsFalse( fn( 1.5f, null ) );
        Assert.IsTrue( fn( null, null ) );
    }

    // ================================================================
    // GreaterThan — nullable float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_NullableFloat( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float?), "a" );
        var b = Expression.Parameter( typeof(float?), "b" );
        var lambda = Expression.Lambda<Func<float?, float?, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 3.0f, 1.0f ) );
        Assert.IsFalse( fn( 1.0f, 3.0f ) );
        Assert.IsFalse( fn( null, 1.0f ) );
        Assert.IsFalse( fn( 3.0f, null ) );
    }

    // ================================================================
    // GreaterThanOrEqual — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5L, 3L ) );
        Assert.IsTrue( fn( 5L, 5L ) );
        Assert.IsFalse( fn( 3L, 5L ) );
        Assert.IsFalse( fn( null, 5L ) );
        Assert.IsFalse( fn( 5L, null ) );
        Assert.IsFalse( fn( null, null ) );
    }

    // ================================================================
    // AddChecked — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddChecked_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.AddChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10L, fn( 4L, 6L ) );
        Assert.IsNull( fn( 4L, null ) );
        Assert.IsNull( fn( null, 6L ) );

        var threw = false;
        try { fn( long.MaxValue, 1L ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from AddChecked overflow on long?." );
    }

    // ================================================================
    // SubtractChecked — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void SubtractChecked_NullableLong( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long?), "a" );
        var b = Expression.Parameter( typeof(long?), "b" );
        var lambda = Expression.Lambda<Func<long?, long?, long?>>( Expression.SubtractChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 4L, fn( 10L, 6L ) );
        Assert.IsNull( fn( 10L, null ) );
        Assert.IsNull( fn( null, 6L ) );

        var threw = false;
        try { fn( long.MinValue, 1L ); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from SubtractChecked overflow on long?." );
    }

    // ================================================================
    // Coalesce — nullable decimal
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableDecimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal?), "a" );
        var coalesce = Expression.Coalesce( a, Expression.Constant( 0m ) );
        var lambda = Expression.Lambda<Func<decimal?, decimal>>( coalesce, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.14m, fn( 3.14m ) );
        Assert.AreEqual( 0m, fn( null ) );
    }

    // ================================================================
    // GetValueOrDefault with explicit default — nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GetValueOrDefault_WithDefault_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var getVal = Expression.Call( a, typeof(int?).GetMethod( "GetValueOrDefault", [typeof(int)] )!, Expression.Constant( 99 ) );
        var lambda = Expression.Lambda<Func<int?, int>>( getVal, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( 99, fn( null ) );
    }
}
