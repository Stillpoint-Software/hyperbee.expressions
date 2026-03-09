using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ComparisonTests
{
    // --- GreaterThan (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1, 0 ) );
        Assert.IsFalse( fn( 0, 0 ) );
        Assert.IsFalse( fn( -1, 0 ) );
        Assert.IsTrue( fn( int.MaxValue, int.MinValue ) );
        Assert.IsFalse( fn( int.MinValue, int.MaxValue ) );
    }

    // --- GreaterThan (long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1L, 0L ) );
        Assert.IsFalse( fn( 0L, 0L ) );
        Assert.IsFalse( fn( -1L, 0L ) );
        Assert.IsTrue( fn( long.MaxValue, long.MinValue ) );
    }

    // --- GreaterThan (double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.0, 0.0 ) );
        Assert.IsFalse( fn( 0.0, 0.0 ) );
        Assert.IsFalse( fn( -1.0, 0.0 ) );

        // NaN comparisons should always return false
        Assert.IsFalse( fn( double.NaN, 0.0 ) );
        Assert.IsFalse( fn( 0.0, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );

        Assert.IsTrue( fn( double.PositiveInfinity, double.MaxValue ) );
        Assert.IsFalse( fn( double.NegativeInfinity, double.MinValue ) );
    }

    // --- GreaterThan (float) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Float( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(float), "a" );
        var b = Expression.Parameter( typeof(float), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1f, 0f ) );
        Assert.IsFalse( fn( 0f, 0f ) );
        Assert.IsFalse( fn( float.NaN, 0f ) );
        Assert.IsFalse( fn( 0f, float.NaN ) );
    }

    // --- LessThan (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 0, 1 ) );
        Assert.IsFalse( fn( 0, 0 ) );
        Assert.IsFalse( fn( 1, 0 ) );
        Assert.IsTrue( fn( int.MinValue, int.MaxValue ) );
        Assert.IsFalse( fn( int.MaxValue, int.MinValue ) );
    }

    // --- LessThan (double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 0.0, 1.0 ) );
        Assert.IsFalse( fn( 0.0, 0.0 ) );
        Assert.IsFalse( fn( double.NaN, 0.0 ) );
        Assert.IsFalse( fn( 0.0, double.NaN ) );
        Assert.IsTrue( fn( double.NegativeInfinity, double.PositiveInfinity ) );
    }

    // --- GreaterThanOrEqual (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1, 0 ) );
        Assert.IsTrue( fn( 0, 0 ) );
        Assert.IsFalse( fn( -1, 0 ) );
        Assert.IsTrue( fn( int.MaxValue, int.MaxValue ) );
    }

    // --- GreaterThanOrEqual (double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.0, 0.0 ) );
        Assert.IsTrue( fn( 0.0, 0.0 ) );
        Assert.IsFalse( fn( double.NaN, 0.0 ) );
        Assert.IsFalse( fn( 0.0, double.NaN ) );
        Assert.IsTrue( fn( double.PositiveInfinity, double.MaxValue ) );
    }

    // --- LessThanOrEqual (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 0, 1 ) );
        Assert.IsTrue( fn( 0, 0 ) );
        Assert.IsFalse( fn( 1, 0 ) );
        Assert.IsTrue( fn( int.MinValue, int.MinValue ) );
    }

    // --- LessThanOrEqual (double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_Double( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 0.0, 1.0 ) );
        Assert.IsTrue( fn( 0.0, 0.0 ) );
        Assert.IsFalse( fn( double.NaN, 0.0 ) );
        Assert.IsFalse( fn( 0.0, double.NaN ) );
    }

    // --- Equal (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 0, 0 ) );
        Assert.IsTrue( fn( 42, 42 ) );
        Assert.IsFalse( fn( 1, 2 ) );
        Assert.IsTrue( fn( int.MaxValue, int.MaxValue ) );
        Assert.IsTrue( fn( int.MinValue, int.MinValue ) );
    }

    // --- Equal (double, NaN) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Double_NaN( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 0.0, 0.0 ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, 0.0 ) );
        Assert.IsTrue( fn( double.PositiveInfinity, double.PositiveInfinity ) );
    }

    // --- Equal (string) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_String( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(string), "a" );
        var b = Expression.Parameter( typeof(string), "b" );
        var lambda = Expression.Lambda<Func<string, string, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( "hello", "hello" ) );
        Assert.IsFalse( fn( "hello", "world" ) );
        Assert.IsTrue( fn( null!, null! ) );
        Assert.IsFalse( fn( "hello", null! ) );
    }

    // --- NotEqual (int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_Int( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, bool>>( Expression.NotEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( 0, 0 ) );
        Assert.IsTrue( fn( 1, 2 ) );
        Assert.IsTrue( fn( -1, 1 ) );
        Assert.IsFalse( fn( int.MaxValue, int.MaxValue ) );
    }

    // --- NotEqual (double, NaN) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_Double_NaN( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double), "a" );
        var b = Expression.Parameter( typeof(double), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.NotEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( 0.0, 0.0 ) );
        Assert.IsTrue( fn( double.NaN, double.NaN ) ); // NaN != NaN is true
        Assert.IsTrue( fn( double.NaN, 0.0 ) );
    }

    // --- Comparison with decimal (operator overload) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Decimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(decimal), "a" );
        var b = Expression.Parameter( typeof(decimal), "b" );
        var node = Expression.GreaterThan( a, b );
        Assert.IsNotNull( node.Method, "Expected decimal GreaterThan to use operator overload." );

        var lambda = Expression.Lambda<Func<decimal, decimal, bool>>( node, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.0m, 0.0m ) );
        Assert.IsFalse( fn( 0.0m, 0.0m ) );
        Assert.IsFalse( fn( -1.0m, 0.0m ) );
    }

    // --- Equal (bool) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Bool( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(bool), "a" );
        var b = Expression.Parameter( typeof(bool), "b" );
        var lambda = Expression.Lambda<Func<bool, bool, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( true, true ) );
        Assert.IsTrue( fn( false, false ) );
        Assert.IsFalse( fn( true, false ) );
        Assert.IsFalse( fn( false, true ) );
    }

    // ================================================================
    // NaN comparisons — IEEE 754 unordered semantics
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Double_BothNaN_IsFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.NaN, double.NaN ) );  // NaN != NaN
        Assert.IsFalse( fn( double.NaN, 1.0 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Float_NaN_IsFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( float.NaN, float.NaN ) );
        Assert.IsFalse( fn( float.NaN, 1.0f ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_Double_NaN_IsTrue( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.NotEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( double.NaN, double.NaN ) );  // NaN != NaN is true
        Assert.IsTrue( fn( double.NaN, 1.0 ) );
        Assert.IsTrue( fn( 1.0, double.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_Float_NaN_IsTrue( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.NotEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( float.NaN, float.NaN ) );
        Assert.IsTrue( fn( float.NaN, 0.0f ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Double_NaN_IsFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.NaN, 1.0 ) );
        Assert.IsFalse( fn( 1.0, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Double_NaN_IsFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.NaN, 1.0 ) );
        Assert.IsFalse( fn( 1.0, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Float_NaN_IsFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( float.NaN, 1.0f ) );
        Assert.IsFalse( fn( 1.0f, float.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_Double_NaN_IsFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.NaN, 1.0 ) );
        Assert.IsFalse( fn( 1.0, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_Double_NaN_IsFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.NaN, 1.0 ) );
        Assert.IsFalse( fn( 1.0, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );
    }

    // ================================================================
    // Infinity comparisons
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Double_Infinity_SameSign_IsTrue( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( double.PositiveInfinity, double.PositiveInfinity ) );
        Assert.IsTrue( fn( double.NegativeInfinity, double.NegativeInfinity ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Double_Infinity_DifferentSign_IsFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.PositiveInfinity, double.NegativeInfinity ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Double_Infinity_VsFinite( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( double.PositiveInfinity, 1e300 ) );
        Assert.IsFalse( fn( 1e300, double.PositiveInfinity ) );
        Assert.IsFalse( fn( double.NegativeInfinity, -1e300 ) );
    }

    // ================================================================
    // Boundary value comparisons
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Decimal_MaxValue( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( decimal ), "a" );
        var b = Expression.Parameter( typeof( decimal ), "b" );
        var lambda = Expression.Lambda<Func<decimal, decimal, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( decimal.MaxValue, decimal.MaxValue ) );
        Assert.IsFalse( fn( decimal.MaxValue, decimal.MinValue ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Double_Epsilon_VsZero( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( double.Epsilon, 0.0 ) );
        Assert.IsFalse( fn( 0.0, double.Epsilon ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Char_Comparison( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( char ), "a" );
        var b = Expression.Parameter( typeof( char ), "b" );
        var lambda = Expression.Lambda<Func<char, char, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 'b', 'a' ) );
        Assert.IsFalse( fn( 'a', 'b' ) );
        Assert.IsFalse( fn( 'a', 'a' ) );
        Assert.IsTrue( fn( 'z', 'A' ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Long_BoundaryValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( long ), "b" );
        var lambda = Expression.Lambda<Func<long, long, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( long.MaxValue, long.MaxValue ) );
        Assert.IsTrue( fn( long.MinValue, long.MinValue ) );
        Assert.IsFalse( fn( long.MaxValue, long.MinValue ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_ULong_BoundaryValues( CompilerType compilerType )
    {
        // FEC known bug: FEC uses signed clt instead of unsigned clt.un for ulong,
        // returning wrong results at boundary values (e.g. 0 < ulong.MaxValue → false).
        // See FecKnownIssues.Pattern23.
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC uses signed comparison for ulong, returning wrong results. See FecKnownIssues.Pattern23." );

        var a = Expression.Parameter( typeof( ulong ), "a" );
        var b = Expression.Parameter( typeof( ulong ), "b" );
        var lambda = Expression.Lambda<Func<ulong, ulong, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 0UL, ulong.MaxValue ) );
        Assert.IsFalse( fn( ulong.MaxValue, 0UL ) );
        Assert.IsFalse( fn( ulong.MaxValue, ulong.MaxValue ) );
    }

    // ================================================================
    // GreaterThan — long boundary
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Long_BoundaryValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( long ), "b" );
        var lambda = Expression.Lambda<Func<long, long, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( long.MaxValue, long.MinValue ) );
        Assert.IsFalse( fn( long.MinValue, long.MaxValue ) );
        Assert.IsFalse( fn( 0L, 0L ) );
    }

    // ================================================================
    // LessThan — long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( long ), "b" );
        var lambda = Expression.Lambda<Func<long, long, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( -1L, 1L ) );
        Assert.IsFalse( fn( 1L, -1L ) );
        Assert.IsFalse( fn( long.MaxValue, long.MaxValue ) );
    }

    // ================================================================
    // GreaterThanOrEqual — long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( long ), "b" );
        var lambda = Expression.Lambda<Func<long, long, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5L, 5L ) );
        Assert.IsTrue( fn( 6L, 5L ) );
        Assert.IsFalse( fn( 4L, 5L ) );
    }

    // ================================================================
    // LessThanOrEqual — long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( long ), "b" );
        var lambda = Expression.Lambda<Func<long, long, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5L, 5L ) );
        Assert.IsTrue( fn( 4L, 5L ) );
        Assert.IsFalse( fn( 6L, 5L ) );
    }

    // ================================================================
    // Equal — byte
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Byte( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( byte ), "a" );
        var b = Expression.Parameter( typeof( byte ), "b" );
        var lambda = Expression.Lambda<Func<byte, byte, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 255, 255 ) );
        Assert.IsFalse( fn( 0, 1 ) );
        Assert.IsTrue( fn( 0, 0 ) );
    }

    // ================================================================
    // NotEqual — long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( long ), "b" );
        var lambda = Expression.Lambda<Func<long, long, bool>>( Expression.NotEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1L, 2L ) );
        Assert.IsFalse( fn( long.MaxValue, long.MaxValue ) );
        Assert.IsTrue( fn( long.MinValue, long.MaxValue ) );
    }

    // ================================================================
    // GreaterThan — uint
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_UInt( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC uses signed comparison for uint, returning wrong results. See FecKnownIssues.Pattern23." );

        var a = Expression.Parameter( typeof( uint ), "a" );
        var b = Expression.Parameter( typeof( uint ), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( uint.MaxValue, 0u ) );
        Assert.IsFalse( fn( 0u, uint.MaxValue ) );
        Assert.IsFalse( fn( 5u, 5u ) );
    }

    // ================================================================
    // LessThan — uint
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_UInt( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC uses signed comparison for uint, returning wrong results. See FecKnownIssues.Pattern23." );

        var a = Expression.Parameter( typeof( uint ), "a" );
        var b = Expression.Parameter( typeof( uint ), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 0u, uint.MaxValue ) );
        Assert.IsFalse( fn( uint.MaxValue, 0u ) );
        Assert.IsFalse( fn( 3u, 3u ) );
    }

    // ================================================================
    // GreaterThanOrEqual — decimal
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_Decimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( decimal ), "a" );
        var b = Expression.Parameter( typeof( decimal ), "b" );
        var lambda = Expression.Lambda<Func<decimal, decimal, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 3.14m, 3.14m ) );
        Assert.IsTrue( fn( 100m, 0m ) );
        Assert.IsFalse( fn( -1m, 0m ) );
    }

    // ================================================================
    // Equal — null string vs non-null
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_NullString_VsNonNull( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( string ), "a" );
        var b = Expression.Parameter( typeof( string ), "b" );
        var lambda = Expression.Lambda<Func<string, string, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( null!, "hello" ) );
        Assert.IsFalse( fn( "hello", null! ) );
        Assert.IsTrue( fn( null!, null! ) );
    }

    // ================================================================
    // LessThanOrEqual — uint
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_UInt( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC uses signed comparison for uint. See FecKnownIssues.Pattern23." );

        var a = Expression.Parameter( typeof( uint ), "a" );
        var b = Expression.Parameter( typeof( uint ), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5u, 5u ) );
        Assert.IsTrue( fn( 4u, 5u ) );
        Assert.IsFalse( fn( 6u, 5u ) );
    }

    // ================================================================
    // Equal — long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Long( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( long ), "a" );
        var b = Expression.Parameter( typeof( long ), "b" );
        var lambda = Expression.Lambda<Func<long, long, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( long.MaxValue, long.MaxValue ) );
        Assert.IsFalse( fn( long.MinValue, long.MaxValue ) );
        Assert.IsTrue( fn( 0L, 0L ) );
    }

    // ================================================================
    // GreaterThanOrEqual — uint
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_UInt( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC uses signed comparison for uint. See FecKnownIssues.Pattern23." );

        var a = Expression.Parameter( typeof( uint ), "a" );
        var b = Expression.Parameter( typeof( uint ), "b" );
        var lambda = Expression.Lambda<Func<uint, uint, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 5u, 5u ) );
        Assert.IsTrue( fn( uint.MaxValue, 0u ) );
        Assert.IsFalse( fn( 0u, 1u ) );
    }

    // ================================================================
    // NaN comparisons — float
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Float_NaN_IsAlwaysFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( float.NaN, float.NaN ) );
        Assert.IsFalse( fn( float.NaN, 0f ) );
        Assert.IsFalse( fn( 0f, float.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_Float_NaN_IsAlwaysTrue( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.NotEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( float.NaN, float.NaN ) );
        Assert.IsTrue( fn( float.NaN, 0f ) );
        Assert.IsTrue( fn( 0f, float.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Float_NaN_IsAlwaysFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( float.NaN, 5f ) );
        Assert.IsFalse( fn( 5f, float.NaN ) );
        Assert.IsFalse( fn( float.NaN, float.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Float_NaN_IsAlwaysFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( float.NaN, 5f ) );
        Assert.IsFalse( fn( 5f, float.NaN ) );
        Assert.IsFalse( fn( float.NaN, float.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_Float_NaN_IsAlwaysFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( float.NaN, 5f ) );
        Assert.IsFalse( fn( 5f, float.NaN ) );
        Assert.IsFalse( fn( float.NaN, float.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_Float_NaN_IsAlwaysFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( float.NaN, 5f ) );
        Assert.IsFalse( fn( 5f, float.NaN ) );
        Assert.IsFalse( fn( float.NaN, float.NaN ) );
    }

    // ================================================================
    // NaN comparisons — double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Double_NaN_IsAlwaysFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.NaN, 5.0 ) );
        Assert.IsFalse( fn( 5.0, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Double_NaN_IsAlwaysFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.NaN, 5.0 ) );
        Assert.IsFalse( fn( 5.0, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_Double_NaN_IsAlwaysFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.NaN, 5.0 ) );
        Assert.IsFalse( fn( 5.0, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_Double_NaN_IsAlwaysFalse( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( double.NaN, 5.0 ) );
        Assert.IsFalse( fn( 5.0, double.NaN ) );
        Assert.IsFalse( fn( double.NaN, double.NaN ) );
    }

    // ================================================================
    // Infinity comparisons — double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Double_Infinity_VsInfinity( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( double.PositiveInfinity, double.PositiveInfinity ) );
        Assert.IsTrue( fn( double.NegativeInfinity, double.NegativeInfinity ) );
        Assert.IsFalse( fn( double.PositiveInfinity, double.NegativeInfinity ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Double_Infinity_VsNormal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( double.PositiveInfinity, double.MaxValue ) );
        Assert.IsFalse( fn( double.NegativeInfinity, double.MinValue ) );
        Assert.IsFalse( fn( double.PositiveInfinity, double.PositiveInfinity ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Double_NegInfinity_VsNormal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( double ), "a" );
        var b = Expression.Parameter( typeof( double ), "b" );
        var lambda = Expression.Lambda<Func<double, double, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( double.NegativeInfinity, double.MinValue ) );
        Assert.IsFalse( fn( double.PositiveInfinity, double.MaxValue ) );
        Assert.IsFalse( fn( double.NegativeInfinity, double.NegativeInfinity ) );
    }

    // ================================================================
    // Float basic comparisons
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Float_BasicValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.5f, 1.5f ) );
        Assert.IsFalse( fn( 1.5f, 1.6f ) );
        Assert.IsTrue( fn( 0f, 0f ) );
        Assert.IsTrue( fn( float.MaxValue, float.MaxValue ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_Float_BasicValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.GreaterThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 2.0f, 1.0f ) );
        Assert.IsFalse( fn( 1.0f, 2.0f ) );
        Assert.IsFalse( fn( 1.0f, 1.0f ) );
        Assert.IsTrue( fn( float.MaxValue, float.MinValue ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Float_BasicValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.0f, 2.0f ) );
        Assert.IsFalse( fn( 2.0f, 1.0f ) );
        Assert.IsFalse( fn( 1.0f, 1.0f ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThanOrEqual_Float_BasicValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.GreaterThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 2.0f, 2.0f ) );
        Assert.IsTrue( fn( 3.0f, 2.0f ) );
        Assert.IsFalse( fn( 1.0f, 2.0f ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThanOrEqual_Float_BasicValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( float ), "a" );
        var b = Expression.Parameter( typeof( float ), "b" );
        var lambda = Expression.Lambda<Func<float, float, bool>>( Expression.LessThanOrEqual( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 2.0f, 2.0f ) );
        Assert.IsTrue( fn( 1.0f, 2.0f ) );
        Assert.IsFalse( fn( 3.0f, 2.0f ) );
    }

    // ================================================================
    // Decimal comparisons
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_Decimal_BasicValues( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( decimal ), "a" );
        var b = Expression.Parameter( typeof( decimal ), "b" );
        var lambda = Expression.Lambda<Func<decimal, decimal, bool>>( Expression.Equal( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.5m, 1.5m ) );
        Assert.IsFalse( fn( 1.5m, 1.6m ) );
        Assert.IsTrue( fn( decimal.MaxValue, decimal.MaxValue ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_Decimal( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( decimal ), "a" );
        var b = Expression.Parameter( typeof( decimal ), "b" );
        var lambda = Expression.Lambda<Func<decimal, decimal, bool>>( Expression.LessThan( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1.0m, 2.0m ) );
        Assert.IsFalse( fn( 2.0m, 1.0m ) );
        Assert.IsFalse( fn( 1.0m, 1.0m ) );
    }
}
