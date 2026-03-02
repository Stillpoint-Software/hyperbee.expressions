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
}
