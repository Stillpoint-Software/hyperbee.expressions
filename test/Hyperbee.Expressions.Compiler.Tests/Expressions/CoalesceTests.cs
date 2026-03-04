using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class CoalesceTests
{
    // ================================================================
    // Coalesce with reference types (string)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_String_NonNull_ReturnsLeft( CompilerType compilerType )
    {
        // (string s) => s ?? "default"
        var s = Expression.Parameter( typeof( string ), "s" );
        var lambda = Expression.Lambda<Func<string, string>>(
            Expression.Coalesce( s, Expression.Constant( "default" ) ),
            s );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn( "hello" ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_String_Null_ReturnsRight( CompilerType compilerType )
    {
        // (string s) => s ?? "default"
        var s = Expression.Parameter( typeof( string ), "s" );
        var lambda = Expression.Lambda<Func<string, string>>(
            Expression.Coalesce( s, Expression.Constant( "default" ) ),
            s );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "default", fn( null! ) );
    }

    // ================================================================
    // Coalesce with nullable value types
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableInt_HasValue_ReturnsValue( CompilerType compilerType )
    {
        // (int? n) => n ?? -1
        var n = Expression.Parameter( typeof( int? ), "n" );
        var lambda = Expression.Lambda<Func<int?, int>>(
            Expression.Coalesce( n, Expression.Constant( -1 ) ),
            n );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableInt_Null_ReturnsDefault( CompilerType compilerType )
    {
        // (int? n) => n ?? -1
        var n = Expression.Parameter( typeof( int? ), "n" );
        var lambda = Expression.Lambda<Func<int?, int>>(
            Expression.Coalesce( n, Expression.Constant( -1 ) ),
            n );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1, fn( null ) );
    }

    // ================================================================
    // Chained coalesce
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_Chained_ReturnsFirstNonNull( CompilerType compilerType )
    {
        // (string a, string b) => a ?? b ?? "fallback"
        var a = Expression.Parameter( typeof( string ), "a" );
        var b = Expression.Parameter( typeof( string ), "b" );
        var lambda = Expression.Lambda<Func<string, string, string>>(
            Expression.Coalesce( a,
                Expression.Coalesce( b, Expression.Constant( "fallback" ) ) ),
            a, b );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "first", fn( "first", "second" ) );
        Assert.AreEqual( "second", fn( null!, "second" ) );
        Assert.AreEqual( "fallback", fn( null!, null! ) );
    }

    // ================================================================
    // Coalesce — nullable double
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableDouble_HasValue_ReturnsValue( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( double? ), "n" );
        var lambda = Expression.Lambda<Func<double?, double>>(
            Expression.Coalesce( n, Expression.Constant( -1.0 ) ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.14, fn( 3.14 ) );
        Assert.AreEqual( 0.0, fn( 0.0 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableDouble_Null_ReturnsDefault( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( double? ), "n" );
        var lambda = Expression.Lambda<Func<double?, double>>(
            Expression.Coalesce( n, Expression.Constant( -1.0 ) ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1.0, fn( null ) );
    }

    // ================================================================
    // Coalesce — nullable long
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableLong_HasValue_ReturnsValue( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( long? ), "n" );
        var lambda = Expression.Lambda<Func<long?, long>>(
            Expression.Coalesce( n, Expression.Constant( 0L ) ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( long.MaxValue, fn( long.MaxValue ) );
        Assert.AreEqual( -1L, fn( -1L ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableLong_Null_ReturnsDefault( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( long? ), "n" );
        var lambda = Expression.Lambda<Func<long?, long>>(
            Expression.Coalesce( n, Expression.Constant( 99L ) ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99L, fn( null ) );
    }

    // ================================================================
    // Coalesce — nullable bool
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableBool_HasValue_ReturnsValue( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( bool? ), "n" );
        var lambda = Expression.Lambda<Func<bool?, bool>>(
            Expression.Coalesce( n, Expression.Constant( false ) ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( true, fn( true ) );
        Assert.AreEqual( false, fn( false ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableBool_Null_ReturnsDefault( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( bool? ), "n" );
        var lambda = Expression.Lambda<Func<bool?, bool>>(
            Expression.Coalesce( n, Expression.Constant( true ) ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( true, fn( null ) );
    }

    // ================================================================
    // Coalesce — object (reference type)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_Object_NonNull_ReturnsLeft( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof( object ), "obj" );
        var fallback = Expression.Constant( "fallback", typeof( object ) );
        var lambda = Expression.Lambda<Func<object, object>>(
            Expression.Coalesce( obj, fallback ), obj );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( "hello", fn( "hello" ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_Object_Null_ReturnsFallback( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof( object ), "obj" );
        var fallback = Expression.Constant( "fallback", typeof( object ) );
        var lambda = Expression.Lambda<Func<object, object>>(
            Expression.Coalesce( obj, fallback ), obj );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "fallback", fn( null! ) );
    }

    // ================================================================
    // Coalesce — nullable decimal
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableDecimal_HasValue_ReturnsValue( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( decimal? ), "n" );
        var lambda = Expression.Lambda<Func<decimal?, decimal>>(
            Expression.Coalesce( n, Expression.Constant( 0m ) ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.14m, fn( 3.14m ) );
        Assert.AreEqual( decimal.MaxValue, fn( decimal.MaxValue ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableDecimal_Null_ReturnsDefault( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( decimal? ), "n" );
        var lambda = Expression.Lambda<Func<decimal?, decimal>>(
            Expression.Coalesce( n, Expression.Constant( 99m ) ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99m, fn( null ) );
    }

    // ================================================================
    // Coalesce — used inside block assigned to variable
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_InsideBlock_AssignedToVariable( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof( string ), "s" );
        var result = Expression.Variable( typeof( string ), "result" );
        var body = Expression.Block(
            new[] { result },
            Expression.Assign( result, Expression.Coalesce( s, Expression.Constant( "default" ) ) ),
            result );
        var lambda = Expression.Lambda<Func<string, string>>( body, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn( "hello" ) );
        Assert.AreEqual( "default", fn( null! ) );
    }

    // ================================================================
    // Coalesce — result used in arithmetic
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_UsedInArithmetic_AddsToResult( CompilerType compilerType )
    {
        // (int? n) => (n ?? 0) + 10
        var n = Expression.Parameter( typeof( int? ), "n" );
        var body = Expression.Add(
            Expression.Coalesce( n, Expression.Constant( 0 ) ),
            Expression.Constant( 10 ) );
        var lambda = Expression.Lambda<Func<int?, int>>( body, n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 15, fn( 5 ) );
        Assert.AreEqual( 10, fn( null ) );
    }

    // ================================================================
    // Coalesce — triple chain with all different types
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_TripleChain_NullableInt( CompilerType compilerType )
    {
        // (int? a, int? b, int? c) => a ?? b ?? c ?? -1
        var a = Expression.Parameter( typeof( int? ), "a" );
        var b = Expression.Parameter( typeof( int? ), "b" );
        var c = Expression.Parameter( typeof( int? ), "c" );
        var lambda = Expression.Lambda<Func<int?, int?, int?, int>>(
            Expression.Coalesce( a,
                Expression.Coalesce( b,
                    Expression.Coalesce( c, Expression.Constant( -1 ) ) ) ),
            a, b, c );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( 1, 2, 3 ) );
        Assert.AreEqual( 2, fn( null, 2, 3 ) );
        Assert.AreEqual( 3, fn( null, null, 3 ) );
        Assert.AreEqual( -1, fn( null, null, null ) );
    }

    // ================================================================
    // Coalesce — string with null-producing expression on right
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Coalesce_NullableInt_ZeroIsNotNull( CompilerType compilerType )
    {
        // Zero is a valid value, not null — should return 0, not default
        var n = Expression.Parameter( typeof( int? ), "n" );
        var lambda = Expression.Lambda<Func<int?, int>>(
            Expression.Coalesce( n, Expression.Constant( 99 ) ), n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0 ) );    // 0 has value, returns 0
        Assert.AreEqual( -1, fn( -1 ) );  // negative value is preserved
        Assert.AreEqual( 99, fn( null ) );
    }
}
