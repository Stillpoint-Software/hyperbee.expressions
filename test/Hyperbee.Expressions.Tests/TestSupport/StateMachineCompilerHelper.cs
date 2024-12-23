using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;

// ReSharper disable InconsistentNaming

namespace Hyperbee.Expressions.Tests.TestSupport;

internal static class StateMachineCompilerHelper
{
    // Keep this workbench for testing the generated lowered state-machine expressions.
    // This class is helpful for debugging and testing lowered expressions.
    //
    // Use this as a starting point for getting to a minimal reproducible example.

    public static BlockExpression CreateLoweredExpression()
    {
        // TEMPLATE PATTERN FOR TESTING LOWERED EXPRESSIONS

        // Methods to call

        var binder = AwaitBinderFactory.GetOrCreate( typeof( Task<int> ) );

        var awaitOnUnsafeCompletedMethod = typeof( AsyncTaskMethodBuilder<int> )
            .GetMethod( "AwaitUnsafeOnCompleted" )!
            .MakeGenericMethod(
                typeof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter ),
                typeof( StateMachine )
        );

        // Define variables
        var stateMachineVar = Variable( typeof( StateMachine ), "stateMachine" );

        var stResume0Label = Label( "ST_RESUME_0" );
        var stResume1Label = Label( "ST_RESUME_1" );
        var stExitLabel = Label( typeof( void ), "ST_EXIT" );

        var smVar = Variable( typeof( StateMachine ), "sm" );

#if FAST_COMPILER // use-local
        var completedTask0 = Variable( typeof( Task<int> ), "completedTask0" );
        var completedTask1 = Variable( typeof( Task<int> ), "completedTask1" );
#endif

        // Build the MoveNext delegate
        var moveNextLambda = Lambda<MoveNextDelegate<StateMachine>>(
            Block(
#if FAST_COMPILER // use-local
                [completedTask0, completedTask1],
#endif

                // if (sm.__state == 0) { sm.__state = -1; goto ST_RESUME_0; }
                Switch(
                    Field( smVar, nameof( StateMachine.__state ) ),
                    Empty(), // Default case: do nothing
                    SwitchCase(
                        Block(
                            Assign(
                                Field( smVar, nameof( StateMachine.__state ) ),
                                Constant( -1 )
                            ),
                            Goto( stResume0Label )
                        ),
                        Constant( 0 ) // Case for 0
                    ),
                    SwitchCase(
                        Block(
                            Assign(
                                Field( smVar, nameof( StateMachine.__state ) ),
                                Constant( -1 )
                            ),
                            Goto( stResume1Label )
                        ),
                        Constant( 1 ) // Case for 1
                    )
                ),

                // ***** FIRST AWAIT *****

#if FAST_COMPILER // use-local
                Assign(
                    completedTask0,
                    Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine.__awaiter0 ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        completedTask0, // immediate result
                        Constant( false )
                    )
                ),
#else
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(10), false);
                Assign(
                    Field( smVar, nameof( StateMachine.__awaiter0 ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        Constant( Task.FromResult( 10 ) ), // immediate result
                        Constant( false )
                    )
                ),
#endif

                // if (!sm.__awaiter.IsCompleted)
                IfThen(
                    IsFalse(
                        Property(
                            Field( smVar, nameof( StateMachine.__awaiter0 ) ),
                            nameof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter.IsCompleted )
                        )
                    ),
                    Block(
                        // sm.__state = 0;
                        Assign(
                            Field( smVar, nameof( StateMachine.__state ) ),
                            Constant( 0 )
                        ),
                        // sm.__builder.AwaitUnsafeOnCompleted(...);
                        Call(
                            Field( smVar, nameof( StateMachine.__builder ) ), // instance
                            awaitOnUnsafeCompletedMethod, // method
                            Field( smVar, nameof( StateMachine.__awaiter0 ) ), // ref awaiter
                            smVar // ref stateMachine
                        ),
                        // return;
                        Return( stExitLabel )
                    )
                ),
                // ST_RESUME_0: sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Label( stResume0Label ),
                Assign(
                    Field( smVar, nameof( StateMachine.__result0 ) ),
                    Call(
                        binder.GetResultMethod,
                        Field( smVar, nameof( StateMachine.__awaiter0 ) )
                    )
                ),

#if !FAST_COMPILER // remove-unary-variable-expression (e.g. `someVar;`)
                Field( smVar, nameof( StateMachine.__result0 ) ), // THIS LINE IS THE CULPRIT
#endif
                // ***** SECOND AWAIT *****

#if FAST_COMPILER // use-local
                Assign(
                    completedTask1,
                    Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 42 ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine.__awaiter1 ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        completedTask1, // immediate result
                        Constant( false )
                    )
                ),
