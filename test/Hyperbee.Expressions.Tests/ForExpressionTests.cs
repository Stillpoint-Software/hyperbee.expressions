﻿using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ForExpressionTests
{

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ForExpression_ShouldLoopCorrectly( CompilerType compiler )
    {
        // Arrange
        var counter = Variable( typeof( int ), "counter" );
        var counterInit = Assign( counter, Constant( 0 ) );

        var condition = LessThan( counter, Constant( 5 ) );
        var iteration = PostIncrementAssign( counter );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] );
        var body = Call( writeLineMethod!, counter );

        var forExpr = For( counterInit, condition, iteration, body );

        // Wrap in a block to capture the counter value
        var block = Block(
            [counter],
            forExpr,
            counter // Return counter
        );

        // Act
        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 5, result, "Counter should be 5 after the loop finishes." );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ForExpression_ShouldSupportCustomBreak( CompilerType compiler )
    {
        // Arrange
        var writeLine = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] )!;

        var counter = Variable( typeof( int ), "counter" );
        var counterInit = Assign( counter, Constant( 0 ) );

        var condition = LessThan( counter, Constant( 10 ) );
        var iteration = PostIncrementAssign( counter );

        var forExpr = For( counterInit, condition, iteration, ( breakLabel, _ ) =>
            IfThenElse(
                Equal( counter, Constant( 5 ) ),
                Break( breakLabel ), // break when counter == 5
                Call( writeLine, counter )
        ) );

        var block = Block(
            [counter],
            forExpr,
            counter
        );

        // Act
        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( 5, result, "Loop should break when counter reaches 5." );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ForExpression_ShouldIterateOverCollection_WithYields( CompilerType compiler )
    {
        // Arrange
        var counter = Variable( typeof( int ), "counter" );

        var forEachExpr = BlockEnumerable(
            [counter],
            For(
                Assign( counter, Constant( 0 ) ),
                LessThan( counter, Constant( 5 ) ),
                PostIncrementAssign( counter ),
                YieldReturn( counter )
            )
        );

        // Act
        var lambda = Lambda<Func<IEnumerable<int>>>( forEachExpr );
        var compiledLambda = lambda.Compile( compiler );

        var results = compiledLambda().ToArray();

        // Assert:
        Assert.AreEqual( 5, results.Length );
        Assert.AreEqual( 0, results[0] );
        Assert.AreEqual( 1, results[1] );
        Assert.AreEqual( 2, results[2] );
        Assert.AreEqual( 3, results[3] );
        Assert.AreEqual( 4, results[4] );
    }
}
