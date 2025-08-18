using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using static System.Linq.Expressions.Expression;
using Expression = Hyperbee.Expressions.Lab.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

public record Person( string FirstName, string LastName );

[TestClass]
public class JsonExpressionTests
{
    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public void JsonExpression_ShouldDeserializeSuccessfully_WithServiceProvider( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var body = Expression.Json( Constant(
            """
            { 
                "FirstName": "John", 
                "LastName": "Doe"
            }
            """ ), typeof( Person ) );

        // Act
        var lambda = Lambda<Func<Person>>( body );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var result = compiledLambda();

        Console.WriteLine( result );

    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public void JsonPathExpression_ShouldSelectSuccessfully_WithServiceProvider( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var body = Expression.JsonPath(
            Expression.Json(
                Constant(
                """
                [
                    { 
                        "FirstName": "John", 
                        "LastName": "Doe"
                    },
                    { 
                        "FirstName": "Jane", 
                        "LastName": "Doe"
                    }
                ]
                """ )
            ),
            Constant( "$[1].FirstName" )
        );

        // Act
        var lambda = Lambda<Func<IEnumerable<JsonElement>>>( body );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var result = compiledLambda();

        Assert.AreEqual( "Jane", result.First().GetString() );
    }


    private static IServiceProvider GetServiceProvider()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices( ( _, services ) =>
            {
                services.AddSingleton( new JsonSerializerOptions() );
            } )
            .Build();

        return host.Services;
    }

}
