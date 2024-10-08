using System.Reflection;

using static System.Linq.Expressions.Expression;
using static Hyperbee.AsyncExpressions.AsyncExpression;

namespace Hyperbee.AsyncExpressions.Tests;

[TestClass]
public class AsyncBlockTests
{
    public static bool AreEqual( int a, int b ) => a == b;

    public static async Task<bool> AreEqualAsync( int a, int b )
    {
        await Task.Delay( 100 );
        return a == b;
    }

    public static MethodInfo GetMethod( string name ) => typeof(AsyncBlockTests).GetMethod( name );

    [TestMethod]
    public async Task TestAsyncBlock_VariableScopeWithMultipleAwaits()
    {
        // Arrange
        var varExpr = Variable( typeof(int), "x" );
        var assignExpr1 = Assign( varExpr, Constant( 10 ) );

        // First await expression
        var awaitExpr1 = Await( Constant( Task.CompletedTask, typeof(Task) ) );

        // Increment variable after first await
        var assignExpr2 = Assign( varExpr, Increment( varExpr ) );

        // Second await expression
        var awaitExpr2 = Await( Constant( Task.CompletedTask, typeof(Task) ) );

        // Assert to check if variable maintains scope and has the expected value
        var assertExpr1 = Call(
            GetMethod( nameof(AreEqual) ),
            Constant( 11 ),
            varExpr );

        // Trailing expression after the last await
        var finalAssignExpr = Assign( varExpr, Add( varExpr, Constant( 1 ) ) );
        var assertExpr2 = Call(
            GetMethod( nameof(AreEqual) ),
            Constant( 12 ),
            varExpr );

        var asyncBlock = BlockAsync(
            [varExpr],
            assignExpr1, awaitExpr1, assignExpr2, awaitExpr2, assertExpr1, finalAssignExpr, assertExpr2 );

        // Act
        var lambda = Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    [ExpectedException( typeof(InvalidOperationException) )]
    public async Task TestAsyncBlock_WithoutAwait_ThrowsException()
    {
        // Arrange
        var varExpr = Variable( typeof(int), "x" );
        var assignExpr1 = Assign( varExpr, Constant( 10 ) );
        var assignExpr2 = Assign( varExpr, Increment( varExpr ) );
        var assertExpr = Call(
            GetMethod( nameof(AreEqual) ),
            Constant( 11 ),
            varExpr );

        var asyncBlock = BlockAsync(
            [varExpr],
            assignExpr1, assignExpr2, assertExpr );

        // Act
        var lambda = Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_SimpleBlockAwait()
    {
        // Arrange
        var expr1 = Constant( 1 );
        var expr2 = Constant( 2 );
        var awaitExpr3 = Await( Constant( Task.CompletedTask, typeof(Task) ) );
        var expr4 = Constant( 4 );

        var asyncBlock = BlockAsync( expr1, expr2, awaitExpr3, expr4 );

        // Act
        var lambda = Lambda<Func<Task<int>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 4, result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_WithoutParameters()
    {
        // Arrange
        var expr1 = Constant( 1 );
        var expr2 = Constant( 2 );
        var awaitExpr3 = Await( Constant( Task.FromResult( 3 ) ) );
        var expr4 = Constant( 4 );
        var expr5 = Constant( 5 );
        var awaitExpr6 = Await( Constant( Task.CompletedTask, typeof(Task) ) );
        var expr7 = Constant( 7 );

        var asyncBlock = BlockAsync( expr1, expr2, awaitExpr3, expr4, expr5, awaitExpr6, expr7 );

        // Act

        var lambda = Lambda<Func<Task<int>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 7, result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_WithParameters()
    {
        // Arrange
        var param1 = Parameter( typeof(int), "param1" );
        var var1 = Variable( typeof(int), "var1" );
        var var2 = Variable( typeof(int), "var2" );

        var exp1 = Assign( var1, Constant( 1 ) );
        var awaitExpr2 = Await( Constant( Task.FromResult( 3 ) ) );
        var exp3 = Assign( var2, awaitExpr2 );
        var add = Add( var1, Add( var2, param1 ) );

        var asyncBlock = BlockAsync(
            [var1, var2],
            exp1, exp3, add
        );

        // Act
        var lambda = Lambda<Func<int, Task<int>>>( asyncBlock, param1 );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda( 3 );

        // Assert
        Assert.AreEqual( 7, result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_SwitchWithParameters_VariableResult()
    {
        var param1 = Parameter( typeof(int), "param1" );
        var var1 = Variable( typeof(int), "var1" );

        var switchCase = Switch(
            param1,
            Constant( 0 ),
            [
                SwitchCase(
                    Assign( var1, Await( Constant( Task.FromResult( 5 ) ) ) ),
                    Constant( 1 ) ),
                SwitchCase(
                    Assign( var1, Await( Constant( Task.FromResult( 7 ) ) ) ),
                    Constant( 2 ) )
            ]
        );

        var asyncBlock = BlockAsync(
            [var1],
            switchCase, var1
        );

        // Act
        var lambda = Lambda<Func<int, Task<int>>>( asyncBlock, param1 );
        var compiledLambda = lambda.Compile();
        var switch1 = await compiledLambda( 1 );
        var switch2 = await compiledLambda( 2 );

        // Assert
        Assert.AreEqual( 5, switch1 );
        Assert.AreEqual( 7, switch2 );
    }

    [TestMethod]
    public async Task TestAsyncBlock_SwitchWithParameters_Result()
    {
        var param1 = Parameter( typeof(int), "param1" );

        var switchCase = Switch(
            param1,
            Await( Constant( Task.FromResult( 2 ) ) ),
            [
                SwitchCase(
                    Await( Constant( Task.FromResult( 5 ) ) ),
                    Constant( 1 ) ),
                SwitchCase(
                    Await( Constant( Task.FromResult( 7 ) ) ),
                    Constant( 2 ) )
            ]
        );

        var asyncBlock = BlockAsync(
            [],
            switchCase
        );

        // Act
        var lambda = Lambda<Func<int, Task<int>>>( asyncBlock, param1 );
        var compiledLambda = lambda.Compile();
        var switch1 = await compiledLambda( 1 );
        var switch2 = await compiledLambda( 2 );

        // Assert
        Assert.AreEqual( 5, switch1 );
        Assert.AreEqual( 7, switch2 );
    }

    [TestMethod]
    public async Task TestAsyncBlock_SwitchWithParameters_NestedResult()
    {
        var param1 = Parameter( typeof(int), "param1" );
        var param2 = Parameter( typeof(int), "param2" );

        var innerSwitchCase = Switch(
            param2,
            Await( Constant( Task.FromResult( 1 ) ) ),
            [
                SwitchCase(
                    Await( Constant( Task.FromResult( 3 ) ) ),
                    Constant( 1 ) ),
                SwitchCase(
                    Await( Constant( Task.FromResult( 4 ) ) ),
                    Constant( 2 ) )
            ]
        );

        var switchCase = Switch(
            param1,
            Await( Constant( Task.FromResult( 2 ) ) ),
            [
                SwitchCase(
                    innerSwitchCase,
                    Constant( 1 ) ),
                SwitchCase(
                    Await( Constant( Task.FromResult( 5 ) ) ),
                    Constant( 2 ) )
            ]
        );

        var asyncBlock = BlockAsync(
            [],
            switchCase
        );

        // Act
        var lambda = Lambda<Func<int, int, Task<int>>>( asyncBlock, param1, param2 );
        var compiledLambda = lambda.Compile();
        var switch1 = await compiledLambda( 1, 1 );
        var switch2 = await compiledLambda( 1, 2 );
        var switch3 = await compiledLambda( 1, 100 );
        var switch4 = await compiledLambda( 2, 100 );
        var switch5 = await compiledLambda( 100, 100 );

        // Assert
        Assert.AreEqual( 3, switch1 );
        Assert.AreEqual( 4, switch2 );
        Assert.AreEqual( 1, switch3 );
        Assert.AreEqual( 5, switch4 );
        Assert.AreEqual( 2, switch5 );
    }

    [TestMethod]
    public async Task TestAsyncBlock_SwitchWithParameters_ReturnsResult()
    {
        var param1 = Parameter( typeof(int), "param1" );
        var returnLabel = Label( typeof(int), "returnTest" );

        var switchCase = Switch(
            param1,
            Return( returnLabel, Await( Constant( Task.FromResult( 2 ) ) ) ),
            [
                SwitchCase(
                    Return( returnLabel, Await( Constant( Task.FromResult( 5 ) ) ) ),
                    Constant( 1 ) ),
                SwitchCase(
                    Return( returnLabel, Await( Constant( Task.FromResult( 7 ) ) ) ),
                    Constant( 2 ) )
            ]
        );
        var returnExpr = Label( returnLabel, Constant( 9 ) );

        var asyncBlock = BlockAsync(
            [],
            switchCase, returnExpr
        );

        // Act
        var lambda = Lambda<Func<int, Task<int>>>( asyncBlock, param1 );
        var compiledLambda = lambda.Compile();
        var switch1 = await compiledLambda( 1 );
        var switch2 = await compiledLambda( 2 );

        // Assert
        Assert.AreEqual( 5, switch1 );
        Assert.AreEqual( 7, switch2 );
    }

    [TestMethod]
    public async Task TestAsyncBlock_ConditionalWithParameters_VariableResult()
    {
        // Arrange
        var param1 = Parameter( typeof(int), "param1" );
        var param2 = Parameter( typeof(bool), "param2" );
        var var1 = Variable( typeof(int), "var1" );
        var var2 = Variable( typeof(int), "var2" );

        var exp1 = Assign( var1, Constant( 1 ) );
        var awaitExpr2 = Await( Constant( Task.FromResult( 3 ) ) );
        var exp3 = Assign( var2, awaitExpr2 );
        var conditionalAdd = IfThenElse( param2,
            Assign( var2, Add( var2, param1 ) ),
            Assign( var2, Add( var1, Add( var2, param1 ) ) )
        );

        var asyncBlock = BlockAsync(
            [var1, var2],
            exp1, exp3, conditionalAdd, var2
        );

        // Act
        var lambda = Lambda<Func<int, bool, Task<int>>>( asyncBlock, param1, param2 );
        var compiledLambda = lambda.Compile();
        var resultTrue = await compiledLambda( 3, true );
        var resultFalse = await compiledLambda( 3, false );

        // Assert
        Assert.AreEqual( 6, resultTrue );
        Assert.AreEqual( 7, resultFalse );
    }

    [TestMethod]
    public async Task TestAsyncBlock_Conditional_AwaitTest()
    {
        // Arrange
        var var1 = Variable( typeof(int), "var1" );
        var var2 = Variable( typeof(int), "var2" );

        var exp1 = Assign( var1, Constant( 1 ) );
        var awaitConditionTest = Await( Constant( Task.FromResult( true ) ) );
        var conditionalAdd = IfThenElse( awaitConditionTest,
            Assign( var2, Add( var2, var1 ) ),
            Assign( var2, Add( var1, Add( var2, var2 ) ) )
        );

        var asyncBlock = BlockAsync(
            [var1, var2],
            exp1, conditionalAdd, var2
        );

        // Act
        var lambda = Lambda<Func<Task<int>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_Conditional_Result()
    {
        // Arrange
        var block = BlockAsync(
            Condition( Constant( true ),
                Await( Constant( Task.FromResult( 1 ) ) ),
                Await( Constant( Task.FromResult( 2 ) ) )
            ) );

        // Act
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_ConditionalWithParameters_ReturnsResult()
    {
        // Arrange
        var param1 = Parameter( typeof(int), "param1" );
        var param2 = Parameter( typeof(bool), "param2" );
        var var1 = Variable( typeof(int), "var1" );
        var var2 = Variable( typeof(int), "var2" );
        var returnLabel = Label( typeof(int), "returnTest" );

        var exp1 = Assign( var1, Constant( 1 ) );
        var awaitExpr2 = Await( Constant( Task.FromResult( 3 ) ) );
        var exp3 = Assign( var2, awaitExpr2 );
        var conditionalAdd = IfThenElse( param2,
            Return( returnLabel, Add( var2, param1 ) ),
            Return( returnLabel, Add( var1, Add( var2, param1 ) ) )
        );
        var returnExpr = Label( returnLabel, Constant( 9 ) );

        var asyncBlock = BlockAsync(
            [var1, var2],
            exp1, exp3, conditionalAdd, returnExpr
        );

        // Act
        var lambda = Lambda<Func<int, bool, Task<int>>>( asyncBlock, param1, param2 );
        var compiledLambda = lambda.Compile();
        var resultTrue = await compiledLambda( 3, true );
        var resultFalse = await compiledLambda( 3, false );

        // Assert
        Assert.AreEqual( 6, resultTrue );
        Assert.AreEqual( 7, resultFalse );
    }

    [TestMethod]
    public async Task TestAsyncBlock_SingleAwait()
    {
        // Arrange
        var varExpr = Variable( typeof(int), "x" );
        var assignExpr = Assign( varExpr, Constant( 5 ) );
        var awaitExpr = Await( Constant( Task.CompletedTask, typeof(Task) ) );
        var assertExpr = Call(
            GetMethod( nameof(AreEqual) ),
            Constant( 5 ),
            varExpr );

        var asyncBlock = BlockAsync(
            [varExpr],
            assignExpr, awaitExpr, assertExpr );

        // Act
        var lambda = Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_MultipleAwaitAndVariableUpdate()
    {
        // Arrange
        var varExpr = Variable( typeof(int), "x" );
        var assignExpr1 = Assign( varExpr, Constant( 1 ) );
        var awaitExpr1 = Await( Constant( Task.CompletedTask, typeof(Task) ) );
        var assignExpr2 = Assign( varExpr, Add( varExpr, Constant( 2 ) ) );
        var awaitExpr2 = Await( Constant( Task.CompletedTask, typeof(Task) ) );
        var assertExpr = Call(
            GetMethod( nameof(AreEqual) ),
            Constant( 3 ),
            varExpr );

        var asyncBlock = BlockAsync(
            [varExpr],
            assignExpr1, awaitExpr1, assignExpr2, awaitExpr2, assertExpr );

        // Act
        var lambda = Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_NestedAsyncBlock()
    {
        // Arrange
        var varExpr = Variable( typeof(int), "x" );
        var assignExpr1 = Assign( varExpr, Constant( 1 ) );
        var awaitExpr1 = Await( Constant( Task.CompletedTask, typeof(Task) ) );

        // Inner async block
        var innerAssign = Assign( varExpr, Add( varExpr, Constant( 2 ) ) );
        var innerAwait = Await( Constant( Task.CompletedTask, typeof(Task) ) );
        var innerBlock = Await(
            BlockAsync( innerAssign, innerAwait, varExpr ) );

        var assertExpr = Call(
            GetMethod( nameof(AreEqual) ),
            Constant( 3 ),
            varExpr );

        var asyncBlock = BlockAsync(
            [varExpr],
            assignExpr1, awaitExpr1, innerBlock, assertExpr );

        // Act
        var lambda = Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_LoopAsyncBlock()
    {
        // Arrange
        var breakLabel = Label( typeof(int), "breakLoop" );
        var continueLabel = Label( "continueLoop" );
        var var1 = Variable( typeof(int), "x" );

        var asyncLoopBlock = BlockAsync(
            [var1],
            Assign( var1, Constant( 0 ) ),
            Loop(
                Block(
                    Assign( var1, Add( var1, Constant( 1 ) ) ),
                    Await( Constant( Task.CompletedTask, typeof(Task) ) ),
                    IfThenElse( GreaterThanOrEqual( var1, Constant( 5 ) ),
                        Break( breakLabel, var1 ),
                        Continue( continueLabel ) ),
                    Constant( "This is unreachable code." ) // This code is unreachable
                ),
                breakLabel,
                continueLabel
            ),
            var1 // TODO: Should this be required?
        );

        // Act
        var lambda = Lambda<Func<Task<int>>>( asyncLoopBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 5, result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_Conditional_NestedResult()
    {
        // Arrange
        var ifTrue = Condition(
            Constant( true ),
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( Constant( Task.FromResult( 2 ) ) ) );

        var ifFalse = Condition( Constant( false ),
            Await( Constant( Task.FromResult( 3 ) ) ),
            Await( Constant( Task.FromResult( 4 ) ) ) );

        var block = BlockAsync(
            Condition( Constant( true ),
                ifTrue,
                ifFalse
            ) );

        // Act
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 1, result );
    }

    [TestMethod]
    public async Task TestAsyncBlock_ContinueOnDelay()
    {
        // Arrange
        var areEqualAsyncMethodInfo = GetMethod( nameof(AreEqualAsync) );

        var awaitExpr = Await(
            Call(
                areEqualAsyncMethodInfo,
                Constant( 1 ),
                Constant( 1 ) ) );

        var asyncBlock = BlockAsync( awaitExpr );

        // Act
        var lambda = Lambda<Func<Task<bool>>>( asyncBlock );
        var compiledLambda = lambda.Compile();
        var result = await compiledLambda();

        // Assert
        Assert.IsTrue( result );
    }

    // [TestMethod]
    // public async Task TestAsyncBlock_ComplexBlock_Result()
    // {
    //     // Arrange
    //     var complexBlock = IfThen(
    //         Await( Constant( Task.FromResult( true ) ) ),
    //         Block(
    //             Await( Constant( Task.FromResult( 0.1 ) ) ),
    //             IfThenElse( Constant( false ), Constant( 1.1 ), Block( Constant( 1.2 ), Constant( 1.3 ) ) ),
    //             Switch(
    //                 Await( Constant( Task.FromResult( "TestValue2" ) ) ),
    //                 Constant( 3.1 ),
    //                 SwitchCase( Constant( 3.2 ), Constant( "TestValue1" ) ),
    //                 SwitchCase( Constant( 3.3 ), Constant( "TestValue2" ) ),
    //                 SwitchCase( Constant( 3.4 ), Constant( "TestValue3" ) )
    //             )
    //         ) );
    //
    //     var asyncBlock = BlockAsync( complexBlock );
    //
    //     // Act
    //     var lambda = Lambda<Func<Task<int>>>( asyncBlock );
    //     var compiledLambda = lambda.Compile();
    //     var result = await compiledLambda();
    //
    //     // Assert
    //     Assert.AreEqual( 3.3, result );
    // }

}
