using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class FastExpressionCompilerTests
{
    public class TestClass
    {
        public int Result0 = 42;
        public int Result1;

        public static int MethodThatTakesARef( ref int argument )
        {
            return argument;
        }
    }

    public class TestClass2
    {
        public static int ExecuteDelegate( Func<int> action ) => action();
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithCustomDelegateParameter( CompilerType compiler )
    {
        // ISSUE #443: https://github.com/dadhi/FastExpressionCompiler/issues/443
        // FEC throws `FastExpressionCompiler.NotSupportedExpressionException: ParameterIsNotVariableNorInPassedParameters`

        var executeDelegate = typeof( TestClass2 ).GetMethod( nameof( TestClass2.ExecuteDelegate ) );
        var local = Variable( typeof( int ), "local" );

        var innerLambda =
            Lambda<Func<int>>(
                Block(
                    [local],
                    Assign( local, Constant( 42 ) ),

                    // Invoke works
                    //Invoke( Lambda<Func<int>>( local ) )

                    // Call does not work
                    Call( executeDelegate, Lambda<Func<int>>( local ) )
                )
            );

        var body = Lambda<Func<int>>(
            Call( executeDelegate, innerLambda )
        );

        var compiledLambda = body.Compile( compiler );

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithConstantRefParameter( CompilerType compiler )
    {
        // FEC throws `System.InvalidProgramException: Common Language Runtime detected an invalid program.`
        // WORKAROUND: Assign to local variable and pass variable by ref

        var callRefMethod = typeof( TestClass ).GetMethod( nameof( TestClass.MethodThatTakesARef ) );

        var block = Block(
            typeof( int ),
            Call( callRefMethod!, Constant( 42 ) )
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithUnusedValue( CompilerType compiler )
    {
        // FEC throws `System.InvalidProgramException: Common Language Runtime detected an invalid program.`
        // WORKAROUND: Remove the unused value from the block

        var variable = Variable( typeof( TestClass ), "testClass" );

        var block = Block(
            [variable],
            Assign(
                variable,
                New( typeof( TestClass ) )
            ),
            Block(
                Field( variable, nameof( TestClass.Result0 ) ), // Unused
                Assign(
                    Field( variable, nameof( TestClass.Result1 ) ),
                    Field( variable, nameof( TestClass.Result0 ) )
                )
            ),
            Field( variable, nameof( TestClass.Result1 ) )
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithSimpleSwitchValue( CompilerType compiler )
    {
        // FEC throws `System.NullReferenceException: Object reference not set to an instance of an object.`

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
    public void Compile_ShouldSucceed_WithEmptySwitch( CompilerType compiler )
    {
        // FEC throws `System.ArgumentOutOfRangeException: Index was out of range.`
        // WORKAROUND: Do not use empty cases

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
    public void Compile_ShouldSucceed_WithGotoInTry( CompilerType compiler )
    {
        // FEC throws `System.ArgumentException: Bad label content in ILGenerator.`

        var label = Label( "label" );
        var variable = Variable( typeof( int ) );
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = Block(
            [variable],
            TryCatch(
                Block(
                    Goto( label ),
                    Throw( Constant( new Exception( "Exception" ) ) ),
                    Label( label ),
                    Assign( variable, Constant( 2 ) )
                ),
                Catch( exceptionParam, Assign( variable, Constant( 50 ) ) )
            ),
            variable
        );

        var lambda = Lambda<Func<int>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();
        Assert.AreEqual( 2, result );
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithGotoLabelOutsideTry( CompilerType compiler )
    {
        // FEC throws `System.InvalidProgramException: Common Language Runtime detected an invalid program.`

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
