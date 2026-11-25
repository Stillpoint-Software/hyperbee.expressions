using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;

#if FAST_COMPILER
using FastExpressionCompiler;
#endif

namespace Hyperbee.Expressions.Tests.Compiler;

// The following tests are to ensure compatibility with both SystemCompiler and FastExpressionCompiler.
// The tests are based on the expression-tree patterns used in the generated state-machine.

[TestClass]
public class CompilerCompatibilityTests
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

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithCustomDelegateParameter( CompilerType compiler )
    {
        var executeDelegate = typeof( TestClass2 ).GetMethod( nameof( TestClass2.ExecuteDelegate ) );
        var local = Variable( typeof( int ), "local" );

        var innerLambda =
            Lambda<Func<int>>(
                Block(
                    [local],
                    Assign( local, Constant( 42 ) ),
                    Call( executeDelegate!, Lambda<Func<int>>( local ) )
                )
            );

        var body = Lambda<Func<int>>(
            Call( executeDelegate, innerLambda )
        );

        var compiledLambda = body.Compile( compiler );

        var result = compiledLambda();

        Assert.AreEqual( 42, result );
    }

    [TestMethod]
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

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithUnusedValue( CompilerType compiler )
    {
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

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithSimpleSwitchValue( CompilerType compiler )
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

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithEmptySwitch( CompilerType compiler )
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

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithGotoInTry( CompilerType compiler )
    {
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

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    public void Compile_ShouldSucceed_WithGotoLabelOutsideTry( CompilerType compiler )
    {
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

#if FAST_COMPILER
    [TestMethod]
    public void CompileFast_ShouldCompile_ReturnGotoFromTryCatchWithAssign()
    {
        // Return( label, Assign( ... ) ) out of a TryCatch. Against FEC 5.x this emitted invalid
        // IL: FEC error 1007 covers Return gotos out of TryCatch, but its detection missed values
        // that are compound expressions containing assignments, so it neither compiled the pattern
        // nor declined it. This test used to assert that FEC returned null, on the expectation that
        // issue 495 would be fixed by detection rather than by support.
        //
        // FEC 6.0.0 compiles it, so the original assertion no longer describes anything. The test
        // is kept as a regression guard in the shape that cannot go stale: compile with both and
        // require them to agree. If FEC regresses to miscompiling, the values diverge; if it starts
        // declining instead, the Fast delegate is null.
        // Original issue: https://github.com/dadhi/FastExpressionCompiler/issues/495

        // Arrange: Build expression with Return(label, Assign(...)) inside TryCatch
        var variable = Variable( typeof( object ), "var" );
        var finalResult = Variable( typeof( object ), "finalResult" );
        var returnLabel = Label( typeof( object ), "return" );
        var exceptionParam = Parameter( typeof( Exception ), "ex" );

        var block = Block(
            new[] { variable, finalResult },
            TryCatch(
                Block(
                    typeof( void ),
                    Assign( variable, Constant( "hello", typeof( object ) ) ),
                    IfThen(
                        NotEqual( variable, Constant( null, typeof( object ) ) ),
                        // FEC should detect this as error 1007 and reject it
                        Return( returnLabel, Assign( finalResult, variable ), typeof( object ) )
                    ),
                    Assign( finalResult, Constant( "default", typeof( object ) ) ),
                    Label( returnLabel, Constant( "fallback", typeof( object ) ) )
                ),
                Catch( exceptionParam, Empty() )
            ),
            finalResult
        );

        var lambda = Lambda<Func<object>>( block );

        // Act
        var fast = lambda.CompileFast( ifFastFailedReturnNull: true );
        var system = lambda.Compile();

        // Assert
        Assert.IsNotNull( fast,
            "FEC 6.0.0 compiles this pattern. A null here means FEC now declines it, which is safe "
            + "but changes what this test is guarding: record it as an FecGap.Declines instead." );

        Assert.AreEqual( system(), fast(),
            "FEC and the System compiler disagree on Return( label, Assign( ... ) ) out of a TryCatch. "
            + "See https://github.com/dadhi/FastExpressionCompiler/issues/495" );
    }
#endif
}
