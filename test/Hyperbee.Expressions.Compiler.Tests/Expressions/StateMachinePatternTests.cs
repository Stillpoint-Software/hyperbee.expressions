using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

/// <summary>
/// Tests that HEC correctly compiles expression tree patterns produced by the async state machine
/// lowerer. Each test compiles the same expression with both System.Linq.Expressions.Compile()
/// and HEC, then asserts results match. Any failure is an HEC bug.
/// </summary>
[TestClass]
public class StateMachinePatternTests
{
    // -----------------------------------------------------------------------
    // Pattern 1: Switch as jump/dispatch table
    // The jump table is: Switch(stateVar, SwitchCase(Goto(resumeLabel), Constant(stateId)))
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Pattern_SwitchDispatchTable_NoMatch_ReturnsDefault()
    {
        // state = 99 (no match) → falls through to after the switch, returns -1
        var state = Variable( typeof(int), "state" );
        var resumeLabel0 = Label( typeof(void), "resume0" );
        var resumeLabel1 = Label( typeof(void), "resume1" );

        var body = Block(
            [state],
            Assign( state, Constant( 99 ) ),
            Switch(
                state,
                (Expression) null,
                SwitchCase( Goto( resumeLabel0 ), Constant( 0 ) ),
                SwitchCase( Goto( resumeLabel1 ), Constant( 1 ) )
            ),
            Label( resumeLabel0 ),
            Label( resumeLabel1 ),
            Constant( -1 )
        );

        AssertSameResult<int>( body );
    }

    [TestMethod]
    public void Pattern_SwitchDispatchTable_MatchesCase1_Jumps()
    {
        // state = 1 → jumps to resumeLabel1 → reads the second result
        var state = Variable( typeof(int), "state" );
        var result = Variable( typeof(int), "result" );
        var resumeLabel0 = Label( typeof(void), "resume0" );
        var resumeLabel1 = Label( typeof(void), "resume1" );
        var endLabel = Label( typeof(int), "end" );

        var body = Block(
            [state, result],
            Assign( state, Constant( 1 ) ),
            Switch(
                state,
                (Expression) null,
                SwitchCase( Goto( resumeLabel0 ), Constant( 0 ) ),
                SwitchCase( Goto( resumeLabel1 ), Constant( 1 ) )
            ),
            Assign( result, Constant( 10 ) ),
            Goto( endLabel, result ),
            Label( resumeLabel0 ),
            Assign( result, Constant( 20 ) ),
            Goto( endLabel, result ),
            Label( resumeLabel1 ),
            Assign( result, Constant( 30 ) ),
            Label( endLabel, result )
        );

        AssertSameResult<int>( body );
    }

    // -----------------------------------------------------------------------
    // Pattern 2: Instance field read on a class parameter
    // Emitted by HoistingVisitor: Field(sm, stateField) → LoadArg + LoadField
    // -----------------------------------------------------------------------

    public class FieldReadHost { public int State; }

    [TestMethod]
    public void Pattern_InstanceFieldRead()
    {
        var sm = Parameter( typeof(FieldReadHost), "sm" );
        var body = Field( sm, typeof(FieldReadHost).GetField( "State" )! );

        var instance = new FieldReadHost { State = 42 };

        var systemResult = Lambda<Func<FieldReadHost, int>>( body, sm ).Compile()( instance );
        var hecResult = HyperbeeCompiler.Compile<Func<FieldReadHost, int>>( Lambda<Func<FieldReadHost, int>>( body, sm ) )( instance );

        Assert.AreEqual( systemResult, hecResult, $"Field read mismatch: system={systemResult}, hec={hecResult}" );
    }

    // -----------------------------------------------------------------------
    // Pattern 3: Instance field write on a class parameter
    // Emitted as: Assign(Field(sm, field), value) → LoadArg + load_value + StoreField
    // -----------------------------------------------------------------------

    public class FieldWriteHost { public int State; }

