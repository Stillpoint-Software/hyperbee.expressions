using System.Reflection;
using static System.Linq.Expressions.Expression;
using static Hyperbee.AsyncExpressions.AsyncExpression;

namespace Hyperbee.AsyncExpressions.Tests;

[TestClass]
public class GotoTransformerVisitorTests
{
    static int Test( int a, int b ) => a + b;
    static async Task<int> TestAsync( int a, int b ) => await Task.FromResult( a + b );

    [TestMethod]
    public void GotoTransformer_WithBodyAwaits()
    {
        var blockAwaits = Block(
            Constant( "before await1" ),
            Await( Constant( Task.FromResult( "await1" ) ) ),
            Constant( "after await1" ),

            Constant( "before await2" ),
            Await( Constant( Task.FromResult( "await2" ) ) ),
            Constant( "after await2" ),

            Constant( "before await3" ),
            Await( Constant( Task.FromResult( "await3" ) ) ),
            Constant( "after await3" )
        );

        // Act
        var transformer = new GotoTransformerVisitor();
        transformer.Transform( blockAwaits );

        // Assert
        transformer.PrintStateMachine();
    }

    [TestMethod]
    public void GotoTransformer_WithIfThen()
    {
        var ifThenElseExpr = Block(
            Constant( 0 ),
            IfThen(
                Constant( true ),
                Block(
                    Constant( "before nested if" ),
                    IfThenElse( Constant( false ), Constant( 1.1 ), Block( Constant( 1.2 ), Constant( 1.3 ) ) ),
                    Constant( "after nested if" )
                ) ),
            Constant( 2 )
        );

        // Act
        var transformer = new GotoTransformerVisitor0();
        transformer.Transform( ifThenElseExpr );

        // Assert
        transformer.PrintStateMachine();
    }

    [TestMethod]
    public void GotoTransformer_WithSwitch()
    {
        var switchBlock = Block(
            Constant( "before switch" ),
            Switch(
                Constant( "switchTest" ),
                Constant( 1.1 ),
                [
                    SwitchCase( Constant( 1.2 ), Constant( "TestValue1" ) ),
                    SwitchCase( Constant( 1.3 ), Constant( "TestValue1" ) ),
                    SwitchCase( Constant( 1.4 ), Constant( "TestValue3" ) )
                ]
            ),
            Constant( "after switch" )
        );

        // Act
        var transformer = new GotoTransformerVisitor0();
        transformer.Transform( switchBlock );

        // Assert
        transformer.PrintStateMachine();
    }

    [TestMethod]
    public void GotoTransformer_WithSwitchAwaits()
    {
        var switchBlock = Block(
            Constant( "before switch" ),
            Switch(
                Await( Constant( Task.FromResult( "await switch Test" ) ) ),
                Constant( 1.1 ),
                [
                    SwitchCase( Constant( 1.2 ), Constant( "TestValue1" ) ),
                    SwitchCase( Constant( 1.3 ), Await( Constant( Task.FromResult( "await switch value" ) ) ) ),
                    SwitchCase( Constant( 1.4 ), Constant( "TestValue3" ) )
                ]
            ),
            Constant( "after switch" )
        );

        // Act
        var transformer = new GotoTransformerVisitor0();
        transformer.Transform( switchBlock );

        // Assert
        transformer.PrintStateMachine();
    }

    [TestMethod]
    public void GotoTransformer_WithMethodAwaits()
    {
        // Arrange
        var methodInfo = GetType()
            .GetMethod( nameof( TestAsync ), BindingFlags.Static | BindingFlags.NonPublic )!;

        var callExpr = Block(
            Constant( "before await" ),
            Await( Call( methodInfo,
                Constant( 1 ),
                Constant( 2 ) ) ),
            Constant( "after await" )
        );

        // Act
        var transformer = new GotoTransformerVisitor0();
        transformer.Transform( callExpr );

        // Assert
        transformer.PrintStateMachine();
    }

