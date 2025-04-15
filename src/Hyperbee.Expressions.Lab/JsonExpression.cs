using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Lab;

public class JsonExpression : Expression, IDependencyInjectionExpression
{
    private IServiceProvider _serviceProvider;
    public Expression InputExpression { get; }
    public Type TargetType { get; }

    public JsonExpression( Expression inputExpression, Type targetType )
    {
        InputExpression = inputExpression;
        TargetType = targetType;
    }

    public override Type Type => TargetType;
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        var optionExpression = (Expression) (_serviceProvider?
            .GetService( typeof( JsonSerializerOptions ) ) is not JsonSerializerOptions options
            ? Default( typeof( JsonSerializerOptions ) )
            : Constant( options ));

        if ( InputExpression.Type == typeof( string ) )
        {
            // Deserialize from a string
            return Call(
                typeof( JsonSerializer ),
                nameof( JsonSerializer.Deserialize ),
                [TargetType],
                InputExpression,
                optionExpression
            );
        }

        if ( InputExpression.Type == typeof( Stream ) )
        {
            var deserializeAsyncMethodInfo = typeof( JsonSerializer )
                .GetMethod( nameof( JsonSerializer.DeserializeAsync ), [
                    typeof(Stream),
                    typeof(JsonSerializerOptions),
                    typeof(CancellationToken)
                ] )!
                .MakeGenericMethod( TargetType );

            // Deserialize from a stream
            return Await( Call(
                deserializeAsyncMethodInfo,
                InputExpression,
                optionExpression
            ) );
        }

        if ( InputExpression.Type == typeof( HttpContent ) )
        {
            var readStreamMethodInfo = typeof( HttpContent )
                .GetMethod( nameof( HttpContent.ReadAsStreamAsync ), Type.EmptyTypes )!;

            // Deserialize from HttpContent using the stream
            var readStreamAsync = Await(
                Call(
                    InputExpression,
                    readStreamMethodInfo
                )
            );

            var deserializeAsyncMethodInfo = typeof( JsonSerializer )
                .GetMethod( nameof( JsonSerializer.DeserializeAsync ), [
                    typeof(Stream),
                    typeof(JsonSerializerOptions),
                    typeof(CancellationToken)
                ] )!
                .MakeGenericMethod( TargetType );

            return Await( Call(
                deserializeAsyncMethodInfo,
                readStreamAsync,
                optionExpression,
                Default( typeof( CancellationToken ) )
            ) );
        }

        throw new InvalidOperationException( "Unsupported input type for JSON deserialization." );

    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newInput = visitor.Visit( InputExpression );

        return newInput == InputExpression
            ? this
            : new JsonExpression( newInput, TargetType );
    }

    public void SetServiceProvider( IServiceProvider serviceProvider )
    {
        _serviceProvider = serviceProvider;
    }
}

public static partial class ExpressionExtensions
{
    public static JsonExpression Json( Expression inputExpression, Type targetType = null )
    {
        return new JsonExpression( inputExpression, targetType ?? typeof( JsonElement ) );
    }
}
