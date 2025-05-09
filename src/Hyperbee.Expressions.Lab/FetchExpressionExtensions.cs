using System.Linq.Expressions;
using System.Net.Http.Json;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Lab;

public static partial class ExpressionExtensions
{
    public static Expression ReadJson( FetchExpression fetch, Type type )
    {
        return ReadJson( Await( fetch ), type );
    }

    public static Expression ReadJson( Expression response, Type type )
    {
        ArgumentNullException.ThrowIfNull( response, nameof( response ) );
        ArgumentNullException.ThrowIfNull( type, nameof( type ) );

        if ( response.Type != typeof( HttpResponseMessage ) )
            throw new ArgumentException( "Response must be of type HttpResponseMessage.", nameof( response ) );

        var readFromJsonMethodInfo = typeof( HttpContentJsonExtensions )
            .GetMethod( nameof( HttpContentJsonExtensions.ReadFromJsonAsync ),
                [typeof( HttpContent ), typeof( CancellationToken )] )!
            .MakeGenericMethod( type );

        var content = Property( response, nameof( HttpResponseMessage.Content ) );
        return Call(
            null,
            readFromJsonMethodInfo,
            content,
            Default( typeof( CancellationToken ) )
        );
    }

    public static Expression ReadText( FetchExpression fetch )
    {
        return ReadText( Await( fetch ) );
    }

    public static Expression ReadText( Expression response )
    {
        ArgumentNullException.ThrowIfNull( response, nameof( response ) );

        return Call(
            Property( response, nameof( HttpResponseMessage.Content ) ),
            nameof( HttpContent.ReadAsStringAsync ),
            Type.EmptyTypes
        );
    }

    public static Expression ReadBytes( FetchExpression fetch )
    {
        return ReadBytes( Await( fetch ) );
    }

    public static Expression ReadBytes( Expression response )
    {
        ArgumentNullException.ThrowIfNull( response, nameof( response ) );

        return Call(
            Property( response, nameof( HttpResponseMessage.Content ) ),
            nameof( HttpContent.ReadAsByteArrayAsync ),
            Type.EmptyTypes
        );
    }

    public static Expression ReadStream( FetchExpression fetch )
    {
        return ReadStream( Await( fetch ) );
    }

    public static Expression ReadStream( Expression response )
    {
        ArgumentNullException.ThrowIfNull( response, nameof( response ) );

        return Call(
            Property( response, nameof( HttpResponseMessage.Content ) ),
            nameof( HttpContent.ReadAsStreamAsync ),
            Type.EmptyTypes
        );
    }
}
