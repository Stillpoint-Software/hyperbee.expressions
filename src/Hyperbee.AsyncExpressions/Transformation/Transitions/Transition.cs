using System.Diagnostics;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

public abstract class Transition
{
    // internal ParameterExpression ReturnVariable { get; set; }
    // internal Expression ReturnValue { get; set; }
    //

    internal abstract Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource );
    internal abstract NodeExpression FallThroughNode { get; } // this node is used to optimize state order

    protected static Expression GotoOrFallThrough( int order, NodeExpression node, bool allowNull = false )
    {
        return order + 1 == node.MachineOrder
            ? allowNull
                ? null
                : Empty()
            : Goto( node.NodeLabel );
    }
}
