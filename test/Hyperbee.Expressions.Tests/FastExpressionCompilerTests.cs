using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class FastExpressionCompilerTests
{

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSuccessed_WithRefParameter( CompilerType compiler )
    {
        // TODO: FEC throws `System.InvalidProgramException: Common Language Runtime detected an invalid program.`
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSuccessed_WithUnusedValue( CompilerType compiler )
    {
        // TODO: FEC throws `System.InvalidProgramException: Common Language Runtime detected an invalid program.`
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSuccessed_WithSimpleSwitchValue( CompilerType compiler )
    {
        // TODO: FEC throws `System.NullReferenceException: Object reference not set to an instance of an object.`

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
    public void Compile_ShouldSuccessed_WithEmptySwitch( CompilerType compiler )
    {
        // TODO: FEC throws `System.ArgumentOutOfRangeException: Index was out of range.`

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
    public void Compile_ShouldSuccessed_WithGotoInTry( CompilerType compiler )
    {
        // TODO: FEC throws `System.ArgumentException: Bad label content in ILGenerator.`
        var label = Label( "label" );
        var resultValue = Parameter( typeof( int ) );
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = Block(
            [resultValue],
            TryCatch(
                Block(
                    Goto( label ),
                    Throw( Constant( new Exception( "Exception" ) ) ),
                    Label( label ),
                    Assign( resultValue, Constant( 2 ) )
                ),
                Catch( exceptionParam, Assign( resultValue, Constant( 50 ) ) )
            ),
            resultValue
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();
        Assert.AreEqual( 2, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSuccessed_WithGotoLabelOutsideTry( CompilerType compiler )
    {
        // TODO: FEC throws `System.InvalidProgramException: Common Language Runtime detected an invalid program.`

        var label = Label( "label" );
        var variable = Variable( typeof( int ), "variable" );

        var block = Block(
            [variable],
            TryCatch(
                Block(
                    Assign( variable, Constant( 5 ) ),
                    Goto( label )
                ),
                Catch(
                    typeof( Exception ),
                    Block(
                        typeof( void ),
                        Assign( variable, Constant( 10 ) )
                    )
                )
            ),
            Label( label ),
            variable
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();
        Assert.AreEqual( 5, result );

    }
}
