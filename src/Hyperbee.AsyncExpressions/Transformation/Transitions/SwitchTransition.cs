using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class SwitchTransition : Transition
{
    private readonly List<SwitchCaseDefinition> _caseNodes = [];
    public NodeExpression DefaultNode { get; set; }
    public Expression SwitchValue { get; set; }

    internal override Expression Reduce( int order, IFieldResolverSource resolverSource )
    {
        var defaultBody = DefaultNode != null
            ? Expression.Goto( DefaultNode.NodeLabel )
            : null;

        var cases = _caseNodes
            .Select( switchCase => switchCase.Reduce() );

        return Expression.Switch(
            SwitchValue,
            defaultBody,
            [.. cases]
        );
    }

    internal override NodeExpression LogicalNextNode => DefaultNode;

    public void AddSwitchCase( List<Expression> testValues, NodeExpression body )
    {
        _caseNodes.Add( new SwitchCaseDefinition( testValues, body ) );
    }

    private record SwitchCaseDefinition( List<Expression> TestValues, NodeExpression Body )
    {
        public SwitchCase Reduce() => Expression.SwitchCase( Expression.Goto( Body.NodeLabel ), TestValues );
    }
}