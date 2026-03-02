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
}
