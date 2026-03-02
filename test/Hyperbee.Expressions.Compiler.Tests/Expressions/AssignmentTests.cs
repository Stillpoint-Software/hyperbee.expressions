using System.Linq.Expressions;
using Hyperbee.Expressions.Compiler.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class AssignmentTests
{
    // --- Simple variable assignment ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_Variable( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 42 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // --- AddAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 10 ) ),
            Expression.AddAssign( x, Expression.Constant( 5 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 15, fn() );
    }

    // --- SubtractAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void SubtractAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 10 ) ),
            Expression.SubtractAssign( x, Expression.Constant( 3 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 7, fn() );
    }

    // --- MultiplyAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void MultiplyAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 6 ) ),
            Expression.MultiplyAssign( x, Expression.Constant( 7 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 42, fn() );
    }

    // --- DivideAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void DivideAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 42 ) ),
            Expression.DivideAssign( x, Expression.Constant( 6 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 7, fn() );
    }

    // --- ModuloAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ModuloAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 10 ) ),
            Expression.ModuloAssign( x, Expression.Constant( 3 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1, fn() );
    }

    // --- AndAssign (bitwise) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AndAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0xFF ) ),
            Expression.AndAssign( x, Expression.Constant( 0x0F ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0x0F, fn() );
    }

    // --- OrAssign (bitwise) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void OrAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0xF0 ) ),
            Expression.OrAssign( x, Expression.Constant( 0x0F ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0xFF, fn() );
    }

    // --- ExclusiveOrAssign (bitwise) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void ExclusiveOrAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 0xFF ) ),
            Expression.ExclusiveOrAssign( x, Expression.Constant( 0x0F ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 0xF0, fn() );
    }

    // --- LeftShiftAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void LeftShiftAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 1 ) ),
            Expression.LeftShiftAssign( x, Expression.Constant( 4 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 16, fn() );
    }

    // --- RightShiftAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void RightShiftAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 256 ) ),
            Expression.RightShiftAssign( x, Expression.Constant( 4 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 16, fn() );
    }

    // --- AddAssignChecked (overflow) ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddAssignChecked_Overflow( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( int.MaxValue ) ),
            Expression.AddAssignChecked( x, Expression.Constant( 1 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        var threw = false;
        try { fn(); } catch ( OverflowException ) { threw = true; }
        Assert.IsTrue( threw, "Expected OverflowException for checked int.MaxValue + 1." );
    }

    // --- Multiple assignments in sequence ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_MultipleVariables_InSequence( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var y = Expression.Variable( typeof(int), "y" );
        var body = Expression.Block(
            new[] { x, y },
            Expression.Assign( x, Expression.Constant( 10 ) ),
            Expression.Assign( y, Expression.Constant( 20 ) ),
            Expression.Add( x, y )
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 30, fn() );
    }

    // --- Assignment expression returns the value ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_ReturnsValue( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        // Use the result of the assignment directly (not in statement position)
        var body = Expression.Block(
            new[] { x },
            Expression.Add(
                Expression.Assign( x, Expression.Constant( 10 ) ),
                Expression.Constant( 5 )
            )
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 15, fn() );
    }

    // --- Compound assign with double ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddAssign_Double( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(double), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 1.5 ) ),
            Expression.AddAssign( x, Expression.Constant( 2.5 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<double>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 4.0, fn() );
    }

    // --- PowerAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void PowerAssign_Double( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(double), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 2.0 ) ),
            Expression.PowerAssign( x, Expression.Constant( 10.0 ) ),
            x
        );
        var lambda = Expression.Lambda<Func<double>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 1024.0, fn() );
    }

    // --- PostIncrementAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void PostIncrementAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 5 ) ),
            Expression.PostIncrementAssign( x ), // returns 5, x becomes 6
            x // should be 6
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn() );
    }

    // --- PreIncrementAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void PreIncrementAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 5 ) ),
            Expression.PreIncrementAssign( x ), // x becomes 6, returns 6
            x // should be 6
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 6, fn() );
    }

    // --- PostDecrementAssign ---

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void PostDecrementAssign_Int( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof(int), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 5 ) ),
            Expression.PostDecrementAssign( x ), // returns 5, x becomes 4
            x // should be 4
        );
        var lambda = Expression.Lambda<Func<int>>( body );
        var fn = lambda.Compile( compilerType );

        Assert.AreEqual( 4, fn() );
    }
}
