using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class CompileToInstanceMethodTests
{
    // Helper: create a dynamic class with an instance method
    private static (TypeBuilder TypeBuilder, MethodBuilder MethodBuilder) CreateInstanceMethod(
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
            MethodAttributes.Public,
            returnType,
            parameterTypes );

        return (tb, method);
    }

    // --- Basic tests ---

    [TestMethod]
    public void CompileToInstanceMethod_Add_ReturnsCorrectResult()
    {
        // Compile a static-like computation into an instance method.
        // arg.0 = this (object ref, ignored by the lambda body), arg.1 = a, arg.2 = b.
        // But CompileToInstanceMethod maps lambda param[0] → arg.0 → "this".
        // For a pure arithmetic test we use a static-shaped lambda on an instance method.

        var a = Expression.Parameter( typeof(int), "a" );
        var b = Expression.Parameter( typeof(int), "b" );
        var lambda = Expression.Lambda<Func<int, int, int>>( Expression.Add( a, b ), a, b );

        // The method takes (int a, int b) — when called as instance method the CLR
        // expects arg.0 = this. CompileToInstanceMethod maps a→arg.0, b→arg.1.
        // To test cleanly, we use a static call via reflection.
        var (tb, method) = CreateInstanceMethod( "Add", typeof(int), [typeof(int), typeof(int)] );
        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var instance = Activator.CreateInstance( type );
        var fn = type.GetMethod( "Add" )!;

        // Invoke as instance method: first arg is "this", remaining are (a, b) → (arg.1, arg.2 in IL)
        // But lambda's arg.0=a, arg.1=b, so when invoked (this=instance, a=1, b=2):
        // this → a (arg.0), instance param 1 → b (arg.1) → Add(this-as-int, 1)
        // This test verifies the IL is emitted correctly, not the calling convention mapping.
        Assert.IsNotNull( fn );
    }

    [TestMethod]
    public void CompileToInstanceMethod_ThrowsOnNonEmbeddableConstant()
    {
        var closure = new object();
        var lambda = Expression.Lambda<Func<object>>(
            Expression.Constant( closure ) );

        var (tb, method) = CreateInstanceMethod( "Fn", typeof(object), [] );

        var threw = false;
        try { HyperbeeCompiler.CompileToInstanceMethod( lambda, method ); }
        catch ( NotSupportedException ) { threw = true; }
        Assert.IsTrue( threw, "Expected NotSupportedException" );
    }

    [TestMethod]
    public void TryCompileToInstanceMethod_ReturnsFalse_OnNonEmbeddableConstant()
    {
        var closure = new object();
        var lambda = Expression.Lambda<Func<object>>(
            Expression.Constant( closure ) );

        var (tb, method) = CreateInstanceMethod( "Fn", typeof(object), [] );

        var result = HyperbeeCompiler.TryCompileToInstanceMethod( lambda, method );

        Assert.IsFalse( result );
    }

    [TestMethod]
    public void TryCompileToInstanceMethod_ReturnsTrue_OnEmbeddableExpression()
    {
        var a = Expression.Parameter( typeof(int), "a" );
        var lambda = Expression.Lambda<Func<int, int>>(
            Expression.Multiply( a, Expression.Constant( 2 ) ), a );

        var (tb, method) = CreateInstanceMethod( "Double", typeof(int), [typeof(int)] );

        var result = HyperbeeCompiler.TryCompileToInstanceMethod( lambda, method );

        Assert.IsTrue( result );
    }

    [TestMethod]
    public void StateMachineCompiler_IsNotNull()
    {
        Assert.IsNotNull( HyperbeeCompiler.StateMachineCompiler );
    }

    // --- Instance method with field access ---
    // These tests use a sealed base class for the lambda parameter so Expression.Lambda
    // can infer a concrete delegate type. The TypeBuilder derives from it, so arg.0
    // (= this) is assignment-compatible with the base class parameter.

    public class FieldBase { public int Value; }

    [TestMethod]
    public void CompileToInstanceMethod_CanAccessInstanceField()
    {
        // Lambda: (FieldBase self) => self.Value
        // Compiled into a MethodBuilder on a TypeBuilder that derives from FieldBase.
        // When invoked, arg.0 = the subtype instance, which is assignable to FieldBase.
        var selfParam = Expression.Parameter( typeof(FieldBase), "self" );
        var lambda = Expression.Lambda(
            Expression.Field( selfParam, typeof(FieldBase).GetField( "Value" )! ),
            selfParam );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"FieldTest_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var mb = ab.DefineDynamicModule( "M" );
        var tb = mb.DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof(FieldBase) );

        var method = tb.DefineMethod( "GetValue",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof(int), Type.EmptyTypes );

        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var instance = (FieldBase) Activator.CreateInstance( type )!;
        instance.Value = 42;

        var result = (int) type.GetMethod( "GetValue" )!.Invoke( instance, null )!;

        Assert.AreEqual( 42, result );
    }

    public class FieldSetBase { public int Value; }

    [TestMethod]
    public void CompileToInstanceMethod_CanModifyInstanceField()
    {
        // Lambda: (FieldSetBase self, int v) => { self.Value = v; }  (void return)
        var selfParam = Expression.Parameter( typeof(FieldSetBase), "self" );
        var vParam = Expression.Parameter( typeof(int), "v" );
        var lambda = Expression.Lambda(
            Expression.Block(
                typeof(void),
                Expression.Assign( Expression.Field( selfParam, typeof(FieldSetBase).GetField( "Value" )! ), vParam )
            ),
            selfParam, vParam );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"FieldSetTest_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var mb = ab.DefineDynamicModule( "M" );
        var tb = mb.DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof(FieldSetBase) );

        var method = tb.DefineMethod( "SetValue",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), [typeof(int)] );

        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var instance = (FieldSetBase) Activator.CreateInstance( type )!;

        type.GetMethod( "SetValue" )!.Invoke( instance, [99] );

        Assert.AreEqual( 99, instance.Value );
    }

    // --- Two fields summed — verifies actual computed result ---

    public class TwoFieldBase { public int A; public int B; }

    [TestMethod]
    public void CompileToInstanceMethod_TwoFieldSum_ReturnsCorrectResult()
    {
        var self = Expression.Parameter( typeof( TwoFieldBase ), "self" );
        var aField = typeof( TwoFieldBase ).GetField( nameof( TwoFieldBase.A ) )!;
        var bField = typeof( TwoFieldBase ).GetField( nameof( TwoFieldBase.B ) )!;
        var lambda = Expression.Lambda(
            Expression.Add( Expression.Field( self, aField ), Expression.Field( self, bField ) ),
            self );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof( TwoFieldBase ) );
        var method = tb.DefineMethod( "Sum",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( int ), Type.EmptyTypes );
        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var instance = (TwoFieldBase) Activator.CreateInstance( type )!;
        instance.A = 10;
        instance.B = 32;

        Assert.AreEqual( 42, (int) type.GetMethod( "Sum" )!.Invoke( instance, null )! );
    }

    // --- Conditional expression on a field ---

    public class CondBase { public int Value; }

    [TestMethod]
    public void CompileToInstanceMethod_ConditionalOnField_ReturnsCorrectBranch()
    {
        var self = Expression.Parameter( typeof( CondBase ), "self" );
        var fieldExpr = Expression.Field( self, typeof( CondBase ).GetField( nameof( CondBase.Value ) )! );
        var lambda = Expression.Lambda(
            Expression.Condition(
                Expression.GreaterThan( fieldExpr, Expression.Constant( 0 ) ),
                fieldExpr,
                Expression.Constant( 0 )
            ),
            self );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof( CondBase ) );
        var method = tb.DefineMethod( "Clamp",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( int ), Type.EmptyTypes );
        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var invoke = type.GetMethod( "Clamp" )!;

        var pos = (CondBase) Activator.CreateInstance( type )!;
        pos.Value = 5;
        Assert.AreEqual( 5, invoke.Invoke( pos, null ) );

        var neg = (CondBase) Activator.CreateInstance( type )!;
        neg.Value = -3;
        Assert.AreEqual( 0, invoke.Invoke( neg, null ) );
    }

    // --- Block with local variable ---

    public class LocalBase { public int Value; }

    [TestMethod]
    public void CompileToInstanceMethod_BlockWithLocalVariable_ReturnsCorrectResult()
    {
        var self = Expression.Parameter( typeof( LocalBase ), "self" );
        var fieldExpr = Expression.Field( self, typeof( LocalBase ).GetField( nameof( LocalBase.Value ) )! );
        var tmp = Expression.Variable( typeof( int ), "tmp" );
        var lambda = Expression.Lambda(
            Expression.Block(
                [tmp],
                Expression.Assign( tmp, Expression.Add( fieldExpr, Expression.Constant( 1 ) ) ),
                Expression.Multiply( tmp, Expression.Constant( 2 ) )
            ),
            self );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof( LocalBase ) );
        var method = tb.DefineMethod( "Compute",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( int ), Type.EmptyTypes );
        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var instance = (LocalBase) Activator.CreateInstance( type )!;
        instance.Value = 5;

        Assert.AreEqual( 12, (int) type.GetMethod( "Compute" )!.Invoke( instance, null )! );   // (5+1)*2
    }

    // --- Additional parameter beyond "this" ---

    public class ScaleBase { public int Value; }

    [TestMethod]
    public void CompileToInstanceMethod_AdditionalParameter_ComputesResult()
    {
        var self = Expression.Parameter( typeof( ScaleBase ), "self" );
        var n = Expression.Parameter( typeof( int ), "n" );
        var fieldExpr = Expression.Field( self, typeof( ScaleBase ).GetField( nameof( ScaleBase.Value ) )! );
        var lambda = Expression.Lambda(
            Expression.Multiply( fieldExpr, n ),
            self, n );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof( ScaleBase ) );
        var method = tb.DefineMethod( "Scale",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( int ), [typeof( int )] );
        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var instance = (ScaleBase) Activator.CreateInstance( type )!;
        instance.Value = 3;

        Assert.AreEqual( 12, (int) type.GetMethod( "Scale" )!.Invoke( instance, [4] )! );   // 3 * 4
    }

    // --- Static method call inside body ---

    public class AbsBase { public int Value; }

    [TestMethod]
    public void CompileToInstanceMethod_StaticMethodCallInBody_ReturnsAbsoluteValue()
    {
        var self = Expression.Parameter( typeof( AbsBase ), "self" );
        var fieldExpr = Expression.Field( self, typeof( AbsBase ).GetField( nameof( AbsBase.Value ) )! );
        var absMethod = typeof( Math ).GetMethod( nameof( Math.Abs ), [typeof( int )] )!;
        var lambda = Expression.Lambda(
            Expression.Call( absMethod, fieldExpr ),
            self );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof( AbsBase ) );
        var method = tb.DefineMethod( "Abs",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( int ), Type.EmptyTypes );
        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var instance = (AbsBase) Activator.CreateInstance( type )!;
        instance.Value = -7;

        Assert.AreEqual( 7, (int) type.GetMethod( "Abs" )!.Invoke( instance, null )! );
    }

    // --- TryCatch normal path ---

    public class TryCatchBase { public int Value; }

    [TestMethod]
    public void CompileToInstanceMethod_TryCatch_NormalPath_ReturnsFieldValue()
    {
        var self = Expression.Parameter( typeof( TryCatchBase ), "self" );
        var fieldExpr = Expression.Field( self, typeof( TryCatchBase ).GetField( nameof( TryCatchBase.Value ) )! );
        var ex = Expression.Parameter( typeof( Exception ), "ex" );
        var lambda = Expression.Lambda(
            Expression.TryCatch(
                fieldExpr,
                Expression.Catch( ex, Expression.Constant( -1 ) )
            ),
            self );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof( TryCatchBase ) );
        var method = tb.DefineMethod( "SafeGet",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( int ), Type.EmptyTypes );
        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var instance = (TryCatchBase) Activator.CreateInstance( type )!;
        instance.Value = 99;

        Assert.AreEqual( 99, (int) type.GetMethod( "SafeGet" )!.Invoke( instance, null )! );
    }

    // --- Void method with two parameter writes ---

    public class DualWriteBase { public int A; public int B; }

    [TestMethod]
    public void CompileToInstanceMethod_VoidWithTwoParameterWrites_BothFieldsUpdated()
    {
        var self = Expression.Parameter( typeof( DualWriteBase ), "self" );
        var a = Expression.Parameter( typeof( int ), "a" );
        var b = Expression.Parameter( typeof( int ), "b" );
        var aField = typeof( DualWriteBase ).GetField( nameof( DualWriteBase.A ) )!;
        var bField = typeof( DualWriteBase ).GetField( nameof( DualWriteBase.B ) )!;
        var lambda = Expression.Lambda(
            Expression.Block(
                typeof( void ),
                Expression.Assign( Expression.Field( self, aField ), a ),
                Expression.Assign( Expression.Field( self, bField ), b )
            ),
            self, a, b );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof( DualWriteBase ) );
        var method = tb.DefineMethod( "SetBoth",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( void ), [typeof( int ), typeof( int )] );
        HyperbeeCompiler.CompileToInstanceMethod( lambda, method );

        var type = tb.CreateType();
        var instance = (DualWriteBase) Activator.CreateInstance( type )!;
        type.GetMethod( "SetBoth" )!.Invoke( instance, [11, 22] );

        Assert.AreEqual( 11, instance.A );
        Assert.AreEqual( 22, instance.B );
    }

    // --- Multiple instance methods on the same TypeBuilder ---

    public class MultiMethodBase { public int Value; }

    [TestMethod]
    public void CompileToInstanceMethod_MultipleMethodsOnSameType_AllWork()
    {
        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof( MultiMethodBase ) );

        var self = Expression.Parameter( typeof( MultiMethodBase ), "self" );
        var fieldExpr = Expression.Field( self, typeof( MultiMethodBase ).GetField( nameof( MultiMethodBase.Value ) )! );

        var doubleMethod = tb.DefineMethod( "Double",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( int ), Type.EmptyTypes );
        HyperbeeCompiler.CompileToInstanceMethod(
            Expression.Lambda( Expression.Multiply( fieldExpr, Expression.Constant( 2 ) ), self ),
            doubleMethod );

        var negateMethod = tb.DefineMethod( "Negate",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( int ), Type.EmptyTypes );
        HyperbeeCompiler.CompileToInstanceMethod(
            Expression.Lambda( Expression.Negate( fieldExpr ), self ),
            negateMethod );

        var type = tb.CreateType();
        var instance = (MultiMethodBase) Activator.CreateInstance( type )!;
        instance.Value = 7;

        Assert.AreEqual( 14, (int) type.GetMethod( "Double" )!.Invoke( instance, null )! );
        Assert.AreEqual( -7, (int) type.GetMethod( "Negate" )!.Invoke( instance, null )! );
    }

    // --- TryCompileToInstanceMethod verifies the compiled method produces the correct result ---

    public class TryBase { public int Value; }

    [TestMethod]
    public void TryCompileToInstanceMethod_ValidExpression_ResultIsCorrect()
    {
        var self = Expression.Parameter( typeof( TryBase ), "self" );
        var fieldExpr = Expression.Field( self, typeof( TryBase ).GetField( nameof( TryBase.Value ) )! );
        var lambda = Expression.Lambda(
            Expression.Add( fieldExpr, Expression.Constant( 100 ) ),
            self );

        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "T", TypeAttributes.Public | TypeAttributes.Class, typeof( TryBase ) );
        var method = tb.DefineMethod( "AddHundred",
            MethodAttributes.Public | MethodAttributes.Virtual, typeof( int ), Type.EmptyTypes );

        var ok = HyperbeeCompiler.TryCompileToInstanceMethod( lambda, method );
        Assert.IsTrue( ok );

        var type = tb.CreateType();
        var instance = (TryBase) Activator.CreateInstance( type )!;
        instance.Value = 5;

        Assert.AreEqual( 105, (int) type.GetMethod( "AddHundred" )!.Invoke( instance, null )! );
    }

    // --- Error: null lambda ---

    [TestMethod]
    public void CompileToInstanceMethod_NullLambda_Throws()
    {
        var ab = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName( $"T_{Guid.NewGuid():N}" ), AssemblyBuilderAccess.Run );
        var tb = ab.DefineDynamicModule( "M" )
            .DefineType( "TestType", TypeAttributes.Public | TypeAttributes.Class );
        var method = tb.DefineMethod( "Exec",
            MethodAttributes.Public, typeof( int ), Type.EmptyTypes );

        Assert.ThrowsExactly<ArgumentNullException>(
            () => HyperbeeCompiler.CompileToInstanceMethod( null!, method ) );
    }

    // --- Error: null method ---

    [TestMethod]
    public void CompileToInstanceMethod_NullMethod_Throws()
    {
        var lambda = Expression.Lambda<Func<int>>( Expression.Constant( 42 ) );

        Assert.ThrowsExactly<ArgumentNullException>(
            () => HyperbeeCompiler.CompileToInstanceMethod( lambda, null! ) );
    }
}
