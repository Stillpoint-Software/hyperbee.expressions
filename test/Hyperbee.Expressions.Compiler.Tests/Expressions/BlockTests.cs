using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class BlockTests
{
    // ================================================================
    // Single expression block returns its value
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_SingleExpression_ReturnsValue( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var block = Expression.Block( x );
        var lambda = Expression.Lambda<Func<int, int>>( block, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( 0, fn( 0 ) );
    }

    // ================================================================
    // Block with local variable assignment and read
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_AssignAndReturn_SingleVariable( CompilerType compilerType )
    {
        var temp = Expression.Variable( typeof(int), "temp" );
        var body = Expression.Block(
            new[] { temp },
            Expression.Assign( temp, Expression.Constant( 99 ) ),
            temp );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }

    // ================================================================
    // Block result is last expression
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_ResultIsLastExpression( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Block(
            Expression.Multiply( x, Expression.Constant( 2 ) ),
            Expression.Add( x, Expression.Constant( 10 ) ) ); // last expr is result
        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 15, fn( 5 ) );  // 5 + 10
        Assert.AreEqual( 10, fn( 0 ) );  // 0 + 10
    }

    // ================================================================
    // Block with intermediate void expressions (side-effects discarded)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_IntermediateVoidExpressions_IgnoredForResult( CompilerType compilerType )
    {
        var sideEffect = Expression.Variable( typeof(int), "sideEffect" );
        var body = Expression.Block(
            new[] { sideEffect },
            Expression.Assign( sideEffect, Expression.Constant( 1 ) ),
            Expression.Assign( sideEffect, Expression.Constant( 2 ) ),
            Expression.Assign( sideEffect, Expression.Constant( 3 ) ),
            sideEffect );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn() );
    }

    // ================================================================
    // Block with multiple variables
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_MultipleVariables_IndependentAssignment( CompilerType compilerType )
    {
        var a = Expression.Variable( typeof(int), "a" );
        var b = Expression.Variable( typeof(int), "b" );
        var body = Expression.Block(
            new[] { a, b },
            Expression.Assign( a, Expression.Constant( 10 ) ),
            Expression.Assign( b, Expression.Constant( 20 ) ),
            Expression.Add( a, b ) );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 30, fn() );
    }

    // ================================================================
    // Block with explicit type (void block)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_VoidBlock_DoesNotReturnValue( CompilerType compilerType )
    {
        var counter = Expression.Variable( typeof(int), "counter" );
        var body = Expression.Block(
            typeof( void ),
            new[] { counter },
            Expression.Assign( counter, Expression.Constant( 0 ) ),
            Expression.Assign( counter, Expression.Add( counter, Expression.Constant( 1 ) ) ) );
        var lambda = Expression.Lambda<Action>( body );
        var fn = lambda.Compile( compilerType );

        fn(); // Should not throw
    }

    // ================================================================
    // Block with explicit type different from last expression
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_WithExplicitType_UpcastToObject( CompilerType compilerType )
    {
        // Explicit type = object, last expr = string constant
        var body = Expression.Block(
            typeof( object ),
            Expression.Constant( "hello" ) );
        var lambda = Expression.Lambda<Func<object>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn() );
    }

    // ================================================================
    // Nested blocks
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_NestedBlocks_InnerValuePropagates( CompilerType compilerType )
    {
        var inner = Expression.Block(
            Expression.Constant( 5 ),
            Expression.Constant( 10 ) ); // inner returns 10
        var outer = Expression.Block(
            inner,
            Expression.Constant( 99 ) ); // outer returns 99
        var lambda = Expression.Lambda<Func<int>>( outer );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 99, fn() );
    }

    // ================================================================
    // Block with parameter from lambda used in nested block
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_WritingToParameter_ThenReturn( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var body = Expression.Block(
            Expression.Assign( x, Expression.Multiply( x, Expression.Constant( 2 ) ) ),
            Expression.Add( x, Expression.Constant( 1 ) ) );
        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 11, fn( 5 ) ); // 5*2 = 10, 10+1 = 11
        Assert.AreEqual( 1, fn( 0 ) );  // 0*2 = 0,  0+1 = 1
    }

    // ================================================================
    // Block with chained method calls
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_ChainedMethodCalls_InSequence( CompilerType compilerType )
    {
        var sb = Expression.Variable( typeof(System.Text.StringBuilder), "sb" );
        var appendMethod = typeof(System.Text.StringBuilder).GetMethod( "Append", [typeof(string)] )!;
        var toStringMethod = typeof(System.Text.StringBuilder).GetMethod( "ToString", Type.EmptyTypes )!;

        var body = Expression.Block(
            new[] { sb },
            Expression.Assign( sb, Expression.New( typeof(System.Text.StringBuilder) ) ),
            Expression.Call( sb, appendMethod, Expression.Constant( "Hello" ) ),
            Expression.Call( sb, appendMethod, Expression.Constant( " World" ) ),
            Expression.Call( sb, toStringMethod ) );
        var lambda = Expression.Lambda<Func<string>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "Hello World", fn() );
    }

    // ================================================================
    // Block variable default-initialized to zero
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_DeclaredVariable_DefaultInitialized( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        // Just declare x and return it — should be default(int) = 0
        var body = Expression.Block( new[] { x }, x );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0, fn() );
    }

    // ================================================================
    // Block with bool variable default-initialized
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_BoolVariable_DefaultInitialized( CompilerType compilerType )
    {
        var flag = Expression.Variable( typeof(bool), "flag" );
        var body = Expression.Block( new[] { flag }, flag );
        var lambda = Expression.Lambda<Func<bool>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.IsFalse( fn() );
    }

    // ================================================================
    // Block with complex sequence: declare, assign, conditional, return
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_ComplexSequence_ConditionalAssignment( CompilerType compilerType )
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var result = Expression.Variable( typeof(int), "result" );
        var body = Expression.Block(
            new[] { result },
            Expression.Assign( result, Expression.Constant( 0 ) ),
            Expression.IfThen(
                Expression.GreaterThan( x, Expression.Constant( 0 ) ),
                Expression.Assign( result, x ) ),
            result );
        var lambda = Expression.Lambda<Func<int, int>>( body, x );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn( 42 ) );
        Assert.AreEqual( 0, fn( -5 ) );
        Assert.AreEqual( 0, fn( 0 ) );
    }

    // ================================================================
    // Block with multiple assignments to same variable (last wins)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_MultipleAssignments_LastValueWins( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 1 ) ),
            Expression.Assign( x, Expression.Constant( 2 ) ),
            Expression.Assign( x, Expression.Constant( 3 ) ),
            x );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 3, fn() );
    }

    // ================================================================
    // Block with accumulation (similar to a for-loop body)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_AccumulationPattern( CompilerType compilerType )
    {
        var n = Expression.Parameter( typeof(int), "n" );
        var sum = Expression.Variable( typeof(int), "sum" );
        // Block: sum = 0; sum += n; sum += n; return sum  → returns 2*n
        var body = Expression.Block(
            new[] { sum },
            Expression.Assign( sum, Expression.Constant( 0 ) ),
            Expression.AddAssign( sum, n ),
            Expression.AddAssign( sum, n ),
            sum );
        var lambda = Expression.Lambda<Func<int, int>>( body, n );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 10, fn( 5 ) );
        Assert.AreEqual( 0, fn( 0 ) );
        Assert.AreEqual( -6, fn( -3 ) );
    }

    // ================================================================
    // Block returning a string (reference type local)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_StringVariable_AssignAndReturn( CompilerType compilerType )
    {
        var s = Expression.Variable( typeof(string), "s" );
        var body = Expression.Block(
            new[] { s },
            Expression.Assign( s, Expression.Constant( "hello" ) ),
            s );
        var lambda = Expression.Lambda<Func<string>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( "hello", fn() );
    }

    // ================================================================
    // Block with nullable variable
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_NullableVariable_DefaultIsNull( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int?), "x" );
        var body = Expression.Block( new[] { x }, x );
        var lambda = Expression.Lambda<Func<int?>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.IsNull( fn() );
    }

    // ================================================================
    // Block with three-level nesting
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_ThreeLevelNesting_ReturnsInnerValue( CompilerType compilerType )
    {
        var inner = Expression.Block(
            Expression.Constant( 1 ),
            Expression.Constant( 2 ) );
        var middle = Expression.Block(
            Expression.Constant( 10 ),
            inner );
        var outer = Expression.Block(
            Expression.Constant( 100 ),
            middle );
        var lambda = Expression.Lambda<Func<int>>( outer );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 2, fn() );
    }

    // ================================================================
    // Block uses parameters from enclosing lambda
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Block_UsesEnclosingLambdaParameters( CompilerType compilerType )
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var temp = Expression.Variable( typeof(int), "temp" );
        var body = Expression.Block(
            new[] { temp },
            Expression.Assign( temp, Expression.Multiply( a, b ) ),
            Expression.Add( temp, a ) );
        var lambda = Expression.Lambda<Func<int, int, int>>( body, a, b );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 18, fn( 3, 5 ) ); // temp=3*5=15, 15+3=18
        Assert.AreEqual( 0, fn( 0, 7 ) );  // temp=0*7=0, 0+0=0
    }
}
