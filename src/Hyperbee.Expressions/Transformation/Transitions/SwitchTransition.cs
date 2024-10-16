using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class SwitchTransition : Transition
{
    internal readonly List<SwitchCaseDefinition> CaseNodes = [];
    public NodeExpression DefaultNode { get; set; }
    public Expression SwitchValue { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        var defaultBody = DefaultNode != null
            ? GotoOrFallThrough( order, DefaultNode, allowNull: true )
            : null;

        var cases = CaseNodes
            .Select( switchCase => switchCase.Reduce() );

        return Switch(
            SwitchValue,
            defaultBody,
            [.. cases]
        );
    }

    internal override NodeExpression FallThroughNode => DefaultNode;

    public void AddSwitchCase( List<Expression> testValues, NodeExpression body )
    {
        CaseNodes.Add( new SwitchCaseDefinition( testValues, body ) );
    }

    internal record SwitchCaseDefinition( List<Expression> TestValues, NodeExpression Body )
    {
        public SwitchCase Reduce() => SwitchCase( Goto( Body.NodeLabel ), TestValues );
    }
}