    [TestMethod]
    public void Pattern_InstanceFieldWrite()
    {
        var sm = Parameter( typeof(FieldWriteHost), "sm" );
        var body = Block(
            typeof(void),
            Assign( Field( sm, typeof(FieldWriteHost).GetField( "State" )! ), Constant( 99 ) )
        );

        var instance = new FieldWriteHost { State = 0 };

        Lambda<Action<FieldWriteHost>>( body, sm ).Compile()( instance );
        var systemValue = instance.State;

        instance.State = 0;
        HyperbeeCompiler.Compile<Action<FieldWriteHost>>( Lambda<Action<FieldWriteHost>>( body, sm ) )( instance );
        var hecValue = instance.State;

        Assert.AreEqual( systemValue, hecValue, $"Field write mismatch: system={systemValue}, hec={hecValue}" );
    }

    // -----------------------------------------------------------------------
    // Pattern 4: IfThen containing Return (the IsCompleted/suspend pattern)
    // IfThen( IsFalse(isCompleted), Block( storeState, Return(exitLabel) ) )
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Pattern_IfThenWithReturn_ConditionFalse_Continues()
    {
        var exitLabel = Label( typeof(void), "exit" );
        var result = Variable( typeof(int), "result" );

        // isCompleted = true → condition fails → does NOT enter IfThen → result = 1
        var body = Block(
            [result],
            Assign( result, Constant( 0 ) ),
            IfThen(
                IsFalse( Constant( true ) ),  // false: skip the block
                Block(
                    Assign( result, Constant( -1 ) ),
                    Return( exitLabel )
                )
            ),
            Assign( result, Constant( 1 ) ),
            Label( exitLabel ),
            result
        );

        AssertSameResult<int>( body );
    }

    [TestMethod]
    public void Pattern_IfThenWithReturn_ConditionTrue_Exits()
    {
        var exitLabel = Label( typeof(void), "exit" );
        var result = Variable( typeof(int), "result" );

        // isCompleted = false → enters IfThen → sets result = -1 and returns early
        var body = Block(
            [result],
            Assign( result, Constant( 0 ) ),
            IfThen(
                IsFalse( Constant( false ) ),  // true: enter the block
                Block(
                    Assign( result, Constant( -1 ) ),
                    Return( exitLabel )
                )
            ),
            Assign( result, Constant( 1 ) ), // unreachable
            Label( exitLabel ),
            result
        );

        AssertSameResult<int>( body );
    }

    // -----------------------------------------------------------------------
    // Pattern 5: Ref parameter method call
    // This is the AwaitUnsafeOnCompleted pattern:
    //   Call(builderField, method, ref awaiterVar, ref smVar)
    // Requires EmitLoadAddress for both ref arguments.
    // -----------------------------------------------------------------------

    public class RefPatternHost
    {
        public int Awaiter;
        public int State;

        // Simulates AwaitUnsafeOnCompleted(ref int awaiter, ref int state)
        public static void SimulateSchedule( ref int awaiter, ref int state )
        {
            awaiter = 100;
            state = 99;
        }
    }

    [TestMethod]
    public void Pattern_RefParameterMethodCall_FieldArgs()
    {
        // Call(SimulateSchedule, ref sm.Awaiter, ref sm.State)
        // After the call: sm.Awaiter == 100, sm.State == 99
        var sm = Parameter( typeof(RefPatternHost), "sm" );

        var awaiterField = typeof(RefPatternHost).GetField( "Awaiter" )!;
        var stateField = typeof(RefPatternHost).GetField( "State" )!;
        var method = typeof(RefPatternHost).GetMethod( "SimulateSchedule" )!;

        var body = Block(
            typeof(void),
            Call(
                method,
                Field( sm, awaiterField ),
                Field( sm, stateField )
            )
        );

        var systemInstance = new RefPatternHost();
        Lambda<Action<RefPatternHost>>( body, sm ).Compile()( systemInstance );

        var hecInstance = new RefPatternHost();
        HyperbeeCompiler.Compile<Action<RefPatternHost>>( Lambda<Action<RefPatternHost>>( body, sm ) )( hecInstance );

        Assert.AreEqual( systemInstance.Awaiter, hecInstance.Awaiter,
            $"Awaiter mismatch: system={systemInstance.Awaiter}, hec={hecInstance.Awaiter}" );
        Assert.AreEqual( systemInstance.State, hecInstance.State,
            $"State mismatch: system={systemInstance.State}, hec={hecInstance.State}" );
    }

