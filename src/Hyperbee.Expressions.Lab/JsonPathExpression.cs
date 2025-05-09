using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hyperbee.Json.Path;

namespace Hyperbee.Expressions.Lab;

public class JsonPathExpression : Expression
{
    public Expression JsonExpression { get; }
    public Expression Path { get; }

    public JsonPathExpression( Expression jsonExpression, Expression path )
    {
        if ( jsonExpression.Type != typeof( JsonElement ) && jsonExpression.Type != typeof( JsonNode ) )
            throw new InvalidOperationException( "Only JsonElement and JsonNode types are supported." );

        JsonExpression = jsonExpression;
        Path = path;
    }

    public override Type Type => typeof( IEnumerable<> ).MakeGenericType( JsonExpression.Type );
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        var delegateType = typeof( NodeProcessorDelegate<> ).MakeGenericType( JsonExpression.Type );
        var selectMethodInfo = typeof( JsonPath<> )
            .MakeGenericType( JsonExpression.Type )
            .GetMethod(
                "Select",
                [
                    JsonExpression.Type.MakeByRefType(),
                    typeof(string),
                    delegateType
                ]
            )!;

        return Call(
            null,
            selectMethodInfo,
            JsonExpression,
            Path,
            Default( delegateType )
        );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newJson = visitor.Visit( JsonExpression );
        var newPath = visitor.Visit( Path );

        if ( newJson == JsonExpression && newPath == Path )
            return this;

        return new JsonPathExpression( newJson, newPath );
    }
}

public static partial class ExpressionExtensions
{
    public static JsonPathExpression JsonPath( Expression jsonExpression, Expression path )
    {
        return new JsonPathExpression( jsonExpression, path );
    }
}
