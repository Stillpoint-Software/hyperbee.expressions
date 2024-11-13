using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class SwitchTransition : Transition
{
    internal List<SwitchCaseDefinition> CaseNodes = [];
    public NodeExpression DefaultNode { get; set; }
    public Expression SwitchValue { get; set; }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return Update( visitor.Visit( SwitchValue ) );
    }

    internal SwitchTransition Update( Expression switchValue )
    {
        if ( switchValue == SwitchValue )
            return this;

        return new SwitchTransition
        {
            DefaultNode = DefaultNode,
            SwitchValue = switchValue,
            CaseNodes = CaseNodes  // TODO: fix visiting Case Test Values
        };
    }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        Expression defaultBody;

        if ( DefaultNode != null )
        {
            defaultBody = GotoOrFallThrough(
                order,
                DefaultNode,
                allowNull: true
            );
        }
        else
        {
            defaultBody = null;
        }

        var cases = CaseNodes
            .Select( switchCase => switchCase.Reduce( order ) )
            .ToArray();

        return Switch( SwitchValue, defaultBody, cases );
    }

    internal override NodeExpression FallThroughNode => DefaultNode;

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        DefaultNode = OptimizeTransition( DefaultNode );
        references.Add( DefaultNode.NodeLabel );

        for ( var index = 0; index < CaseNodes.Count; index++ )
        {
            var caseNode = CaseNodes[index];
            caseNode.Body = OptimizeTransition( caseNode.Body );

            references.Add( caseNode.Body.NodeLabel );
        }
    }

    public void AddSwitchCase( List<Expression> testValues, NodeExpression body )
    {
        CaseNodes.Add( new SwitchCaseDefinition( testValues, body ) );
    }

    internal sealed class SwitchCaseDefinition( List<Expression> testValues, NodeExpression body )
    {
        public List<Expression> TestValues = testValues;
        public NodeExpression Body { get; set; } = body;
        public SwitchCase Reduce( int order ) => SwitchCase( GotoOrFallThrough( order, Body ), TestValues );
    }
}
