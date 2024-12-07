using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Tests.TestSupport;
using Hyperbee.Expressions.Transformation;
// ReSharper disable InconsistentNaming

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class CompilerTests
{
    /*
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
    */

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
    public async Task Compiler_Test0_Lowered( CompilerType compiler )  //BF ME
    {
        //var block = ExpressionExtensions.BlockAsync(
        //    ExpressionExtensions.Await( Expression.Constant( Task.FromResult( 42 ) ) )
        //);

        try
        {
            var block = CreateExpressionTree();

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

    private static BlockExpression CreateExpressionTree()
    {
        // Define all variables upfront
        var stateMachineVar = Expression.Variable( typeof( StateMachine1 ), "stateMachine" );
        var moveNextDelegateVar = Expression.Variable( typeof( MoveNextDelegate<StateMachine1> ), "moveNextDelegate" );

        // Methods to call

        var binder = AwaitBinderFactory.GetOrCreate( typeof( Task<int> ) );

        var taskFromResultMethod = typeof( Task )
            .GetMethod( "FromResult" )!
            .MakeGenericMethod( typeof( int ) );

        var awaitOnUnsafeCompletedMethod = typeof( AsyncTaskMethodBuilder<int> )
            .GetMethod( "AwaitUnsafeOnCompleted" )!
            .MakeGenericMethod(
                typeof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter ),
                typeof( StateMachine1 )
        );

        // Define labels
        var st0002Label = Expression.Label( "ST_0002" );
        var st0001Label = Expression.Label( "ST_0001" );
        var stExitLabel = Expression.Label( typeof( void ), "ST_EXIT" );

        // Build the MoveNext delegate
        var moveNextLambda = Expression.Lambda<MoveNextDelegate<StateMachine1>>(
            Expression.Block(
                // if (sm.__state != -1) goto ST_0002;
                Expression.IfThen(
                    Expression.NotEqual(
                        Expression.Field( stateMachineVar, nameof( StateMachine1.__state ) ),
                        Expression.Constant( -1 )
                    ),
                    Expression.Goto( st0002Label )
                ),
                // sm.__awaiter = AwaitBinder.GetAwaiter<int>(ref Task.FromResult(42), false);
                Expression.Assign(
                    Expression.Field( stateMachineVar, nameof( StateMachine1.__awaiter ) ),
                    Expression.Call(
                        binder.GetAwaiterMethod,
                        Expression.Call( taskFromResultMethod, Expression.Constant( 42 ) ),
                        Expression.Constant( false )
                    )
                ),
                // if (!sm.__awaiter.IsCompleted)
                Expression.IfThen(
                    Expression.IsFalse(
                        Expression.Property(
                            Expression.Field( stateMachineVar, nameof( StateMachine1.__awaiter ) ),
                            nameof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter.IsCompleted )
                        )
                    ),
                    Expression.Block(
                        // sm.__state = 0;
                        Expression.Assign(
                            Expression.Field( stateMachineVar, nameof( StateMachine1.__state ) ),
                            Expression.Constant( 0 )
                        ),
                        // sm.__builder.AwaitUnsafeOnCompleted(...);
                        Expression.Call(
                            Expression.Field( stateMachineVar, nameof( StateMachine1.__builder ) ), // instance
                            awaitOnUnsafeCompletedMethod, // method
                            Expression.Field( stateMachineVar, nameof( StateMachine1.__awaiter ) ), // ref awaiter
                            stateMachineVar // ref stateMachine
                        ),
                        // return;
                        Expression.Return( stExitLabel )
                    )
                ),
                // ST_0002: sm.__result = AwaitBinder.GetResult<int>(ref sm.__awaiter);
                Expression.Label( st0002Label ),
                Expression.Assign(
                    Expression.Field( stateMachineVar, nameof( StateMachine1.__result ) ),
                    Expression.Call(
                        binder.GetResultMethod,
                        Expression.Field( stateMachineVar, nameof( StateMachine1.__awaiter ) )
                    )
                ),
                // ST_0001: sm.__final = sm.__result;
                Expression.Label( st0001Label ),
                Expression.Assign(
                    Expression.Field( stateMachineVar, nameof( StateMachine1.__final ) ),
                    Expression.Field( stateMachineVar, nameof( StateMachine1.__result ) )
                ),
                Expression.Assign(
                    Expression.Field( stateMachineVar, nameof( StateMachine1.__state ) ),
                    Expression.Constant( -2 )
                ),
                Expression.Call(
                    Expression.Field( stateMachineVar, nameof( StateMachine1.__builder ) ),
                    typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.SetResult ) )!,
                    Expression.Field( stateMachineVar, nameof( StateMachine1.__final ) )
                ),
                Expression.Label( stExitLabel )
            ),
            stateMachineVar // MoveNext delegate parameter
        );

        // Assign the delegate to the state machine
        var mainBlock = Expression.Block(
            [stateMachineVar, moveNextDelegateVar],
            // stateMachine = new StateMachine1();
            Expression.Assign( stateMachineVar, Expression.New( typeof( StateMachine1 ) ) ),
            // stateMachine.__state = -1;
            Expression.Assign(
                Expression.Field( stateMachineVar, nameof( StateMachine1.__state ) ),
                Expression.Constant( -1 )
            ),
            // moveNextDelegate = (StateMachine1 sm) => { ... }
            Expression.Assign(
                moveNextDelegateVar,
                moveNextLambda
            ),
            // stateMachine.__moveNextDelegate = moveNextDelegate;
            Expression.Assign(
                Expression.Field( stateMachineVar, nameof( StateMachine1.__moveNextDelegate ) ),
                moveNextDelegateVar
            ),
            // stateMachine.__builder.Start<StateMachine1>(ref stateMachine);
            Expression.Call(
                Expression.Field( stateMachineVar, nameof( StateMachine1.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.Start ) )!.MakeGenericMethod( typeof( StateMachine1 ) ),
                stateMachineVar
            ),
            // stateMachine.__builder.Task;
            Expression.Call(
                Expression.Field( stateMachineVar, nameof( StateMachine1.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetProperty( nameof( AsyncTaskMethodBuilder<int>.Task ) )!.GetMethod!
            )
        );

        return mainBlock;
    }

    public class StateMachine1 : IAsyncStateMachine
    {
        public AsyncTaskMethodBuilder<int> __builder;
        public int __state;
        public ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter __awaiter;
        public int __result;
        public int __final;
        public MoveNextDelegate<StateMachine1> __moveNextDelegate;

        public void MoveNext() { }
        public void SetStateMachine( IAsyncStateMachine stateMachine ) { }
    }
}
