using System.Globalization;
using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

public record GotoTransformerResult
{
    public List<StateNode> Nodes { get; set; }
    public JumpTableExpression JumpTable { get; set; }
    public ParameterExpression ReturnValue { get; set; }
    public int AwaitCount { get; set; }

    internal string DebugView
    {
        get
        {
            using StringWriter writer = new StringWriter( CultureInfo.CurrentCulture );
            DebugViewWriter.WriteTo( writer, Nodes );
            return writer.ToString();
        }
    }

    public void Deconstruct( out List<StateNode> states, out JumpTableExpression jumpTable )
    {
        states = Nodes;
        jumpTable = JumpTable;
    }
}
