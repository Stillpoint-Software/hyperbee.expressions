using System.Linq.Expressions;
using System.Reflection;
using static System.Linq.Expressions.Expression;
using static Hyperbee.AsyncExpressions.AsyncExpression;

namespace Hyperbee.AsyncExpressions.Tests;

[TestClass]
public class AwaitSplitVisitorTests
{
    static int Test( int a, int b ) => a + b;

    [TestMethod]
    public async Task ShouldFindAwait_WhenUsingCall()
    {
        // Arrange
        var methodInfo = GetType()
            .GetMethod( nameof(Test), BindingFlags.Static | BindingFlags.NonPublic )!;

        var callExpr = Call(
            methodInfo,
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( Constant( Task.FromResult( 2 ) ) ) );

        // Act
        var visitor = new AwaitSplitVisitor();
        var updated = visitor.Visit( callExpr );

        // Assert
        Assert.AreEqual( 5, (updated as BlockExpression)!.Expressions.Count );

        var lambda = Lambda<Func<Task<int>>>( BlockAsync( callExpr ) );
        var result = await lambda.Compile()();
        Assert.AreEqual( 3, result );
    }

    [TestMethod]
    public async Task ShouldFindAwait_WhenUsingAssign()
    {
        // Arrange
        var varExpr = Variable( typeof(int), "x" );
        var assignExpr = Assign( varExpr, Await( Constant( Task.FromResult( 1 ) ) ) );

        // Act
        var visitor = new AwaitSplitVisitor();
        var updated = visitor.Visit( assignExpr );

        // Assert
        var lambda = Lambda<Func<Task>>( BlockAsync( assignExpr ) );
        await lambda.Compile()();
    }

    [TestMethod]
    public async Task ShouldFindAwait_WhenUsingConditions()
    {
        // Arrange
        var ifThenExpr = IfThen(
            Await( Constant( Task.FromResult( true ) ) ),
            Await( Constant( Task.FromResult( 1 ) ) ) );

        // Act
        var visitor = new AwaitSplitVisitor();
        var updated = visitor.Visit( ifThenExpr );

        // Assert
        Assert.AreEqual( 3, (updated as BlockExpression)!.Expressions.Count );

        var lambda = Lambda<Func<Task>>( BlockAsync( ifThenExpr ) );
        await lambda.Compile()();
    }


    [TestMethod]
    public async Task ShouldFindAwait_WhenComplex()
    {
        // Arrange
        var returnLabel = Label( typeof(int), "exit" );
        var ifThenElseBlockExpr = Block(
            IfThenElse( Await( Constant( Task.FromResult( true ) ) ),
                Block(
                    Constant( "hello" ),
                    Constant( "world" ),
                    Return( returnLabel, Await( Constant( Task.FromResult( 1 ) ) ) ) ),
                Return( returnLabel, Await( Constant( Task.FromResult( 2 ) ) ) ) ),
            Label( returnLabel, Constant( 3 ) )
        );

        /* Should be...
           Block(
               Block(
                   Constant( Task.FromResult( true ) )
               ),
               Block(
                   AwaitResult(),  //var __awaitResult<0> == _awaiter<0>.GetResult()
                   IfThenElse( __awaitResult<0>,
                       Block(
                           Block(
                                ...sync stuff
                               Await( Constant( Task.FromResult( 1 ) )
                           ),
                           Block(
                               AwaitResult(), // create and set a variable call __await_var1
                               Return( returnLabel, __awaitResult<1> )
                           )
                       ),
                       Block(
                           Block(
                               Await( Constant( Task.FromResult( 2 ) )
                           ),
                           Block(
                               AwaitResult(), // create and set a variable call __await_var2
                               Return( returnLabel, __awaitResult<2> )
                           )
                       )
                    ),
                    Label( returnLabel, Constant( 3 ) )
                )
         )
        */

        // Act
        var visitor = new AwaitSplitVisitor();
        var updated = visitor.Visit( ifThenElseBlockExpr );

        // Assert
        Assert.AreEqual( 2, (updated as BlockExpression)!.Expressions.Count );

        var lambda = Lambda<Func<Task<int>>>( BlockAsync( ifThenElseBlockExpr ) );
        var result = await lambda.Compile()();
        Assert.AreEqual( 2, result );
    }

