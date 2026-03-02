using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class ClosureTests
{
    // ================================================================
    // Simple lambda without captures (Invoke of non-capturing lambda)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_SimpleLambda_NoCapturedVariables( CompilerType compilerType )
    {
        // var addOne = (int x) => x + 1;
        // return addOne(41);
        var x = Expression.Parameter( typeof( int ), "x" );
        var addOne = Expression.Lambda<Func<int, int>>(
            Expression.Add( x, Expression.Constant( 1 ) ),
            x );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Invoke( addOne, Expression.Constant( 41 ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_VoidLambda_NoCapturedVariables( CompilerType compilerType )
    {
        // Action that does nothing interesting, but verifies void invoke works
        // var action = () => { };
        // action();
        // return 99;
        var action = Expression.Lambda<Action>( Expression.Empty() );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                Expression.Invoke( action ),
                Expression.Constant( 99 ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }

    // ================================================================
    // Lambda with single captured variable (mutable)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_CapturedVariable_SingleIncrement( CompilerType compilerType )
    {
        // var counter = 0;
        // Action increment = () => counter = counter + 1;
        // increment();
        // return counter;
        var counter = Expression.Variable( typeof( int ), "counter" );
        var increment = Expression.Lambda<Action>(
            Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ) );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { counter },
                Expression.Assign( counter, Expression.Constant( 0 ) ),
                Expression.Invoke( increment ),
                counter ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_CapturedVariable_DoubleIncrement( CompilerType compilerType )
    {
        // var counter = 0;
        // Action increment = () => counter = counter + 1;
        // increment();
        // increment();
        // return counter;
        var counter = Expression.Variable( typeof( int ), "counter" );
        var increment = Expression.Lambda<Action>(
            Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ) );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { counter },
                Expression.Assign( counter, Expression.Constant( 0 ) ),
                Expression.Invoke( increment ),
                Expression.Invoke( increment ),
                counter ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_CapturedVariable_TripleIncrement_StartingAt10( CompilerType compilerType )
    {
        // var counter = 10;
        // Action increment = () => counter = counter + 1;
        // increment();
        // increment();
        // increment();
        // return counter;
        var counter = Expression.Variable( typeof( int ), "counter" );
        var increment = Expression.Lambda<Action>(
            Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ) );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { counter },
                Expression.Assign( counter, Expression.Constant( 10 ) ),
                Expression.Invoke( increment ),
                Expression.Invoke( increment ),
                Expression.Invoke( increment ),
                counter ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 13, fn() );
    }

    // ================================================================
    // Lambda capturing multiple variables
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_CapturedMultipleVariables_AddThem( CompilerType compilerType )
    {
        // var a = 10;
        // var b = 20;
        // Func<int> getSum = () => a + b;
        // return getSum();
        var a = Expression.Variable( typeof( int ), "a" );
        var b = Expression.Variable( typeof( int ), "b" );
        var getSum = Expression.Lambda<Func<int>>(
            Expression.Add( a, b ) );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { a, b },
                Expression.Assign( a, Expression.Constant( 10 ) ),
                Expression.Assign( b, Expression.Constant( 20 ) ),
                Expression.Invoke( getSum ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 30, fn() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_CapturedMultipleVariables_MutateIndependently( CompilerType compilerType )
    {
        // var x = 0;
        // var y = 0;
        // Action incX = () => x = x + 1;
        // Action incY = () => y = y + 10;
        // incX();
        // incY();
        // incX();
        // return x + y;
        var x = Expression.Variable( typeof( int ), "x" );
        var y = Expression.Variable( typeof( int ), "y" );

        var incX = Expression.Lambda<Action>(
            Expression.Assign( x, Expression.Add( x, Expression.Constant( 1 ) ) ) );
        var incY = Expression.Lambda<Action>(
            Expression.Assign( y, Expression.Add( y, Expression.Constant( 10 ) ) ) );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { x, y },
                Expression.Assign( x, Expression.Constant( 0 ) ),
                Expression.Assign( y, Expression.Constant( 0 ) ),
                Expression.Invoke( incX ),
                Expression.Invoke( incY ),
                Expression.Invoke( incX ),
                Expression.Add( x, y ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 12, fn() );
    }

    // ================================================================
    // Lambda with captured variable and parameters
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_CapturedVariable_LambdaWithParameter( CompilerType compilerType )
    {
        // var total = 0;
        // Action<int> addAmount = (int amount) => total = total + amount;
        // addAmount(5);
        // addAmount(3);
        // return total;
        var total = Expression.Variable( typeof( int ), "total" );
        var amount = Expression.Parameter( typeof( int ), "amount" );
        var addAmount = Expression.Lambda<Action<int>>(
            Expression.Assign( total, Expression.Add( total, amount ) ),
            amount );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { total },
                Expression.Assign( total, Expression.Constant( 0 ) ),
                Expression.Invoke( addAmount, Expression.Constant( 5 ) ),
                Expression.Invoke( addAmount, Expression.Constant( 3 ) ),
                total ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 8, fn() );
    }

    // ================================================================
    // Captured variable read (not mutated) in nested lambda
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_CapturedVariable_ReadOnly( CompilerType compilerType )
    {
        // var value = 42;
        // Func<int> getter = () => value;
        // return getter();
        var value = Expression.Variable( typeof( int ), "value" );
        var getter = Expression.Lambda<Func<int>>( value );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { value },
                Expression.Assign( value, Expression.Constant( 42 ) ),
                Expression.Invoke( getter ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // ================================================================
    // Mixed: some variables captured, some not
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_MixedVariables_CapturedAndLocal( CompilerType compilerType )
    {
        // var captured = 100;
        // var local = 5;
        // Func<int> getCaptured = () => captured;
        // return getCaptured() + local;
        var captured = Expression.Variable( typeof( int ), "captured" );
        var local = Expression.Variable( typeof( int ), "local" );
        var getCaptured = Expression.Lambda<Func<int>>( captured );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { captured, local },
                Expression.Assign( captured, Expression.Constant( 100 ) ),
                Expression.Assign( local, Expression.Constant( 5 ) ),
                Expression.Add( Expression.Invoke( getCaptured ), local ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 105, fn() );
    }

    // ================================================================
    // Captured string variable
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_CapturedStringVariable( CompilerType compilerType )
    {
        // var msg = "hello";
        // Func<string> getter = () => msg;
        // return getter();
        var msg = Expression.Variable( typeof( string ), "msg" );
        var getter = Expression.Lambda<Func<string>>( msg );

        var lambda = Expression.Lambda<Func<string>>(
            Expression.Block(
                new[] { msg },
                Expression.Assign( msg, Expression.Constant( "hello" ) ),
                Expression.Invoke( getter ) ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn() );
    }

    // ================================================================
    // Captured variable with conditional logic
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_CapturedVariable_ConditionalModification( CompilerType compilerType )
    {
        // var counter = 0;
        // Action increment = () => counter = counter + 1;
        // if (true) increment();
        // return counter;
        var counter = Expression.Variable( typeof( int ), "counter" );
        var increment = Expression.Lambda<Action>(
            Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ) );

        var lambda = Expression.Lambda<Func<int>>(
            Expression.Block(
                new[] { counter },
                Expression.Assign( counter, Expression.Constant( 0 ) ),
                Expression.IfThen(
                    Expression.Constant( true ),
                    Expression.Invoke( increment ) ),
                counter ) );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn() );
    }
}
