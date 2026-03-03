using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Integration;

/// <summary>
/// Integration tests verifying Switch patterns inside BlockAsync
/// when the state machine MoveNext is compiled by HEC.
/// </summary>
[TestClass]
public class BlockAsyncSwitchTests
{
    private static ExpressionRuntimeOptions HecOptions() => new()
    {
        DelegateBuilder = HyperbeeCoroutineDelegateBuilder.Instance
    };

    // -----------------------------------------------------------------------
    // Awaited value used as the switch discriminant
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitInSwitchValue_MatchesCase( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Switch(
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 1 ) ) ),
                    Constant( 0 ),
                    SwitchCase( Constant( 10 ), Constant( 1 ) ),
                    SwitchCase( Constant( 20 ), Constant( 2 ) )
                )
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — switch value 1 matches first case → 10
        Assert.AreEqual( 10, result );
    }

    // -----------------------------------------------------------------------
    // No case matches — await in default body
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitInDefaultBody_ReturnsDefaultValue( CompilerType compiler )
    {
        // Arrange — switch value 3 doesn't match any case
        var block = BlockAsync(
            new Expression[]
            {
                Switch(
                    Constant( 3 ),
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 99 ) ) ),
                    SwitchCase( Constant( 10 ), Constant( 1 ) ),
                    SwitchCase( Constant( 20 ), Constant( 2 ) )
                )
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — default body awaited → 99
        Assert.AreEqual( 99, result );
    }

    // -----------------------------------------------------------------------
    // Await inside a matched switch case body
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitInCaseBody_MatchedCase( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Switch(
                    Constant( 1 ),
                    Constant( 0 ),
                    SwitchCase(
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 100 ) ) ),
                        Constant( 1 )
                    ),
                    SwitchCase( Constant( 200 ), Constant( 2 ) )
                )
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — case 1 body awaited → 100
        Assert.AreEqual( 100, result );
    }

    // -----------------------------------------------------------------------
    // Await in both switch value and matched case body
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitInSwitchValueAndCaseBody_BothAwaited( CompilerType compiler )
    {
        // Arrange — switch value is awaited (→ 2), case 2 body is also awaited (→ 50)
        var block = BlockAsync(
            new Expression[]
            {
                Switch(
                    Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 2 ) ) ),
                    Constant( 0 ),
                    SwitchCase(
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 50 ) ) ),
                        Constant( 2 )
                    ),
                    SwitchCase( Constant( 20 ), Constant( 3 ) )
                )
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — switch value 2 matches case, case body awaited → 50
        Assert.AreEqual( 50, result );
    }

    // -----------------------------------------------------------------------
    // Nested switches — outer takes default, inner matches case with await
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_NestedSwitchWithAwaits_InnerCaseReturnsValue( CompilerType compiler )
    {
        // Arrange — outer switch value 1 doesn't match → default is inner switch; inner case 1 matches
        var innerSwitch = Switch(
            Constant( 1 ),
            Constant( 0 ),
            SwitchCase(
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 30 ) ) ),
                Constant( 1 )
            ),
            SwitchCase( Constant( 50 ), Constant( 2 ) )
        );

        var block = BlockAsync(
            new Expression[]
            {
                Switch(
                    Constant( 1 ),
                    innerSwitch,
                    SwitchCase( Constant( 20 ), Constant( 2 ) )
                )
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — outer case 1 matches → 20... wait, outer case 1 matches so default (innerSwitch) is NOT used.
        // Outer: switch(1) { case 2 → 20; default → innerSwitch }
        // 1 doesn't match case 2, falls to default (innerSwitch)
        // innerSwitch: switch(1) { case 1 → await(30) } → 30
        Assert.AreEqual( 30, result );
    }

    // -----------------------------------------------------------------------
    // Complex switch value: arithmetic on awaited result
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ComplexSwitchValue_ArithmeticOnAwaited_MatchesCase( CompilerType compiler )
    {
        // Arrange — switch value is await(2) + 1 = 3, matches case 3
        var block = BlockAsync(
            new Expression[]
            {
                Switch(
                    Add(
                        Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 2 ) ) ),
                        Constant( 1 )
                    ),
                    Constant( 0 ),
                    SwitchCase( Constant( 10 ), Constant( 3 ) ),
                    SwitchCase( Constant( 20 ), Constant( 4 ) )
                )
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — 2 + 1 = 3 matches case 3 → 10
        Assert.AreEqual( 10, result );
    }

    // -----------------------------------------------------------------------
    // Await before and after a non-async switch
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_AwaitBeforeAndAfterSwitch_ReturnsLastAwaited( CompilerType compiler )
    {
        // Arrange
        var block = BlockAsync(
            new Expression[]
            {
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 5 ) ) ),
                Switch(
                    Constant( 1 ),
                    Constant( 0 ),
                    SwitchCase( Constant( 10 ), Constant( 1 ) ),
                    SwitchCase( Constant( 20 ), Constant( 2 ) )
                ),
                Await( Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 15 ) ) )
            },
            HecOptions()
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiled = lambda.Compile( compiler );

        // Act
        var result = await compiled();

        // Assert — last awaited value returned
        Assert.AreEqual( 15, result );
    }
}
