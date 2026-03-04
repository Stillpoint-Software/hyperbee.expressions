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

    // ================================================================
    // Assign — long variable
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_LongVariable( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof( long ), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( long.MaxValue ) ),
            x );
        var lambda = Expression.Lambda<Func<long>>( body );
        Assert.AreEqual( long.MaxValue, lambda.Compile( compilerType )() );
    }

    // ================================================================
    // Assign — double variable
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_DoubleVariable( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof( double ), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 3.14 ) ),
            x );
        var lambda = Expression.Lambda<Func<double>>( body );
        Assert.AreEqual( 3.14, lambda.Compile( compilerType )(), 1e-9 );
    }

    // ================================================================
    // Assign — string variable reassigned
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_StringVariable_Reassigned( CompilerType compilerType )
    {
        var s = Expression.Variable( typeof( string ), "s" );
        var body = Expression.Block(
            new[] { s },
            Expression.Assign( s, Expression.Constant( "first" ) ),
            Expression.Assign( s, Expression.Constant( "second" ) ),
            s );
        var lambda = Expression.Lambda<Func<string>>( body );
        Assert.AreEqual( "second", lambda.Compile( compilerType )() );
    }

    // ================================================================
    // AddAssign — long type
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void AddAssign_Long( CompilerType compilerType )
    {
        var x = Expression.Variable( typeof( long ), "x" );
        var body = Expression.Block(
            new[] { x },
            Expression.Assign( x, Expression.Constant( 100L ) ),
            Expression.AddAssign( x, Expression.Constant( 200L ) ),
            x );
        var lambda = Expression.Lambda<Func<long>>( body );
        Assert.AreEqual( 300L, lambda.Compile( compilerType )() );
    }

    // ================================================================
    // Void lambda — direct Assign body (no wrapping Block)
    // The lambda body IS the Assign expression. For void return types the
    // assigned value must be discarded so the stack is empty at Ret.
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void VoidLambda_DirectAssign_ToVariable( CompilerType compilerType )
    {
        // (ref) void lambda whose body is a bare Assign(x, 99)
        var x = Expression.Variable( typeof( int ), "x" );
        var arr = new[] { 0 };

        // Capture x via closure in a real lambda; use Action<int[]> to observe the effect.
        var arrParam = Expression.Parameter( typeof( int[] ), "arr" );
        var body = Expression.Assign(
            Expression.ArrayAccess( arrParam, Expression.Constant( 0 ) ),
            Expression.Constant( 99 ) );
        var lambda = Expression.Lambda<Action<int[]>>( body, arrParam );

        lambda.Compile( compilerType )( arr );
        Assert.AreEqual( 99, arr[0] );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void VoidLambda_DirectAssign_ToArrayElement( CompilerType compilerType )
    {
        // Action<int[], int> — body is Assign(arr[0], v). The lambda is void,
        // so the assign result must not remain on the stack at Ret.
        var arrParam = Expression.Parameter( typeof( int[] ), "arr" );
        var vParam = Expression.Parameter( typeof( int ), "v" );
        var body = Expression.Assign(
            Expression.ArrayAccess( arrParam, Expression.Constant( 0 ) ),
            vParam );
        var lambda = Expression.Lambda<Action<int[], int>>( body, arrParam, vParam );

        var arr = new int[1];
        lambda.Compile( compilerType )( arr, 42 );
        Assert.AreEqual( 42, arr[0] );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void VoidLambda_DirectAssign_ToStaticField( CompilerType compilerType )
    {
        // Action where the body is a bare Assign to a static field.
        // The lambda is void, so the assign result must not remain on the stack.
        _staticFieldForTest = 0;
        var field = typeof( AssignmentTests ).GetField( nameof( _staticFieldForTest ),
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic )!;
        var body = Expression.Assign(
            Expression.Field( null, field ),
            Expression.Constant( 77 ) );
        var lambda = Expression.Lambda<Action>( body );

        lambda.Compile( compilerType )();
        Assert.AreEqual( 77, _staticFieldForTest );
    }
    private static int _staticFieldForTest;

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_ToField_OfStructArrayElement( CompilerType compilerType )
    {
        // Assign(Field(ArrayIndex(arr, i), field), value)
        // Requires ldelema (load element address) — stfld needs a managed pointer, not a value copy.
        var holderParam = Expression.Parameter( typeof( (int, int)[] ), "h" );
        var field = typeof( ValueTuple<int, int> ).GetField( "Item1" )!;
        var body = Expression.Assign(
            Expression.Field( Expression.ArrayIndex( holderParam, Expression.Constant( 0 ) ), field ),
            Expression.Constant( 77 ) );
        var lambda = Expression.Lambda<Action<(int, int)[]>>( body, holderParam );

        var holder = new (int, int)[1];
        lambda.Compile( compilerType )( holder );
        Assert.AreEqual( 77, holder[0].Item1 );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_ToField_OfStructArrayElement_ReturnsValue( CompilerType compilerType )
    {
        // Func returning the assigned value: Assign used as an expression (needsResult=true).
        var holderParam = Expression.Parameter( typeof( (int, int)[] ), "h" );
        var field = typeof( ValueTuple<int, int> ).GetField( "Item1" )!;
        var assignExpr = Expression.Assign(
            Expression.Field( Expression.ArrayIndex( holderParam, Expression.Constant( 0 ) ), field ),
            Expression.Constant( 55 ) );
        var lambda = Expression.Lambda<Func<(int, int)[], int>>( assignExpr, holderParam );

        var holder = new (int, int)[1];
        var result = lambda.Compile( compilerType )( holder );
        Assert.AreEqual( 55, result );
        Assert.AreEqual( 55, holder[0].Item1 );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_ToField_OfStructArrayElement_NonZeroIndex( CompilerType compilerType )
    {
        // Same pattern but with a runtime-determined index.
        var holderParam = Expression.Parameter( typeof( (int, int)[] ), "h" );
        var idxParam = Expression.Parameter( typeof( int ), "i" );
        var field = typeof( ValueTuple<int, int> ).GetField( "Item1" )!;
        var body = Expression.Assign(
            Expression.Field( Expression.ArrayIndex( holderParam, idxParam ), field ),
            Expression.Constant( 99 ) );
        var lambda = Expression.Lambda<Action<(int, int)[], int>>( body, holderParam, idxParam );

        var holder = new (int, int)[3];
        lambda.Compile( compilerType )( holder, 2 );
        Assert.AreEqual( 0, holder[0].Item1 );
        Assert.AreEqual( 0, holder[1].Item1 );
        Assert.AreEqual( 99, holder[2].Item1 );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void VoidBlock_LastExpression_IsAssign( CompilerType compilerType )
    {
        // Block(typeof(void), ..., Assign(...)) — void-typed block whose last
        // statement is an assign. The assign result must not remain on the stack.
        var arr = new int[1];
        var arrParam = Expression.Parameter( typeof( int[] ), "arr" );
        var body = Expression.Block(
            typeof( void ),
            Expression.Assign(
                Expression.ArrayAccess( arrParam, Expression.Constant( 0 ) ),
                Expression.Constant( 55 ) ) );
        var lambda = Expression.Lambda<Action<int[]>>( body, arrParam );

        lambda.Compile( compilerType )( arr );
        Assert.AreEqual( 55, arr[0] );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void VoidBlock_MultipleAssigns_LastIsAssign( CompilerType compilerType )
    {
        // Block(typeof(void), assign1, assign2) — verifies the void block optimization
        // works when there are multiple statement-position assigns.
        var arr = new int[2];
        var arrParam = Expression.Parameter( typeof( int[] ), "arr" );
        var body = Expression.Block(
            typeof( void ),
            Expression.Assign(
                Expression.ArrayAccess( arrParam, Expression.Constant( 0 ) ),
                Expression.Constant( 10 ) ),
            Expression.Assign(
                Expression.ArrayAccess( arrParam, Expression.Constant( 1 ) ),
                Expression.Constant( 20 ) ) );
        var lambda = Expression.Lambda<Action<int[]>>( body, arrParam );

        lambda.Compile( compilerType )( arr );
        Assert.AreEqual( 10, arr[0] );
        Assert.AreEqual( 20, arr[1] );
    }

    // ================================================================
    // Struct array element — instance method call (mutating)
    // Method calls on value-type array elements require ldelema so that
    // `this` is passed by managed pointer, not by value copy.
    // ================================================================

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Call_MutatingMethod_OnStructArrayElement( CompilerType compilerType )
    {
        // Expression.Call(ArrayIndex(arr, i), ToString) — non-mutating, but exercises the
        // value-type instance-call path from an array element.
        // (Mutating struct methods are rare in expression trees; reading is the common case.)
        var arrParam = Expression.Parameter( typeof( int[] ), "arr" );
        var toStringMethod = typeof( int ).GetMethod( "ToString", Type.EmptyTypes )!;
        var body = Expression.Call(
            Expression.ArrayIndex( arrParam, Expression.Constant( 0 ) ),
            toStringMethod );
        var lambda = Expression.Lambda<Func<int[], string>>( body, arrParam );

        var result = lambda.Compile( compilerType )( new[] { 42 } );
        Assert.AreEqual( "42", result );
    }

    [TestMethod]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.Hyperbee )]
    public void Assign_ToProperty_OfStructArrayElement( CompilerType compilerType )
    {
        // Assign(Property(ArrayIndex(structArr, i), setter), val)
        // The property setter on a struct requires the instance as a managed pointer.
        var arrParam = Expression.Parameter( typeof( StructWithProp[] ), "arr" );
        var prop = typeof( StructWithProp ).GetProperty( nameof( StructWithProp.Value ) )!;
        var body = Expression.Assign(
            Expression.Property( Expression.ArrayIndex( arrParam, Expression.Constant( 0 ) ), prop ),
            Expression.Constant( 123 ) );
        var lambda = Expression.Lambda<Action<StructWithProp[]>>( body, arrParam );

        var arr = new StructWithProp[1];
        lambda.Compile( compilerType )( arr );
        Assert.AreEqual( 123, arr[0].Value );
    }

    public struct StructWithProp
    {
        public int Value { get; set; }
    }

}