    [TestMethod]
    public void AwaitExpressionSplitter_WithSimpleCondition()
    {
        // Arrange
        var returnLabel = Label( typeof(int), "exit" );
        var ifThenElseBlockExpr = Block(
            IfThenElse( Await( Constant( Task.FromResult( true ) ) ),
                Block(
                    Constant( "hello" ),
                    Constant( "world" ),
                    Return( returnLabel, Await( Constant( Task.FromResult( 1 ) ) ) ) ),
                Return( returnLabel, Await( Constant( Task.FromResult( 2 ) ) ) ) ),
            Label( returnLabel, Constant( 3 ) )
        );

        var splitterVisitor = new AwaitExpressionSplitter();

        splitterVisitor.Visit( ifThenElseBlockExpr );
        var count = splitterVisitor.StateBlocks.Count;

        Assert.AreEqual( 5, count );
    }

    [TestMethod]
    public void AwaitExpressionSplitter_WithNestedCondition()
    {
        // Arrange
        var returnLabel = Label( typeof(int), "exit" );
        var ifThenElseBlockExpr = Block(
            IfThenElse( Await( Constant( Task.FromResult( true ) ) ),
                IfThen( Constant( true ),
                    Block(
                        Constant( "hello" ),
                        Constant( "world" ),
                        Return( returnLabel, Await( Constant( Task.FromResult( 1 ) ) ) ) ) ),
                Return( returnLabel, Await( Constant( Task.FromResult( 2 ) ) ) ) ),
            Label( returnLabel, Constant( 3 ) )
        );

        /*
        case 0: IfThenElse
        case 1: Await( Constant( Task.FromResult( true ) ) )


        // The idea:
        if ( await Task.CompletedTask( true ) )  // AWAIT 1
        {
            // move 1
            if( true )
            {
                Console.WriteLine("hello");
                Console.WriteLine("world");
                return await Task.FromResult(1);  // AWAIT 2
            }

            return await Task.FromResult(2); // AWAIT 3
        }
        else
        {
            // move 2
            //default
        }

        // The rewrite:
        var _resumeState = -1;
        switch ( _resumeState )
        {
            case 1:
                goto awaiter1;
            case 2:
                goto awaiter2;
            default:
            {
                // AWAIT 1 BEGIN
                var _awaiter1 = Task.CompletedTask( true ).GetAwaiter();
                if ( !_awaiter1.IsCompleted )
                {
                    _resumeState = 1;
                    // _builder.UnsafeNotifyCompletion( awaiter, this );
                    // return;
                }

                awaiter1:
                var result1 = _awaiter1.GetResult();
                // AWAIT 1 END

                if ( result1 )
                {
                    if ( true )
                    {
                        Console.WriteLine( "hello" );
                        Console.WriteLine( "world" );


                        // AWAIT 2 BEGIN
                        var _awaiter2 = Task.FromResult( 1 ).GetAwaiter();
                        if ( !_awaiter2.IsCompleted )
                        {
                            _resumeState = 2;
                            // _builder.UnsafeNotifyCompletion( awaiter, this );
                            // return;
                        }

                        awaiter2:
                        var result2 = _awaiter1.GetResult();
                        // AWAIT 1 END
                        return result2;
                    }

                    return await Task.FromResult( 2 );
                }

                }
        }
        */

        var splitterVisitor = new AwaitExpressionSplitter();

        splitterVisitor.Visit( ifThenElseBlockExpr );
        var count = splitterVisitor.StateBlocks.Count;

        Assert.AreEqual( 6, count );
    }

