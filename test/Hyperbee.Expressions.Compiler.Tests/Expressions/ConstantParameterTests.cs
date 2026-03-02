using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ConstantParameterTests
{
    // --- ConstantExpression: int ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_Int( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<int>>( Expression.Constant( 42 ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // --- ConstantExpression: string ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_String( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<string>>( Expression.Constant( "hello" ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn() );
    }

    // --- ConstantExpression: bool ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_Bool( CompilerType compilerType )
    {
        var lambdaTrue  = Expression.Lambda<Func<bool>>( Expression.Constant( true ) );
        var lambdaFalse = Expression.Lambda<Func<bool>>( Expression.Constant( false ) );

        Assert.IsTrue( lambdaTrue.Compile( compilerType )() );
        Assert.IsFalse( lambdaFalse.Compile( compilerType )() );
    }

    // --- ConstantExpression: null ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_Null( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<string?>>( Expression.Constant( null, typeof(string) ) );
        var fn = lambda.Compile( compilerType );

        Assert.IsNull( fn() );
    }

    // --- ConstantExpression: object reference ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_ObjectReference( CompilerType compilerType )
    {
        var obj = new object();
        var lambda = Expression.Lambda<Func<object>>( Expression.Constant( obj ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreSame( obj, fn() );
    }

    // --- ParameterExpression: single parameter ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Parameter_Single( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int, int>>( x, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( -1, fn( -1 ) );
    }

    // --- ParameterExpression: multiple parameters ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Parameter_Multiple( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 2, 3 ) );
        Assert.AreEqual( 0, fn( -1, 1 ) );
    }

    // --- ParameterExpression: parameter used twice ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Parameter_UsedTwice( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        // x * x (same parameter node used twice)
        var lambda = Expression.Lambda<Func<int, int>>( Expression.Multiply( x, x ), x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( 1, fn( 1 ) );
        Assert.AreEqual( 4, fn( 2 ) );
        Assert.AreEqual( 9, fn( 3 ) );
        Assert.AreEqual( 1, fn( -1 ) );
    }

    // --- Nullary lambda (no parameters) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_Nullary( CompilerType compilerType )
    {
        // () => 99
        var lambda = Expression.Lambda<Func<int>>( Expression.Constant( 99 ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }
}
