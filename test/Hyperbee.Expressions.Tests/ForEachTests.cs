using Hyperbee.Expressions.Tests.TestSupport;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ForEachExpressionTests
{
    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ForEachExpression_ShouldIterateOverCollection( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Variable( typeof( int ), "element" );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] );
        var body = Call( writeLineMethod!, element );

        var forEachExpr = ForEach( list, element, body );

        // Act
        var lambda = Lambda<Action>( forEachExpr );
        var compiledLambda = lambda.Compile( compiler );

        compiledLambda();

        // Assert: No assertion needed
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ForEachExpression_ShouldBreakOnCondition( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Variable( typeof( int ), "element" );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] )!;

        var forEachExpr = ForEach( list, element, ( breakLabel, continueLabel ) =>
            IfThenElse(
                Equal( element, Constant( 3 ) ),
                Break( breakLabel ),
                Call( writeLineMethod, element )
        ) );

        // Act
        var lambda = Lambda<Action>( forEachExpr );
        var compiledLambda = lambda.Compile( compiler );

        compiledLambda();

        // Assert: No assertion needed
    }

    [DataTestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ForEachExpression_ShouldUseCustomBreakAndContinueLabels( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Variable( typeof( int ), "element" );

        var customBreakLabel = Label( "customBreak" );
        var customContinueLabel = Label( "customContinue" );

        var breakCondition = Equal( element, Constant( 4 ) );
        var continueCondition = Equal( element, Constant( 2 ) );

        var writeLineMethod = typeof( Console ).GetMethod( "WriteLine", [typeof( int )] )!;

        var body = Block(
            IfThen( continueCondition, Continue( customContinueLabel ) ),
            IfThenElse(
                breakCondition,
                Break( customBreakLabel ),
                Call( writeLineMethod, element )
            )
        );

        var forEachExpr = ForEach( list, element, body, customBreakLabel, customContinueLabel );

        // Act
        var lambda = Lambda<Action>( forEachExpr );
        var compiledLambda = lambda.Compile( compiler );

        compiledLambda();

        // Assert: No assert needed
    }

    [DataTestMethod]
    [DataRow( CompleterType.Immediate, CompilerType.Fast )]
    [DataRow( CompleterType.Immediate, CompilerType.System )]
    [DataRow( CompleterType.Immediate, CompilerType.Interpret )]
    [DataRow( CompleterType.Deferred, CompilerType.Fast )]
    [DataRow( CompleterType.Deferred, CompilerType.System )]
    [DataRow( CompleterType.Deferred, CompilerType.Interpret )]
    public async Task ForEachExpression_ShouldIterateOverCollection_WithAwaits( CompleterType completer, CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Variable( typeof( int ), "element" );
        var result = Variable( typeof( int ), "result" );

        var body = Block(
                        Assign( result,
                            Add( result, Await( AsyncHelper.Completer(
                                Constant( completer ),
                                Constant( 1 )
                            )
                        ) ) )
                    );

        var forEachExpr = BlockAsync(
            [result],
            Block(
                Assign( result, Constant( 2 ) ),
                ForEach( list, element, body )
            ),
            result
        );

        // Act
        var lambda = Lambda<Func<Task<int>>>( forEachExpr );
        var compiledLambda = lambda.Compile( compiler );

        var total = await compiledLambda();

        // Assert:
        Assert.AreEqual( 7, total );
    }

    [DataTestMethod]
    //[DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    //[DataRow( CompilerType.Interpret )]
    public void ForEachExpression_ShouldIterateOverCollection_WithYields( CompilerType compiler )
    {
        // Arrange
        var list = Constant( new List<int> { 1, 2, 3, 4, 5 } );
        var element = Variable( typeof( int ), "element" );

        var forEachExpr = BlockEnumerable(
            ForEach( list, element, YieldReturn( element ) )
        );

        // Act
        var lambda = Lambda<Func<IEnumerable<int>>>( forEachExpr );
        var compiledLambda = lambda.Compile( compiler );

        var results = compiledLambda().ToArray();

        // Assert:
        Assert.AreEqual( 5, results.Length );
        Assert.AreEqual( 1, results[0] );
        Assert.AreEqual( 2, results[1] );
        Assert.AreEqual( 3, results[2] );
        Assert.AreEqual( 4, results[3] );
        Assert.AreEqual( 5, results[4] );
    }
}
