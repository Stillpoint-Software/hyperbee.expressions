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

            Expression defaultBody;

            if ( DefaultNode != null )
            {
                defaultBody = GotoOrFallThrough(
                    stateOrder,
                    DefaultNode,
                    allowNull: true
                );
            }
#if FAST_COMPILER
            else
            {
                defaultBody = Empty();
            }
#endif
            var cases = CaseNodes
                .Select( switchCase => switchCase.Reduce( stateOrder ) )
                .ToArray();

            return Switch(
                SwitchValue,
#if FAST_COMPILER
                defaultBody,
#endif
                cases
             );
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
