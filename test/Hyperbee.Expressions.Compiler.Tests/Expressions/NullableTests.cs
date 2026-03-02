using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class NullableTests
{
    // --- Nullable Add (lifted) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var add = Expression.Add( a, b );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( add, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn( 1, 2 ) );
        Assert.IsNull( fn( 1, null ) );
        Assert.IsNull( fn( null, 2 ) );
        Assert.IsNull( fn( null, null ) );
    }

    // --- Nullable Subtract (lifted) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Subtract_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var sub = Expression.Subtract( a, b );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( sub, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn( 5, 2 ) );
        Assert.IsNull( fn( 5, null ) );
        Assert.IsNull( fn( null, 2 ) );
    }

    // --- Nullable Multiply (lifted) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Multiply_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var mul = Expression.Multiply( a, b );
        var lambda = Expression.Lambda<Func<int?, int?, int?>>( mul, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 2, 3 ) );
        Assert.IsNull( fn( null, 3 ) );
    }

    // --- Nullable Equal ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Equal_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var eq = Expression.Equal( a, b );
        var lambda = Expression.Lambda<Func<int?, int?, bool>>( eq, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1, 1 ) );
        Assert.IsFalse( fn( 1, 2 ) );
        Assert.IsFalse( fn( 1, null ) );
        Assert.IsFalse( fn( null, 1 ) );
        Assert.IsTrue( fn( null, null ) );
    }

    // --- Nullable NotEqual ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void NotEqual_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var neq = Expression.NotEqual( a, b );
        var lambda = Expression.Lambda<Func<int?, int?, bool>>( neq, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( 1, 1 ) );
        Assert.IsTrue( fn( 1, 2 ) );
        Assert.IsTrue( fn( 1, null ) );
        Assert.IsTrue( fn( null, 1 ) );
        Assert.IsFalse( fn( null, null ) );
    }

    // --- Nullable GreaterThan ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GreaterThan_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var gt = Expression.GreaterThan( a, b );
        var lambda = Expression.Lambda<Func<int?, int?, bool>>( gt, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 2, 1 ) );
        Assert.IsFalse( fn( 1, 2 ) );
        Assert.IsFalse( fn( 1, 1 ) );
        Assert.IsFalse( fn( null, 1 ) );
        Assert.IsFalse( fn( 1, null ) );
        Assert.IsFalse( fn( null, null ) );
    }

    // --- Nullable LessThan ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LessThan_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var b = Expression.Parameter( typeof(int?), "b" );
        var lt = Expression.LessThan( a, b );
        var lambda = Expression.Lambda<Func<int?, int?, bool>>( lt, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1, 2 ) );
        Assert.IsFalse( fn( 2, 1 ) );
        Assert.IsFalse( fn( null, 1 ) );
        Assert.IsFalse( fn( 1, null ) );
    }

    // --- Nullable Negate ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Negate_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var neg = Expression.Negate( a );
        var lambda = Expression.Lambda<Func<int?, int?>>( neg, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -42, fn( 42 ) );
        Assert.AreEqual( 42, fn( -42 ) );
        Assert.IsNull( fn( null ) );
    }

    // --- Nullable Not (bool?) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Not_NullableBool( CompilerType compilerType )
    {
        // FEC known bug: FEC generates incorrect IL for Not(bool?).
        // Calling ANY value through the compiled delegate causes AccessViolationException
        // (crashes the test host). Guard prevents delegate invocation to avoid process crash.
        // See FecKnownIssues.Pattern21_Not_NullableBool_HyperbeeNative for Hyperbee verification.
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC Not(bool?) generates invalid IL that crashes " +
                                 "the test host (AccessViolationException). See FecKnownIssues.Pattern21." );

        var a = Expression.Parameter( typeof(bool?), "a" );
        var not = Expression.Not( a );
        var lambda = Expression.Lambda<Func<bool?, bool?>>( not, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( false, fn( true ) );
        Assert.AreEqual( true, fn( false ) );
        Assert.IsNull( fn( null ) );
    }

    // --- HasValue check ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void HasValue_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var hasValue = Expression.Property( a, "HasValue" );
        var lambda = Expression.Lambda<Func<int?, bool>>( hasValue, a );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 42 ) );
        Assert.IsFalse( fn( null ) );
    }

    // --- GetValueOrDefault ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void GetValueOrDefault_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var getVal = Expression.Call( a, typeof(int?).GetMethod( "GetValueOrDefault", Type.EmptyTypes )! );
        var lambda = Expression.Lambda<Func<int?, int>>( getVal, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( 0, fn( null ) );
    }

    // --- Nullable Add with double ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Add_NullableDouble( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(double?), "a" );
        var b = Expression.Parameter( typeof(double?), "b" );
        var add = Expression.Add( a, b );
        var lambda = Expression.Lambda<Func<double?, double?, double?>>( add, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.5, fn( 1.0, 2.5 ) );
        Assert.IsNull( fn( null, 2.5 ) );
    }

    // --- Coalesce: a ?? b ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableInt( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var coalesce = Expression.Coalesce( a, Expression.Constant( 99 ) );
        var lambda = Expression.Lambda<Func<int?, int>>( coalesce, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( 99, fn( null ) );
    }

    // --- Convert: int? -> int (unwrap, throws on null) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Convert_NullableIntToInt_ThrowsOnNull( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var convert = Expression.Convert( a, typeof(int) );
        var lambda = Expression.Lambda<Func<int?, int>>( convert, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        var threw = false;
        try { fn( null ); } catch ( InvalidOperationException ) { threw = true; }
        Assert.IsTrue( threw, "Expected InvalidOperationException for null int? -> int." );
    }

    // --- Nullable conditional: if a.HasValue then a.Value + 1 else -1 ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_WithNullableCheck( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int?), "a" );
        var body = Expression.Condition(
            Expression.Property( a, "HasValue" ),
            Expression.Add(
                Expression.Convert( a, typeof(int) ),
                Expression.Constant( 1 )
            ),
            Expression.Constant( -1 )
        );
        var lambda = Expression.Lambda<Func<int?, int>>( body, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 43, fn( 42 ) );
        Assert.AreEqual( -1, fn( null ) );
    }
}
