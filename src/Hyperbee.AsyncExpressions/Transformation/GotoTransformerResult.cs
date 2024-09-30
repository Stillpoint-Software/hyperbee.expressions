using System.Globalization;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

public record GotoTransformerResult
{
    public List<StateNode> Nodes { get; init; }
    public IReadOnlyDictionary<LabelTarget, int> JumpCases { get; init; }
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
