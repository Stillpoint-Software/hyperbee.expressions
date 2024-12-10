#define _WITH_GOTO 

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Tests.TestSupport;
using Hyperbee.Expressions.Transformation;
using static System.Linq.Expressions.Expression;

// ReSharper disable InconsistentNaming

namespace Hyperbee.Expressions.Tests.Compiler;

[TestClass]
public class CompilerTests3
{
    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    public async Task Compiler_Test3_TryCatch( CompleterType completer, CompilerType compiler )
    {
        try
        {
            var block = CreateFullExpressionTree();

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

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    public void Compiler_Test3_TryCatchMin( CompleterType completer, CompilerType compiler )
    {
        try
        {
            var block = CreateMinimalFailureExpressionTree();

            var lambda = Lambda<Func<int>>( block );
            var compiledLambda = lambda.Compile( compiler );

            compiledLambda();
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
    public void Compiler_Test3_TryCatch_WithGoto( CompleterType completer, CompilerType compiler )
    {
        try
        {
            var stResume0Label = Label( "ST_RESUME_0" );
            var variable = Variable( typeof( int ), "variable" );

            var block = Block(
                [variable],
                TryCatch(
                    Block(
                        Assign( variable, Constant( 5 ) ),
                        Goto( stResume0Label )
                    ),
                    Catch(
                        typeof( Exception ),
                        Block(
                            typeof( void ),
                            Assign( variable, Constant( 10 ) )
                        )
                    )
                ),
                Label( stResume0Label ),

                variable
            );

            var lambda = Lambda<Func<int>>( block );
            var compiledLambda = lambda.Compile( compiler );

            compiledLambda();
        }
        catch ( Exception e )
        {
            Console.WriteLine( e );
            throw;
        }
    }

    private static BlockExpression CreateMinimalFailureExpressionTree()
    {
        var variable = Variable( typeof( int ), "variable" );
        var state = Variable( typeof( int ), "state" );

        var stResume0Label = Label( "ST_RESUME_0" );
        var stResume1Label = Label( "ST_RESUME_1" );
        var stResume2Label = Label( "ST_RESUME_2" );

        var lambda = Lambda<Func<int>>(
            Block(
                [variable, state],
                Assign( state, Constant( -1 ) ),

                Switch(
                    state,
                    Empty(), // Default case: do nothing
                    SwitchCase(
                        Goto( stResume0Label ),  // not a block
                        Constant( 0 ) // Case for 0
                    )
                ),

                Assign( variable, Constant( 5 ) ),

                Label( stResume0Label ),

                TryCatch(
                    Block(
                        typeof( void ),
                        Switch(
                            state,
                            Empty(), // Default case: do nothing
                            SwitchCase(
                                Block(
                                    Assign( variable, Constant( 15 ) ),
                                    Goto( stResume1Label )
                                ),
                                Constant( 0 )
                            )
                        ),

                        Label( stResume1Label ),

                        Assign( variable, Constant( 10 ) )
#if _WITH_GOTO
                        , Goto( stResume2Label )
#endif
                    ),
                    Catch(
                        typeof( Exception ),
                        Block(
                            typeof( void ),
                            Assign( variable, Constant( 20 ) )
                        )
                    )
                ),
                Switch(
                    state,
                    Empty(), // Default case: do nothing
                    SwitchCase(
                        Goto( stResume2Label ),
                        Constant( 1 ) // Case for 1
                    )
                ),

                Label( stResume2Label ),

                variable
            )
        );

        return Block( Invoke( lambda ) );
    }

    private static BlockExpression CreateFullExpressionTree()
    {
        // Methods to call

        var binder = AwaitBinderFactory.GetOrCreate( typeof( Task<int> ) );

        var awaitOnUnsafeCompletedMethod = typeof( AsyncTaskMethodBuilder<int> )
            .GetMethod( "AwaitUnsafeOnCompleted" )!
            .MakeGenericMethod(
                typeof( ConfiguredTaskAwaitable<int>.ConfiguredTaskAwaiter ),
                typeof( StateMachine3 )
        );

        // Define variables
        var stateMachineVar = Variable( typeof( StateMachine3 ), "stateMachine" );

        var stResume0Label = Label( "ST_RESUME_0" );
        var stResume1Label = Label( "ST_RESUME_1" );
        var stResume2Label = Label( "ST_RESUME_2" );
        var stResume3Label = Label( "ST_RESUME_3" );
        var stExitLabel = Label( typeof( void ), "ST_EXIT" );

        var smVar = Variable( typeof( StateMachine3 ), "sm" );


        // Build the MoveNext delegate
        var moveNextLambda = Lambda<MoveNextDelegate<StateMachine3>>(
            Block(
                Switch(
                    Field( smVar, nameof( StateMachine3.__state ) ),
                    Empty(), // Default case: do nothing
                    SwitchCase(
                        Goto( stResume0Label ),  // not a block
                        Constant( 0 ) // Case for 0
                    )
                ),

                Assign(
                    Field( smVar, nameof( StateMachine3.__result0 ) ),
                    Constant( 10 )
                ),

                Label( stResume0Label ),

                TryCatch(
                    Block(
                        Switch(
                            Field( smVar, nameof( StateMachine3.__state ) ),
                            Empty(), // Default case: do nothing
                            SwitchCase(
                                Block(
                                    Assign(
                                        Field( smVar, nameof( StateMachine3.__state ) ),
                                        Constant( -1 )
                                    ),
                                    Goto( stResume1Label )
                                ),
                                Constant( 0 ) // Case for 0
                            )
                        ),
                        Label( stResume1Label ),

                        Assign(
                            Field( smVar, nameof( StateMachine3.__result0 ) ),
                            Constant( 20 )
                        ),

                        Goto( stResume3Label )
                    ),
                    Catch(
                        typeof( Exception ),
                        Block(
                            typeof( void ),
                            Assign(
                                Field( smVar, nameof( StateMachine3.__try ) ),
                                Constant( 1 )
                            )
                        )
                    )
                ),
                Switch(
                    Field( smVar, nameof( StateMachine3.__try ) ),
                    Empty(), // Default case: do nothing
                    SwitchCase(
                        Goto( stResume2Label ),
                        Constant( 1 ) // Case for 1
                    )
                ),

                Label( stResume2Label ),

                Assign(
                    Field( smVar, nameof( StateMachine3.__result0 ) ),
                    Constant( 30 )
                ),

                Label( stResume3Label ),

                // ST_FINAL
                //
                // sm.__final = sm.__result;

                Assign(
                    Field( smVar, nameof( StateMachine3.__final ) ),
                    Field( smVar, nameof( StateMachine3.__result0 ) )
                ),
                Assign(
                    Field( smVar, nameof( StateMachine3.__state ) ),
                    Constant( -2 )
                ),
                Call(
                    Field( smVar, nameof( StateMachine3.__builder ) ),
                    typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.SetResult ) )!,
                    Field( smVar, nameof( StateMachine3.__final ) )
                ),
                Label( stExitLabel )
            ),
            smVar // MoveNext delegate parameter
        );

        // Assign the delegate to the state machine
        var mainBlock = Block(
            [stateMachineVar],
            // stateMachine = new StateMachine1();
            Assign( stateMachineVar, New( typeof( StateMachine3 ) ) ),
            // stateMachine.__state = -1;
            Assign(
                Field( stateMachineVar, nameof( StateMachine3.__state ) ),
                Constant( -1 )
            ),
            // stateMachine.__moveNextDelegate = (StateMachine1 sm) => { ... }
            Assign(
                Field( stateMachineVar, nameof( StateMachine3.__moveNextDelegate ) ),
                moveNextLambda
            ),
            // stateMachine.__builder.Start<StateMachine1>(ref stateMachine);
            Call(
                Field( stateMachineVar, nameof( StateMachine3.__builder ) ),
                typeof( AsyncTaskMethodBuilder<int> ).GetMethod( nameof( AsyncTaskMethodBuilder<int>.Start ) )!.MakeGenericMethod( typeof( StateMachine3 ) ),
                stateMachineVar
            ),
            // stateMachine.__builder.Task;
            Property(
                Field( stateMachineVar, stateMachineVar.Type.GetField( nameof( StateMachine3.__builder ) )! ),
                stateMachineVar.Type.GetField( nameof( StateMachine3.__builder ) )!.FieldType.GetProperty( "Task" )!
            )
        );

        return mainBlock;
    }
}

public class StateMachine3 : IAsyncStateMachine
{
    public int __state;
    public int __final;

    public AsyncTaskMethodBuilder<int> __builder;
    public MoveNextDelegate<StateMachine3> __moveNextDelegate;

    public int __try;
    public int __result0;

    public void MoveNext()
    {
        __moveNextDelegate( this );
    }

    public void SetStateMachine( IAsyncStateMachine stateMachine )
    {
        __builder.SetStateMachine( stateMachine );
    }
}

