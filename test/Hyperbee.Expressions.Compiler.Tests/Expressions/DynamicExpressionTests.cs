using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Binder = Microsoft.CSharp.RuntimeBinder.Binder;

namespace Hyperbee.Expressions.Compiler.Tests.Expressions;

[TestClass]
public class DynamicExpressionTests
{
    [TestMethod]
    public void Dynamic_ThrowsNotSupportedException_WithClearMessage()
    {
        // Create a simple dynamic expression: ((dynamic)obj).ToString()
        var objParam = Expression.Parameter( typeof(object), "obj" );
        var callSiteBinder = Binder.InvokeMember(
            CSharpBinderFlags.None,
            "ToString",
            null,
            typeof(DynamicExpressionTests),
            new[] { CSharpArgumentInfo.Create( CSharpArgumentInfoFlags.None, null ) } );

        var dynamicExpr = Expression.Dynamic( callSiteBinder, typeof(object), objParam );
        var lambda = Expression.Lambda<Func<object, object>>( dynamicExpr, objParam );

        // Verify System compiler handles this fine
        var systemFn = lambda.Compile();
        Assert.IsNotNull( systemFn( "hello" ) );

        // Verify Hyperbee throws NotSupportedException with a clear message
        var ex = Assert.ThrowsExactly<NotSupportedException>(
            () => HyperbeeCompiler.Compile( lambda ) );

        Assert.IsTrue( ex.Message.Contains( "DynamicExpression" ),
            $"Error message should mention DynamicExpression. Got: {ex.Message}" );
        Assert.IsTrue( ex.Message.Contains( "DLR" ),
            $"Error message should mention DLR. Got: {ex.Message}" );
    }

    [TestMethod]
    public void Dynamic_TryCompile_ReturnsNull()
    {
        var objParam = Expression.Parameter( typeof(object), "obj" );
        var callSiteBinder = Binder.InvokeMember(
            CSharpBinderFlags.None,
            "ToString",
            null,
            typeof(DynamicExpressionTests),
            new[] { CSharpArgumentInfo.Create( CSharpArgumentInfoFlags.None, null ) } );

        var dynamicExpr = Expression.Dynamic( callSiteBinder, typeof(object), objParam );
        var lambda = Expression.Lambda<Func<object, object>>( dynamicExpr, objParam );

        // TryCompile should return null gracefully
        var result = HyperbeeCompiler.TryCompile( lambda );
        Assert.IsNull( result );
    }

    [TestMethod]
    public void Dynamic_CompileWithFallback_FallsToSystemCompiler()
    {
        var objParam = Expression.Parameter( typeof(object), "obj" );
        var callSiteBinder = Binder.InvokeMember(
            CSharpBinderFlags.None,
            "ToString",
            null,
            typeof(DynamicExpressionTests),
            new[] { CSharpArgumentInfo.Create( CSharpArgumentInfoFlags.None, null ) } );

        var dynamicExpr = Expression.Dynamic( callSiteBinder, typeof(object), objParam );
        var lambda = Expression.Lambda<Func<object, object>>( dynamicExpr, objParam );

        // CompileWithFallback should work via System compiler
        var fn = HyperbeeCompiler.CompileWithFallback( lambda );
        Assert.AreEqual( "hello", fn( "hello" ) );
    }
}
