using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.Expressions.Transformation;
using Hyperbee.Expressions.Transformation.Transitions;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class LoweringVisitorTests
{
    static int Test( int a, int b ) => a + b;
    static async Task<int> TestAsync( int a, int b ) => await Task.FromResult( a + b );

    public static MethodInfo GetMethod( string name ) => typeof( LoweringVisitorTests ).GetMethod( name, BindingFlags.Static | BindingFlags.NonPublic );


    [TestMethod]
    public void Lowering_ShouldHaveSingleNode_WhenAssigningVariable()
    {
        // Arrange
        var varExpr = Variable( typeof( int ), "x" );
        var assignExpr = Assign( varExpr, Add( varExpr, Constant( 2 ) ) );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( [varExpr], assignExpr );

        // Assert
        AssertTransition.AssertResult( result, nodes: 1, variables: 1 );

        AssertTransition.AssertLabel( result.Scopes[0].Nodes[0].NodeLabel, "ST_0000", typeof( void ) );
        AssertTransition.AssertFinal( result.Scopes[0].Nodes[0] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WhenBlockHasMultipleAwaits()
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
        AssertTransition.AssertLabel( result.Scopes[0].Nodes[0].NodeLabel, "ST_0000", typeof( void ) );

        var firstBefore = AssertTransition.AssertAwait( result.Scopes[0].Nodes[0].Transition, "ST_0002" );
        var firstAfter = AssertTransition.AssertAwaitResult( firstBefore.Transition, "ST_0001" );
        var secondBefore = AssertTransition.AssertAwait( firstAfter.Transition, "ST_0004" );
        var secondAfter = AssertTransition.AssertAwaitResult( secondBefore.Transition, "ST_0003" );
        AssertTransition.AssertFinal( secondAfter );

        AssertTransition.AssertFinal( result.Scopes[0].Nodes[3] );

    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithIfThen()
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
        AssertTransition.AssertLabel( result.Scopes[0].Nodes[0].NodeLabel, "ST_0000", typeof( void ) );

        var (ifThenTrue, ifThenFalse) =
            AssertTransition.AssertConditional( result.Scopes[0].Nodes[0].Transition, "ST_0002", "ST_0001" );
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
    public void Lowering_ShouldNotGatherVariables_WhenUsedWithinBlock()
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
        AssertTransition.AssertResult( result, nodes: 1, variables: 0 );
        AssertTransition.AssertFinal( result.Scopes[0].Nodes[0] );
    }

    [TestMethod]
    public void Lowering_ShouldNotGatherVariables_WhenUsedWithinNestedBlocks()
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
        AssertTransition.AssertResult( result, nodes: 1, variables: 0 );
        AssertTransition.AssertFinal( result.Scopes[0].Nodes[0] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithConditionalParameters()
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
        AssertTransition.AssertResult( result, nodes: 4 );
        AssertTransition.AssertFinal( result.Scopes[0].Nodes[1] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithNestedSwitch()
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
        AssertTransition.AssertResult( result, nodes: 10, variables: 2 );

        AssertTransition.AssertSwitch( result.Scopes[0].Nodes[0].Transition,
            defaultLabel: "ST_0002",
            "ST_0003", "ST_0004", "ST_0009" );

        AssertTransition.AssertSwitch( result.Scopes[0].Nodes[4].Transition,
            defaultLabel: "ST_0006",
            "ST_0007", "ST_0008" );

        AssertTransition.AssertFinal( result.Scopes[0].Nodes[1] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithSwitch()
    {
        var gotoLabel = Label( "goto" );
        var switchBlock = Block(
            Constant( "before switch" ),
            Switch(
                Constant( "switchTest" ),
                SwitchCase( Goto( gotoLabel ), Constant( "TestValue1" ) ),
                SwitchCase( Goto( gotoLabel ), Constant( "TestValue1" ) ),
                SwitchCase( Goto( gotoLabel ), Constant( "TestValue3" ) )
            ),
            Label( gotoLabel )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( switchBlock );

        // Assert
        AssertTransition.AssertResult( result, nodes: 5, variables: 0 );

        AssertTransition.AssertSwitch( result.Scopes[0].Nodes[0].Transition,
            defaultLabel: null,
            "ST_0002", "ST_0003", "ST_0004" );

        AssertTransition.AssertFinal( result.Scopes[0].Nodes[1] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithDefaultSwitch()
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
        AssertTransition.AssertResult( result, nodes: 6, variables: 1 );

        AssertTransition.AssertSwitch( result.Scopes[0].Nodes[0].Transition,
            defaultLabel: "ST_0002",
            "ST_0003", "ST_0004", "ST_0005" );

        AssertTransition.AssertFinal( result.Scopes[0].Nodes[1] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithSwitchAwaits()
    {
        var switchBlock = Block(
            Constant( "before switch" ),
            Switch(
                Await( Constant( Task.FromResult( "await switch Test" ) ) ),
                Constant( 1.1 ),
                SwitchCase( Constant( 1.2 ), Constant( "TestValue1" ) ),
                SwitchCase( Await( Constant( Task.FromResult( 1.3 ) ) ), Constant( "TestValue2" ) ),
                SwitchCase( Constant( 1.4 ), Constant( "TestValue3" ) )
            ),
            Constant( "after switch" )
        );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( switchBlock );

        // Assert
        AssertTransition.AssertResult( result, nodes: 10, variables: 5, jumps: 2 );

        AssertTransition.AssertSwitch( result.Scopes[0].Nodes[1].Transition,
            defaultLabel: "ST_0004",
            "ST_0005", "ST_0006", "ST_0009" );

        AssertTransition.AssertFinal( result.Scopes[0].Nodes[3] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithAwaitAssignments()
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
        AssertTransition.AssertResult( result, nodes: 5, variables: 4, jumps: 2 );
        AssertTransition.AssertFinal( result.Scopes[0].Nodes[3] );

    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithNestedBlockAndAwaitParameters()
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
        AssertTransition.AssertResult( result, nodes: 3, variables: 2, jumps: 1 );
        AssertTransition.AssertFinal( result.Scopes[0].Nodes[1] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithMethodAwaits()
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
        AssertTransition.AssertResult( result, nodes: 3, variables: 2, jumps: 1 );

        AssertTransition.AssertAwait( result.Scopes[0].Nodes[0].Transition, "ST_0002" );
        AssertTransition.AssertFinal( result.Scopes[0].Nodes[1] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithMethodAwaitArguments()
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
        AssertTransition.AssertResult( result, nodes: 5, variables: 4, jumps: 2 );

        AssertTransition.AssertAwait( result.Scopes[0].Nodes[0].Transition, "ST_0002" );
        AssertTransition.AssertAwait( result.Scopes[0].Nodes[1].Transition, "ST_0004" );
        AssertTransition.AssertFinal( result.Scopes[0].Nodes[3] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithLoop()
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
        AssertTransition.AssertResult( result, nodes: 6, variables: 1 );

        AssertTransition.AssertLoop( result.Scopes[0].Nodes[0].Transition, "ST_0002" );

        AssertTransition.AssertGoto( result.Scopes[0].Nodes[3].Transition, "ST_0002" );
        AssertTransition.AssertGoto( result.Scopes[0].Nodes[4].Transition, "ST_0001" );
        AssertTransition.AssertGoto( result.Scopes[0].Nodes[5].Transition, "ST_0002" );

        AssertTransition.AssertFinal( result.Scopes[0].Nodes[1] );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithTryCatch()
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
        AssertTransition.AssertResult( result, nodes: 4, variables: 3 );

        AssertTransition.AssertTryCatch( result.Scopes[0].Nodes[0].Transition, null,
            typeof( Exception ),
            typeof( ArgumentException ) );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithTryCatchFinally()
    {
        // Arrange
        var tryCatchExpr = Block(
            Constant( "before try" ),
            TryCatchFinally(
                Block(
                    Constant( "try body before exception" ),
                    Throw( Constant( new Exception( "Test Exception" ) ) ),
                    Constant( "try body after exception" ) ),
                Block(
                    Constant( "finally block" ) ),
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
        AssertTransition.AssertResult( result, nodes: 5, variables: 3 );

        AssertTransition.AssertTryCatch( result.Scopes[0].Nodes[0].Transition, "ST_0002",
            typeof( Exception ),
            typeof( ArgumentException ) );
    }

    [TestMethod]
    public void Lowering_ShouldBranch_WithComplexConditions()
    {
        // Arrange
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

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( ifThenElseExpr );

        // Assert
        AssertTransition.AssertResult( result, nodes: 17, variables: 7, jumps: 3 );
        AssertTransition.AssertFinal( result.Scopes[0].Nodes[3] );
    }

    public static class AssertTransition
    {

        public static class TransitionType
        {
            public static Type Goto => typeof( GotoTransition );
            public static Type Await => typeof( AwaitTransition );
            public static Type AwaitResult => typeof( AwaitResultTransition );
            public static Type Conditional => typeof( ConditionalTransition );
            public static Type Loop => typeof( LoopTransition );
            public static Type Switch => typeof( SwitchTransition );
            public static Type TryCatch => typeof( TryCatchTransition );
        }

        public static void AssertResult( LoweringResult result, int nodes = 0, int variables = 0, int jumps = 0 )
        {
            Assert.AreEqual( nodes, result.Scopes[0].Nodes.Count );
            Assert.AreEqual( variables, result.Variables.Length );
            Assert.AreEqual( jumps, result.Scopes[0].JumpCases.Count );
        }

        public static NodeExpression AssertGoto( Transition transition, string labelName )
        {
            Assert.AreEqual( TransitionType.Goto, transition.GetType() );
            var gotoTransition = (GotoTransition) transition;
            Assert.AreEqual( labelName, gotoTransition.TargetNode.NodeLabel.Name );
            return gotoTransition.TargetNode;
        }

        public static NodeExpression AssertAwait( Transition transition, string completionLabel )
        {
            Assert.AreEqual( TransitionType.Await, transition.GetType() );
            var awaitTransition = (AwaitTransition) transition;
            Assert.AreEqual( completionLabel, awaitTransition.CompletionNode.NodeLabel.Name );
            return awaitTransition.CompletionNode;
        }

        public static NodeExpression AssertAwaitResult( Transition transition, string labelName )
        {
            Assert.AreEqual( TransitionType.AwaitResult, transition.GetType() );
            var awaitTransition = (AwaitResultTransition) transition;
            Assert.AreEqual( labelName, awaitTransition.TargetNode.NodeLabel.Name );
            return awaitTransition.TargetNode;
        }

        public static NodeExpression AssertLoop( Transition transition, string labelName )
        {
            Assert.AreEqual( TransitionType.Loop, transition.GetType() );
            var loopTransition = (LoopTransition) transition;
            Assert.AreEqual( labelName, loopTransition.BodyNode.NodeLabel.Name );
            return loopTransition.BodyNode;
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

        public static void AssertTryCatch(
            Transition transition,
            string finalLabel,
            params Type[] catchTests )
        {
            Assert.AreEqual( TransitionType.TryCatch, transition.GetType() );
            var tryCatchTransition = (TryCatchTransition) transition;

            Assert.AreEqual( catchTests.Length, tryCatchTransition.CatchBlocks.Count );
            for ( var i = 0; i < tryCatchTransition.CatchBlocks.Count; i++ )
            {
                Assert.AreEqual( catchTests[i], tryCatchTransition.CatchBlocks[i].Handler.Test );
            }

            if ( finalLabel != null || tryCatchTransition.FinallyNode != null )
            {
                Assert.AreEqual( finalLabel, tryCatchTransition.FinallyNode.NodeLabel.Name );
            }
        }

        public static void AssertSwitch(
            Transition transition,
            string defaultLabel,
            params string[] caseLabels )
        {
            Assert.AreEqual( TransitionType.Switch, transition.GetType() );
            var switchTransition = (SwitchTransition) transition;

            Assert.AreEqual( caseLabels.Length, switchTransition.CaseNodes.Count );
            for ( var i = 0; i < switchTransition.CaseNodes.Count; i++ )
            {
                Assert.AreEqual( caseLabels[i], switchTransition.CaseNodes[i].Body.NodeLabel.Name );
            }

            if ( defaultLabel != null || switchTransition.DefaultNode != null )
            {
                Assert.AreEqual( defaultLabel, switchTransition.DefaultNode.NodeLabel.Name );
            }
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
