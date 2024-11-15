using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class ConditionalTransition : Transition
{
    public Expression Test { get; set; }
    public NodeExpression IfTrue { get; set; }
    public NodeExpression IfFalse { get; set; }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        return Update( visitor.Visit( Test ) );
    }

    internal ConditionalTransition Update( Expression test )
    {
        if ( test == Test )
            return this;

        return new ConditionalTransition
        {
            Test = test,
            IfTrue = IfTrue,
            IfFalse = IfFalse
        };
    }

    internal override Expression Reduce( int order, int scopeId, NodeExpression expression, StateMachineSource resolverSource )
    {
        var fallThrough = GotoOrFallThrough( order, scopeId, IfFalse, true );

        if ( fallThrough == null )
            return IfThen( Test, Goto( IfTrue.NodeLabel ) );

        return IfThenElse(
            Test,
            Goto( IfTrue.NodeLabel ),
            fallThrough
        );
    }

    internal override NodeExpression FallThroughNode => IfFalse;

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        IfTrue = OptimizeTransition( IfTrue );
        IfFalse = OptimizeTransition( IfFalse );

        references.Add( IfTrue.NodeLabel );
        references.Add( IfFalse.NodeLabel );
    }
}
