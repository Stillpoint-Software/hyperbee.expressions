using System.Diagnostics;
using System.Linq.Expressions;
namespace Hyperbee.Expressions.Transformation.Transitions;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]
public abstract class Transition : Expression
{
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    internal abstract Expression Reduce( int order, int scopeId, NodeExpression expression, StateMachineSource resolverSource );
    internal abstract NodeExpression FallThroughNode { get; } // this node is used to optimize state order
    internal abstract void OptimizeTransition( HashSet<LabelTarget> references ); // this method is used to optimize state transitions

    protected override Expression VisitChildren( ExpressionVisitor visitor ) => this;

    protected static NodeExpression OptimizeTransition( NodeExpression node )
    {
        while ( node.IsNoOp && node.Transition is GotoTransition gotoTransition )
        {
            node = gotoTransition.TargetNode;
        }

        return node;
    }

    protected static Expression GotoOrFallThrough( int order, int scopeId, NodeExpression node, bool allowNull = false )
    {
        if( node.ScopeId != scopeId )
            return Goto( node.NodeLabel );

        return order + 1 == node.StateOrder
            ? allowNull
                ? null
                : Empty()
            : Goto( node.NodeLabel );
    }
}
