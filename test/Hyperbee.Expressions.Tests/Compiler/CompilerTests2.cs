using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Tests.TestSupport;
using Hyperbee.Expressions.CompilerServices;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

// ReSharper disable InconsistentNaming

namespace Hyperbee.Expressions.Tests.Compiler;

[TestClass]
public class CompilerTests2
{
    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    public async Task Compiler_Test2_CustomAwaitable( CompleterType completer, CompilerType compiler )
    {
        // REAL-WORLD TEST WITH CUSTOM AWAITABLE
        try
        {
            var block = BlockAsync(
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 10 )
                ) ),
                Await( AsyncHelper.Completer(
                    Constant( completer ),
                    Constant( 42 )
                ) )
            );

            var lambda = Lambda<Func<Task<int>>>( block );
            var compiledLambda = lambda.Compile( compiler );

            var result = await compiledLambda();

            Assert.AreEqual( 42, result );
        }
        catch ( Exception e )
        {
            Console.WriteLine( e );
            throw;
        }
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    public async Task Compiler_Test2_CompletedTask( CompleterType completer, CompilerType compiler )
    {
        // REAL-WORLD TEST WITH TASK.FromResult
        try
        {
            var block = BlockAsync(
                Await( Constant( Task.FromResult( 10 ) ) ),
                Await( Constant( Task.FromResult( 42 ) ) )
            );

            var lambda = Lambda<Func<Task<int>>>( block );
            var compiledLambda = lambda.Compile( compiler );

            var result = await compiledLambda();

            Assert.AreEqual( 42, result );
        }
        catch ( Exception e )
        {
            Console.WriteLine( e );
            throw;
        }
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public async Task Compiler_Test2_CompletedTask_Lowered( CompilerType compiler )
    {
        // MANUALLY LOWERED TEST WITH TASK.FromResult
        // 
        // Conceptually:
        //
        // var block = BlockAsync(
        //     Await( Constant( Task.FromResult( 10 ) ) ),
        //     Await( Constant( Task.FromResult( 42 ) ) )
        // );

        try
        {
            var block = CreateFullExpressionTree();
            //var block = CreateMinimalFailureExpressionTree(); 

            var lambda = Lambda<Func<Task<int>>>( block );
            var compiledLambda = lambda.Compile( compiler );

            await compiledLambda();
        }
        catch ( Exception e )
        {
            Console.WriteLine( e );
            throw;
        }
    }

    private static BlockExpression CreateMinimalFailureExpressionTree()
    {
        // Methods to call

        var binder = AwaitBinderFactory.GetOrCreate( typeof( Task<int> ) );

        // Define variables
        var stateMachineVar = Variable( typeof( StateMachine2 ), "stateMachine" );
        var smVar = Variable( typeof( StateMachine2 ), "sm" );

#if FAST_COMPILER //use-local
        var completedTask0 = Variable( typeof( Task<int> ), "completedTask0" );
        var completedTask1 = Variable( typeof( Task<int> ), "completedTask1" );
#endif

        // Build the MoveNext delegate
        var moveNextLambda = Lambda<MoveNextDelegate<StateMachine2>>(
            Block(
#if FAST_COMPILER //use-local
                [completedTask0, completedTask1],
#endif


                // ***** FIRST AWAIT *****
#if FAST_COMPILER //use-local
                Assign(
                    completedTask0,
                    Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 10 ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine2.__awaiter0 ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        completedTask0, // immediate result
                        Constant( false )
                    )
                ),
#else
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(10), false);
                Assign(
                    Field( smVar, nameof( StateMachine2.__awaiter0 ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        Constant( Task.FromResult( 10 ) ), // immediate result
                        Constant( false )
                    )
                ),
#endif
                // sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Assign(
                    Field( smVar, nameof( StateMachine2.__result0 ) ),
                    Call(
                        binder.GetResultMethod,
                        Field( smVar, nameof( StateMachine2.__awaiter0 ) )
                    )
                ),

#if !FAST_COMPILER // remove-unary-variable-expression (e.g. `someVar;`)
                Field( smVar, nameof( StateMachine2.__result0 ) ), // THIS LINE IS THE CULPRIT
#endif

                // ***** SECOND AWAIT *****

#if FAST_COMPILER //use-local
                Assign(
                    completedTask1,
                    Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 42 ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine2.__awaiter1 ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        completedTask1, // immediate result
                        Constant( false )
                    )
                ),
#else
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(42), false);
                Assign(
                    Field( smVar, nameof(StateMachine2.__awaiter1) ),
                    Call(
                        binder.GetAwaiterMethod,
                        Constant( Task.FromResult( 42 ) ), // immediate result
                        Constant( false )
                    )
                ),
