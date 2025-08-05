using Hyperbee.Expressions.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class ConfigurationExpressionTests
{
    [TestMethod]
    [DataRow( CompilerType.Fast )]
    [DataRow( CompilerType.System )]
    [DataRow( CompilerType.Interpret )]
    public void ConfigurationExpression_ShouldConfigurationSuccessfully_WithConfiguration( CompilerType compiler )
    {
        // Arrange
        var configuration = GetServiceProvider().GetService<IConfiguration>();

        var block = ConfigurationValue<string>( configuration, Key );

        // Act
        var lambda = Lambda<Func<string>>( block );
        var compiledLambda = lambda.Compile( compiler );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( Value, result );
    }

    [TestMethod]
    [DataRow( false )]
    [DataRow( true )]
    public void ConfigurationExpression_ShouldConfigurationSuccessfully_WithCustomCompile( bool interpret )
    {
        // Arrange
        var block = ConfigurationValue<string>( Key );

        // Act
        var lambda = Lambda<Func<string>>( block );
        var compiledLambda = lambda.Compile( GetServiceProvider(), interpret );

        var result = compiledLambda();

        // Assert
        Assert.AreEqual( Value, result );
    }

    private const string Key = "Hello";
    private const string Value = "Hello, World!";

    private static IServiceProvider GetServiceProvider()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration( ( _, config ) =>
            {
                config.AddInMemoryCollection( new Dictionary<string, string>
                {
                    {Key, Value}
                } );
            } )
            .Build();

        return host.Services;
    }

}