#else
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(42), false);
                Assign(
                    Field( smVar, nameof( StateMachine.__awaiter1 ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        Constant( Task.FromResult( 42 ) ), // immediate result
                        Constant( false )
                    )
                ),
#endif

                // if (!sm.__awaiter.IsCompleted)
                IfThen(
                    IsFalse(
                        Property(
                            Field( smVar, nameof( StateMachine.__awaiter1 ) ),
                            nameof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter.IsCompleted )
                        )
                    ),
                    Block(
                        // sm.__state = 1;
                        Assign(
                            Field( smVar, nameof( StateMachine.__state ) ),
                            Constant( 1 )
                        ),
                        // sm.__builder.AwaitUnsafeOnCompleted(...);
                        Call(
                            Field( smVar, nameof( StateMachine.__builder ) ), // instance
                            awaitOnUnsafeCompletedMethod, // method
                            Field( smVar, nameof( StateMachine.__awaiter1 ) ), // ref awaiter
                            smVar // ref stateMachine
                        ),
                        // return;
                        Return( stExitLabel )
                    )
                ),
                // ST_RESUME_1: sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Label( stResume1Label ),
                Assign(
                    Field( smVar, nameof( StateMachine.__result1 ) ),
                    Call(
                        binder.GetResultMethod,
                        Field( smVar, nameof( StateMachine.__awaiter1 ) )
                    )
                ),

                // ST_FINAL
                //
                // sm.__final = sm.__result;

                Assign(
                    Field( smVar, nameof( StateMachine.__final ) ),
                    Field( smVar, nameof( StateMachine.__result1 ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine.__state ) ),
                    Constant( -2 )
                ),
                Call(
                    Field( smVar, nameof( StateMachine.__builder ) ),
                    typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.SetResult ) )!,
                    Field( smVar, nameof( StateMachine.__final ) )
                ),
                Label( stExitLabel )
            ),
            smVar // MoveNext delegate parameter
        );

        // Assign the delegate to the state machine
        var mainBlock = Block(
            [stateMachineVar],
            // stateMachine = new StateMachine1();
            Assign( stateMachineVar, New( typeof( StateMachine ) ) ),
            // stateMachine.__state = -1;
            Assign(
                Field( stateMachineVar, nameof( StateMachine.__state ) ),
                Constant( -1 )
            ),
            // stateMachine.__moveNextDelegate = (StateMachine1 sm) => { ... }
            Assign(
                Field( stateMachineVar, nameof( StateMachine.__moveNextDelegate ) ),
                moveNextLambda
            ),
            // stateMachine.__builder.Start<StateMachine1>(ref stateMachine);
            Call(
                Field( stateMachineVar, nameof( StateMachine.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.Start ) )!.MakeGenericMethod( typeof( StateMachine ) ),
                stateMachineVar
            ),
            // stateMachine.__builder.Task;
            Property(
                Field( stateMachineVar, stateMachineVar.Type.GetField( nameof( StateMachine.__builder ) )! ),
                stateMachineVar.Type.GetField( nameof( StateMachine.__builder ) )!.FieldType.GetProperty( "Task" )!
            )
        );

        return mainBlock;
    }

    public class StateMachine : IAsyncStateMachine
    {
        public int __state;
        public int __final;

        public AsyncTaskMethodBuilder<int> __builder;
        public MoveNextDelegate<StateMachine> __moveNextDelegate;

        public ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter __awaiter0;
        public ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter __awaiter1;

        public int __result0;
        public int __result1;

        public void MoveNext()
        {
            __moveNextDelegate( this );
        }

        public void SetStateMachine( IAsyncStateMachine stateMachine )
        {
            __builder.SetStateMachine( stateMachine );
        }
    }
}

