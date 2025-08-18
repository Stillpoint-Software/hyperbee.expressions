using System.Reflection;
using Hyperbee.Expressions.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class InjectExpressionTests
{
    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void InjectExpression_ShouldInjectSuccessfully_WithServiceProvider( CompilerType compiler )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = Call( Inject<ITestService>( serviceProvider ), TestService.DoSomethingMethodInfo );

        // Act
        var lambda = Lambda<Func<string>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( "Hello, World!", result );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void InjectExpression_ShouldInjectSuccessfully_WithKeyedServiceProvider( CompilerType compiler )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = Call( Inject<ITestService>( serviceProvider, "TestKey" ),
                TestService.DoSomethingMethodInfo );

        // Act
        var lambda = Lambda<Func<string>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( "Hello, World! And Universe!", result );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void InjectExpression_ShouldInjectSuccessfully_WithKeyedFallback( CompilerType compiler )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var defaultService = Constant( new TestService( " Oh No!" ), typeof( ITestService ) );

        var block = Call( Inject<ITestService>( serviceProvider, "BadKey", defaultService ),
            TestService.DoSomethingMethodInfo );

        // Act
        var lambda = Lambda<Func<string>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( "Hello, World! Oh No!", result );
    }

    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    [ExpectedException( typeof( InvalidOperationException ), "Service is not available." )]
    public void InjectExpression_ShouldInjectSuccessfully_WithBadKey( CompilerType compiler )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = Call(
            Inject<ITestService>( serviceProvider, "BadKey" ),
            TestService.DoSomethingMethodInfo );

        // Act
        var lambda = Lambda<Func<string>>( block );
        var compiledLambda = lambda.Compile( compiler );

        compiledLambda();
    }

    [TestMethod]
    [DataRow( false )]
    [DataRow( true )]
    public void InjectExpression_ShouldInjectSuccessfully_WithCustomCompileGetService( bool interpret )
    {
        // Arrange
        var body = Inject<ITestService>( "TestKey" );

        // Act
        var lambda = Lambda<Func<ITestService>>( body );
        var compiledLambda = lambda.Compile( GetServiceProvider(), interpret );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( "Hello, World! And Universe!", result.DoSomething() );
    }

    [TestMethod]
    [DataRow( false )]
    [DataRow( true )]
    public void InjectExpression_ShouldInjectSuccessfully_WithCustomCompile( bool interpret )
    {
        // Arrange
        var block = Block(
            Call( Inject<ITestService>(), TestService.DoSomethingMethodInfo )
        );

        // Act
        var lambda = Lambda<Func<string>>( block );
        var compiledLambda = lambda.Compile( GetServiceProvider(), interpret );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( "Hello, World!", result );
    }

    [TestMethod]
    [DataRow( false )]
    [DataRow( true )]
    public void InjectExpression_ShouldInjectSuccessfully_WithKeyedCustomCompile( bool interpret )
    {
        // Arrange
        var block = Call( Inject<ITestService>( "TestKey" ), TestService.DoSomethingMethodInfo );

        // Act
        var lambda = Lambda<Func<string>>( block );
        var compiledLambda = lambda.Compile( GetServiceProvider(), interpret );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( "Hello, World! And Universe!", result );
    }

    private static IServiceProvider GetServiceProvider()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices( ( _, services ) =>
            {
                services.AddSingleton<ITestService, TestService>();
                services.AddKeyedSingleton<ITestService>( "TestKey", ( _, _ ) => new TestService( " And Universe!" ) );
            } )
            .Build();

        return host.Services;
    }

}

public interface ITestService
{
    string DoSomething();
}

public class TestService : ITestService
{
    public TestService() { }
    public TestService( string extra ) => Extra = extra;
    public string Extra { get; set; }

    public static MethodInfo DoSomethingMethodInfo = typeof( ITestService ).GetMethod( nameof( ITestService.DoSomething ) );

    public string DoSomething() => "Hello, World!" + Extra;
}
