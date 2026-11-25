using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

/// <summary>
/// A branch that a constant test proves unreachable is removed before coroutine lowering.
/// </summary>
/// <remarks>
/// Every behavioural test runs against the System compiler as well, so the semantics are
/// pinned against the reference implementation rather than against HEC's own idea of them.
/// The elimination must be invisible except in the size of the generated state machine,
/// which <see cref="ConstFalseGuardedAwait_ProducesNoExtraStateMachineField"/> guards.
/// </remarks>
[TestClass]
public class ConstantBranchEliminationTests
{
    private static int _sideEffects;

    public static int Bump()
    {
        _sideEffects++;
        return 1;
    }

    private static MethodInfo BumpMethod => typeof( ConstantBranchEliminationTests )
        .GetMethod( nameof( Bump ) )!;

    public static bool CaseInsensitive( string left, string right )
        => string.Equals( left, right, StringComparison.OrdinalIgnoreCase );

    private static MethodInfo CaseInsensitiveMethod => typeof( ConstantBranchEliminationTests )
        .GetMethod( nameof( CaseInsensitive ) )!;

    public static Task<int> EchoAsync( int value ) => Task.FromResult( value );

    private static MethodInfo EchoMethod => typeof( ConstantBranchEliminationTests )
        .GetMethod( nameof( EchoAsync ) )!;

