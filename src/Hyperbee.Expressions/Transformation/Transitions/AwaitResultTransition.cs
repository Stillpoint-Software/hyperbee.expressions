using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

public class AwaitResultTransition : Transition
{
    public ParameterExpression AwaiterVariable { get; set; }
    public ParameterExpression ResultVariable { get; set; }
    public NodeExpression TargetNode { get; set; }
    public AwaitBinder AwaitBinder { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource )
    {
        var getResultMethod = AwaitBinder.GetResultMethod;

        var getResultCall = getResultMethod.IsStatic
            ? Call( getResultMethod, AwaiterVariable )
            : Call( Constant( AwaitBinder ), getResultMethod, AwaiterVariable );

        if ( ResultVariable == null )
        {
            var transition = GotoOrFallThrough( order, TargetNode );
            
            return transition == Empty() 
                ? getResultCall 
                : Block( getResultCall, transition );
        }

        var getResult = Assign( ResultVariable, getResultCall );

        return Block( 
            getResult, 
            GotoOrFallThrough( order, TargetNode ) 
        );
    }

    internal override NodeExpression FallThroughNode => TargetNode;
    internal override void OptimizeTransition( HashSet<LabelTarget> references )
    {
        TargetNode = OptimizeTransition( TargetNode );
        references.Add( TargetNode.NodeLabel );
    }
}
