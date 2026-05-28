using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices.Transitions;

internal class SwitchTransition : Transition
{
    public StateNode DefaultNode { get; set; }
    public Expression SwitchValue { get; set; }
    internal List<SwitchCaseDefinition> CaseNodes = [];

    internal override StateNode FallThroughNode => DefaultNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        base.AddExpressions( expressions, context );
        expressions.Add( Expression() );
        return;

        Expression Expression()
        {
            var stateOrder = context.StateNode.StateOrder;

            var cases = CaseNodes
                .Select( switchCase => switchCase.Reduce( stateOrder ) )
                .ToArray();

            // Route the no-match path explicitly to the fall-through (DefaultNode). Without an
            // explicit default, a no-match would fall through to whatever state is physically
            // next, which can be one of the case bodies (they use fall-through optimization).
            // When the fall-through target is already the next state, GotoOrFallThrough returns
            // null and we omit the default, preserving the optimal fall-through codegen.

            var defaultBody = GotoOrFallThrough( stateOrder, DefaultNode, allowNull: true );

            return defaultBody == null
                ? Switch( SwitchValue, cases )
                : Switch( SwitchValue, defaultBody, cases );
        }
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        DefaultNode = OptimizeGotos( DefaultNode );
        references.Add( DefaultNode.NodeLabel );

        for ( var index = 0; index < CaseNodes.Count; index++ )
        {
            var caseNode = CaseNodes[index];
            caseNode.Body = OptimizeGotos( caseNode.Body );

            references.Add( caseNode.Body.NodeLabel );
        }
    }

    public void AddSwitchCase( List<Expression> testValues, StateNode body )
    {
        CaseNodes.Add( new SwitchCaseDefinition( testValues, body ) );
    }

    internal sealed class SwitchCaseDefinition( List<Expression> testValues, StateNode body )
    {
        public List<Expression> TestValues = testValues;
        public StateNode Body { get; set; } = body;
        public SwitchCase Reduce( int order ) => SwitchCase( GotoOrFallThrough( order, Body ), TestValues );
    }
}
