//#define _INCLUDE_ALL_TESTS
#define _WORKAROUND //BF ME

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Tests.TestSupport;
using Hyperbee.Expressions.Transformation;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

// ReSharper disable InconsistentNaming

namespace Hyperbee.Expressions.Tests.Compiler;

[TestClass]
public class CompilerTests1
{
    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    public async Task Compiler_Test1( CompleterType completer, CompilerType compiler )
    {
        // this pattern now works

        var block = BlockAsync(
            Await(
                Constant( Task.FromResult( 42 ) )
            )
        );

        var lambda = Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        await compiledLambda();
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public async Task Compiler_Test1_Lowered( CompilerType compiler )
    {
        //var block = ExpressionExtensions.BlockAsync(
        //    ExpressionExtensions.Await( Expression.Constant( Task.FromResult( 42 ) ) )
        //);

        try
        {
            //var block = CreateFullExpressionTree();
            var block = CreateMinimalFailureExpressionTree();

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
        var stateMachineVar = Variable( typeof( StateMachine1 ), "stateMachine" );
        var smVar = Variable( typeof( StateMachine1 ), "sm" );

#if _WORKAROUND
        var completedTask = Variable( typeof( Task<int> ), "completedTask" );
#endif

        // Build the MoveNext delegate
        var moveNextLambda = Lambda<MoveNextDelegate<StateMachine1>>(
            Block(
#if _WORKAROUND
                [completedTask],
#endif

                // ***** FIRST AWAIT *****

#if _WORKAROUND //BF ME
                Assign(
                    completedTask,
                    Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Constant( 42 ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine1.__awaiter ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        completedTask, // immediate result
                        Constant( false )
                    )
                ),
#else
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(42), false);
                Assign(
                    Field( smVar, nameof( StateMachine1.__awaiter ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        Expression.Constant( Task.FromResult( 42 ) ), // immediate result
                        Constant( false )
                    )
                ),
#endif
                // sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Assign(
                    Field( smVar, nameof( StateMachine1.__result ) ),
                    Call(
                        binder.GetResultMethod,
                        Field( smVar, nameof( StateMachine1.__awaiter ) )
                    )
                ),

                // ***** FINAL RESULT *****
                Call(
                    Field( smVar, nameof( StateMachine1.__builder ) ),
                    typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.SetResult ) )!,
                    Field( smVar, nameof( StateMachine1.__result ) )
                )
            ),
            smVar // MoveNext delegate parameter
        );

