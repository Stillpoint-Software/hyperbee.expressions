using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

/// <summary>
/// A lambda invoked in place is inlined at the call site rather than compiled to a delegate.
/// These tests pin the semantics that inlining has to preserve: argument evaluation order and
/// count, parameter scoping, and shadowing between the lambda's parameters and the enclosing
/// scope.
/// </summary>
[TestClass]
public class InvokeInliningTests
{
    private static int _sideEffects;

    private static int Track( int value )
    {
        _sideEffects++;
        return value;
    }

    private static Expression TrackCall( Expression value ) =>
        Call( typeof( InvokeInliningTests ), nameof( Track ), Type.EmptyTypes, value );

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_NoParameters( CompilerType compiler )
    {
        var input = Parameter( typeof( int ), "input" );
        var inner = Lambda<Func<int>>( Add( input, Constant( 1 ) ) );

        var lambda = Lambda<Func<int, int>>( Add( Invoke( inner ), Invoke( inner ) ), input );

        Assert.AreEqual( 8, lambda.Compile( compiler )( 3 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_WithParameters( CompilerType compiler )
    {
        var a = Parameter( typeof( int ), "a" );
        var b = Parameter( typeof( int ), "b" );
        var inner = Lambda<Func<int, int, int>>( Subtract( a, b ), a, b );

        var lambda = Lambda<Func<int>>( Invoke( inner, Constant( 10 ), Constant( 4 ) ) );

        Assert.AreEqual( 6, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_ArgumentReadsTheParameterItBinds( CompilerType compiler )
    {
        // The lambda's parameter is the SAME instance as the enclosing parameter, and the
        // argument reads it. The argument has to be evaluated before the parameter is bound,
        // or the binding shadows the value the argument meant to read.
        var x = Parameter( typeof( int ), "x" );
        var inner = Lambda<Func<int, int>>( Multiply( x, Constant( 2 ) ), x );

        var lambda = Lambda<Func<int, int>>(
            Invoke( inner, Call( typeof( Math ), nameof( Math.Abs ), Type.EmptyTypes, x ) ), x );

        var compiled = lambda.Compile( compiler );

        Assert.AreEqual( 10, compiled( 5 ) );
        Assert.AreEqual( 10, compiled( -5 ) );
        Assert.AreEqual( 0, compiled( 0 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_ParameterShadowsEnclosingVariable( CompilerType compiler )
    {
        // The lambda binds an instance the enclosing block also declares. The binding is
        // scoped to the body; the enclosing value must survive.
        var value = Parameter( typeof( int ), "value" );

        var inner = Lambda<Func<int, int>>( Multiply( value, Constant( 10 ) ), value );

        var lambda = Lambda<Func<int>>(
            Block(
                new[] { value },
                Assign( value, Constant( 3 ) ),
                Add( Invoke( inner, Constant( 7 ) ), value ) ) );

        Assert.AreEqual( 73, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_EvaluatesArgumentsOnceInOrder( CompilerType compiler )
    {
        var a = Parameter( typeof( int ), "a" );
        var b = Parameter( typeof( int ), "b" );

        // b - a, so an out-of-order evaluation changes the result
        var inner = Lambda<Func<int, int, int>>( Subtract( b, a ), a, b );

        var lambda = Lambda<Func<int>>(
            Invoke( inner, TrackCall( Constant( 4 ) ), TrackCall( Constant( 10 ) ) ) );

        var compiled = lambda.Compile( compiler );

        _sideEffects = 0;
        var result = compiled();

        Assert.AreEqual( 6, result );
        Assert.AreEqual( 2, _sideEffects ); // each argument evaluated exactly once
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_VoidLambda( CompilerType compiler )
    {
        var sink = Parameter( typeof( int ), "sink" );
        var inner = Lambda<Action<int>>( TrackCall( sink ), sink );

        var lambda = Lambda<Action>( Invoke( inner, Constant( 1 ) ) );

        var compiled = lambda.Compile( compiler );

        _sideEffects = 0;
        compiled();

        Assert.AreEqual( 1, _sideEffects );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_NestedInvocations( CompilerType compiler )
    {
        var x = Parameter( typeof( int ), "x" );
        var doubler = Lambda<Func<int, int>>( Multiply( x, Constant( 2 ) ), x );

        var input = Parameter( typeof( int ), "input" );

        var lambda = Lambda<Func<int, int>>(
            Invoke( doubler, Invoke( doubler, input ) ), input );

        Assert.AreEqual( 20, lambda.Compile( compiler )( 5 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_LambdaAlsoUsedAsValue( CompilerType compiler )
    {
        // The same lambda instance is inlined at one site and materialized as a delegate at
        // another. The capture has to survive for the delegate use.
        var input = Parameter( typeof( int ), "input" );
        var inner = Lambda<Func<int>>( Add( input, Constant( 1 ) ) );
        var held = Parameter( typeof( Func<int> ), "held" );

        var lambda = Lambda<Func<int, int>>(
            Block(
                new[] { held },
                Assign( held, inner ),
                Add( Invoke( inner ), Invoke( held ) ) ),
            input );

        Assert.AreEqual( 8, lambda.Compile( compiler )( 3 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Invoke_BodyWritesEnclosingVariable( CompilerType compiler )
    {
        // Inlining must not turn a write to an enclosing variable into a write to a copy.
        var total = Parameter( typeof( int ), "total" );
        var inner = Lambda<Action>( Assign( total, Add( total, Constant( 5 ) ) ) );

        var lambda = Lambda<Func<int>>(
            Block(
                new[] { total },
                Assign( total, Constant( 1 ) ),
                Invoke( inner ),
                Invoke( inner ),
                total ) );

        Assert.AreEqual( 11, lambda.Compile( compiler )() );
    }
}
