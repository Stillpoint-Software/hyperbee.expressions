using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.AsyncExpressions.Transformation;
using Hyperbee.AsyncExpressions.Transformation.Transitions;
using static System.Linq.Expressions.Expression;
using static Hyperbee.AsyncExpressions.AsyncExpression;

namespace Hyperbee.AsyncExpressions.Tests;

[TestClass]
public class LoweringVisitorTests
{
    static int Test( int a, int b ) => a + b;
    static async Task<int> TestAsync( int a, int b ) => await Task.FromResult( a + b );

    public static MethodInfo GetMethod( string name ) => typeof(LoweringVisitorTests).GetMethod( name, BindingFlags.Static | BindingFlags.NonPublic );


    [TestMethod]
    public void Transform_ShouldHaveSingleSingle_WhenAssigningVariable()
    {
        // Arrange
        var varExpr = Variable( typeof( int ), "x" );
        var assignExpr = Assign( varExpr, Add( varExpr, Constant( 2 ) ) );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( [varExpr], assignExpr );

        // Assert
        AssertTransition.AssertResult( result, nodes: 1, variables: 1 );

        AssertTransition.AssertLabel( result.Nodes[0].NodeLabel, "ST_0000", typeof(void) );
    }

    [TestMethod]
    public void Transform_ShouldSplitAwaits_WhenBlockHasMultipleAwaits()
    {
        // Arrange
        var blockAwaits = Block(
            Constant( "before await1" ),
            Await( Constant( Task.FromResult( "await1" ) ) ),
            Constant( "after await1" ),

            Constant( "before await2" ),
            Await( Constant( Task.FromResult( "await2" ) ) ),
            Constant( "after await2" )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( blockAwaits );

        // Assert
        AssertTransition.AssertResult( result, nodes: 5, variables: 4, jumps: 2 );
        AssertTransition.AssertLabel( result.Nodes[0].NodeLabel, "ST_0000", typeof(void) );

        var firstBefore = AssertTransition.AssertAwait( result.Nodes[0].Transition, "ST_0002" );
        var firstAfter = AssertTransition.AssertAwaitResult( firstBefore.Transition, "ST_0001" );
        var secondBefore = AssertTransition.AssertAwait( firstAfter.Transition, "ST_0004" );
        var secondAfter = AssertTransition.AssertAwaitResult( secondBefore.Transition, "ST_0003" );
        AssertTransition.AssertFinal( secondAfter );

    }

    [TestMethod]
    public void Transform_ShouldBranchAndMerge_WithIfThen()
    {
        // Arrange
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
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( ifThenElseExpr );

        // Assert
        AssertTransition.AssertResult( result, nodes: 6 );
        AssertTransition.AssertLabel( result.Nodes[0].NodeLabel, "ST_0000", typeof( void ) );

        var (ifThenTrue, ifThenFalse) =
            AssertTransition.AssertConditional( result.Nodes[0].Transition, "ST_0002", "ST_0001" );
        var (ifThenElseTrue, ifThenElseFalse) =
            AssertTransition.AssertConditional( ifThenTrue.Transition, "ST_0004", "ST_0005" );
        Assert.IsNull( ifThenFalse.Transition ); // No else block

        Assert.AreEqual( 1, ifThenElseTrue.Expressions.Count );
        var joinNode = AssertTransition.AssertGoto( ifThenElseTrue.Transition, "ST_0003" );
        var finalNodes = AssertTransition.AssertGoto( joinNode.Transition, "ST_0001" );
        AssertTransition.AssertFinal( finalNodes );

        Assert.AreEqual( 2, ifThenElseFalse.Expressions.Count );
        joinNode = AssertTransition.AssertGoto( ifThenElseFalse.Transition, "ST_0003" );
        finalNodes = AssertTransition.AssertGoto( joinNode.Transition, "ST_0001" );
        AssertTransition.AssertFinal( finalNodes );
    }


    [TestMethod]
    public void GotoTransformer_WithParameters()
    {
        // Arrange
        var methodInfo = GetMethod( nameof( Test ) )!;

        var aParam = Parameter( typeof( int ), "a" );
        var bParam = Parameter( typeof( int ), "b" );
        var variables = Block(
            [aParam, bParam],
            Constant( "before parameters" ),
            Assign( aParam, Constant( 1 ) ),
            Assign( bParam, Constant( 2 ) ),
            Constant( "after parameters" ),
            Call( methodInfo, aParam, bParam )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( variables );

        // Assert
        Console.WriteLine( result.DebugView );
    }

    [TestMethod]
    public void GotoTransformer_WithNestedBlockParameters()
    {
        // Arrange
        var methodInfo = GetMethod( nameof( Test ) );

        var aParam = Parameter( typeof( int ), "a" );
        var b2Param = Parameter( typeof( int ), "b" );  // Same name, inner scope
        var childBlock = Block(
            [aParam, b2Param],
            Constant( "before nested parameters" ),
            Assign( aParam, Constant( 3 ) ),
            Assign( b2Param, Constant( 4 ) ),
            Constant( "after nested parameters" ),
            Call( methodInfo, aParam, b2Param )
        );

        var bParam = Parameter( typeof( int ), "b" );
        var variables = Block(
            [aParam, bParam],
            Constant( "before parameters" ),
            Assign( aParam, Constant( 1 ) ),
            Assign( bParam, Constant( 2 ) ),
            Constant( "after parameters" ),
            childBlock
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( variables );

        // Assert
        Console.WriteLine( result.DebugView );
    }

    [TestMethod]
    public void GotoTransformer_WithConditionalParameters()
    {
        // Arrange
        var methodInfo = GetMethod( nameof( Test ) );

        var aParam = Parameter( typeof( int ), "a" );
        var bParam = Parameter( typeof( int ), "b" );
        var conditionalParameters = Block(
            [aParam, bParam],
            Constant( "before if" ),
            IfThenElse( Constant( true ),
                Block(
                    Constant( "before nested parameters" ),
                    Assign( aParam, Constant( 3 ) ),
                    Assign( bParam, Constant( 4 ) ),
                    Constant( "after nested parameters" ),
                    Call( methodInfo, aParam, bParam )
                ),
                Block(
                    Constant( "before nested parameters" ),
                    Assign( aParam, Constant( 5 ) ),
                    Assign( bParam, Constant( 6 ) ),
                    Constant( "after nested parameters" ),
                    Call( methodInfo, aParam, bParam )
                )
            ),
            Constant( "after if" )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( conditionalParameters );

        // Assert
        Console.WriteLine( result.DebugView );
    }

    [TestMethod]
    public void GotoTransformer_WithNestedSwitch()
    {
        var switchBlock = Block(
            Constant( "before switch" ),
            Switch(
                Constant( "switchTest" ),
                Constant( 1.1 ),
                [
                    SwitchCase( Constant( 1.2 ), Constant( "TestValue1" ) ),
                    SwitchCase( Block(
                        Constant( "nested switch" ),
                        Switch(
                            Constant( "nestedSwitchTest" ),
                            Constant( 2.1 ),
                            [
                                SwitchCase( Constant( 2.2 ), Constant( "NestedTestValue1" ) ),
                                SwitchCase( Constant( 2.3 ), Constant( "NestedTestValue2" ) )
                            ]
                        ),
                        Constant( 1.3 )
                    ), Constant( "TestValue2" ) ),
                    SwitchCase( Constant( 1.4 ), Constant( "TestValue3" ) )
                ]
            ),
            Constant( "after switch" )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( switchBlock );

        // Assert
        Console.WriteLine( result.DebugView );
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
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( switchBlock );

        // Assert
        Console.WriteLine( result.DebugView );
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
                    SwitchCase( Await( Constant( Task.FromResult( 1.3 ) ) ), Constant( "TestValue2" ) ),
                    SwitchCase( Constant( 1.4 ), Constant( "TestValue3" ) )
                ]
            ),
            Constant( "after switch" )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( switchBlock );

        // Assert
        Console.WriteLine( result.DebugView );
    }

    [TestMethod]
    public void GotoTransformer_WithAwaitAssignments()
    {
        // Arrange
        var methodInfo = GetMethod( nameof( Test ) );

        var aParam = Parameter( typeof( int ), "a" );
        var bParam = Parameter( typeof( int ), "b" );
        var methodWithParameter = Block(
            [aParam, bParam],
            Constant( "before parameters" ),
            Assign( aParam, Await( Constant( Task.FromResult( 1 ) ) ) ),
            Assign( bParam, Await( Constant( Task.FromResult( 2 ) ) ) ),
            Constant( "after parameters" ),
            Call( methodInfo, aParam, bParam )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( methodWithParameter );

        // Assert
        Console.WriteLine( result.DebugView );
    }

    [TestMethod]
    public void GotoTransformer_WithNestedBlockAndAwaitParameters()
    {
        // Arrange
        var methodInfo = GetMethod( nameof( TestAsync ) );

        var aParam = Parameter( typeof( int ), "a" );
        var bParam = Parameter( typeof( int ), "b" );
        var methodWithParameter = Block(
            [aParam, bParam],
            Constant( "before parameters" ),
            Assign( aParam, Constant( 1 ) ),
            Assign( bParam, Constant( 2 ) ),
            Constant( "after parameters" ),
            Await( Call( methodInfo, aParam, bParam ) )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( methodWithParameter );

        // Assert
        Console.WriteLine( result.DebugView );
    }

    [TestMethod]
    public void GotoTransformer_WithMethodAwaits()
    {
        // Arrange
        var methodInfo = GetMethod( nameof( TestAsync ) );

        var callExpr = Block(
            Constant( "before await" ),
            Await( Call( methodInfo,
                Constant( 1 ),
                Constant( 2 ) ) ),
            Constant( "after await" )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( callExpr );

        // Assert
        Console.WriteLine( result.DebugView );
    }

    [TestMethod]
    public void GotoTransformer_WithMethodAwaitArguments()
    {
        // Arrange
        var methodInfo = GetMethod( nameof( Test ) );

        var callExpr = Block( Call(
            methodInfo,
            Await( Constant( Task.FromResult( 1 ) ) ),
            Await( Constant( Task.FromResult( 2 ) ) ) )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( callExpr );

        // Assert
        Console.WriteLine( result.DebugView );
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
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( whileBlockExpr );

        // Assert
        Console.WriteLine( result.DebugView );
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
                Catch( typeof( Exception ), Block(
                    Constant( "Exception catch body" ) ) ),
                Catch( typeof( ArgumentException ), Block(
                    Constant( "ArgumentException catch body" ) ) ) ),
            Constant( "after try" )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( tryCatchExpr );

        // Assert
        Console.WriteLine( result.DebugView );
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
                        Await( Constant( Task.FromResult( "AwaitTestValue2" ) ) ),
                        Constant( 3.1 ),
                        [
                            SwitchCase( Constant( 3.2 ), Constant( "TestValue1" ) ),
                            SwitchCase( Constant( 3.3 ), Await( Constant( Task.FromResult( "AwaitTestValue2" ) ) ) ),
                            SwitchCase( Constant( 3.4 ), Constant( "TestValue3" ) )
                        ]
                    ),
                    Constant( 4 )
                ) ),
            Constant( 5 )
        );

        var visitor = new LoweringVisitor();
        var result = visitor.Transform( ifThenElseExpr );

        Console.WriteLine( result.DebugView );
    }

    public static class AssertTransition
    {

        public static class TransitionType
        {
            public static Type Goto => typeof(GotoTransition);
            public static Type Await => typeof(AwaitTransition);
            public static Type AwaitResult => typeof(AwaitResultTransition);
            public static Type Conditional => typeof(ConditionalTransition);

        }

        public static void AssertResult( LoweringResult result, int nodes = 0, int variables = 0, int jumps = 0 )
        {
            Assert.AreEqual( nodes, result.Nodes.Count );
            Assert.AreEqual( variables, result.Variables.Count );
            Assert.AreEqual( jumps, result.JumpCases.Count );
        }

        public static NodeExpression AssertGoto( Transition transition, string labelName )
        {
            Assert.AreEqual( TransitionType.Goto, transition.GetType() );
            var gotoTransition = (GotoTransition) transition;
            Assert.AreEqual( labelName, gotoTransition.TargetNode.NodeLabel.Name );
            return gotoTransition.TargetNode;
        }

        public static NodeExpression AssertAwait( Transition transition, string labelName )
        {
            Assert.AreEqual( TransitionType.Await, transition.GetType() );
            var awaitTransition = (AwaitTransition) transition;
            Assert.AreEqual( labelName, awaitTransition.CompletionNode.NodeLabel.Name );
            return awaitTransition.CompletionNode;
        }

        public static NodeExpression AssertAwaitResult( Transition transition, string labelName )
        {
            Assert.AreEqual( TransitionType.AwaitResult, transition.GetType() );
            var awaitTransition = (AwaitResultTransition) transition;
            Assert.AreEqual( labelName, awaitTransition.TargetNode.NodeLabel.Name );
            return awaitTransition.TargetNode;
        }

        public static (NodeExpression IfTrueNode, NodeExpression IfFalseNode) AssertConditional(
            Transition transition,
            string trueLabelName,
            string falseLabelName )
        {
            Assert.AreEqual( TransitionType.Conditional, transition.GetType() );
            var conditionalTransition = (ConditionalTransition) transition;
            Assert.AreEqual( trueLabelName, conditionalTransition.IfTrue.NodeLabel.Name );
            Assert.AreEqual( falseLabelName, conditionalTransition.IfFalse.NodeLabel.Name );
            return (conditionalTransition.IfTrue, conditionalTransition.IfFalse);
        }

        public static void AssertLabel( LabelTarget label, string expectedName, Type expectedType )
        {
            Assert.AreEqual( expectedName, label.Name );
            Assert.AreEqual( expectedType, label.Type );
        }

        public static void AssertFinal( NodeExpression finalNode )
        {
            Assert.IsTrue( finalNode.Transition == null );
        }
    }
}
