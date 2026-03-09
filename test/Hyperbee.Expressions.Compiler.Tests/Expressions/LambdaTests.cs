using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class LambdaTests
{
    // ================================================================
    // Zero-parameter lambda returning constant
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ZeroParameters_ReturnsConstant( CompilerType compilerType )
    {
        var lambda = Expression.Lambda<Func<int>>( Expression.Constant( 42 ) );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // ================================================================
    // Zero-parameter lambda with block body
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ZeroParameters_Block( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 10 ) ),
            Expression.Multiply( x, Expression.Constant( 3 ) ) );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 30, fn() );
    }

    // ================================================================
    // Lambda with two int parameters
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_TwoIntParameters_Add( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Add( a, b ), a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 2, 3 ) );
        Assert.AreEqual( 0, fn( -1, 1 ) );
    }

    // ================================================================
    // Lambda with three parameters
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ThreeParameters_SumAll( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var c = Expression.Parameter( typeof(int), "c" );
        var body = Expression.Add( Expression.Add( a, b ), c );
        var lambda = Expression.Lambda<Func<int, int, int, int>>( body, a, b, c );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 1, 2, 3 ) );
        Assert.AreEqual( 0, fn( 0, 0, 0 ) );
        Assert.AreEqual( -1, fn( -1, 0, 0 ) );
    }

    // ================================================================
    // Lambda returning string
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_StringParam_Uppercase( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var toUpper = typeof(string).GetMethod( "ToUpper", Type.EmptyTypes )!;
        var lambda = Expression.Lambda<Func<string, string>>(
            Expression.Call( s, toUpper ), s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "HELLO", fn( "hello" ) );
        Assert.AreEqual( "WORLD", fn( "World" ) );
    }

    // ================================================================
    // Lambda returning bool from comparison
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_BoolReturn_GreaterThan( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int, bool>>(
            Expression.GreaterThan( x, Expression.Constant( 0 ) ), x );
        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( 1 ) );
        Assert.IsFalse( fn( 0 ) );
        Assert.IsFalse( fn( -1 ) );
    }

    // ================================================================
    // Lambda returning nullable int
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ReturnsNullableInt( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Condition(
            Expression.GreaterThan( x, Expression.Constant( 0 ) ),
            Expression.Convert( x, typeof(int?) ),
            Expression.Constant( null, typeof(int?) ) );
        var lambda = Expression.Lambda<Func<int, int?>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 5 ) );
        Assert.IsNull( fn( 0 ) );
        Assert.IsNull( fn( -3 ) );
    }

    // ================================================================
    // Action lambda (void return)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_VoidReturn_Action( CompilerType compilerType )
    {
        var counter = Expression.Variable( typeof(int), "counter" );
        var body = Expression.Block(
            typeof( void ),
            new[] { counter },
            Expression.Assign( counter, Expression.Constant( 0 ) ) );
        var lambda = Expression.Lambda<Action>( body );
        var fn = lambda.Compile( compilerType );

        fn(); // Should not throw
    }

    // ================================================================
    // Lambda invoke on inner lambda
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_InvokeOnInnerLambda( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var inner = Expression.Lambda<Func<int, int>>(
            Expression.Multiply( x, Expression.Constant( 2 ) ), x );
        var invoke = Expression.Invoke( inner, Expression.Constant( 5 ) );
        var lambda = Expression.Lambda<Func<int>>( invoke );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn() );
    }

    // ================================================================
    // Lambda with parameter used multiple times
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ParameterUsedMultipleTimes( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Add(
            Expression.Multiply( x, x ),
            x );
        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 2 ) );  // 2*2 + 2 = 6
        Assert.AreEqual( 12, fn( 3 ) ); // 3*3 + 3 = 12
        Assert.AreEqual( 0, fn( 0 ) );  // 0*0 + 0 = 0
    }

    // ================================================================
    // Lambda with conditional expression as body
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_ConditionalBody_AbsoluteValue( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Condition(
            Expression.GreaterThanOrEqual( x, Expression.Constant( 0 ) ),
            x,
            Expression.Negate( x ) );
        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 5, fn( 5 ) );
        Assert.AreEqual( 5, fn( -5 ) );
        Assert.AreEqual( 0, fn( 0 ) );
    }

    // ================================================================
    // Invoke: pass lambda as Expression.Constant and invoke it
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_InvokeConstantDelegate( CompilerType compilerType )
    {
        Func<int, int> addOne = x => x + 1;
        var delegateConst = Expression.Constant( addOne );
        var param = Expression.Parameter( typeof(int), "n" );
        var invoke = Expression.Invoke( delegateConst, param );
        var lambda = Expression.Lambda<Func<int, int>>( invoke, param );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 5 ) );
        Assert.AreEqual( 1, fn( 0 ) );
    }

    // ================================================================
    // Lambda with string concatenation (3 params)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_StringConcat_ThreeParams( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(string), "a" );
        var b = Expression.Parameter( typeof(string), "b" );
        var c = Expression.Parameter( typeof(string), "c" );
        var concatMethod = typeof(string).GetMethod( "Concat",
            [typeof(string), typeof(string), typeof(string)] )!;
        var body = Expression.Call( null, concatMethod, a, b, c );
        var lambda = Expression.Lambda<Func<string, string, string, string>>( body, a, b, c );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "abc", fn( "a", "b", "c" ) );
        Assert.AreEqual( "hello world!", fn( "hello", " world", "!" ) );
    }

    // ================================================================
    // Lambda with type conversion parameter
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_CastParamBeforeUse( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(object), "x" );
        var body = Expression.Add(
            Expression.Convert( x, typeof(int) ),
            Expression.Constant( 1 ) );
        var lambda = Expression.Lambda<Func<object, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 5 ) );
        Assert.AreEqual( 1, fn( 0 ) );
    }

    // ================================================================
    // Two separate lambdas with same parameter names — no conflicts
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_SameParamNames_TwoLambdas_Independent( CompilerType compilerType )
    {
        var x1 = Expression.Parameter( typeof(int), "x" );
        var lambda1 = Expression.Lambda<Func<int, int>>(
            Expression.Multiply( x1, Expression.Constant( 2 ) ), x1 );

        var x2 = Expression.Parameter( typeof(int), "x" );
        var lambda2 = Expression.Lambda<Func<int, int>>(
            Expression.Add( x2, Expression.Constant( 100 ) ), x2 );

        var fn1 = lambda1.Compile( compilerType );
        var fn2 = lambda2.Compile( compilerType );

        Assert.AreEqual( 10, fn1( 5 ) );
        Assert.AreEqual( 105, fn2( 5 ) );
    }

    // ================================================================
    // Lambda with default parameter value behavior
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_DefaultValueExpression_Int( CompilerType compilerType )
    {
        var body = Expression.Default( typeof(int) );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn() );
    }

    // ================================================================
    // Lambda with boxing: int param returned as object
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_BoxIntParam_ReturnsObject( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Convert( x, typeof(object) );
        var lambda = Expression.Lambda<Func<int, object>>( body, x );
        var fn = lambda.Compile( compilerType );

        var result = fn( 42 );
        Assert.IsInstanceOfType<int>( result );
        Assert.AreEqual( 42, (int) result );
    }

    // ================================================================
    // Lambda with compound expression: (a + b) * (a - b)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_CompoundArithmetic( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var body = Expression.Multiply(
            Expression.Add( a, b ),
            Expression.Subtract( a, b ) );
        var lambda = Expression.Lambda<Func<int, int, int>>( body, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( (5 + 3) * (5 - 3), fn( 5, 3 ) ); // 16
        Assert.AreEqual( 0, fn( 4, 4 ) );  // (4+4)*(4-4)=0
        Assert.AreEqual( -9, fn( 0, 3 ) ); // (0+3)*(0-3)=-9
    }

    // ================================================================
    // Lambda capturing variable — returned as delegate from block
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_CapturesVariable_ReturnedAsDelegateFromBlock( CompilerType compilerType )
    {
        // () => { var m = 3; return (x) => x * m; }
        var multiplier = Expression.Variable( typeof(int), "multiplier" );
        var x = Expression.Parameter( typeof(int), "x" );
        var inner = Expression.Lambda<Func<int, int>>( Expression.Multiply( x, multiplier ), x );
        var outer = Expression.Lambda<Func<Func<int, int>>>(
            Expression.Block(
                new[] { multiplier },
                Expression.Assign( multiplier, Expression.Constant( 3 ) ),
                inner ) );

        var getMultiplier = outer.Compile( compilerType );
        var multiply = getMultiplier();

        Assert.AreEqual( 21, multiply( 7 ) );
        Assert.AreEqual( 0, multiply( 0 ) );
        Assert.AreEqual( -3, multiply( -1 ) );
    }

    // ================================================================
    // Lambda with five parameters
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_FiveParameters_SumAll( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var c = Expression.Parameter( typeof(int), "c" );
        var d = Expression.Parameter( typeof(int), "d" );
        var e = Expression.Parameter( typeof(int), "e" );
        var body = Expression.Add( Expression.Add( Expression.Add( Expression.Add( a, b ), c ), d ), e );
        var lambda = Expression.Lambda<Func<int, int, int, int, int, int>>( body, a, b, c, d, e );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 15, fn( 1, 2, 3, 4, 5 ) );
        Assert.AreEqual( 0, fn( 0, 0, 0, 0, 0 ) );
        Assert.AreEqual( -5, fn( -1, -1, -1, -1, -1 ) );
    }

    // ================================================================
    // Lambda captures outer parameter in inner lambda
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_CaptureOuterParam_InInnerLambda( CompilerType compilerType )
    {
        // outer: (int factor) => (int x) => x * factor
        var factor = Expression.Parameter( typeof(int), "factor" );
        var x = Expression.Parameter( typeof(int), "x" );
        var inner = Expression.Lambda<Func<int, int>>( Expression.Multiply( x, factor ), x );
        var outer = Expression.Lambda<Func<int, Func<int, int>>>( inner, factor );

        var makeMultiplier = outer.Compile( compilerType );
        var times3 = makeMultiplier( 3 );
        var times10 = makeMultiplier( 10 );

        Assert.AreEqual( 15, times3( 5 ) );
        Assert.AreEqual( 30, times10( 3 ) );
        Assert.AreEqual( 0, times3( 0 ) );
    }

    // ================================================================
    // Invoke with method call as argument
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_InvokeWithMethodCallArg( CompilerType compilerType )
    {
        if ( compilerType == CompilerType.Fast )
            Assert.Inconclusive( "Suppressed: FEC fails on Invoke(lambda, MethodCall(param)) pattern." );
        var absMethod = typeof(Math).GetMethod( "Abs", [typeof(int)] )!;
        var x = Expression.Parameter( typeof(int), "x" );
        var inner = Expression.Lambda<Func<int, int>>(
            Expression.Multiply( x, Expression.Constant( 2 ) ), x );
        // invoke inner(Math.Abs(x))
        var body = Expression.Invoke( inner, Expression.Call( null, absMethod, x ) );
        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn( 5 ) );
        Assert.AreEqual( 10, fn( -5 ) );
        Assert.AreEqual( 0, fn( 0 ) );
    }

    // ================================================================
    // Lambda with null-coalescing body
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_NullCoalescingBody( CompilerType compilerType )
    {
        var s = Expression.Parameter( typeof(string), "s" );
        var body = Expression.Coalesce( s, Expression.Constant( "default" ) );
        var lambda = Expression.Lambda<Func<string, string>>( body, s );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn( "hello" ) );
        Assert.AreEqual( "default", fn( null! ) );
    }

    // ================================================================
    // Lambda with long-typed parameters
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_LongParams_ArithmeticResult( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var body = Expression.Subtract( Expression.Multiply( a, a ), Expression.Multiply( b, b ) );
        var lambda = Expression.Lambda<Func<long, long, long>>( body, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 9L * 9L - 4L * 4L, fn( 9L, 4L ) ); // 81 - 16 = 65
        Assert.AreEqual( 0L, fn( 5L, 5L ) );
    }

    // ================================================================
    // Lambda with switch in body
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_SwitchInBody( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var switchExpr = Expression.Switch(
            x,
            Expression.Constant( "other" ),
            Expression.SwitchCase( Expression.Constant( "one" ),   Expression.Constant( 1 ) ),
            Expression.SwitchCase( Expression.Constant( "two" ),   Expression.Constant( 2 ) ),
            Expression.SwitchCase( Expression.Constant( "three" ), Expression.Constant( 3 ) ) );
        var lambda = Expression.Lambda<Func<int, string>>( switchExpr, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "one",   fn( 1 ) );
        Assert.AreEqual( "two",   fn( 2 ) );
        Assert.AreEqual( "three", fn( 3 ) );
        Assert.AreEqual( "other", fn( 9 ) );
    }

    // ================================================================
    // Lambda invoked twice accumulates result via external state
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_InvokedTwice_AccumulatedResult( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var addThree = Expression.Lambda<Func<int, int>>(
            Expression.Add( x, Expression.Constant( 3 ) ), x );
        // chain: addThree(addThree(5)) = addThree(8) = 11
        var param = Expression.Parameter( typeof(int), "n" );
        var body = Expression.Invoke( addThree,
            Expression.Invoke( addThree, param ) );
        var lambda = Expression.Lambda<Func<int, int>>( body, param );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 11, fn( 5 ) );  // 5+3=8, 8+3=11
        Assert.AreEqual( 6, fn( 0 ) );   // 0+3=3, 3+3=6
    }

    // ================================================================
    // Lambda with TailCall flag — compilation must not crash
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_TailCallFlag_DoesNotCrash( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.Add( x, Expression.Constant( 1 ) ),
            tailCall: true,
            parameters: [x] );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn( 5 ) );
        Assert.AreEqual( 1, fn( 0 ) );
    }

    // ================================================================
    // Lambda returning result of TypeAs
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_TypeAs_ReturnsNullForMismatch( CompilerType compilerType )
    {
        var obj = Expression.Parameter( typeof(object), "obj" );
        var body = Expression.TypeAs( obj, typeof(string) );
        var lambda = Expression.Lambda<Func<object, string?>>( body, obj );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn( "hello" ) );
        Assert.IsNull( fn( 42 ) );
        Assert.IsNull( fn( null! ) );
    }

    // ================================================================
    // Nested invokes — chained result
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Lambda_NestedInvoke_ThreeLevels( CompilerType compilerType )
    {
        // f = x => x * 2
        // g = x => f(f(x))  — quadruples x
        var x = Expression.Parameter( typeof(int), "x" );
        var f = Expression.Lambda<Func<int, int>>( Expression.Multiply( x, Expression.Constant( 2 ) ), x );

        var n = Expression.Parameter( typeof(int), "n" );
        var g = Expression.Lambda<Func<int, int>>(
            Expression.Invoke( f, Expression.Invoke( f, n ) ), n );

        var fn = g.Compile( compilerType );

        Assert.AreEqual( 20, fn( 5 ) );  // 5*2*2=20
        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( -8, fn( -2 ) ); // -2*2=-4, -4*2=-8
    }
}
