using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class DefaultExpressionTests
{
    // --- default(int) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_Int( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(int) );
        var lambda = Expression.Lambda<Func<int>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn() );
    }

    // --- default(long) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_Long( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(long) );
        var lambda = Expression.Lambda<Func<long>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0L, fn() );
    }

    // --- default(double) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_Double( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(double) );
        var lambda = Expression.Lambda<Func<double>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0.0, fn() );
    }

    // --- default(bool) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_Bool( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(bool) );
        var lambda = Expression.Lambda<Func<bool>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn() );
    }

    // --- default(string) — reference type returns null ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_String( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(string) );
        var lambda = Expression.Lambda<Func<string?>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.IsNull( fn() );
    }

    // --- default(object) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_Object( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(object) );
        var lambda = Expression.Lambda<Func<object?>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.IsNull( fn() );
    }

    // --- default(int?) — nullable value type ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_NullableInt( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(int?) );
        var lambda = Expression.Lambda<Func<int?>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.IsNull( fn() );
    }

    // --- default(DateTime) — struct ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_DateTime( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(DateTime) );
        var boxed = Expression.Convert( expr, typeof(object) );
        var lambda = Expression.Lambda<Func<object>>( boxed );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( default(DateTime), fn() );
    }

    // --- default(byte) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_Byte( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(byte) );
        var lambda = Expression.Lambda<Func<byte>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (byte) 0, fn() );
    }

    // --- default(float) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_Float( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(float) );
        var lambda = Expression.Lambda<Func<float>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0f, fn() );
    }

    // --- default(char) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_Char( CompilerType compilerType )
    {
        var expr = Expression.Default( typeof(char) );
        var lambda = Expression.Lambda<Func<char>>( expr );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( '\0', fn() );
    }

    // --- default in conditional: return default if null ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Default_InConditional( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(string), "a" );
        var body = Expression.Condition(
            Expression.Equal( a, Expression.Constant( null, typeof(string) ) ),
            Expression.Constant( "default" ),
            a
        );
        var lambda = Expression.Lambda<Func<string, string>>( body, a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "default", fn( null! ) );
        Assert.AreEqual( "hello", fn( "hello" ) );
    }
}
