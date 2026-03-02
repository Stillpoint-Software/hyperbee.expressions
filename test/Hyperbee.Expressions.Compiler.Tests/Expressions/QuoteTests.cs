using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class QuoteTests
{
    // ================================================================
    // Quote returns expression tree as data
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Quote_ReturnsExpressionTree( CompilerType compilerType )
    {
        // () => (Expression<Func<int, int>>)(x => x + 1)
        var x = Expression.Parameter( typeof( int ), "x" );
        var innerLambda = Expression.Lambda<Func<int, int>>(
            Expression.Add( x, Expression.Constant( 1 ) ), x );

        var quote = Expression.Quote( innerLambda );

        var lambda = Expression.Lambda<Func<Expression<Func<int, int>>>>( quote );
        var fn = lambda.Compile( compilerType );

        var resultExpr = fn();
        Assert.IsNotNull( resultExpr );

        // Compile the returned expression and verify it works
        var compiled = resultExpr.Compile();
        Assert.AreEqual( 6, compiled( 5 ) );
    }

    // ================================================================
    // TypeEqual (exact type match)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void TypeEqual_ExactMatch_ReturnsTrue( CompilerType compilerType )
    {
        // (object o) => o.GetType() == typeof(string)
        var o = Expression.Parameter( typeof( object ), "o" );
        var lambda = Expression.Lambda<Func<object, bool>>(
            Expression.TypeEqual( o, typeof( string ) ),
            o );

        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( "hello" ) );
        Assert.IsFalse( fn( 42 ) );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void TypeEqual_DerivedType_ReturnsFalse( CompilerType compilerType )
    {
        // TypeEqual checks exact match, not inheritance
        var o = Expression.Parameter( typeof( object ), "o" );
        var lambda = Expression.Lambda<Func<object, bool>>(
            Expression.TypeEqual( o, typeof( Exception ) ),
            o );

        var fn = lambda.Compile( compilerType );

        Assert.IsTrue( fn( new Exception() ) );
        Assert.IsFalse( fn( new InvalidOperationException() ) ); // Derived, not exact
    }

    // ================================================================
    // Power (Math.Pow)
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Hyperbee )]
    public void Power_ComputesCorrectResult( CompilerType compilerType )
    {
        // (double x, double y) => Math.Pow(x, y)
        var x = Expression.Parameter( typeof( double ), "x" );
        var y = Expression.Parameter( typeof( double ), "y" );
        var lambda = Expression.Lambda<Func<double, double, double>>(
            Expression.Power( x, y ),
            x, y );

        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 8.0, fn( 2.0, 3.0 ) );
        Assert.AreEqual( 1.0, fn( 5.0, 0.0 ) );
        Assert.AreEqual( 27.0, fn( 3.0, 3.0 ) );
    }
}
