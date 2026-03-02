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

    // ================================================================
    // Switch on byte type
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_ByteCases_ReturnsMatchingCase( CompilerType compilerType )
    {
        var b = Expression.Parameter( typeof(byte), "b" );
        var switchExpr = Expression.Switch(
            b,
            Expression.Constant( "other" ),
            Expression.SwitchCase( Expression.Constant( "zero-or-one" ), Expression.Constant( (byte) 0 ), Expression.Constant( (byte) 1 ) ),
            Expression.SwitchCase( Expression.Constant( "mid" ), Expression.Constant( (byte) 128 ) ),
            Expression.SwitchCase( Expression.Constant( "max" ), Expression.Constant( byte.MaxValue ) ) );
        var lambda = Expression.Lambda<Func<byte, string>>( switchExpr, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "zero-or-one", fn( 0 ) );
        Assert.AreEqual( "zero-or-one", fn( 1 ) );
        Assert.AreEqual( "mid", fn( 128 ) );
        Assert.AreEqual( "max", fn( 255 ) );
        Assert.AreEqual( "other", fn( 50 ) );
    }

    // ================================================================
    // Switch on uint type
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_UIntCases_ReturnsMatchingCase( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(uint), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( -1 ),
            Expression.SwitchCase( Expression.Constant( 10 ), Expression.Constant( 0u ) ),
            Expression.SwitchCase( Expression.Constant( 20 ), Expression.Constant( uint.MaxValue ) ) );
        var lambda = Expression.Lambda<Func<uint, int>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn( 0u ) );
        Assert.AreEqual( 20, fn( uint.MaxValue ) );
        Assert.AreEqual( -1, fn( 5u ) );
    }

    // ================================================================
    // Switch with negative int case values
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_NegativeIntCases( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( "none" ),
            Expression.SwitchCase( Expression.Constant( "neg-one" ), Expression.Constant( -1 ) ),
            Expression.SwitchCase( Expression.Constant( "neg-two" ), Expression.Constant( -2 ) ),
            Expression.SwitchCase( Expression.Constant( "minval" ), Expression.Constant( int.MinValue ) ) );
        var lambda = Expression.Lambda<Func<int, string>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "neg-one", fn( -1 ) );
        Assert.AreEqual( "neg-two", fn( -2 ) );
        Assert.AreEqual( "minval", fn( int.MinValue ) );
        Assert.AreEqual( "none", fn( 0 ) );
    }

    // ================================================================
    // Switch with negative long case values
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_NegativeLongCases( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(long), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( "none" ),
            Expression.SwitchCase( Expression.Constant( "neg" ), Expression.Constant( -100L ) ),
            Expression.SwitchCase( Expression.Constant( "minval" ), Expression.Constant( long.MinValue ) ) );
        var lambda = Expression.Lambda<Func<long, string>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "neg", fn( -100L ) );
        Assert.AreEqual( "minval", fn( long.MinValue ) );
        Assert.AreEqual( "none", fn( 0L ) );
    }

    // ================================================================
    // Switch with Block body in case
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_CaseBodyIsBlock_WithLocalVariable( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var tmp = Expression.Variable( typeof(int), "tmp" );

        // case 1: { tmp = x * 10; return tmp + 1; }
        var caseBody = Expression.Block(
            new[] { tmp },
            Expression.Assign( tmp, Expression.Multiply( x, Expression.Constant( 10 ) ) ),
            Expression.Add( tmp, Expression.Constant( 1 ) ) );

        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( -1 ),
            Expression.SwitchCase( caseBody, Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( 0 ), Expression.Constant( 0 ) ) );

        var lambda = Expression.Lambda<Func<int, int>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 11, fn( 1 ) );   // 1*10 + 1
        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( -1, fn( 5 ) );
    }

    // ================================================================
    // Switch result used in arithmetic
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_ResultUsedInArithmetic( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( 0 ),
            Expression.SwitchCase( Expression.Constant( 10 ), Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( 20 ), Expression.Constant( 2 ) ) );

        // result = switch * 2
        var body = Expression.Multiply( switchExpr, Expression.Constant( 2 ) );
        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 20, fn( 1 ) );
        Assert.AreEqual( 40, fn( 2 ) );
        Assert.AreEqual( 0, fn( 99 ) );
    }

    // ================================================================
    // Switch with five dense consecutive int cases
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_FiveDenseCases( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( "none" ),
            Expression.SwitchCase( Expression.Constant( "one" ),   Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( "two" ),   Expression.Constant( 2 ) ),
            Expression.SwitchCase( Expression.Constant( "three" ), Expression.Constant( 3 ) ),
            Expression.SwitchCase( Expression.Constant( "four" ),  Expression.Constant( 4 ) ),
            Expression.SwitchCase( Expression.Constant( "five" ),  Expression.Constant( 5 ) ) );
        var lambda = Expression.Lambda<Func<int, string>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "one",   fn( 1 ) );
        Assert.AreEqual( "three", fn( 3 ) );
        Assert.AreEqual( "five",  fn( 5 ) );
        Assert.AreEqual( "none",  fn( 0 ) );
        Assert.AreEqual( "none",  fn( 6 ) );
    }

    // ================================================================
    // Switch with eight dense consecutive int cases (jump table candidate)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_EightDenseCases_JumpTable( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( -1 ),
            Expression.SwitchCase( Expression.Constant( 10 ), Expression.Constant( 0 ) ),
            Expression.SwitchCase( Expression.Constant( 11 ), Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( 12 ), Expression.Constant( 2 ) ),
            Expression.SwitchCase( Expression.Constant( 13 ), Expression.Constant( 3 ) ),
            Expression.SwitchCase( Expression.Constant( 14 ), Expression.Constant( 4 ) ),
            Expression.SwitchCase( Expression.Constant( 15 ), Expression.Constant( 5 ) ),
            Expression.SwitchCase( Expression.Constant( 16 ), Expression.Constant( 6 ) ),
            Expression.SwitchCase( Expression.Constant( 17 ), Expression.Constant( 7 ) ) );
        var lambda = Expression.Lambda<Func<int, int>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        for ( var i = 0; i < 8; i++ )
            Assert.AreEqual( 10 + i, fn( i ) );
        Assert.AreEqual( -1, fn( 8 ) );
        Assert.AreEqual( -1, fn( -1 ) );
    }

    // ================================================================
    // Switch on bool type
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_BoolCases( CompilerType compilerType )
    {
        var flag = Expression.Parameter( typeof(bool), "flag" );
        var switchExpr = Expression.Switch(
            flag,
            Expression.Constant( "unknown" ),
            Expression.SwitchCase( Expression.Constant( "yes" ), Expression.Constant( true ) ),
            Expression.SwitchCase( Expression.Constant( "no" ),  Expression.Constant( false ) ) );
        var lambda = Expression.Lambda<Func<bool, string>>( switchExpr, flag );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "yes", fn( true ) );
        Assert.AreEqual( "no", fn( false ) );
    }

    // ================================================================
    // Switch on char — digit characters
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_CharDigitCases( CompilerType compilerType )
    {
        var c = Expression.Parameter( typeof(char), "c" );
        var switchExpr = Expression.Switch(
            c,
            Expression.Constant( -1 ),
            Expression.SwitchCase( Expression.Constant( 0 ), Expression.Constant( '0' ) ),
            Expression.SwitchCase( Expression.Constant( 1 ), Expression.Constant( '1' ) ),
            Expression.SwitchCase( Expression.Constant( 2 ), Expression.Constant( '2' ) ),
            Expression.SwitchCase( Expression.Constant( 3 ), Expression.Constant( '3' ) ),
            Expression.SwitchCase( Expression.Constant( 4 ), Expression.Constant( '4' ) ) );
        var lambda = Expression.Lambda<Func<char, int>>( switchExpr, c );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( '0' ) );
        Assert.AreEqual( 2, fn( '2' ) );
        Assert.AreEqual( 4, fn( '4' ) );
        Assert.AreEqual( -1, fn( '5' ) );
        Assert.AreEqual( -1, fn( 'a' ) );
    }

    // ================================================================
    // Switch with four string cases
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_FourStringCases( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var switchExpr = Expression.Switch(
            s,
            Expression.Constant( 0 ),
            Expression.SwitchCase( Expression.Constant( 1 ), Expression.Constant( "north" ) ),
            Expression.SwitchCase( Expression.Constant( 2 ), Expression.Constant( "south" ) ),
            Expression.SwitchCase( Expression.Constant( 3 ), Expression.Constant( "east" ) ),
            Expression.SwitchCase( Expression.Constant( 4 ), Expression.Constant( "west" ) ) );
        var lambda = Expression.Lambda<Func<string, int>>( switchExpr, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( "north" ) );
        Assert.AreEqual( 2, fn( "south" ) );
        Assert.AreEqual( 3, fn( "east" ) );
        Assert.AreEqual( 4, fn( "west" ) );
        Assert.AreEqual( 0, fn( "up" ) );
    }

    // ================================================================
    // Switch with six string cases
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_SixStringCases( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var switchExpr = Expression.Switch(
            s,
            Expression.Constant( "unknown" ),
            Expression.SwitchCase( Expression.Constant( "january" ),  Expression.Constant( "Jan" ) ),
            Expression.SwitchCase( Expression.Constant( "february" ), Expression.Constant( "Feb" ) ),
            Expression.SwitchCase( Expression.Constant( "march" ),    Expression.Constant( "Mar" ) ),
            Expression.SwitchCase( Expression.Constant( "april" ),    Expression.Constant( "Apr" ) ),
            Expression.SwitchCase( Expression.Constant( "may" ),      Expression.Constant( "May" ) ),
            Expression.SwitchCase( Expression.Constant( "june" ),     Expression.Constant( "Jun" ) ) );
        var lambda = Expression.Lambda<Func<string, string>>( switchExpr, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "january",  fn( "Jan" ) );
        Assert.AreEqual( "march",    fn( "Mar" ) );
        Assert.AreEqual( "june",     fn( "Jun" ) );
        Assert.AreEqual( "unknown",  fn( "Jul" ) );
    }

    // ================================================================
    // Switch with Block as default body
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_DefaultBodyIsBlock( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var tmp = Expression.Variable( typeof(int), "tmp" );

        var defaultBody = Expression.Block(
            new[] { tmp },
            Expression.Assign( tmp, Expression.Negate( x ) ),
            tmp );

        var switchExpr = Expression.Switch(
            x,
            defaultBody,
            Expression.SwitchCase( Expression.Constant( 100 ), Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( 200 ), Expression.Constant( 2 ) ) );

        var lambda = Expression.Lambda<Func<int, int>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 100, fn( 1 ) );
        Assert.AreEqual( 200, fn( 2 ) );
        Assert.AreEqual( -5, fn( 5 ) );   // default: -x
        Assert.AreEqual( -99, fn( 99 ) );
    }

    // ================================================================
    // Switch with all DayOfWeek enum values covered
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_AllDaysOfWeek( CompilerType compilerType )
    {
        var d = Expression.Parameter( typeof(DayOfWeek), "d" );
        var switchExpr = Expression.Switch(
            d,
            Expression.Constant( 0 ),
            Expression.SwitchCase( Expression.Constant( 7 ), Expression.Constant( DayOfWeek.Sunday ) ),
            Expression.SwitchCase( Expression.Constant( 1 ), Expression.Constant( DayOfWeek.Monday ) ),
            Expression.SwitchCase( Expression.Constant( 2 ), Expression.Constant( DayOfWeek.Tuesday ) ),
            Expression.SwitchCase( Expression.Constant( 3 ), Expression.Constant( DayOfWeek.Wednesday ) ),
            Expression.SwitchCase( Expression.Constant( 4 ), Expression.Constant( DayOfWeek.Thursday ) ),
            Expression.SwitchCase( Expression.Constant( 5 ), Expression.Constant( DayOfWeek.Friday ) ),
            Expression.SwitchCase( Expression.Constant( 6 ), Expression.Constant( DayOfWeek.Saturday ) ) );
        var lambda = Expression.Lambda<Func<DayOfWeek, int>>( switchExpr, d );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 7, fn( DayOfWeek.Sunday ) );
        Assert.AreEqual( 1, fn( DayOfWeek.Monday ) );
        Assert.AreEqual( 6, fn( DayOfWeek.Saturday ) );
    }

    // ================================================================
    // Switch with empty string case
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_StringCases_EmptyStringMatchesCase( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var switchExpr = Expression.Switch(
            s,
            Expression.Constant( "other" ),
            Expression.SwitchCase( Expression.Constant( "empty" ), Expression.Constant( string.Empty ) ),
            Expression.SwitchCase( Expression.Constant( "hello" ), Expression.Constant( "hello" ) ) );
        var lambda = Expression.Lambda<Func<string, string>>( switchExpr, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "empty", fn( "" ) );
        Assert.AreEqual( "hello", fn( "hello" ) );
        Assert.AreEqual( "other", fn( "world" ) );
    }

    // ================================================================
    // Switch result assigned to a variable
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_ResultAssignedToVariable( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var result = Expression.Variable( typeof(int), "result" );

        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( -1 ),
            Expression.SwitchCase( Expression.Constant( 10 ), Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( 20 ), Expression.Constant( 2 ) ),
            Expression.SwitchCase( Expression.Constant( 30 ), Expression.Constant( 3 ) ) );

        var body = Expression.Block(
            new[] { result },
            Expression.Assign( result, switchExpr ),
            Expression.Add( result, Expression.Constant( 1 ) ) );

        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 11, fn( 1 ) );
        Assert.AreEqual( 21, fn( 2 ) );
        Assert.AreEqual( 31, fn( 3 ) );
        Assert.AreEqual( 0, fn( 99 ) );   // -1 + 1
    }

    // ================================================================
    // Switch inside block with outer variable
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_InsideBlock_ModifiesOuterVariable( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var accum = Expression.Variable( typeof(int), "accum" );

        var switchExpr = Expression.Switch(
            typeof(void),
            x,
            Expression.Assign( accum, Expression.Constant( -1 ) ),
            null,
            Expression.SwitchCase( Expression.Assign( accum, Expression.Constant( 10 ) ), Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Assign( accum, Expression.Constant( 20 ) ), Expression.Constant( 2 ) ) );

        var body = Expression.Block(
            new[] { accum },
            Expression.Assign( accum, Expression.Constant( 0 ) ),
            switchExpr,
            accum );

        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn( 1 ) );
        Assert.AreEqual( 20, fn( 2 ) );
        Assert.AreEqual( -1, fn( 99 ) );
    }
}
