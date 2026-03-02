using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class SwitchTests
{
    // ================================================================
    // Switch with int cases
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_IntCases_ReturnsMatchingCase( CompilerType compilerType )
    {
        // switch (x) { case 1: return 10; case 2: return 20; case 3: return 30; default: return -1; }
        var x = Expression.Parameter( typeof( int ), "x" );

        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( -1 ),
            Expression.SwitchCase( Expression.Constant( 10 ), Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( 20 ), Expression.Constant( 2 ) ),
            Expression.SwitchCase( Expression.Constant( 30 ), Expression.Constant( 3 ) ) );

        var lambda = Expression.Lambda<Func<int, int>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn( 1 ) );
        Assert.AreEqual( 20, fn( 2 ) );
        Assert.AreEqual( 30, fn( 3 ) );
        Assert.AreEqual( -1, fn( 99 ) );
    }

    // ================================================================
    // Switch with multiple test values per case
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_MultipleTestValues_MatchesAny( CompilerType compilerType )
    {
        // switch (x) { case 1: case 2: return 100; case 3: case 4: return 200; default: return 0; }
        var x = Expression.Parameter( typeof( int ), "x" );

        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( 0 ),
            Expression.SwitchCase(
                Expression.Constant( 100 ),
                Expression.Constant( 1 ), Expression.Constant( 2 ) ),
            Expression.SwitchCase(
                Expression.Constant( 200 ),
                Expression.Constant( 3 ), Expression.Constant( 4 ) ) );

        var lambda = Expression.Lambda<Func<int, int>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 100, fn( 1 ) );
        Assert.AreEqual( 100, fn( 2 ) );
        Assert.AreEqual( 200, fn( 3 ) );
        Assert.AreEqual( 200, fn( 4 ) );
        Assert.AreEqual( 0, fn( 5 ) );
    }

    // ================================================================
    // Switch with string cases
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_StringCases_WithComparison( CompilerType compilerType )
    {
        // switch (s) { case "hello": return 1; case "world": return 2; default: return 0; }
        var s = Expression.Parameter( typeof( string ), "s" );

        var equalsMethod = typeof( string ).GetMethod(
            "Equals",
            [typeof( string ), typeof( string )] )!;

        var switchExpr = Expression.Switch(
            s,
            Expression.Constant( 0 ),
            equalsMethod,
            Expression.SwitchCase( Expression.Constant( 1 ), Expression.Constant( "hello" ) ),
            Expression.SwitchCase( Expression.Constant( 2 ), Expression.Constant( "world" ) ) );

        var lambda = Expression.Lambda<Func<string, int>>( switchExpr, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( "hello" ) );
        Assert.AreEqual( 2, fn( "world" ) );
        Assert.AreEqual( 0, fn( "other" ) );
    }

    // ================================================================
    // Void switch (no return value)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_VoidBody_SetsVariable( CompilerType compilerType )
    {
        // int result = 0;
        // switch (x) { case 1: result = 10; break; case 2: result = 20; break; }
        // return result;
        var x = Expression.Parameter( typeof( int ), "x" );
        var result = Expression.Variable( typeof( int ), "result" );

        var switchExpr = Expression.Switch(
            typeof( void ),
            x,
            null, // no default body
            null, // no comparison
            Expression.SwitchCase(
                Expression.Assign( result, Expression.Constant( 10 ) ),
                Expression.Constant( 1 ) ),
            Expression.SwitchCase(
                Expression.Assign( result, Expression.Constant( 20 ) ),
                Expression.Constant( 2 ) ) );

        var body = Expression.Block(
            new[] { result },
            Expression.Assign( result, Expression.Constant( 0 ) ),
            switchExpr,
            result );

        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn( 1 ) );
        Assert.AreEqual( 20, fn( 2 ) );
        Assert.AreEqual( 0, fn( 99 ) );
    }
}
