using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

internal class SwitchTransition : Transition
{
    public IStateNode DefaultNode { get; set; }
    public Expression SwitchValue { get; set; }
    internal List<SwitchCaseDefinition> CaseNodes = [];

    internal override IStateNode FallThroughNode => DefaultNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        base.AddExpressions( expressions, context );
        expressions.Add( Expression() );
        return;

        Expression Expression()
        {
            var stateOrder = context.NodeInfo.StateOrder;

            Expression defaultBody;

            if ( DefaultNode != null )
            {
                defaultBody = GotoOrFallThrough(
                    stateOrder,
                    DefaultNode,
                    allowNull: true
                );
            }
            else
            {
                defaultBody = null;
            }

            var cases = CaseNodes
                .Select( switchCase => switchCase.Reduce( stateOrder ) )
                .ToArray();

            return Switch( SwitchValue, defaultBody, cases );
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

    public void AddSwitchCase( List<Expression> testValues, IStateNode body )
    {
        CaseNodes.Add( new SwitchCaseDefinition( testValues, body ) );
    }

    internal sealed class SwitchCaseDefinition( List<Expression> testValues, IStateNode body )
    {
        public List<Expression> TestValues = testValues;
        public IStateNode Body { get; set; } = body;
        public SwitchCase Reduce( int order ) => SwitchCase( GotoOrFallThrough( order, Body ), TestValues );

        //internal SwitchCaseDefinition Update( List<Expression> testValues )
        //{
        //    // Check if TestValues are the same
        //    if ( testValues.SequenceEqual( TestValues ) )
        //        return this;

        //    return new SwitchCaseDefinition( testValues, Body );
        //}
    }
}
