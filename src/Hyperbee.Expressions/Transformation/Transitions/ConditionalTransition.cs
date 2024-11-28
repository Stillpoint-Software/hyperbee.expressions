using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class ConditionalTransition : Transition
{
    public Expression Test { get; set; }
    public NodeExpression IfTrue { get; set; }
    public NodeExpression IfFalse { get; set; }

    internal override NodeExpression FallThroughNode => IfFalse;

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

    protected override List<Expression> ReduceTransition()
    {
        return [GetExpression()];

        Expression GetExpression()
        {
            var fallThrough = GotoOrFallThrough( Parent.StateOrder, IfFalse, true );

            if ( fallThrough == null )
                return IfThen( Test, Goto( IfTrue.NodeLabel ) );

            return IfThenElse(
                Test,
                Goto( IfTrue.NodeLabel ),
                fallThrough
            );
        }
    }

    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        IfTrue = OptimizeGotos( IfTrue );
        IfFalse = OptimizeGotos( IfFalse );

        references.Add( IfTrue.NodeLabel );
        references.Add( IfFalse.NodeLabel );
    }
}