#endif
                // sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Assign(
                    Field( smVar, nameof( StateMachine2.__result1 ) ),
                    Call(
                        binder.GetResultMethod,
                        Field( smVar, nameof( StateMachine2.__awaiter1 ) )
                    )
                ),

                // ***** FINAL RESULT *****

                // sm.__builder.SetResult(sm.__result);
                Call(
                    Field( smVar, nameof( StateMachine2.__builder ) ),
                    typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.SetResult ) )!,
                    Field( smVar, nameof( StateMachine2.__result1 ) )
                )

            ),
            smVar // MoveNext delegate parameter
        );

        // Assign the delegate to the state machine
        var mainBlock = Block(
            [stateMachineVar],
            // stateMachine = new StateMachine2();
            Assign( stateMachineVar, New( typeof( StateMachine2 ) ) ),
            // stateMachine.__moveNextDelegate = (StateMachine2 sm) => { ... }
            Assign(
                Field( stateMachineVar, nameof( StateMachine2.__moveNextDelegate ) ),
                moveNextLambda
            ),
            // stateMachine.__builder.Start<StateMachine2>(ref stateMachine);
            Call(
                Field( stateMachineVar, nameof( StateMachine2.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.Start ) )!.MakeGenericMethod( typeof( StateMachine2 ) ),
                stateMachineVar
            ),
            // stateMachine.__builder.Task;
            Property(
                Field( stateMachineVar, stateMachineVar.Type.GetField( nameof( StateMachine2.__builder ) )! ),
                stateMachineVar.Type.GetField( nameof( StateMachine2.__builder ) )!.FieldType.GetProperty( "Task" )!
            )
        );

        return mainBlock;
    }

    private static BlockExpression CreateFullExpressionTree()
    {
        // Methods to call

        var binder = AwaitBinderFactory.GetOrCreate( typeof( Task<int> ) );

        var awaitOnUnsafeCompletedMethod = typeof( AsyncTaskMethodBuilder<int> )
            .GetMethod( "AwaitUnsafeOnCompleted" )!
            .MakeGenericMethod(
                typeof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter ),
                typeof( StateMachine2 )
        );

        // Define variables
        var stateMachineVar = Variable( typeof( StateMachine2 ), "stateMachine" );

        var stResume0Label = Label( "ST_RESUME_0" );
        var stResume1Label = Label( "ST_RESUME_1" );
        var stExitLabel = Label( typeof( void ), "ST_EXIT" );

        var smVar = Variable( typeof( StateMachine2 ), "sm" );

#if FAST_COMPILER // use-local
        var completedTask0 = Variable( typeof( Task<int> ), "completedTask0" );
        var completedTask1 = Variable( typeof( Task<int> ), "completedTask1" );
