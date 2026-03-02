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

    // ================================================================
    // String switch without explicit comparison
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_StringCases_NoExplicitComparison( CompilerType compilerType )
    {
        // Expression.Switch auto-resolves string.op_Equality when no comparison is provided
        var s = Expression.Parameter( typeof(string), "s" );

        var switchExpr = Expression.Switch(
            s,
            Expression.Constant( 0 ),
            Expression.SwitchCase( Expression.Constant( 1 ), Expression.Constant( "hello" ) ),
            Expression.SwitchCase( Expression.Constant( 2 ), Expression.Constant( "world" ) ) );

        var lambda = Expression.Lambda<Func<string, int>>( switchExpr, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( "hello" ) );
        Assert.AreEqual( 2, fn( "world" ) );
        Assert.AreEqual( 0, fn( "other" ) );
    }

    // ================================================================
    // Switch with long cases
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_LongCases_ReturnsMatchingCase( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(long), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( "other" ),
            Expression.SwitchCase( Expression.Constant( "one" ), Expression.Constant( 1L ) ),
            Expression.SwitchCase( Expression.Constant( "two" ), Expression.Constant( 2L ) ),
            Expression.SwitchCase( Expression.Constant( "billion" ), Expression.Constant( 1_000_000_000L ) ) );
        var lambda = Expression.Lambda<Func<long, string>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "one", fn( 1L ) );
        Assert.AreEqual( "two", fn( 2L ) );
        Assert.AreEqual( "billion", fn( 1_000_000_000L ) );
        Assert.AreEqual( "other", fn( 99L ) );
    }

    // ================================================================
    // Switch with char cases
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_CharCases_ReturnsMatchingCase( CompilerType compilerType )
    {
        var c = Expression.Parameter( typeof(char), "c" );
        var switchExpr = Expression.Switch(
            c,
            Expression.Constant( "other" ),
            Expression.SwitchCase( Expression.Constant( "vowel" ), Expression.Constant( 'a' ), Expression.Constant( 'e' ), Expression.Constant( 'i' ), Expression.Constant( 'o' ), Expression.Constant( 'u' ) ),
            Expression.SwitchCase( Expression.Constant( "space" ), Expression.Constant( ' ' ) ) );
        var lambda = Expression.Lambda<Func<char, string>>( switchExpr, c );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "vowel", fn( 'a' ) );
        Assert.AreEqual( "vowel", fn( 'e' ) );
        Assert.AreEqual( "space", fn( ' ' ) );
        Assert.AreEqual( "other", fn( 'b' ) );
    }

    // ================================================================
    // Switch with enum cases
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_EnumCases_ReturnsMatchingCase( CompilerType compilerType )
    {
        var d = Expression.Parameter( typeof(DayOfWeek), "d" );
        var switchExpr = Expression.Switch(
            d,
            Expression.Constant( "weekday" ),
            Expression.SwitchCase(
                Expression.Constant( "weekend" ),
                Expression.Constant( DayOfWeek.Saturday ),
                Expression.Constant( DayOfWeek.Sunday ) ) );
        var lambda = Expression.Lambda<Func<DayOfWeek, string>>( switchExpr, d );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "weekend", fn( DayOfWeek.Saturday ) );
        Assert.AreEqual( "weekend", fn( DayOfWeek.Sunday ) );
        Assert.AreEqual( "weekday", fn( DayOfWeek.Monday ) );
        Assert.AreEqual( "weekday", fn( DayOfWeek.Friday ) );
    }

    // ================================================================
    // Switch with null string handling — null falls to default
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_NullString_FallsToDefault( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var switchExpr = Expression.Switch(
            s,
            Expression.Constant( "default" ),
            Expression.SwitchCase( Expression.Constant( "hello-match" ), Expression.Constant( "hello" ) ) );
        var lambda = Expression.Lambda<Func<string, string>>( switchExpr, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello-match", fn( "hello" ) );
        Assert.AreEqual( "default", fn( null! ) );
        Assert.AreEqual( "default", fn( "other" ) );
    }

    // ================================================================
    // Switch with three test values on one case
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_MultiValueCase_ThreeValuesOnOneCase( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( 0 ),
            Expression.SwitchCase(
                Expression.Constant( 1 ),
                Expression.Constant( 10 ),
                Expression.Constant( 20 ),
                Expression.Constant( 30 ) ) );
        var lambda = Expression.Lambda<Func<int, int>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( 10 ) );
        Assert.AreEqual( 1, fn( 20 ) );
        Assert.AreEqual( 1, fn( 30 ) );
        Assert.AreEqual( 0, fn( 15 ) );
    }

    // ================================================================
    // Switch with no default (void result, side-effect only)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_NoDefault_VoidResult_SideEffect( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var result = Expression.Variable( typeof(string), "result" );

        var switchExpr = Expression.Switch(
            typeof( void ),
            x,
            null,
            null,
            Expression.SwitchCase(
                Expression.Assign( result, Expression.Constant( "A" ) ),
                Expression.Constant( 1 ) ),
            Expression.SwitchCase(
                Expression.Assign( result, Expression.Constant( "B" ) ),
                Expression.Constant( 2 ) ) );

        var body = Expression.Block(
            new[] { result },
            Expression.Assign( result, Expression.Constant( "none" ) ),
            switchExpr,
            result );

        var lambda = Expression.Lambda<Func<int, string>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "A", fn( 1 ) );
        Assert.AreEqual( "B", fn( 2 ) );
        Assert.AreEqual( "none", fn( 99 ) );  // no default, result stays "none"
    }

    // ================================================================
    // Switch with sparse high-value cases (tests conditional-chain fallback)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_SparseHighValues( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( -1 ),
            Expression.SwitchCase( Expression.Constant( 100 ), Expression.Constant( 1000 ) ),
            Expression.SwitchCase( Expression.Constant( 200 ), Expression.Constant( 9999 ) ),
            Expression.SwitchCase( Expression.Constant( 300 ), Expression.Constant( int.MaxValue ) ) );
        var lambda = Expression.Lambda<Func<int, int>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 100, fn( 1000 ) );
        Assert.AreEqual( 200, fn( 9999 ) );
        Assert.AreEqual( 300, fn( int.MaxValue ) );
        Assert.AreEqual( -1, fn( 0 ) );
    }

    // ================================================================
    // Switch nested inside another switch
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_NestedSwitch_InCaseBody( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );

        var innerSwitch = Expression.Switch(
            b,
            Expression.Constant( "b-other" ),
            Expression.SwitchCase( Expression.Constant( "b1" ), Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( "b2" ), Expression.Constant( 2 ) ) );

        var outerSwitch = Expression.Switch(
            a,
            Expression.Constant( "a-other" ),
            Expression.SwitchCase( innerSwitch, Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( "a2" ), Expression.Constant( 2 ) ) );

        var lambda = Expression.Lambda<Func<int, int, string>>( outerSwitch, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "b1", fn( 1, 1 ) );
        Assert.AreEqual( "b2", fn( 1, 2 ) );
        Assert.AreEqual( "b-other", fn( 1, 9 ) );
        Assert.AreEqual( "a2", fn( 2, 1 ) );
        Assert.AreEqual( "a-other", fn( 9, 1 ) );
    }
}