    [TestMethod]
    public void Pattern_RefParameterMethodCall_LocalArgs()
    {
        // Call(SimulateSchedule, ref localAwaiter, ref localState)
        var awaiter = Variable( typeof(int), "awaiter" );
        var state = Variable( typeof(int), "state" );
        var method = typeof(RefPatternHost).GetMethod( "SimulateSchedule" )!;

        var body = Block(
            [awaiter, state],
            Assign( awaiter, Constant( 0 ) ),
            Assign( state, Constant( 0 ) ),
            Call( method, awaiter, state ),
            Add( awaiter, state )  // 100 + 99 = 199
        );

        AssertSameResult<int>( body );
    }

    // -----------------------------------------------------------------------
    // Pattern 6: TryCatch wrapping entire state machine body
    // -----------------------------------------------------------------------

    [TestMethod]
    public void Pattern_TryCatch_NoException_CompletesNormally()
    {
        var result = Variable( typeof(int), "result" );
        var ex = Parameter( typeof(Exception), "ex" );

        var body = Block(
            [result],
            TryCatch(
                Block(
                    typeof(void),
                    Assign( result, Constant( 42 ) )
                ),
                Catch(
                    ex,
                    Block( typeof(void), Assign( result, Constant( -1 ) ) )
                )
            ),
            result
        );

        AssertSameResult<int>( body );
    }

    [TestMethod]
    public void Pattern_TryCatch_Exception_CatchHandled()
    {
        var result = Variable( typeof(int), "result" );
        var ex = Parameter( typeof(Exception), "ex" );

        var body = Block(
            [result],
            TryCatch(
                Block(
                    typeof(void),
                    Assign( result, Constant( 1 ) ),
                    Throw( New( typeof(InvalidOperationException).GetConstructor( Type.EmptyTypes )! ) )
                ),
                Catch(
                    ex,
                    Block( typeof(void), Assign( result, Constant( -99 ) ) )
                )
            ),
            result
        );

        AssertSameResult<int>( body );
    }

    // -----------------------------------------------------------------------
    // Pattern 7: Multi-state MoveNext body (combined shape)
    // Simulates a 2-await state machine body.
    // State -1 = initial, 0 = after first await, 1 = after second await, -2 = done
    // -----------------------------------------------------------------------

    public class FakeSm
    {
        public int State;
        public int Awaiter;
        public int FinalResult;
    }

