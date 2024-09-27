using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

public record GotoTransformerResult
{
    public List<StateNode> Nodes { get; set; }
    public JumpTableExpression JumpTable { get; set; }
    public ParameterExpression ReturnValue { get; set; }
    public int AwaitCount { get; set; }

    public void PrintNodes() => DiagnosticHelper.WriteNodes( Nodes );

    public void Deconstruct( out List<StateNode> states, out JumpTableExpression jumpTable )
    {
        states = Nodes;
        jumpTable = JumpTable;
    }
}