    [TestMethod]
    public void AwaitExpressionSplitter_WithSwitchCase()
    {
        // Arrange
        var returnLabel = Label( typeof(int), "exit" );
        var switchBlockExpr = Block(
            Switch( Constant( 1 ),
                SwitchCase(
                    Block(
                        Constant( "hello" ),
                        Constant( "world" ),
                        Return( returnLabel, Await( Constant( Task.FromResult( 1 ) ) ) ) ),
                    Constant( 1 ) ),
                SwitchCase(
                    Return( returnLabel, Await( Constant( Task.FromResult( 2 ) ) ) ),
                    Constant( 2 ) ) ),
            Label( returnLabel, Constant( 3 ) )
        );

        var splitterVisitor = new AwaitExpressionSplitter();

        splitterVisitor.Visit( switchBlockExpr );
        var count = splitterVisitor.StateBlocks.Count;

        Assert.AreEqual( 5, count );
    }

    [TestMethod]
    public void AwaitExpressionSplitter_WithWhileLoop()
    {
        // Arrange
        var breakLabel = Label( typeof(int), "breakLoop" );
        var continueLabel = Label( "continueLoop" );
        var whileBlockExpr = Block(
            Loop(
                Block(
                    IfThenElse( Await( Constant( Task.FromResult( true ) ) ),
                        Break( breakLabel, Await( Constant( Task.FromResult( 1 ) ) ) ),
                        Continue( continueLabel )
                    ),
                    Constant( "hello" ),
                    Constant( "world" ) ),
                breakLabel,
                continueLabel )
        );

        var splitterVisitor = new AwaitExpressionSplitter();

        splitterVisitor.Visit( whileBlockExpr );
        var count = splitterVisitor.StateBlocks.Count;

        Assert.AreEqual( 7, count );
    }

    [TestMethod]
    public void AwaitExpressionSplitter_WithTryCatch()
    {
        // Arrange
        var returnSuccessLabel = Label( typeof(int), "successExit" );
        var returnErrorLabel = Label( typeof(int), "errorExit" );
        var tryCatchBlock = Block(
            TryCatchFinally(
                Block(
                    Constant( "hello" ),
                    Constant( "world" ),
                    Throw( Constant( new Exception() ), typeof(Exception) ),
                    Return( returnSuccessLabel, Constant( 1 ) ) ),
                Block( Return( returnSuccessLabel, Constant( 2 ) ) ),
                Catch(
                    Parameter( typeof(Exception), "ex" ),
                    Block(
                        Constant( "error" ),
                        Return( returnErrorLabel, Await( Constant( Task.FromResult( 3 ) ) ) ) ) ) ),
            Label( returnSuccessLabel, Constant( 4 ) ),
            Label( returnErrorLabel, Constant( 5 ) )
        );

        var splitterVisitor = new AwaitExpressionSplitter();

        splitterVisitor.Visit( tryCatchBlock );
        var count = splitterVisitor.StateBlocks.Count;

        Assert.AreEqual( 7, count );
    }

    [TestMethod]
    public void AwaitExpressionSplitter_WithMultipleAwaitBlocks()
    {
        // Arrange
        var awaitBlocks = Block(
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( Constant( Task.FromResult( 2 ) ) ),
            Await( Constant( Task.FromResult( 3 ) ) )
        );

        var splitterVisitor = new AwaitExpressionSplitter();

        splitterVisitor.Visit( awaitBlocks );
        var count = splitterVisitor.StateBlocks.Count;

        Assert.AreEqual( 10, count );
    }

    [TestMethod]
    public void AwaitExpressionSplitter_WithMixedAwaitBlocks()
    {
        // Arrange
        var awaitBlocks = Block(
            Constant( 0 ),
            Await( Constant( Task.FromResult( 1.0 ) ) ),
            Constant( 1.1 ),
            Constant( 1.2 ),
            Await( Constant( Task.FromResult( 2 ) ) ),
            Constant( 2.1 ),
            Await( Constant( Task.FromResult( 3 ) ) )
        );

        var splitterVisitor = new AwaitExpressionSplitter();

        splitterVisitor.Visit( awaitBlocks );
        var count = splitterVisitor.StateBlocks.Count;

        Assert.AreEqual( 10, count );
    }
}

