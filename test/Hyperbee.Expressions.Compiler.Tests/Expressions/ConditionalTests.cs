using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ConditionalTests
{
    // --- Simple ternary: a > b ? a : b ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_SimpleTernary_Max( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>(
            Expression.Condition( Expression.GreaterThan( a, b ), a, b ),
            a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 5, 3 ) );
        Assert.AreEqual( 5, fn( 3, 5 ) );
        Assert.AreEqual( 4, fn( 4, 4 ) );
        Assert.AreEqual( 0, fn( 0, -1 ) );
    }

    // --- Nested conditional ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_Nested( CompilerType compilerType )
    {
        // x < 0 ? -1 : (x == 0 ? 0 : 1)  — sign function
        var x = Expression.Parameter( typeof(int), "x" );
        var inner = Expression.Condition(
            Expression.Equal( x, Expression.Constant( 0 ) ),
            Expression.Constant( 0 ),
            Expression.Constant( 1 ) );
        var outer = Expression.Condition(
            Expression.LessThan( x, Expression.Constant( 0 ) ),
            Expression.Constant( -1 ),
            inner );
        var lambda = Expression.Lambda<Func<int, int>>( outer, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( -1, fn( -5 ) );
        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( 1, fn( 7 ) );
    }

    // --- Conditional with method calls in branches ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_WithMethodCallsInBranches( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var toUpper = typeof(string).GetMethod( nameof(string.ToUpper), Type.EmptyTypes )!;
        var toLower = typeof(string).GetMethod( nameof(string.ToLower), Type.EmptyTypes )!;
        var length  = typeof(string).GetProperty( nameof(string.Length) )!;

        // s.Length > 3 ? s.ToUpper() : s.ToLower()
        var lambda = Expression.Lambda<Func<string, string>>(
            Expression.Condition(
                Expression.GreaterThan(
                    Expression.Property( s, length ),
                    Expression.Constant( 3 ) ),
                Expression.Call( s, toUpper ),
                Expression.Call( s, toLower ) ),
            s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "HELLO", fn( "hello" ) );
        Assert.AreEqual( "hi", fn( "HI" ) );
        Assert.AreEqual( "FOUR", fn( "four" ) );
    }

    // --- IfThen (void, no else) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThen_Void( CompilerType compilerType )
    {
        var result = Expression.Variable( typeof(int), "result" );
        var condition = Expression.GreaterThan( result, Expression.Constant( 0 ) );
        var assign = Expression.Assign( result, Expression.Constant( 99 ) );

        // Block: result = 1; if (result > 0) result = 99; return result;
        var body = Expression.Block(
            new[] { result },
            Expression.Assign( result, Expression.Constant( 1 ) ),
            Expression.IfThen( condition, assign ),
            result );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }

    // --- Conditional with boxing/unboxing in branches ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_BoxUnbox_InBranches( CompilerType compilerType )
    {
        // a > 0 ? (int)(object)a : -1   — box then unbox in true branch
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.Condition(
                Expression.GreaterThan( a, Expression.Constant( 0 ) ),
                Expression.Convert(
                    Expression.Convert( a, typeof(object) ),  // box
                    typeof(int) ),                             // unbox
                Expression.Constant( -1 ) ),
            a );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( -1, fn( -1 ) );
        Assert.AreEqual( -1, fn( 0 ) );
    }

    // --- Nested conditional with branches of different types (boxing to object) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_Nested_DifferentTypeBranches_BoxedToObject( CompilerType compilerType )
    {
        // x > 0 ? (x > 10 ? (object)x : (object)"medium") : (object)"negative"
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int, object>>(
            Expression.Condition(
                Expression.GreaterThan( x, Expression.Constant( 0 ) ),
                Expression.Condition(
                    Expression.GreaterThan( x, Expression.Constant( 10 ) ),
                    Expression.Convert( x, typeof(object) ),
                    Expression.Convert( Expression.Constant( "medium" ), typeof(object) ) ),
                Expression.Convert( Expression.Constant( "negative" ), typeof(object) ) ),
            x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( "medium", fn( 5 ) );
        Assert.AreEqual( "negative", fn( -1 ) );
    }

    // --- IfThenElse with typed result ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThenElse_TypedResult( CompilerType compilerType )
    {
        var flag = Expression.Parameter( typeof(bool), "flag" );

        // flag ? "yes" : "no"
        var lambda = Expression.Lambda<Func<bool, string>>(
            Expression.Condition(
                flag,
                Expression.Constant( "yes" ),
                Expression.Constant( "no" ) ),
            flag );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "yes", fn( true ) );
        Assert.AreEqual( "no", fn( false ) );
    }

    // --- Conditional chained three-way ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_ThreeWay_GradeClassification( CompilerType compilerType )
    {
        // score >= 90 ? "A" : (score >= 70 ? "B" : "C")
        var score = Expression.Parameter( typeof( int ), "score" );
        var lambda = Expression.Lambda<Func<int, string>>(
            Expression.Condition(
                Expression.GreaterThanOrEqual( score, Expression.Constant( 90 ) ),
                Expression.Constant( "A" ),
                Expression.Condition(
                    Expression.GreaterThanOrEqual( score, Expression.Constant( 70 ) ),
                    Expression.Constant( "B" ),
                    Expression.Constant( "C" ) ) ),
            score );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "A", fn( 95 ) );
        Assert.AreEqual( "A", fn( 90 ) );
        Assert.AreEqual( "B", fn( 80 ) );
        Assert.AreEqual( "B", fn( 70 ) );
        Assert.AreEqual( "C", fn( 69 ) );
        Assert.AreEqual( "C", fn( 0 ) );
    }

    // --- IfThenElse with block in both branches ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThenElse_BlockInBothBranches( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof( int ), "x" );
        var result = Expression.Variable( typeof( int ), "result" );

        var trueBlock = Expression.Block(
            Expression.Assign( result, Expression.Multiply( x, Expression.Constant( 2 ) ) ),
            result );
        var falseBlock = Expression.Block(
            Expression.Assign( result, Expression.Multiply( x, Expression.Constant( 3 ) ) ),
            result );

        var body = Expression.Block(
            new[] { result },
            Expression.Condition(
                Expression.GreaterThan( x, Expression.Constant( 0 ) ),
                trueBlock,
                falseBlock ) );

        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn( 5 ) );    // 5 * 2 = 10
        Assert.AreEqual( -9, fn( -3 ) );   // -3 * 3 = -9
    }

    // --- IfThen — condition is false, body never executes ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThen_FalseCondition_BodyNotExecuted( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof( int ), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0 ) ),
            Expression.IfThen(
                Expression.Constant( false ),
                Expression.Assign( x, Expression.Constant( 999 ) ) ),
            x );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn() );
    }

    // --- Conditional returning nullable ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_ReturnsNullable_OrNull( CompilerType compilerType )
    {
        // (int n) => n > 0 ? (int?)n : (int?)null
        var n = Expression.Parameter( typeof( int ), "n" );
        var lambda = Expression.Lambda<Func<int, int?>>(
            Expression.Condition(
                Expression.GreaterThan( n, Expression.Constant( 0 ) ),
                Expression.Convert( n, typeof( int? ) ),
                Expression.Constant( null, typeof( int? ) ) ),
            n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 5 ) );
        Assert.IsNull( fn( 0 ) );
        Assert.IsNull( fn( -1 ) );
    }

    // --- Conditional — equality check on strings ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_StringEquality_BranchOnValue( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof( string ), "s" );
        var equalsMethod = typeof( string ).GetMethod( "op_Equality", [typeof( string ), typeof( string )] )!;

        var lambda = Expression.Lambda<Func<string, int>>(
            Expression.Condition(
                Expression.Call( equalsMethod, s, Expression.Constant( "yes" ) ),
                Expression.Constant( 1 ),
                Expression.Constant( 0 ) ),
            s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( "yes" ) );
        Assert.AreEqual( 0, fn( "no" ) );
        Assert.AreEqual( 0, fn( "" ) );
    }

    // --- IfThenElse — short-circuit side effect not triggered ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThenElse_OnlyOneBranchExecutes( CompilerType compilerType )
    {
        // Verifies that only the taken branch runs
        var flag = Expression.Parameter( typeof( bool ), "flag" );
        var trueCount = Expression.Variable( typeof( int ), "trueCount" );
        var falseCount = Expression.Variable( typeof( int ), "falseCount" );

        var body = Expression.Block(
            new[] { trueCount, falseCount },
            Expression.Assign( trueCount, Expression.Constant( 0 ) ),
            Expression.Assign( falseCount, Expression.Constant( 0 ) ),
            Expression.Condition(
                flag,
                Expression.Assign( trueCount, Expression.Constant( 1 ) ),
                Expression.Assign( falseCount, Expression.Constant( 1 ) ) ),
            Expression.Add( trueCount, falseCount ) );

        var lambda = Expression.Lambda<Func<bool, int>>( body, flag );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( true ) );   // trueCount=1, falseCount=0
        Assert.AreEqual( 1, fn( false ) );  // trueCount=0, falseCount=1
    }

    // --- Conditional — comparing doubles ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_DoubleComparison_ClassifySign( CompilerType compilerType )
    {
        var d = Expression.Parameter( typeof( double ), "d" );
        var lambda = Expression.Lambda<Func<double, int>>(
            Expression.Condition(
                Expression.GreaterThan( d, Expression.Constant( 0.0 ) ),
                Expression.Constant( 1 ),
                Expression.Condition(
                    Expression.LessThan( d, Expression.Constant( 0.0 ) ),
                    Expression.Constant( -1 ),
                    Expression.Constant( 0 ) ) ),
            d );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn( 3.14 ) );
        Assert.AreEqual( -1, fn( -0.001 ) );
        Assert.AreEqual( 0, fn( 0.0 ) );
    }

    // --- Conditional — result assigned and used later ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_ResultAssignedToVariable( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof( int ), "n" );
        var temp = Expression.Variable( typeof( int ), "temp" );

        var body = Expression.Block(
            new[] { temp },
            Expression.Assign(
                temp,
                Expression.Condition(
                    Expression.GreaterThan( n, Expression.Constant( 0 ) ),
                    Expression.Constant( 100 ),
                    Expression.Constant( -100 ) ) ),
            Expression.Add( temp, n ) );

        var lambda = Expression.Lambda<Func<int, int>>( body, n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 105, fn( 5 ) );   // 100 + 5
        Assert.AreEqual( -103, fn( -3 ) ); // -100 + (-3)
    }

    // --- IfThen — condition from method result ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThen_ConditionFromMethodResult( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof( string ), "s" );
        var result = Expression.Variable( typeof( int ), "result" );
        var isNullOrEmpty = typeof( string ).GetMethod( nameof( string.IsNullOrEmpty ) )!;

        var body = Expression.Block(
            new[] { result },
            Expression.Assign( result, Expression.Constant( 1 ) ),
            Expression.IfThen(
                Expression.Call( isNullOrEmpty, s ),
                Expression.Assign( result, Expression.Constant( 0 ) ) ),
            result );

        var lambda = Expression.Lambda<Func<string, int>>( body, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn( "" ) );
        Assert.AreEqual( 0, fn( null! ) );
        Assert.AreEqual( 1, fn( "hello" ) );
    }

    // --- Conditional — bool parameter result ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_BoolResult_NegateCondition( CompilerType compilerType )
    {
        // (bool b) => b ? false : true   (same as !b)
        var b = Expression.Parameter( typeof( bool ), "b" );
        var lambda = Expression.Lambda<Func<bool, bool>>(
            Expression.Condition( b, Expression.Constant( false ), Expression.Constant( true ) ),
            b );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn( true ) );
        Assert.IsTrue( fn( false ) );
    }

    // --- Conditional inside lambda body chain ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_InsideArithmeticExpression( CompilerType compilerType )
    {
        // (int x) => x * (x > 0 ? 1 : -1)  — absolute value
        var x = Expression.Parameter( typeof( int ), "x" );
        var sign = Expression.Condition(
            Expression.GreaterThan( x, Expression.Constant( 0 ) ),
            Expression.Constant( 1 ),
            Expression.Constant( -1 ) );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.Multiply( x, sign ), x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 5 ) );
        Assert.AreEqual( 3, fn( -3 ) );   // -3 * -1 = 3
        Assert.AreEqual( 0, fn( 0 ) );
    }

    // --- Conditional — long branches ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_LongType_Clamp( CompilerType compilerType )
    {
        // (long v, long lo, long hi) => v < lo ? lo : (v > hi ? hi : v)
        var v = Expression.Parameter( typeof( long ), "v" );
        var lo = Expression.Parameter( typeof( long ), "lo" );
        var hi = Expression.Parameter( typeof( long ), "hi" );
        var lambda = Expression.Lambda<Func<long, long, long, long>>(
            Expression.Condition(
                Expression.LessThan( v, lo ),
                lo,
                Expression.Condition(
                    Expression.GreaterThan( v, hi ),
                    hi,
                    v ) ),
            v, lo, hi );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5L, fn( 3L, 5L, 10L ) );   // clamp up to 5
        Assert.AreEqual( 10L, fn( 15L, 5L, 10L ) );  // clamp down to 10
        Assert.AreEqual( 7L, fn( 7L, 5L, 10L ) );    // in range
    }

    // --- Conditional — four nested levels ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Conditional_FourLevels_Nested( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof( int ), "x" );
        // x >= 100 ? "3-digit" : x >= 10 ? "2-digit" : x >= 1 ? "1-digit" : "zero-or-neg"
        var lambda = Expression.Lambda<Func<int, string>>(
            Expression.Condition(
                Expression.GreaterThanOrEqual( x, Expression.Constant( 100 ) ),
                Expression.Constant( "3-digit" ),
                Expression.Condition(
                    Expression.GreaterThanOrEqual( x, Expression.Constant( 10 ) ),
                    Expression.Constant( "2-digit" ),
                    Expression.Condition(
                        Expression.GreaterThanOrEqual( x, Expression.Constant( 1 ) ),
                        Expression.Constant( "1-digit" ),
                        Expression.Constant( "zero-or-neg" ) ) ) ),
            x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "3-digit", fn( 999 ) );
        Assert.AreEqual( "2-digit", fn( 42 ) );
        Assert.AreEqual( "1-digit", fn( 7 ) );
        Assert.AreEqual( "zero-or-neg", fn( 0 ) );
        Assert.AreEqual( "zero-or-neg", fn( -5 ) );
    }
}
