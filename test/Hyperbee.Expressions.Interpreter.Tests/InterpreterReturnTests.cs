using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Interpreter.Tests;

[TestClass]
public class InterpreterReturnTests
{
    [TestMethod]
    public void Compile_ShouldSucceed_WithMatchingNestedReturns()
    {
        var x = Variable( typeof( int ), "x" );
        var returnLabel = Label( typeof( int ), "ReturnLabel" );

        var lambda = Lambda<Func<int>>(
            Block(
                [x],
                Assign( x, Constant( 10 ) ),
                IfThenElse(
                    Constant( true ),
                    Block(
                        IfThenElse(
                            Constant( false ),
                            Return( returnLabel, Constant( 100 ) ),
                            Constant( 20 )
                        ),
                        Return( returnLabel, Constant( 42 ) )
                    ),
                    Constant( 30 )
                ),
                Label(
                    returnLabel,
                    Default(
                        typeof( int )
                    )
                )
            )
        );

        var compiledLambda = lambda.Interpreter();

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    public void Compile_ShouldSucceed_WithIntReturn()
    {
        var x = Parameter( typeof( int ), "x" );

        var returnLabel = Label( typeof( int ), "ReturnLabel" );

        var lambda = Lambda<Func<int>>(
            Block(
                [x],
                Assign( x, Constant( 10 ) ),
                Condition(
                    Constant( true ),
                    Return( returnLabel, Constant( 42 ), typeof( int ) ),
                    Default( typeof( int ) ),
                    typeof( int )
                ),
                Constant( 10 ),
                Label(
                    returnLabel,
                    Default(
                        typeof( int )
                    )
                )
            )
        );

        var compiledLambda = lambda.Interpreter();

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    public void Compile_ShouldSucceed_WithReturnInLoop()
    {
        var loopResult = Parameter( typeof( int ), "result" );

        var returnLabel = Label( typeof( int ), "ReturnLabel" );
        var breakLabel = Label( "Break" );
        var continueLabel = Label( "Continue" );

        var lambda = Lambda<Func<int>>(
            Block(
                [loopResult],
                Assign( loopResult, Constant( 0 ) ),
                Loop(
                    Block(
                        Assign( loopResult, Constant( 10 ) ),
                        Return( returnLabel, Constant( 42 ) )
                    ),
                    breakLabel,
                    continueLabel
                ),
                Return(
                    returnLabel,
                    loopResult
                ),
                Label(
                    returnLabel,
                    Default( typeof( int ) )
                )
            )
        );
        var compiledLambda = lambda.Interpreter();

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    public void Compile_ShouldSucceed_WithReturnInSwitch()
    {
        var x = Parameter( typeof( int ), "x" );

        var returnLabel = Label( typeof( int ), "ReturnLabel" );

        var lambda = Lambda<Func<int>>(
            Block(
                [x],
                Assign( x, Constant( 3 ) ),
                Switch(
                    x,
                    Return( returnLabel, Constant( 0 ) ),
                    SwitchCase(
                        Return( returnLabel, Constant( 10 ) ),
                        Constant( 1 )
                    ),
                    SwitchCase(
                        Return( returnLabel, Constant( 42 ) ),
                        Constant( 3 )
                    )
                ),
                Return( returnLabel, x ),
                Label(
                    returnLabel,
                    Default( typeof( int ) )
                )
            )
        );

        var compiledLambda = lambda.Interpreter();

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    public void Compile_ShouldSucceed_WithLambdaInvokeChaining()
    {
        var myLambda = Parameter( typeof(Func<int, Func<int, int>>), "myLambda" );
        var outerParam = Parameter( typeof(int), "outerParam" );
        var innerParam = Parameter( typeof(int), "innerParam" );

        // myLambda(20)(21);
        var lambda = Lambda<Func<int>>(
            Block(
                [myLambda],
                // myLambda = outerParam => innerParam => outerParam + innerParam + 1;
                Assign(
                    myLambda,
                    Lambda( Lambda( Add( Add( outerParam, innerParam ), Constant( 1 ) ), innerParam ) , outerParam )
                ),
                Invoke(
                    Invoke( myLambda, Constant( 20 ) ),
                    Constant( 21 )
                )
            )
        );

        var compiledLambda = lambda.Interpreter();

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }

    [TestMethod]
    public void Compile_ShouldSucceed_WithMethodChaining()
    {
        var myLambda = Parameter( typeof( Func<int, int> ), "myLambda" );
        var x = Parameter( typeof( int ), "x" );

        var returnLabel = Label( typeof( int ), "ReturnLabel" );

        var lambda = Lambda<Func<string>>(
            Block(
                [myLambda],
                Assign(
                    myLambda,
                    Lambda(
                        Block(
                            Return( returnLabel, Add( x, Constant( 1 ) ) ),
                            Label( returnLabel, Default( typeof( int ) ) )
                        ), x )
                ),
                Call(
                    Invoke( myLambda, Constant( 41 ) ),
                    typeof( int ).GetMethod( "ToString", Type.EmptyTypes )!
                )
            )
        );

        var compiledLambda = lambda.Interpreter();

        var result = compiledLambda();

        Assert.AreEqual( "42", result );
    }

    [TestMethod]
    public void Compile_ShouldSucceed_WithReturnStatement()
    {
        var myLambda = Parameter( typeof( Func<int, int> ), "myLambda" );
        var x = Parameter( typeof( int ), "x" );

        var returnLabel = Label( typeof( int ), "ReturnLabel" );

        var lambda = Lambda<Func<int>>(
            Block(
                [myLambda],
                Assign(
                    myLambda,
                    Lambda(
                        Block(
                            Return(
                                returnLabel,
                                Add( x, Constant( 1 ) )
                            ),
                            Label( returnLabel, Default( typeof( int ) ) )
                        ), x )
                ),
                Invoke( myLambda, Constant( 12 ) )
            )
        );

        var compiledLambda = lambda.Interpreter();

        var result = compiledLambda();

        Assert.AreEqual( 13, result );
    }

    [TestMethod]
    public void Compile_ShouldRethrowException()
    {
        var x = Parameter( typeof( int ), "x" );
        var e = Parameter( typeof( InvalidOperationException ), "e" );
        var invOpeExc = Parameter( typeof( InvalidOperationException ), "InvOpeExc" );

        var exceptionCtor = typeof( InvalidOperationException ).GetConstructor( Type.EmptyTypes )!;

        var lambda = Lambda<Func<int>>(
            Block(
                [x],
                Assign( x, Constant( 0 ) ),
                TryCatch(
                    TryCatch(
                        Block(
                            Throw( New( exceptionCtor ) ),
                            x
                        ),
                        Catch( e,
                            Block(
                                Assign( x, Constant( 32 ) ),
                                Throw( e ),
                                x
                            )
                        ) ),
                    Catch( invOpeExc, AddAssign( x, Constant( 10 ) ) )
                ), x )
        );

        var compiledLambda = lambda.Interpreter();

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }
}
