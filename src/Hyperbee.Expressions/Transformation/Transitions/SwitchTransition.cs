using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class SwitchTransition : Transition
{
    internal List<SwitchCaseDefinition> CaseNodes = [];
    public NodeExpression DefaultNode { get; set; }
    public Expression SwitchValue { get; set; }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return Update(
            visitor.Visit( SwitchValue ),
            CaseNodes.Select( x => x.Update( x.TestValues.Select( visitor.Visit ).ToList() ) ).ToList()
        );
    }

    internal SwitchTransition Update( Expression switchValue, List<SwitchCaseDefinition> caseNodes )
    {
        if ( switchValue == SwitchValue )
            return this;

        return new SwitchTransition
        {
            DefaultNode = DefaultNode,
            SwitchValue = switchValue,
            CaseNodes = caseNodes
        };
    }

    internal override Expression Reduce( int order, NodeExpression expression, StateMachineSource resolverSource )
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

        internal SwitchCaseDefinition Update( List<Expression> testValues )
        {
            // Check if TestValues are the same
            if ( testValues.SequenceEqual( TestValues ) )
                return this;

            return new SwitchCaseDefinition( testValues, Body );
        }
    }
}
