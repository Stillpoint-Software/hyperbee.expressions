﻿using System.Reflection;
using Hyperbee.AsyncExpressions.Transformation;
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
    public void GotoTransformer_CollectExpressions()
    {
        // Arrange
        var varExpr = Variable( typeof( int ), "x" );
        var assignExpr = Assign( varExpr, Add( varExpr, Constant( 2 ) ) );

        // Act
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( assignExpr );

        // Assert
        Console.WriteLine( result.DebugView );
    }


    [TestMethod]
    public void GotoTransformer_WithBodyAwaits()
    {
        // Arrange
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
        var visitor = new LoweringVisitor();
        var result = visitor.Transform( blockAwaits );

        // Assert
        Console.WriteLine( result.DebugView );
    }

    [TestMethod]
    public void GotoTransformer_WithIfThen()
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
        Console.WriteLine( result.DebugView );
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
}