﻿using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class FastExpressionCompilerTests
{
    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Block_TryCatch_WithGoto( CompilerType compiler )
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

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Block_ShouldSuccessed_WithSimpleSwitchValue( CompilerType compiler )
    {
        var label = Label( "label" );
        var block = Block(
            Switch(
                Constant( 1 ),
                SwitchCase( Goto( label ), Constant( 1 ) )
            ),
            Label( label ),
            Constant( 2 )
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        Assert.AreEqual( 2, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Block_ShouldSuccessed_WithEmptySwitch( CompilerType compiler )
    {
        var block = Block(
            Switch(
                Constant( 1 ),
                []  // empty cases
            ),
            Constant( 2 )
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        Assert.AreEqual( 2, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Block_TryCatch_WithNestedTry( CompilerType compiler )
    {
        try
        {
            var resultValue = Parameter( typeof( int ) );
            var outerExceptionParam = Parameter( typeof( Exception ), "outerEx" );
            var innerExceptionParam = Parameter( typeof( Exception ), "innerEx" );

            var block = Block(
                [resultValue],
                TryCatch(
                    Block(
                        Throw( Constant( new Exception( "Outer Exception" ) ) ),
                        TryCatch(
                            Block(
                                Throw( Constant( new Exception( "Inner Exception" ) ) ),
                                Constant( 0 )
                            ),
                            Catch( innerExceptionParam, Assign( resultValue, Constant( 20 ) ) )
                        ),
                        Constant( 0 )
                    ),
                    Catch( outerExceptionParam, Assign( resultValue, Constant( 50 ) ) )
                ),
                resultValue
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
}