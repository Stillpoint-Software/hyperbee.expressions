using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class BoundaryValueTests
{
    // ================================================================
    // Division by zero
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_Int_ByZero_Throws( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        var threw = false;
        try { fn( 1, 0 ); }
        catch ( DivideByZeroException ) { threw = true; }
        Assert.IsTrue( threw, "Expected DivideByZeroException for integer division by zero." );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_Int_ByZero_Throws( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        var threw = false;
        try { fn( 1, 0 ); }
        catch ( DivideByZeroException ) { threw = true; }
        Assert.IsTrue( threw, "Expected DivideByZeroException for integer modulo by zero." );
    }

    // --- Float division by zero produces Infinity, not exception ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_Double_ByZero_ReturnsInfinity( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( double.PositiveInfinity, fn( 1.0, 0.0 ) );
        Assert.AreEqual( double.NegativeInfinity, fn( -1.0, 0.0 ) );
        Assert.IsTrue( double.IsNaN( fn( 0.0, 0.0 ) ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Divide_Float_ByZero_ReturnsInfinity( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var b = Expression.Parameter( typeof(float), "b" );
        var lambda = Expression.Lambda<Func<float, float, float>>( Expression.Divide( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( float.PositiveInfinity, fn( 1f, 0f ) );
        Assert.AreEqual( float.NegativeInfinity, fn( -1f, 0f ) );
        Assert.IsTrue( float.IsNaN( fn( 0f, 0f ) ) );
    }

    // ================================================================
    // NaN arithmetic propagation
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Double_NaN_Propagates( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( double.IsNaN( fn( double.NaN, 1.0 ) ) );
        Assert.IsTrue( double.IsNaN( fn( 1.0, double.NaN ) ) );
        Assert.IsTrue( double.IsNaN( fn( double.NaN, double.NaN ) ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_Double_NaN_Propagates( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( double.IsNaN( fn( double.NaN, 1.0 ) ) );
        Assert.IsTrue( double.IsNaN( fn( 1.0, double.NaN ) ) );
        Assert.IsTrue( double.IsNaN( fn( 0.0, double.NaN ) ) );
    }

    // ================================================================
    // Infinity arithmetic
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Double_Infinity( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( double.PositiveInfinity, fn( double.PositiveInfinity, 1.0 ) );
        Assert.AreEqual( double.NegativeInfinity, fn( double.NegativeInfinity, 1.0 ) );
        Assert.IsTrue( double.IsNaN( fn( double.PositiveInfinity, double.NegativeInfinity ) ) );
        Assert.AreEqual( double.PositiveInfinity, fn( double.PositiveInfinity, double.PositiveInfinity ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_Double_Infinity( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Multiply( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( double.PositiveInfinity, fn( double.PositiveInfinity, 1.0 ) );
        Assert.AreEqual( double.NegativeInfinity, fn( double.PositiveInfinity, -1.0 ) );
        Assert.IsTrue( double.IsNaN( fn( double.PositiveInfinity, 0.0 ) ) );
    }

    // ================================================================
    // MaxValue / MinValue boundaries
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Int_MaxValue_Wraps( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        // Unchecked add wraps around
        Assert.AreEqual( int.MinValue, fn( int.MaxValue, 1 ) );
        Assert.AreEqual( int.MaxValue, fn( int.MinValue, -1 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_Int_MinValue_Wraps( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        // Unchecked subtract wraps around
        Assert.AreEqual( int.MaxValue, fn( int.MinValue, 1 ) );
    }

    // ================================================================
    // Checked overflow
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddChecked_Long_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.AddChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3L, fn( 1L, 2L ) );

        var threw = false;
        try { fn( long.MaxValue, 1L ); }
        catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from AddChecked(long.MaxValue, 1)." );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void MultiplyChecked_Long_Overflow( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.MultiplyChecked( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6L, fn( 2L, 3L ) );

        var threw = false;
        try { fn( long.MaxValue, 2L ); }
        catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from MultiplyChecked(long.MaxValue, 2)." );
    }

    // ================================================================
    // Null handling
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Object_BothNull( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(object), "a" );
        var b = Expression.Parameter( typeof(object), "b" );
        var lambda = Expression.Lambda<Func<object, object, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( null!, null! ) );
        Assert.IsFalse( fn( "hello", null! ) );
        Assert.IsFalse( fn( null!, "hello" ) );
    }

    // ================================================================
    // MinValue edge cases for negate
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_Int_MinValue_WrapsUnchecked( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>( Expression.Negate( a ), a );
        var fn = lambda.Compile( compilerType );

        // Unchecked negate of MinValue wraps to MinValue (two's complement)
        Assert.AreEqual( int.MinValue, fn( int.MinValue ) );
    }

    // ================================================================
    // Decimal boundary values
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_Decimal_MaxValue_Throws( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var b = Expression.Parameter( typeof(decimal), "b" );
        var lambda = Expression.Lambda<Func<decimal, decimal, decimal>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.0m, fn( 1.0m, 2.0m ) );

        var threw = false;
        try { fn( decimal.MaxValue, 1.0m ); }
        catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException from decimal.MaxValue + 1." );
    }

    // ================================================================
    // Double modulo by zero
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Modulo_Double_ByZero_ReturnsNaN( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, double>>( Expression.Modulo( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( double.IsNaN( fn( 1.0, 0.0 ) ) );
        Assert.IsTrue( double.IsNaN( fn( 0.0, 0.0 ) ) );
    }
}
