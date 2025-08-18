using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;
using static Hyperbee.Expressions.Lab.ExpressionExtensions;

namespace Hyperbee.Expressions.Tests;

[TestClass]
public class FetchExpressionTests
{
    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldHandleGetRequest( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = BlockAsync(
            Await(
                Fetch(
                    Constant( "Test" ),
                    Constant( "/api" )
                ) )
        );

        // Act
        var lambda = Lambda<Func<Task<HttpResponseMessage>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( HttpStatusCode.OK, response.StatusCode );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldHandlePostRequest( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = BlockAsync(
            Await(
                Fetch(
                    Constant( "Test" ),
                    Constant( "/api/post" ),
                    Constant( HttpMethod.Post ),
                    Constant( new StringContent( "{\"key\":\"value\"}", Encoding.UTF8, "application/json" ) )
                ) )
        );

        // Act
        var lambda = Lambda<Func<Task<HttpResponseMessage>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( HttpStatusCode.OK, response.StatusCode );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldHandlePutRequest( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = BlockAsync(
            Await(
                Fetch(
                    Constant( "Test" ),
                    Constant( "/api/put" ),
                    Constant( HttpMethod.Put ),
                    Constant(
                        new StringContent( "{\"updateKey\":\"updateValue\"}", Encoding.UTF8, "application/json" )
                    )
                ) )
        );

        // Act
        var lambda = Lambda<Func<Task<HttpResponseMessage>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( HttpStatusCode.OK, response.StatusCode );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldHandleDeleteRequest( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = BlockAsync(
            Await(
                Fetch(
                    Constant( "Test" ),
                    Constant( "/api/delete" ),
                    Constant( HttpMethod.Delete )
                ) )
        );

        // Act
        var lambda = Lambda<Func<Task<HttpResponseMessage>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( HttpStatusCode.OK, response.StatusCode );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldHandleInvalidEndpoint( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider( new MockHttpMessageHandler( HttpStatusCode.NotFound ) );

        var block = BlockAsync(
            Await(
                Fetch(
                    Constant( "Test" ),
                    Constant( "/invalid-endpoint" ),
                    Constant( HttpMethod.Get )
                ) )
        );

        // Act
        var lambda = Lambda<Func<Task<HttpResponseMessage>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( HttpStatusCode.NotFound, response.StatusCode );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldHandleBadRequest( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider( new MockHttpMessageHandler( HttpStatusCode.BadRequest ) );

        var block = BlockAsync(
            Await(
                Fetch(
                    Constant( "Test" ),
                    Constant( "/api/bad-request" ),
                    Constant( HttpMethod.Get )
                ) )
        );

        // Act
        var lambda = Lambda<Func<Task<HttpResponseMessage>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( HttpStatusCode.BadRequest, response.StatusCode );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldHandleGetRequestWithHeaders( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var headers = new Dictionary<string, string>
        {
            { "Authorization", "Bearer test-token" },
            { "Custom-Header", "CustomValue" }
        };

        var block = BlockAsync(
            Await(
                Fetch(
                    Constant( "Test" ),
                    Constant( "/api/headers" ),
                    Constant( HttpMethod.Get ),
                    null,
                    Constant( headers )
                )
            )
        );

        // Act
        var lambda = Lambda<Func<Task<HttpResponseMessage>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( HttpStatusCode.OK, response.StatusCode );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldHandlePostRequestWithHeaders( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var headers = new Dictionary<string, string>
        {
            { "Authorization", "Bearer test-token" },
            { "Custom-Header", "CustomValue" }
        };

        var block = BlockAsync(
            Await(
                Fetch(
                    Constant( "Test" ),
                    Constant( "/api/headers-post" ),
                    Constant( HttpMethod.Post ),
                    Constant( new StringContent( "{\"key\":\"value\"}", Encoding.UTF8, "application/json" ) ),
                    Constant( headers )
                )
            )
        );

        // Act
        var lambda = Lambda<Func<Task<HttpResponseMessage>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( HttpStatusCode.OK, response.StatusCode );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldHandleNullContent( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();
        var block = BlockAsync(
            Await(
                Fetch(
                    Constant( "Test" ),
                    Constant( "/api" ),
                    Constant( HttpMethod.Post ),
                    null
                ) )
        );

        // Act
        var lambda = Lambda<Func<Task<HttpResponseMessage>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );
        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( HttpStatusCode.OK, response.StatusCode );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldReadJsonStream( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = BlockAsync(
            Await(
                ReadJson(
                    Fetch(
                        Constant( "Test" ),
                        Constant( "/api" )
                    ),
                    typeof( Dictionary<string, string> ) )
            )
        );

        // Act
        var lambda = Lambda<Func<Task<Dictionary<string, string>>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( "mockValue", response["mockKey"] );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldReadText( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = BlockAsync(
            Await( ReadText(
                Fetch( Constant( "Test" ), Constant( "/api" ) ) )
            )
        );

        // Act
        var lambda = Lambda<Func<Task<string>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        Assert.AreEqual( "{\"mockKey\":\"mockValue\"}", response );
    }

    [TestMethod]
    [DataRow( true )]
    [DataRow( false )]
    public async Task FetchExpression_ShouldReadStream( bool preferInterpretation )
    {
        // Arrange
        var serviceProvider = GetServiceProvider();

        var block = ReadStream(
            Fetch(
                Constant( "Test" ),
                Constant( "/api" )
            ) );

        // Act
        var lambda = Lambda<Func<Task<Stream>>>( block );
        var compiledLambda = lambda.Compile( serviceProvider, preferInterpretation );

        var response = await compiledLambda();

        // Assert
        var buffer = new byte[response.Length];
        response.ReadExactly( buffer );
        Assert.AreEqual( "{\"mockKey\":\"mockValue\"}", Encoding.Default.GetString( buffer ) );
    }
    private static IServiceProvider GetServiceProvider( HttpMessageHandler messageHandler = null )
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices( ( _, services ) =>
            {
                services.AddSingleton( new JsonSerializerOptions() );

                // Replace HttpClient with a mock or fake implementation for testing
                services.AddHttpClient( "Test", ( client ) =>
                    {
                        client.BaseAddress = new Uri( "https://example.com" );
                    } )
                    .ConfigurePrimaryHttpMessageHandler( () => messageHandler ?? new MockHttpMessageHandler() );
            } )
            .Build();

        return host.Services;
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public MockHttpMessageHandler( HttpStatusCode statusCode = HttpStatusCode.OK )
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken )
        {
            return Task.FromResult( new HttpResponseMessage( _statusCode )
            {
                Content = new StringContent( "{\"mockKey\":\"mockValue\"}", Encoding.UTF8, "application/json" )
            } );
        }
    }
}
