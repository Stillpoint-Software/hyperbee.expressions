using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Lab;

public class FetchExpression : Expression, IDependencyInjectionExpression
{
    public Expression Url { get; }
    public Expression Method { get; }
    public Expression Headers { get; }
    public Expression Content { get; }
    public Expression ClientName { get; }

    private IServiceProvider _serviceProvider;

    public FetchExpression(
        Expression url,
        Expression method,
        Expression headers = null,
        Expression content = null,
        Expression clientName = null )
    {
        ArgumentNullException.ThrowIfNull( url, nameof( url ) );
        ArgumentNullException.ThrowIfNull( method, nameof( method ) );

        if ( url.Type != typeof( string ) )
            throw new ArgumentException( "Url must be of type string.", nameof( url ) );

        if ( method.Type != typeof( HttpMethod ) )
            throw new ArgumentException( "Method must be of type HttpMethod.", nameof( method ) );

        if ( headers != null && headers.Type != typeof( Dictionary<string, string> ) )
            throw new ArgumentException( "Headers must be of type Dictionary<string, string>.", nameof( headers ) );

        Url = url;
        Method = method;
        Headers = headers;
        Content = content;
        ClientName = clientName;
    }

    public override Type Type => typeof( Task<HttpResponseMessage> );
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;

    private static readonly ConstructorInfo RequestConstructorInfo = typeof( HttpRequestMessage ).GetConstructor( [typeof( HttpMethod ), typeof( string )] );

    public override Expression Reduce()
    {
        if ( _serviceProvider == null )
            throw new InvalidOperationException( "IServiceProvider has not been set." );

        // Create service provider constant
        var providerConst = Constant( _serviceProvider );

        Expression resolveHttpClient;

        if ( ClientName != null )
        {
            // Resolve IHttpClientFactory and call CreateClient(name)
            var getFactory = Call(
                typeof( ServiceProviderServiceExtensions ),
                nameof( ServiceProviderServiceExtensions.GetRequiredService ),
                [typeof( IHttpClientFactory )],
                providerConst
            );

            var factoryVar = Variable( typeof( IHttpClientFactory ), "factory" );

            var assignFactory = Assign( factoryVar, getFactory );
            var createClient = Call( factoryVar, nameof( IHttpClientFactory.CreateClient ), null, ClientName );

            resolveHttpClient = Block(
                [factoryVar],
                assignFactory,
                createClient
            );
        }
        else
        {
            // Resolve HttpClient directly
            resolveHttpClient = Call(
                typeof( ServiceProviderServiceExtensions ),
                nameof( ServiceProviderServiceExtensions.GetRequiredService ),
                [typeof( HttpClient )],
                providerConst
            );
        }

        var clientVar = Variable( typeof( HttpClient ), "client" );
        var assignClient = Assign( clientVar, resolveHttpClient );

        // Create HttpRequestMessage
        var requestVar = Variable( typeof( HttpRequestMessage ), "request" );

        var requestCtor = New(
            RequestConstructorInfo,
            Method,
            Url
        );
        var assignRequest = Assign( requestVar, requestCtor );

        var variables = new List<ParameterExpression> { clientVar, requestVar };
        var block = new List<Expression> {
                    assignClient,
                    assignRequest
                };

        // Optional headers
        if ( Headers != null )
        {
            var headersVar = Variable( typeof( Dictionary<string, string> ), "headers" );
            variables.Add( headersVar );
            block.Add( Assign( headersVar, Headers ) );

            var kvp = Parameter( typeof( KeyValuePair<string, string> ), "kvp" );

            var addHeader = Call(
                Property( requestVar, nameof( HttpRequestMessage.Headers ) ),
                nameof( HttpRequestHeaders.Add ),
                null,
                Property( kvp, nameof( KeyValuePair<string, string>.Key ) ),
                Property( kvp, nameof( KeyValuePair<string, string>.Value ) )
            );

            block.Add( ForEach( headersVar, kvp, addHeader ) );
        }

        // Optional content
        if ( Content != null )
        {
            block.Add( Assign(
                Property( requestVar, nameof( HttpRequestMessage.Content ) ),
                Content
            ) );
        }

        // SendAsync call
        var sendCall = Call( clientVar, nameof( HttpClient.SendAsync ), null, requestVar );

        return Block(
            variables,
            block.Append( sendCall )
        );
    }

    public void SetServiceProvider( IServiceProvider serviceProvider )
    {
        _serviceProvider = serviceProvider;
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return new FetchExpression(
            visitor.Visit( Url ),
            visitor.Visit( Method ),
            Headers != null ? visitor.Visit( Headers ) : null,
            Content != null ? visitor.Visit( Content ) : null,
            ClientName
        );
    }
}

public static partial class ExpressionExtensions
{
    public static FetchExpression Fetch(
        Expression clientName,
        Expression url )
    {
        return new FetchExpression( url, Expression.Constant( HttpMethod.Get ), null, null, clientName );
    }

    public static FetchExpression Fetch( Expression clientName,
        Expression url,
        Expression method,
        Expression content = null,
        Expression headers = null )
    {
        return new FetchExpression( url, method, headers, content, clientName );
    }
}