    [TestMethod]
    public void GotoTransformer_WithMethodAwaitArguments()
    {
        // Arrange
        var methodInfo = GetType()
            .GetMethod( nameof( Test ), BindingFlags.Static | BindingFlags.NonPublic )!;

        var callExpr = Block( Call(
            methodInfo,
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( Constant( Task.FromResult( 2 ) ) ) )
        );

        // Act
        var transformer = new GotoTransformerVisitor0();
        transformer.Transform( callExpr );

        // Assert
        transformer.PrintStateMachine();
    }

    [TestMethod]
    public void GotoTransformer_WithGoto()
    {
        // Arrange
        var gotoLabel = Label( "gotoLabel" );
        var gotoExpr = Block(

            Constant( "before goto" ),
            Goto( gotoLabel ),
            Constant( "after goto" ),

            Label( gotoLabel ),
            Constant( "after label" )
        );

        // Act
        var transformer = new GotoTransformerVisitor0();
        transformer.Transform( gotoExpr );

        // Assert
        transformer.PrintStateMachine();
    }

    [TestMethod]
    public void GotoTransformer_WithLoop()
    {
        // Arrange
        var breakLabel = Label( typeof( bool ), "breakLoop" );
        var continueLabel = Label( "continueLoop" );
        var whileBlockExpr = Block(
            Constant( "before loop" ),
            Loop(
                Block(
                    Constant( "loop body before" ),
                    IfThenElse( Constant( true ),
                        Break( breakLabel, Constant( false ) ),
                        Continue( continueLabel )
                    ),
                    Constant( "loop body after" ) ),
                breakLabel,
                continueLabel ),
            Constant( "after loop" )
        );

        // Act
        var transformer = new GotoTransformerVisitor0();
        transformer.Transform( whileBlockExpr );
        
        // Assert
        transformer.PrintStateMachine();
    }

    [TestMethod]
    public void GotoTransformer_WithTryCatch()
    {
        // Arrange
        var tryCatchExpr = Block(
            Constant( "before try" ),
            TryCatch(
                Block(
                    Constant( "try body before exception" ),
                    Throw( Constant( new Exception( "Test Exception" ) ) ),
                    Constant( "try body after exception" ) ),
                Catch( typeof(Exception), Block(
                    Constant( "Exception catch body" ) ) ),
                Catch( typeof(ArgumentException), Block(
                    Constant( "ArgumentException catch body" ) ) ) ),
            Constant( "after try" )
        );

        // Act
        var transformer = new GotoTransformerVisitor0();
        transformer.Transform( tryCatchExpr );

        // Assert
        transformer.PrintStateMachine();
    }
        
    [TestMethod]
    public void GotoTransformer_WithComplexConditions()
    {
        var ifThenElseExpr = Block(
            Constant( 0 ),
            IfThen(
                Await( Constant( Task.FromResult( true ) ) ),
                Block(
                    Constant( "before await" ),
                    Await( Constant( Task.FromResult( "await" ) ) ),
                    Constant( "before if" ),
                    IfThenElse( Constant( false ), Constant( 1.1 ), Block( Constant( 1.2 ), Constant( 1.3 ) ) ),
                    Constant( "after if" ),
                    Switch(
                        Await( Constant( Task.FromResult( "await switch Test" ) ) ),
                        Constant( 3.1 ),
                        [
                            SwitchCase( Constant( 3.2 ), Constant( "TestValue1" ) ),
                            SwitchCase( Constant( 3.3 ), Await( Constant( Task.FromResult( "await switch value" ) ) ) ),
                            SwitchCase( Constant( 3.4 ), Constant( "TestValue3" ) )
                        ]
                    ),
                    Constant( 4 )
                ) ),
            Constant( 5 )
        );

        var transformer = new GotoTransformerVisitor0();
        transformer.Transform( ifThenElseExpr );

        transformer.PrintStateMachine();
    }

}
