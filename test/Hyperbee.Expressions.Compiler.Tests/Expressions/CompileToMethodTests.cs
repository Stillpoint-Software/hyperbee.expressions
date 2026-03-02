using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class CompileToMethodTests
{
    private static (TypeBuilder TypeBuilder, MethodBuilder MethodBuilder) CreateStaticMethod(
        string methodName,
        Type returnType,
        Type[] parameterTypes )
    {
        var assemblyName = new AssemblyName( $"TestAssembly_{Guid.NewGuid():N}" );
        var ab = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var mb = ab.DefineDynamicModule( "TestModule" );
        var tb = mb.DefineType( "TestType", TypeAttributes.Public | TypeAttributes.Class );
        var method = tb.DefineMethod(
            methodName,
            MethodAttributes.Public | MethodAttributes.Static,
            returnType,
            parameterTypes );

        return (tb, method);
    }

    // --- Basic arithmetic ---

    [TestMethod]
    public void CompileToMethod_Add_Int_ReturnsCorrectResult()
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Add( a, b ), a, b );

        var (tb, method) = CreateStaticMethod( "Add", typeof(int), [typeof(int), typeof(int)] );
        HyperbeeCompiler.CompileToMethod( lambda, method );

        var type = tb.CreateType();
        var fn = type.GetMethod( "Add" )!;

        Assert.AreEqual( 3, fn.Invoke( null, [1, 2] ) );
        Assert.AreEqual( 0, fn.Invoke( null, [0, 0] ) );
        Assert.AreEqual( -1, fn.Invoke( null, [int.MaxValue, int.MinValue] ) );
    }

    [TestMethod]
    public void CompileToMethod_Multiply_Long_ReturnsCorrectResult()
    {
        var a = Expression.Parameter( typeof(long), "a" );
        var b = Expression.Parameter( typeof(long), "b" );
        var lambda = Expression.Lambda<Func<long, long, long>>( Expression.Multiply( a, b ), a, b );

        var (tb, method) = CreateStaticMethod( "Mul", typeof(long), [typeof(long), typeof(long)] );
        HyperbeeCompiler.CompileToMethod( lambda, method );

        var type = tb.CreateType();
        var fn = type.GetMethod( "Mul" )!;

        Assert.AreEqual( 6L, fn.Invoke( null, [2L, 3L] ) );
        Assert.AreEqual( 0L, fn.Invoke( null, [0L, 100L] ) );
    }

    // --- Conditional ---

    [TestMethod]
    public void CompileToMethod_Conditional_ReturnsCorrectBranch()
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.Condition(
                Expression.GreaterThan( x, Expression.Constant( 0 ) ),
                Expression.Multiply( x, Expression.Constant( 2 ) ),
                Expression.Negate( x )
            ), x );

        var (tb, method) = CreateStaticMethod( "Exec", typeof(int), [typeof(int)] );
        HyperbeeCompiler.CompileToMethod( lambda, method );

        var type = tb.CreateType();
        var fn = type.GetMethod( "Exec" )!;

        Assert.AreEqual( 10, fn.Invoke( null, [5] ) );
        Assert.AreEqual( 3, fn.Invoke( null, [-3] ) );
        Assert.AreEqual( 0, fn.Invoke( null, [0] ) );
    }

    // --- String constant (embeddable) ---

    [TestMethod]
    public void CompileToMethod_StringConstant_ReturnsCorrectResult()
    {
        var lambda = Expression.Lambda<Func<string>>(
            Expression.Constant( "hello" ) );

        var (tb, method) = CreateStaticMethod( "Exec", typeof(string), Type.EmptyTypes );
        HyperbeeCompiler.CompileToMethod( lambda, method );

        var type = tb.CreateType();
        var fn = type.GetMethod( "Exec" )!;

        Assert.AreEqual( "hello", fn.Invoke( null, [] ) );
    }

    // --- Void-returning method ---

    [TestMethod]
    public void CompileToMethod_VoidReturn_DoesNotThrow()
    {
        // Expression that calls a static void method
        var writeLineMethod = typeof(Console).GetMethod( "WriteLine", [typeof(int)] )!;
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Action<int>>(
            Expression.Call( writeLineMethod, x ), x );

        var (tb, method) = CreateStaticMethod( "Exec", typeof(void), [typeof(int)] );
        HyperbeeCompiler.CompileToMethod( lambda, method );

        var type = tb.CreateType();
        var fn = type.GetMethod( "Exec" )!;

        // Should not throw
        fn.Invoke( null, [42] );
    }

    // --- Block with locals ---

    [TestMethod]
    public void CompileToMethod_BlockWithLocals_ReturnsCorrectResult()
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var temp = Expression.Variable( typeof(int), "temp" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.Block(
                new[] { temp },
                Expression.Assign( temp, Expression.Multiply( x, Expression.Constant( 3 ) ) ),
                Expression.Add( temp, Expression.Constant( 1 ) )
            ), x );

        var (tb, method) = CreateStaticMethod( "Exec", typeof(int), [typeof(int)] );
        HyperbeeCompiler.CompileToMethod( lambda, method );

        var type = tb.CreateType();
        var fn = type.GetMethod( "Exec" )!;

        Assert.AreEqual( 16, fn.Invoke( null, [5] ) );   // 5*3 + 1
        Assert.AreEqual( 1, fn.Invoke( null, [0] ) );    // 0*3 + 1
    }

    // --- Comparison and boolean ---

    [TestMethod]
    public void CompileToMethod_Comparison_ReturnsCorrectResult()
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, bool>>(
            Expression.LessThan( a, b ), a, b );

        var (tb, method) = CreateStaticMethod( "Exec", typeof(bool), [typeof(int), typeof(int)] );
        HyperbeeCompiler.CompileToMethod( lambda, method );

        var type = tb.CreateType();
        var fn = type.GetMethod( "Exec" )!;

        Assert.AreEqual( true, fn.Invoke( null, [1, 2] ) );
        Assert.AreEqual( false, fn.Invoke( null, [2, 1] ) );
        Assert.AreEqual( false, fn.Invoke( null, [1, 1] ) );
    }

    // --- Multiple methods on same type ---

    [TestMethod]
    public void CompileToMethod_MultipleMethodsOnSameType_AllWork()
    {
        var assemblyName = new AssemblyName( $"TestAssembly_{Guid.NewGuid():N}" );
        var ab = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var mb = ab.DefineDynamicModule( "TestModule" );
        var tb = mb.DefineType( "MathOps", TypeAttributes.Public | TypeAttributes.Class );

        // Add method
        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var addLambda = Expression.Lambda<Func<int, int, int>>( Expression.Add( a, b ), a, b );
        var addMethod = tb.DefineMethod( "Add", MethodAttributes.Public | MethodAttributes.Static,
            typeof(int), [typeof(int), typeof(int)] );
        HyperbeeCompiler.CompileToMethod( addLambda, addMethod );

        // Subtract method
        var subLambda = Expression.Lambda<Func<int, int, int>>( Expression.Subtract( a, b ), a, b );
        var subMethod = tb.DefineMethod( "Sub", MethodAttributes.Public | MethodAttributes.Static,
            typeof(int), [typeof(int), typeof(int)] );
        HyperbeeCompiler.CompileToMethod( subLambda, subMethod );

        var type = tb.CreateType();

        Assert.AreEqual( 7, type.GetMethod( "Add" )!.Invoke( null, [3, 4] ) );
        Assert.AreEqual( -1, type.GetMethod( "Sub" )!.Invoke( null, [3, 4] ) );
    }

    // --- TryCompileToMethod ---

    [TestMethod]
    public void TryCompileToMethod_ValidExpression_ReturnsTrue()
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.Add( x, Expression.Constant( 1 ) ), x );

        var (tb, method) = CreateStaticMethod( "Exec", typeof(int), [typeof(int)] );
        var result = HyperbeeCompiler.TryCompileToMethod( lambda, method );

        Assert.IsTrue( result );

        var type = tb.CreateType();
        Assert.AreEqual( 6, type.GetMethod( "Exec" )!.Invoke( null, [5] ) );
    }

    [TestMethod]
    public void TryCompileToMethod_NonEmbeddableConstant_ReturnsFalse()
    {
        // object reference is not embeddable
        var obj = new List<int> { 1, 2, 3 };
        var lambda = Expression.Lambda<Func<List<int>>>(
            Expression.Constant( obj, typeof(List<int>) ) );

        var (_, method) = CreateStaticMethod( "Exec", typeof(List<int>), Type.EmptyTypes );
        var result = HyperbeeCompiler.TryCompileToMethod( lambda, method );

        Assert.IsFalse( result );
    }

    // --- Error cases ---

    [TestMethod]
    public void CompileToMethod_NonStaticMethod_Throws()
    {
        var lambda = Expression.Lambda<Func<int>>( Expression.Constant( 42 ) );

        var assemblyName = new AssemblyName( $"TestAssembly_{Guid.NewGuid():N}" );
        var ab = AssemblyBuilder.DefineDynamicAssembly( assemblyName, AssemblyBuilderAccess.Run );
        var mb = ab.DefineDynamicModule( "TestModule" );
        var tb = mb.DefineType( "TestType", TypeAttributes.Public | TypeAttributes.Class );
        var method = tb.DefineMethod( "Exec",
            MethodAttributes.Public, // not static
            typeof(int), Type.EmptyTypes );

        Assert.ThrowsExactly<ArgumentException>(
            () => HyperbeeCompiler.CompileToMethod( lambda, method ) );
    }

    [TestMethod]
    public void CompileToMethod_NonEmbeddableConstant_Throws()
    {
        var obj = new object();
        var lambda = Expression.Lambda<Func<object>>(
            Expression.Constant( obj, typeof(object) ) );

        var (_, method) = CreateStaticMethod( "Exec", typeof(object), Type.EmptyTypes );
        Assert.ThrowsExactly<NotSupportedException>(
            () => HyperbeeCompiler.CompileToMethod( lambda, method ) );
    }

    [TestMethod]
    public void CompileToMethod_NestedLambda_Throws()
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var inner = Expression.Lambda<Func<int>>( x );
        var lambda = Expression.Lambda<Func<int, Func<int>>>(
            inner, x );

        var (_, method) = CreateStaticMethod( "Exec", typeof(Func<int>), [typeof(int)] );
        Assert.ThrowsExactly<NotSupportedException>(
            () => HyperbeeCompiler.CompileToMethod( lambda, method ) );
    }

    [TestMethod]
    public void CompileToMethod_NullLambda_Throws()
    {
        var (_, method) = CreateStaticMethod( "Exec", typeof(int), Type.EmptyTypes );
        Assert.ThrowsExactly<ArgumentNullException>(
            () => HyperbeeCompiler.CompileToMethod( null!, method ) );
    }

    [TestMethod]
    public void CompileToMethod_NullMethod_Throws()
    {
        var lambda = Expression.Lambda<Func<int>>( Expression.Constant( 42 ) );
        Assert.ThrowsExactly<ArgumentNullException>(
            () => HyperbeeCompiler.CompileToMethod( lambda, null! ) );
    }

    // --- Switch expression ---

    [TestMethod]
    public void CompileToMethod_Switch_ReturnsCorrectResult()
    {
        var x = Expression.Parameter( typeof(int), "x" );
        var lambda = Expression.Lambda<Func<int, string>>(
            Expression.Switch(
                x,
                Expression.Constant( "other" ),
                Expression.SwitchCase( Expression.Constant( "one" ), Expression.Constant( 1 ) ),
                Expression.SwitchCase( Expression.Constant( "two" ), Expression.Constant( 2 ) ),
                Expression.SwitchCase( Expression.Constant( "three" ), Expression.Constant( 3 ) )
            ), x );

        var (tb, method) = CreateStaticMethod( "Exec", typeof(string), [typeof(int)] );
        HyperbeeCompiler.CompileToMethod( lambda, method );

        var type = tb.CreateType();
        var fn = type.GetMethod( "Exec" )!;

        Assert.AreEqual( "one", fn.Invoke( null, [1] ) );
        Assert.AreEqual( "two", fn.Invoke( null, [2] ) );
        Assert.AreEqual( "three", fn.Invoke( null, [3] ) );
        Assert.AreEqual( "other", fn.Invoke( null, [99] ) );
    }

    // --- Exception handling ---

    [TestMethod]
    public void CompileToMethod_TryCatch_ReturnsCorrectResult()
    {
        var lambda = Expression.Lambda<Func<int>>(
            Expression.TryCatch(
                Expression.Block(
                    Expression.Throw( Expression.New( typeof(InvalidOperationException) ) ),
                    Expression.Constant( 0 )
                ),
                Expression.Catch( typeof(InvalidOperationException), Expression.Constant( 42 ) )
            ) );

        var (tb, method) = CreateStaticMethod( "Exec", typeof(int), Type.EmptyTypes );
        HyperbeeCompiler.CompileToMethod( lambda, method );

        var type = tb.CreateType();
        var fn = type.GetMethod( "Exec" )!;

        Assert.AreEqual( 42, fn.Invoke( null, [] ) );
    }
}
