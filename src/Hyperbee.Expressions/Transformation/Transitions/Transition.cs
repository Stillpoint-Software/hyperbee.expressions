using System.Diagnostics;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.Transformation.Transitions;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]
public abstract class Transition
{
    internal abstract Expression Reduce( int order, NodeExpression expression, IHoistingSource resolverSource );
    internal abstract NodeExpression FallThroughNode { get; } // this node is used to optimize state order
    internal abstract void OptimizeTransition( HashSet<LabelTarget> references ); // this method is used to optimize state transitions

    protected static NodeExpression OptimizeTransition( NodeExpression node )
    {
        while ( node.IsNoOp && node.Transition is GotoTransition gotoTransition )
        {
            node = gotoTransition.TargetNode;
        }

        return node;
    }

    protected static Expression GotoOrFallThrough( int order, NodeExpression node, bool allowNull = false )
    {
        return order + 1 == node.MachineOrder
            ? allowNull
                ? null
                : Empty()
            : Goto( node.NodeLabel );
    }
}
