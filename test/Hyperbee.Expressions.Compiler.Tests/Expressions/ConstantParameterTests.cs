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

    // --- Constant string ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_String_ReturnsValue( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<string>>( Expression.Constant( "hello" ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn() );
    }

    // --- Constant null string ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_NullString_ReturnsNull( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<string>>( Expression.Constant( null, typeof( string ) ) );
        var fn = lambda.Compile( compilerType );

        Assert.IsNull( fn() );
    }

    // --- Constant double ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_Double_ReturnsValue( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<double>>( Expression.Constant( 3.14 ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3.14, fn(), 1e-9 );
    }

    // --- Constant bool ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_Bool_True( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<bool>>( Expression.Constant( true ) );
        Assert.IsTrue( lambda.Compile( compilerType )() );
    }

    // --- Constant long ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_Long_MaxValue( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<long>>( Expression.Constant( long.MaxValue ) );
        Assert.AreEqual( long.MaxValue, lambda.Compile( compilerType )() );
    }

    // --- Parameter — two params, subtraction ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Parameter_TwoParams_Subtraction( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( int ), "a" );
        var b = Expression.Parameter( typeof( int ), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Subtract( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn( 5, 2 ) );
        Assert.AreEqual( -3, fn( 2, 5 ) );
    }

    // --- Parameter — five params summed ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Parameter_FiveParams_Sum( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof( int ), "a" );
        var b = Expression.Parameter( typeof( int ), "b" );
        var c = Expression.Parameter( typeof( int ), "c" );
        var d = Expression.Parameter( typeof( int ), "d" );
        var e = Expression.Parameter( typeof( int ), "e" );
        var body = Expression.Add( a, Expression.Add( b, Expression.Add( c, Expression.Add( d, e ) ) ) );
        var lambda = Expression.Lambda<Func<int, int, int, int, int, int>>( body, a, b, c, d, e );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 15, fn( 1, 2, 3, 4, 5 ) );
        Assert.AreEqual( 0, fn( 0, 0, 0, 0, 0 ) );
    }

    // --- Constant — decimal ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_Decimal_ReturnsValue( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<decimal>>( Expression.Constant( 99.99m ) );
        Assert.AreEqual( 99.99m, lambda.Compile( compilerType )() );
    }

    // --- Parameter — string parameter returned ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Parameter_String_ReturnedDirectly( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof( string ), "s" );
        var lambda = Expression.Lambda<Func<string, string>>( s, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn( "hello" ) );
        Assert.IsNull( fn( null! ) );
    }

    // --- Constant int array ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Constant_Object_IntArray( CompilerType compilerType )
    {
        var arr = new int[] { 1, 2, 3 };
        var lambda = Expression.Lambda<Func<int[]>>( Expression.Constant( arr ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreSame( arr, fn() );
    }
}