        // Assign the delegate to the state machine
        var mainBlock = Block(
            [stateMachineVar],
            // stateMachine = new StateMachine1();
            Assign( stateMachineVar, New( typeof( StateMachine1 ) ) ),
            // stateMachine.__moveNextDelegate = (StateMachine1 sm) => { ... }
            Assign(
                Field( stateMachineVar, nameof( StateMachine1.__moveNextDelegate ) ),
                moveNextLambda
            ),
            // stateMachine.__builder.Start<StateMachine1>(ref stateMachine);
            Call(
                Field( stateMachineVar, nameof( StateMachine1.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.Start ) )!.MakeGenericMethod( typeof( StateMachine1 ) ),
                stateMachineVar
            ),
            // stateMachine.__builder.Task;
            Property(
                Field( stateMachineVar, stateMachineVar.Type.GetField( nameof( StateMachine1.__builder ) )! ),
                stateMachineVar.Type.GetField( nameof( StateMachine1.__builder ) )!.FieldType.GetProperty( "Task" )!
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
                typeof( StateMachine1 )
        );

        // Define variables
        var stateMachineVar = Variable( typeof( StateMachine1 ), "stateMachine" );

        var stResumeLabel = Label( "ST_RESUME" );
        var stExitLabel = Label( typeof( void ), "ST_EXIT" );

        var smVar = Variable( typeof( StateMachine1 ), "sm" );

        // Build the MoveNext delegate
        var moveNextLambda = Lambda<MoveNextDelegate<StateMachine1>>(
            Block(
                // if (sm.__state == 0) { sm.__state = -1; goto ST_RESUME; }
                IfThen(
                    Equal(
                        Field( smVar, nameof( StateMachine1.__state ) ),
                        Constant( 0 )
                    ),
                    Block(
                        Assign(
                            Field( smVar, nameof( StateMachine1.__state ) ),
                            Constant( -1 )
                        ),
                        Goto( stResumeLabel )
                    )
                ),
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(42), false);
                Assign(
                    Field( smVar, nameof( StateMachine1.__awaiter ) ),
                    Call(
                        binder.GetAwaiterMethod,
                        Constant( Task.FromResult( 42 ) ), // immediate result
                        Constant( false )
                    )
                ),
                // if (!sm.__awaiter.IsCompleted)
                IfThen(
                    IsFalse(
                        Property(
                            Field( smVar, nameof( StateMachine1.__awaiter ) ),
                            nameof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter.IsCompleted )
                        )
                    ),
                    Block(
                        // sm.__state = 0;
                        Assign(
                            Field( smVar, nameof( StateMachine1.__state ) ),
                            Constant( 0 )
                        ),
                        // sm.__builder.AwaitUnsafeOnCompleted(...);
                        Call(
                            Field( smVar, nameof( StateMachine1.__builder ) ), // instance
                            awaitOnUnsafeCompletedMethod, // method
                            Field( smVar, nameof( StateMachine1.__awaiter ) ), // ref awaiter
                            smVar // ref stateMachine
                        ),
                        // return;
                        Return( stExitLabel )
                    )
                ),
                // ST_RESUME: sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Label( stResumeLabel ),
                Assign(
                    Field( smVar, nameof( StateMachine1.__result ) ),
                    Call(
                        binder.GetResultMethod,
                        Field( smVar, nameof( StateMachine1.__awaiter ) )
                    )
                ),
                // ST_FINAL: sm.__final = sm.__result;
                Assign(
                    Field( smVar, nameof( StateMachine1.__final ) ),
                    Field( smVar, nameof( StateMachine1.__result ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine1.__state ) ),
                    Constant( -2 )
                ),
                Call(
                    Field( smVar, nameof( StateMachine1.__builder ) ),
                    typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.SetResult ) )!,
                    Field( smVar, nameof( StateMachine1.__final ) )
                ),
                Label( stExitLabel )
            ),
            smVar // MoveNext delegate parameter
        );

        // Assign the delegate to the state machine
        var mainBlock = Block(
            [stateMachineVar],
            // stateMachine = new StateMachine1();
            Assign( stateMachineVar, New( typeof( StateMachine1 ) ) ),
            // stateMachine.__state = -1;
            Assign(
                Field( stateMachineVar, nameof( StateMachine1.__state ) ),
                Constant( -1 )
            ),
            // stateMachine.__moveNextDelegate = (StateMachine1 sm) => { ... }
            Assign(
                Field( stateMachineVar, nameof( StateMachine1.__moveNextDelegate ) ),
                moveNextLambda
            ),
            // stateMachine.__builder.Start<StateMachine1>(ref stateMachine);
            Call(
                Field( stateMachineVar, nameof( StateMachine1.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.Start ) )!.MakeGenericMethod( typeof( StateMachine1 ) ),
                stateMachineVar
            ),
            // stateMachine.__builder.Task;
            Property(
                Field( stateMachineVar, stateMachineVar.Type.GetField( nameof( StateMachine1.__builder ) )! ),
                stateMachineVar.Type.GetField( nameof( StateMachine1.__builder ) )!.FieldType.GetProperty( "Task" )!
            )
        );

        return mainBlock;
    }
}

public class StateMachine1 : IAsyncStateMachine
{
    public int __state;
    public int __final;

    public AsyncTaskMethodBuilder<int> __builder;
    public MoveNextDelegate<StateMachine1> __moveNextDelegate;

    public ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter __awaiter;

    public int __result;

    public void MoveNext()
    {
        __moveNextDelegate( this );
    }

    public void SetStateMachine( IAsyncStateMachine stateMachine )
    {
        __builder.SetStateMachine( stateMachine );
    }
}
