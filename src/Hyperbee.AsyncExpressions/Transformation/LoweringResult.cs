using System.Globalization;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

public record LoweringResult
{
    public List<NodeExpression> Nodes { get; init; }
    public List<NodeScope> Scopes { get; init; }
    public ParameterExpression ReturnValue { get; init; }
    public int AwaitCount { get; init; }
    public HashSet<ParameterExpression> Variables { get; init; }

    internal string DebugView
    {
        get
        {
            using StringWriter writer = new StringWriter( CultureInfo.CurrentCulture );
            DebugViewWriter.WriteTo( writer, Nodes, Variables );
            return writer.ToString();
        }
    }
}