#endif

        // Build the MoveNext delegate
        var moveNextLambda = Lambda<MoveNextDelegate<StateMachine2>>(
            Block(
#if FAST_COMPILER // use-local
                [completedTask0, completedTask1],
#endif

                // if (sm.__state == 0) { sm.__state = -1; goto ST_RESUME_0; }
                Switch(
                    Field( smVar, nameof( StateMachine2.__state ) ),
                    Empty(), // Default case: do nothing
                    SwitchCase(
                        Block(
                            Assign(
                                Field( smVar, nameof( StateMachine2.__state ) ),
                                Constant( -1 )
                            ),
                            Goto( stResume0Label )
                        ),
                        Constant( 0 ) // Case for 0
                    ),
                    SwitchCase(
                        Block(
                            Assign(
                                Field( smVar, nameof( StateMachine2.__state ) ),
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
                    Field( smVar, nameof( StateMachine2.__awaiter0 ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        completedTask0, // immediate result
                        Constant( false )
                    )
                ),
#else
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(10), false);
                Assign(
                    Field( smVar, nameof( StateMachine2.__awaiter0 ) ),
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
                            Field( smVar, nameof( StateMachine2.__awaiter0 ) ),
                            nameof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter.IsCompleted )
                        )
                    ),
                    Block(
                        // sm.__state = 0;
                        Assign(
                            Field( smVar, nameof( StateMachine2.__state ) ),
                            Constant( 0 )
                        ),
                        // sm.__builder.AwaitUnsafeOnCompleted(...);
                        Call(
                            Field( smVar, nameof( StateMachine2.__builder ) ), // instance
                            awaitOnUnsafeCompletedMethod, // method
                            Field( smVar, nameof( StateMachine2.__awaiter0 ) ), // ref awaiter
                            smVar // ref stateMachine
                        ),
                        // return;
                        Return( stExitLabel )
                    )
                ),
                // ST_RESUME_0: sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Label( stResume0Label ),
                Assign(
                    Field( smVar, nameof( StateMachine2.__result0 ) ),
                    Call(
                        binder.GetResultMethod,
                        Field( smVar, nameof( StateMachine2.__awaiter0 ) )
                    )
                ),

#if !FAST_COMPILER // remove-unary-variable-expression (e.g. `someVar;`)
                Field( smVar, nameof( StateMachine2.__result0 ) ), // THIS LINE IS THE CULPRIT
#endif
                // ***** SECOND AWAIT *****

#if FAST_COMPILER // use-local
                Assign(
                    completedTask1,
                    Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 42 ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine2.__awaiter1 ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        completedTask1, // immediate result
                        Constant( false )
                    )
                ),
#else
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(42), false);
                Assign(
                    Field( smVar, nameof(StateMachine2.__awaiter1) ),
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
                            Field( smVar, nameof( StateMachine2.__awaiter1 ) ),
                            nameof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter.IsCompleted )
                        )
                    ),
                    Block(
                        // sm.__state = 1;
                        Assign(
                            Field( smVar, nameof( StateMachine2.__state ) ),
                            Constant( 1 )
                        ),
                        // sm.__builder.AwaitUnsafeOnCompleted(...);
                        Call(
                            Field( smVar, nameof( StateMachine2.__builder ) ), // instance
                            awaitOnUnsafeCompletedMethod, // method
                            Field( smVar, nameof( StateMachine2.__awaiter1 ) ), // ref awaiter
                            smVar // ref stateMachine
                        ),
                        // return;
                        Return( stExitLabel )
                    )
                ),
                // ST_RESUME_1: sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Label( stResume1Label ),
                Assign(
                    Field( smVar, nameof( StateMachine2.__result1 ) ),
                    Call(
                        binder.GetResultMethod,
                        Field( smVar, nameof( StateMachine2.__awaiter1 ) )
                    )
                ),

                // ST_FINAL
                //
                // sm.__final = sm.__result;

                Assign(
                    Field( smVar, nameof( StateMachine2.__final ) ),
                    Field( smVar, nameof( StateMachine2.__result1 ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine2.__state ) ),
                    Constant( -2 )
                ),
                Call(
                    Field( smVar, nameof( StateMachine2.__builder ) ),
                    typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.SetResult ) )!,
                    Field( smVar, nameof( StateMachine2.__final ) )
                ),
                Label( stExitLabel )
            ),
            smVar // MoveNext delegate parameter
        );

        // Assign the delegate to the state machine
        var mainBlock = Block(
            [stateMachineVar],
            // stateMachine = new StateMachine1();
            Assign( stateMachineVar, New( typeof( StateMachine2 ) ) ),
            // stateMachine.__state = -1;
            Assign(
                Field( stateMachineVar, nameof( StateMachine2.__state ) ),
                Constant( -1 )
            ),
            // stateMachine.__moveNextDelegate = (StateMachine1 sm) => { ... }
            Assign(
                Field( stateMachineVar, nameof( StateMachine2.__moveNextDelegate ) ),
                moveNextLambda
            ),
            // stateMachine.__builder.Start<StateMachine1>(ref stateMachine);
            Call(
                Field( stateMachineVar, nameof( StateMachine2.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.Start ) )!.MakeGenericMethod( typeof( StateMachine2 ) ),
                stateMachineVar
            ),
            // stateMachine.__builder.Task;
            Property(
                Field( stateMachineVar, stateMachineVar.Type.GetField( nameof( StateMachine2.__builder ) )! ),
                stateMachineVar.Type.GetField( nameof( StateMachine2.__builder ) )!.FieldType.GetProperty( "Task" )!
            )
        );

        return mainBlock;
    }
}

public class StateMachine2 : IAsyncStateMachine
{
    public int __state;
    public int __final;

    public AsyncTaskMethodBuilder<int> __builder;
    public MoveNextDelegate<StateMachine2> __moveNextDelegate;

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

