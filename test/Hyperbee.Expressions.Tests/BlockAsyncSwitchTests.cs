﻿using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class BlockAsyncSwitchTests
{
    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInSwitchValue( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await in the switch value
        var switchValue = Await( AsyncHelper.Completer(
            Constant( completer ),
            Constant( 1 )
        ) );
        var block = BlockAsync(
            Switch(
                switchValue,
                Constant( 0 ),
                SwitchCase( Constant( 10 ), Constant( 1 ) ),
                SwitchCase( Constant( 20 ), Constant( 2 ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result ); // Awaited switch value should match the first case
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInDefaultBody( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Default case contains an awaited task
        var switchValue = Constant( 3 ); // No case matches this value
        var block = BlockAsync(
            Switch(
                switchValue,
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 99 )
                ) ), // Default body
                SwitchCase( Constant( 10 ), Constant( 1 ) ),
                SwitchCase( Constant( 20 ), Constant( 2 ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 99, result ); // Default body should return 99
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInSwitchCaseBody( CompleterType completer, CompilerType compiler )
    {
        // Arrange: One of the case bodies contains an awaited task
        var switchValue = Constant( 1 );
        var block = BlockAsync(
            Switch(
                switchValue,
                Constant( 0 ),
                SwitchCase(
                    Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 100 )
                    ) ),
                    Constant( 1 )
                ),
                SwitchCase( Constant( 200 ), Constant( 2 ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 100, result ); // Case 1 body should be awaited and return 100
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitInSwitchValueAndCaseBody( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Await both in switch value and case body
        var switchValue = Await( AsyncHelper.Completer( Constant( completer ), Constant( 2 ) ) );
        var block = BlockAsync(
            Switch(
                switchValue,
                Constant( 0 ),
                SwitchCase(
                    Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 50 )
                    ) ),
                    Constant( 2 )
                ),
                SwitchCase( Constant( 20 ), Constant( 3 ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 50, result ); // Case 2 body should be awaited and return 50
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithNestedSwitchesAndAwaits( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Outer and inner switch cases involve awaited tasks
        var switchValue = Constant( 1 );
        var nestedSwitch = Switch(
            Constant( 1 ),
            Constant( 0 ),
            SwitchCase(
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 30 )
                ) ),
                Constant( 1 )
            ),
            SwitchCase( Constant( 50 ), Constant( 2 ) )
        );

        var block = BlockAsync(
            Switch(
                switchValue,
                nestedSwitch,
                SwitchCase( Constant( 20 ), Constant( 2 ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 30, result ); // First case of nested switch should return 30
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    [ExpectedException( typeof( ArgumentException ) )]
    public async Task AsyncBlock_ShouldThrowException_WithAwaitInSwitchCaseTestValues( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Switch case test values cannot contain awaited tasks
        var block = Switch(
            Constant( 1 ),
            Constant( 0 ),
            SwitchCase(
                Constant( 10 ),
                Await( BlockAsync(
                    Await( AsyncHelper.Completer(
                        Constant( completer ),
                        Constant( 1 )
                    ) )
                ) )
            ),
            SwitchCase( Constant( 20 ), Constant( 2 ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        await compiledLambda();
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldUseAwaitedValue_WithComplexExpressionInSwitchTestValues( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Complex expression affects the switch value
        var switchValue = Add(
            Await( AsyncHelper.Completer(
                Constant( completer ),
                Constant( 2 )
            ) ),
            Constant( 1 )
        );
        var block = BlockAsync(
            Switch(
                switchValue,
                Constant( 0 ),
                SwitchCase( Constant( 10 ), Constant( 3 ) ), // 2 + 1 = 3, should match this case
                SwitchCase( Constant( 20 ), Constant( 4 ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 10, result ); // Matching case with complex awaited value should return 10
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldHandleTaskDelaySuccessfully_InSwitchCase( CompilerType compiler )
    {
        // Arrange: One of the switch cases has a delayed task
        var switchValue = Constant( 1 );
        var delayedTask = Await( Constant( Task.Delay( 100 ).ContinueWith( _ => 100 ) ) );
        var block = BlockAsync(
            Switch(
                switchValue,
                Constant( 0 ),
                SwitchCase( delayedTask, Constant( 1 ) ),
                SwitchCase( Constant( 200 ), Constant( 2 ) )
            )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 100, result ); // Awaiting a delayed task should return 100 after completion
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task AsyncBlock_ShouldAwaitSuccessfully_WithAwaitBeforeAndAfterSwitch( CompleterType completer, CompilerType compiler )
    {
        // Arrange: Awaiting tasks before and after the switch expression
        var block = BlockAsync(
            Await( AsyncHelper.Completer(
                Constant( completer ),
                Constant( 5 )
            ) ),
            Switch(
                Constant( 1 ),
                Constant( 0 ),
                SwitchCase( Constant( 10 ), Constant( 1 ) ),
                SwitchCase( Constant( 20 ), Constant( 2 ) )
            ),
            Await( AsyncHelper.Completer( Constant( completer ), Constant( 15 ) ) )
        );
        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        // Act
        var result = await compiledLambda();

        // Assert
        Assert.AreEqual( 15, result ); // The last awaited value should be 15
    }

}