    // -----------------------------------------------------------------------
    // Conditional — the live branch is kept, the dead one is not evaluated
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Condition_ConstantTrue_TakesIfTrue( CompilerType compiler )
    {
        var lambda = Lambda<Func<int>>(
            Condition( Constant( true ), Constant( 11 ), Constant( 22 ) ) );

        Assert.AreEqual( 11, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Condition_ConstantFalse_TakesIfFalse( CompilerType compiler )
    {
        var lambda = Lambda<Func<int>>(
            Condition( Constant( false ), Constant( 11 ), Constant( 22 ) ) );

        Assert.AreEqual( 22, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThen_ConstantFalse_DoesNotRunTheBody( CompilerType compiler )
    {
        var result = Variable( typeof( int ), "result" );

        var lambda = Lambda<Func<int>>(
            Block(
                new[] { result },
                Assign( result, Constant( 5 ) ),
                IfThen( Constant( false ), Assign( result, Call( BumpMethod ) ) ),
                result ) );

        var compiled = lambda.Compile( compiler );

        _sideEffects = 0;

        Assert.AreEqual( 5, compiled() );
        Assert.AreEqual( 0, _sideEffects, "the dead branch must not be evaluated" );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void IfThen_ConstantTrue_RunsTheBody( CompilerType compiler )
    {
        var result = Variable( typeof( int ), "result" );

        var lambda = Lambda<Func<int>>(
            Block(
                new[] { result },
                Assign( result, Constant( 5 ) ),
                IfThen( Constant( true ), Assign( result, Call( BumpMethod ) ) ),
                result ) );

        var compiled = lambda.Compile( compiler );

        _sideEffects = 0;

        Assert.AreEqual( 1, compiled() );
        Assert.AreEqual( 1, _sideEffects );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Condition_DeclaredTypeWiderThanBranches_KeepsTheDeclaredType( CompilerType compiler )
    {
        // The node's type is object while both branches are string, so the surviving branch
        // has to be widened back rather than narrowing what the caller sees.
        var lambda = Lambda<Func<object>>(
            Condition( Constant( false ), Constant( "yes" ), Constant( "no" ), typeof( object ) ) );

        var value = lambda.Compile( compiler )();

        Assert.AreEqual( "no", value );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Condition_NestedConstantTests_ResolveToTheInnermostLiveBranch( CompilerType compiler )
    {
        var lambda = Lambda<Func<int>>(
            Condition(
                Constant( false ),
                Constant( 1 ),
                Condition( Constant( true ), Constant( 2 ), Constant( 3 ) ) ) );

        Assert.AreEqual( 2, lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Condition_NonConstantTest_IsUntouched( CompilerType compiler )
    {
        var input = Parameter( typeof( int ), "input" );

        var lambda = Lambda<Func<int, int>>(
            Condition( GreaterThan( input, Constant( 0 ) ), Constant( 11 ), Constant( 22 ) ), input );

        var compiled = lambda.Compile( compiler );

        Assert.AreEqual( 11, compiled( 1 ) );
        Assert.AreEqual( 22, compiled( -1 ) );
    }

    // -----------------------------------------------------------------------
    // Switch on a constant
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_ConstantValue_TakesTheMatchingCase( CompilerType compiler )
    {
        var lambda = Lambda<Func<string>>(
            Switch(
                Constant( 2 ),
                Constant( "default" ),
                SwitchCase( Constant( "one" ), Constant( 1 ) ),
                SwitchCase( Constant( "two" ), Constant( 2 ) ),
                SwitchCase( Constant( "three" ), Constant( 3 ) ) ) );

        Assert.AreEqual( "two", lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_ConstantValue_NoMatch_TakesTheDefault( CompilerType compiler )
    {
        var lambda = Lambda<Func<string>>(
            Switch(
                Constant( 99 ),
                Constant( "default" ),
                SwitchCase( Constant( "one" ), Constant( 1 ) ),
                SwitchCase( Constant( "two" ), Constant( 2 ) ) ) );

        Assert.AreEqual( "default", lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_ConstantValue_MultipleTestValuesOnOneCase( CompilerType compiler )
    {
        var lambda = Lambda<Func<string>>(
            Switch(
                Constant( 3 ),
                Constant( "default" ),
                SwitchCase( Constant( "low" ), Constant( 1 ), Constant( 2 ) ),
                SwitchCase( Constant( "high" ), Constant( 3 ), Constant( 4 ) ) ) );

        Assert.AreEqual( "high", lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Switch_WithCustomComparison_IsNotResolvedButStillCorrect( CompilerType compiler )
    {
        // This is a trap for the eliminator, not just a smoke test. Resolving the switch by
        // ordinal equality would conclude that "B" matches nothing and pick the default. The
        // comparison method says otherwise, so the eliminator has to leave the switch alone
        // and let it run.
        var lambda = Lambda<Func<string>>(
            Switch(
                typeof( string ),
                Constant( "B" ),
                Constant( "default" ),
                CaseInsensitiveMethod,
                [SwitchCase( Constant( "matched" ), Constant( "b" ) )] ) );

        Assert.AreEqual( "matched", lambda.Compile( compiler )() );
    }

    // -----------------------------------------------------------------------
    // Label safety
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void DeadBranch_DefiningALabel_StillCompilesAndBehaves( CompilerType compiler )
    {
        // The eliminator declines to remove a branch that defines a label, so a jump into it
        // can never be orphaned. Expression tree scoping already forbids jumping in from
        // outside, so this cannot be driven to a wrong answer -- it pins that the shape
        // compiles and that the dead branch still does not run.
        var skip = Label( "skip" );
        var result = Variable( typeof( int ), "result" );

        var lambda = Lambda<Func<int>>(
            Block(
                new[] { result },
                Assign( result, Constant( 7 ) ),
                IfThen(
                    Constant( false ),
                    Block(
                        Goto( skip ),
                        Assign( result, Constant( 99 ) ),
                        Label( skip ) ) ),
                result ) );

        Assert.AreEqual( 7, lambda.Compile( compiler )() );
    }

    // -----------------------------------------------------------------------
    // Coroutines — the reason this pass exists
    // -----------------------------------------------------------------------

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ConstFalseGuardedAwait_ReturnsTheLiveResult( CompilerType compiler )
    {
        var local = Variable( typeof( int ), "local" );

        var lambda = Lambda<Func<Task<int>>>(
            BlockAsync(
                new[] { local },
                Assign( local, Constant( 7 ) ),
                Assign( local, Await( Call( EchoMethod, local ) ) ),
                IfThen( Constant( false ), Assign( local, Await( Call( EchoMethod, Constant( 99 ) ) ) ) ),
                local ) );

        Assert.AreEqual( 7, await lambda.Compile( compiler )() );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public async Task BlockAsync_ConstTrueGuardedAwait_RunsTheGuardedAwait( CompilerType compiler )
    {
        var local = Variable( typeof( int ), "local" );

        var lambda = Lambda<Func<Task<int>>>(
            BlockAsync(
                new[] { local },
                Assign( local, Constant( 7 ) ),
                Assign( local, Await( Call( EchoMethod, local ) ) ),
                IfThen( Constant( true ), Assign( local, Await( Call( EchoMethod, Constant( 99 ) ) ) ) ),
                local ) );

        Assert.AreEqual( 99, await lambda.Compile( compiler )() );
    }

    [TestMethod]
    public void ConstFalseGuardedAwait_ProducesNoExtraStateMachineField()
    {
        // The regression guard. A dead await used to be lowered in full, taking its own state
        // and its own result field, which cost 17.6 KB of a 45.7 KB compile. If this pass
        // stops firing, the guarded shape grows a field again and this fails.
        var withoutGuard = StateMachineFields(
            ( _, _ ) => null );

        var withDeadAwait = StateMachineFields(
            ( _, local ) => IfThen( Constant( false ),
                Assign( local, Await( Call( EchoMethod, Constant( 99 ) ) ) ) ) );

        var withLiveAwait = StateMachineFields(
            ( _, local ) => Assign( local, Await( Call( EchoMethod, Constant( 99 ) ) ) ) );

        Assert.AreEqual( withoutGuard, withDeadAwait,
            "an await under Constant( false ) must not add state machine fields" );

        Assert.IsTrue( withLiveAwait > withoutGuard,
            $"a live second await must still add a field ({withLiveAwait} vs {withoutGuard}), " +
            "otherwise this test is not measuring what it claims" );
    }

    private sealed class CapturingProvider : IModuleBuilderProvider
    {
        public ModuleBuilder Module { get; }

        public CapturingProvider()
        {
            var name = "BranchElimProbe_" + Guid.NewGuid().ToString( "N" );

            Module = AssemblyBuilder
                .DefineDynamicAssembly( new AssemblyName( name ), AssemblyBuilderAccess.RunAndCollect )
                .DefineDynamicModule( name );
        }

        public ModuleBuilder GetModuleBuilder( ModuleKind kind ) => Module;
    }

    private static int StateMachineFields(
        Func<ParameterExpression, ParameterExpression, Expression?> extra )
    {
        var provider = new CapturingProvider();
        var options = new ExpressionRuntimeOptions { ModuleBuilderProvider = provider };

        var input = Parameter( typeof( int ), "input" );
        var local = Variable( typeof( int ), "local" );

        var body = new List<Expression>
        {
            Assign( local, Constant( 7 ) ),
            Assign( local, Await( Call( EchoMethod, local ) ) )
        };

        var tail = extra( input, local );

        if ( tail != null )
            body.Add( tail );

        body.Add( local );

        HyperbeeCompiler.Compile(
            Lambda<Func<int, Task<int>>>(
                BlockAsync( new[] { local }, body.ToArray(), options ), input ) );

        var stateMachine = provider.Module.GetTypes()
            .Single( t => t.Name.StartsWith( "StateMachine", StringComparison.Ordinal ) );

        return stateMachine
            .GetFields( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance )
            .Length;
    }
}
