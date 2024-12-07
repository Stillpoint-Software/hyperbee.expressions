//#define _INCLUDE_ALL_TESTS
#define _WORKAROUND //BF ME

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Tests.TestSupport;
using Hyperbee.Expressions.Transformation;
// ReSharper disable InconsistentNaming

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class CompilerTests
{
#if _INCLUDE_ALL_TESTS
    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    public async Task Compiler_Test1( CompleterType completer, CompilerType compiler )
    {
        // this pattern throws a bad IL exception
        // this pattern works for flag = false; but not for flag = true;

        const bool flag = true;

        var block = ExpressionExtensions.BlockAsync(

            Expression.Condition( Expression.Constant( flag ),
                Expression.Constant( 10 ),
                ExpressionExtensions.Await( AsyncHelper.Completer(
                    Expression.Constant( completer ),
                    Expression.Constant( 20 )
                ) )
            )

        );
        var lambda = Expression.Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        await compiledLambda();
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    public void Compiler_Test2( CompleterType completer, CompilerType compiler )
    {
        const bool flag = true;

        var block = Expression.Block(

            Expression.Condition( Expression.Constant( flag ),
                Expression.Constant( 10 ),
                ExpressionExtensions.Await( AsyncHelper.Completer(
                    Expression.Constant( completer ),
                    Expression.Constant( 20 )
                ) )
            )

        );
        var lambda = Expression.Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        compiledLambda();
    }
#endif

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    public async Task Compiler_Test0( CompleterType completer, CompilerType compiler )
    {
        // this pattern throws a null reference exception

        var block = ExpressionExtensions.BlockAsync(
            ExpressionExtensions.Await( Expression.Constant( Task.FromResult( 42 ) ) )
        );

        var lambda = Expression.Lambda<Func<Task<int>>>( block );
        var compiledLambda = lambda.Compile( compiler );

        await compiledLambda();
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public async Task Compiler_Test0_Lowered( CompilerType compiler ) //BF ME
    {
        //var block = ExpressionExtensions.BlockAsync(
        //    ExpressionExtensions.Await( Expression.Constant( Task.FromResult( 42 ) ) )
        //);

        try
        {
            //var block = CreateFullExpressionTree();
            var block = CreateMinimalFailureExpressionTree();

            var lambda = Expression.Lambda<Func<Task<int>>>( block );
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
        var stateMachineVar = Expression.Variable( typeof( StateMachine1 ), "stateMachine" );
        var smVar = Expression.Variable( typeof( StateMachine1 ), "sm" );

#if _WORKAROUND
        var completedTask = Expression.Variable( typeof( Task<int> ), "completedTask" );
#endif

        // Build the MoveNext delegate
        var moveNextLambda = Expression.Lambda<MoveNextDelegate<StateMachine1>>(
            Expression.Block(

#if _WORKAROUND //BF ME
                [completedTask],
                // completedTask = Task.FromResult(42);
                Expression.Assign(
                    completedTask,
                    Expression.Call( typeof( Task ), nameof( Task.FromResult ), [typeof( int )], Expression.Constant( 42 ) )
                ),
#endif

                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(42), false);
                Expression.Assign(
                    Expression.Field( smVar, nameof( StateMachine1.__awaiter ) ),
                    Expression.Call(
                        binder.GetAwaiterMethod,
#if _WORKAROUND
                        completedTask, // immediate result
#else
                        Expression.Constant( Task.FromResult( 42 ) ), // immediate result
#endif
                        Expression.Constant( false )
                    )
                ),

                // sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Expression.Assign(
                    Expression.Field( smVar, nameof( StateMachine1.__result ) ),
                    Expression.Call(
                        binder.GetResultMethod,
                        Expression.Field( smVar, nameof( StateMachine1.__awaiter ) )
                    )
                ),
                // sm.__builder.SetResult(sm.__result);
                Expression.Call(
                    Expression.Field( smVar, nameof( StateMachine1.__builder ) ),
                    typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.SetResult ) )!,
                    Expression.Field( smVar, nameof( StateMachine1.__result ) )
                )
            ),
            smVar // MoveNext delegate parameter
        );

        // Assign the delegate to the state machine
        var mainBlock = Expression.Block(
            [stateMachineVar],
            // stateMachine = new StateMachine1();
            Expression.Assign( stateMachineVar, Expression.New( typeof( StateMachine1 ) ) ),
            // stateMachine.__moveNextDelegate = (StateMachine1 sm) => { ... }
            Expression.Assign(
                Expression.Field( stateMachineVar, nameof( StateMachine1.__moveNextDelegate ) ),
                moveNextLambda
            ),
            // stateMachine.__builder.Start<StateMachine1>(ref stateMachine);
            Expression.Call(
                Expression.Field( stateMachineVar, nameof( StateMachine1.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.Start ) )!.MakeGenericMethod( typeof( StateMachine1 ) ),
                stateMachineVar
            ),
            // stateMachine.__builder.Task;
            Expression.Property(
                Expression.Field( stateMachineVar, stateMachineVar.Type.GetField( nameof( StateMachine1.__builder ) )! ),
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
        var stateMachineVar = Expression.Variable( typeof( StateMachine1 ), "stateMachine" );

        var stResumeLabel = Expression.Label( "ST_RESUME" );
        var stExitLabel = Expression.Label( typeof( void ), "ST_EXIT" );

        var smVar = Expression.Variable( typeof( StateMachine1 ), "sm" );

        // Build the MoveNext delegate
        var moveNextLambda = Expression.Lambda<MoveNextDelegate<StateMachine1>>(
            Expression.Block(
                // if (sm.__state == 0) { sm.__state = -1; goto ST_RESUME; }
                Expression.IfThen(
                    Expression.Equal(
                        Expression.Field( smVar, nameof( StateMachine1.__state ) ),
                        Expression.Constant( 0 )
                    ),
                    Expression.Block(
                        Expression.Assign(
                            Expression.Field( smVar, nameof( StateMachine1.__state ) ),
                            Expression.Constant( -1 )
                        ),
                        Expression.Goto( stResumeLabel )
                    )
                ),
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(42), false);
                Expression.Assign(
                    Expression.Field( smVar, nameof( StateMachine1.__awaiter ) ),
                    Expression.Call(
                        binder.GetAwaiterMethod,
                        Expression.Constant( Task.FromResult( 42 ) ), // immediate result
                        Expression.Constant( false )
                    )
                ),
                // if (!sm.__awaiter.IsCompleted)
                Expression.IfThen(
                    Expression.IsFalse(
                        Expression.Property(
                            Expression.Field( smVar, nameof( StateMachine1.__awaiter ) ),
                            nameof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter.IsCompleted )
                        )
                    ),
                    Expression.Block(
                        // sm.__state = 0;
                        Expression.Assign(
                            Expression.Field( smVar, nameof( StateMachine1.__state ) ),
                            Expression.Constant( 0 )
                        ),
                        // sm.__builder.AwaitUnsafeOnCompleted(...);
                        Expression.Call(
                            Expression.Field( smVar, nameof( StateMachine1.__builder ) ), // instance
                            awaitOnUnsafeCompletedMethod, // method
                            Expression.Field( smVar, nameof( StateMachine1.__awaiter ) ), // ref awaiter
                            smVar // ref stateMachine
                        ),
                        // return;
                        Expression.Return( stExitLabel )
                    )
                ),
                // ST_RESUME: sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Expression.Label( stResumeLabel ),
                Expression.Assign(
                    Expression.Field( smVar, nameof( StateMachine1.__result ) ),
                    Expression.Call(
                        binder.GetResultMethod,
                        Expression.Field( smVar, nameof( StateMachine1.__awaiter ) )
                    )
                ),
                // ST_FINAL: sm.__final = sm.__result;
                Expression.Assign(
                    Expression.Field( smVar, nameof( StateMachine1.__final ) ),
                    Expression.Field( smVar, nameof( StateMachine1.__result ) )
                ),
                Expression.Assign(
                    Expression.Field( smVar, nameof( StateMachine1.__state ) ),
                    Expression.Constant( -2 )
                ),
                Expression.Call(
                    Expression.Field( smVar, nameof( StateMachine1.__builder ) ),
                    typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.SetResult ) )!,
                    Expression.Field( smVar, nameof( StateMachine1.__final ) )
                ),
                Expression.Label( stExitLabel )
            ),
            smVar // MoveNext delegate parameter
        );

        // Assign the delegate to the state machine
        var mainBlock = Expression.Block(
            [stateMachineVar],
            // stateMachine = new StateMachine1();
            Expression.Assign( stateMachineVar, Expression.New( typeof( StateMachine1 ) ) ),
            // stateMachine.__state = -1;
            Expression.Assign(
                Expression.Field( stateMachineVar, nameof( StateMachine1.__state ) ),
                Expression.Constant( -1 )
            ),
            // stateMachine.__moveNextDelegate = (StateMachine1 sm) => { ... }
            Expression.Assign(
                Expression.Field( stateMachineVar, nameof( StateMachine1.__moveNextDelegate ) ),
                moveNextLambda
            ),
            // stateMachine.__builder.Start<StateMachine1>(ref stateMachine);
            Expression.Call(
                Expression.Field( stateMachineVar, nameof( StateMachine1.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.Start ) )!.MakeGenericMethod( typeof( StateMachine1 ) ),
                stateMachineVar
            ),
            // stateMachine.__builder.Task;
            Expression.Property(
                Expression.Field( stateMachineVar, stateMachineVar.Type.GetField( nameof( StateMachine1.__builder ) )! ),
                stateMachineVar.Type.GetField( nameof( StateMachine1.__builder ) )!.FieldType.GetProperty( "Task" )!
            )
        );

        return mainBlock;
    }
}

public class StateMachine1 : IAsyncStateMachine
{
    public int __state;
    public int __result;
    public int __final;

    public AsyncTaskMethodBuilder<int> __builder;
    public ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter __awaiter;
    public MoveNextDelegate<StateMachine1> __moveNextDelegate;

    public void MoveNext()
    {
        __moveNextDelegate( this );
    }

    public void SetStateMachine( IAsyncStateMachine stateMachine )
    {
        __builder.SetStateMachine( stateMachine );
    }
}
