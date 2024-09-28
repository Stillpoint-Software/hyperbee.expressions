using System.Globalization;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

public record GotoTransformerResult
{
    public List<StateNode> Nodes { get; init; }
    public IReadOnlyDictionary<LabelTarget, int> JumpCases { get; init; }
    public ParameterExpression ReturnValue { get; init; }
    public int AwaitCount { get; init; }

    internal string DebugView
    {
        get
        {
            using StringWriter writer = new StringWriter( CultureInfo.CurrentCulture );
            DebugViewWriter.WriteTo( writer, Nodes );
            return writer.ToString();
        }
    }

    public void Deconstruct( out List<StateNode> states, out IReadOnlyDictionary<LabelTarget, int> jumpCases )
    {
        states = Nodes;
        jumpCases = JumpCases;
    }
}