    [TestMethod]
    public void Pattern_MultiState_MoveNextShape_State0()
    {
        // Simulate MoveNext body when State = 0 (resume at label0, set Awaiter=10, set State=1, exit)
        var sm = Parameter( typeof(FakeSm), "sm" );

        var stateField = typeof(FakeSm).GetField( "State" )!;
        var awaiterField = typeof(FakeSm).GetField( "Awaiter" )!;
        var finalField = typeof(FakeSm).GetField( "FinalResult" )!;

        var exitLabel = Label( typeof(void), "exit" );
        var resume0 = Label( typeof(void), "resume0" );
        var resume1 = Label( typeof(void), "resume1" );

        var body = Block(
            typeof(void),
            // Jump table
            Switch(
                Field( sm, stateField ),
                (Expression) null,
                SwitchCase( Goto( resume0 ), Constant( 0 ) ),
                SwitchCase( Goto( resume1 ), Constant( 1 ) )
            ),
            // State -1: initial execution
            Assign( Field( sm, awaiterField ), Constant( 10 ) ),
            Assign( Field( sm, stateField ), Constant( 0 ) ),
            Return( exitLabel ),   // suspend

            // State 0: resume
            Label( resume0 ),
            Assign( Field( sm, awaiterField ), Constant( 20 ) ),
            Assign( Field( sm, stateField ), Constant( 1 ) ),
            Return( exitLabel ),   // suspend

            // State 1: second resume - complete
            Label( resume1 ),
            Assign( Field( sm, finalField ), Field( sm, awaiterField ) ),
            Assign( Field( sm, stateField ), Constant( -2 ) ),

            Label( exitLabel )
        );

        // Test with State = -1 (initial run)
        var systemSm = new FakeSm { State = -1 };
        Lambda<Action<FakeSm>>( body, sm ).Compile()( systemSm );

        var hecSm = new FakeSm { State = -1 };
        HyperbeeCompiler.Compile<Action<FakeSm>>( Lambda<Action<FakeSm>>( body, sm ) )( hecSm );

        Assert.AreEqual( systemSm.State, hecSm.State, "State mismatch after initial run" );
        Assert.AreEqual( systemSm.Awaiter, hecSm.Awaiter, "Awaiter mismatch after initial run" );
    }

    [TestMethod]
    public void Pattern_MultiState_MoveNextShape_State1Resume()
    {
        var sm = Parameter( typeof(FakeSm), "sm" );

        var stateField = typeof(FakeSm).GetField( "State" )!;
        var awaiterField = typeof(FakeSm).GetField( "Awaiter" )!;
        var finalField = typeof(FakeSm).GetField( "FinalResult" )!;

        var exitLabel = Label( typeof(void), "exit" );
        var resume0 = Label( typeof(void), "resume0" );
        var resume1 = Label( typeof(void), "resume1" );

        var body = Block(
            typeof(void),
            Switch(
                Field( sm, stateField ),
                (Expression) null,
                SwitchCase( Goto( resume0 ), Constant( 0 ) ),
                SwitchCase( Goto( resume1 ), Constant( 1 ) )
            ),
            Assign( Field( sm, awaiterField ), Constant( 10 ) ),
            Assign( Field( sm, stateField ), Constant( 0 ) ),
            Return( exitLabel ),
            Label( resume0 ),
            Assign( Field( sm, awaiterField ), Constant( 20 ) ),
            Assign( Field( sm, stateField ), Constant( 1 ) ),
            Return( exitLabel ),
            Label( resume1 ),
            Assign( Field( sm, finalField ), Field( sm, awaiterField ) ),
            Assign( Field( sm, stateField ), Constant( -2 ) ),
            Label( exitLabel )
        );

        // Test with State = 1 (second resume)
        var systemSm = new FakeSm { State = 1, Awaiter = 77 };
        Lambda<Action<FakeSm>>( body, sm ).Compile()( systemSm );

        var hecSm = new FakeSm { State = 1, Awaiter = 77 };
        HyperbeeCompiler.Compile<Action<FakeSm>>( Lambda<Action<FakeSm>>( body, sm ) )( hecSm );

        Assert.AreEqual( systemSm.State, hecSm.State, "State mismatch after second resume" );
        Assert.AreEqual( systemSm.FinalResult, hecSm.FinalResult, "FinalResult mismatch after second resume" );
    }

    // -----------------------------------------------------------------------
    // Helper
    // -----------------------------------------------------------------------

    private static void AssertSameResult<T>( Expression body )
    {
        var lambda = Lambda<Func<T>>( body );

        var systemResult = lambda.Compile()();
        var hecResult = HyperbeeCompiler.Compile<Func<T>>( Lambda<Func<T>>( body ) )();

        Assert.AreEqual( systemResult, hecResult,
            $"Result mismatch: system={systemResult}, hec={hecResult}" );
    }
}
