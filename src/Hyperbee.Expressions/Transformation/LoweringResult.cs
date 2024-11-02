using System.Globalization;
using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

public record LoweringResult
{
    public List<StateContext.Scope> Scopes { get; init; }
    public ParameterExpression ReturnValue { get; init; }
    public int AwaitCount { get; init; }
    public IReadOnlyCollection<ParameterExpression> Variables { get; init; }

    internal string DebugView
    {
        get
        {
            using StringWriter writer = new StringWriter( CultureInfo.CurrentCulture );
            DebugViewWriter.WriteTo( writer, Scopes, Variables );
            return writer.ToString();
        }
    }
}
