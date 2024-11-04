using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

public interface INodeOptimizer
{
    public void Optimize( IReadOnlyList<StateContext.Scope> scopes );
}

internal sealed class NodeOptimizer : INodeOptimizer
{
    public void Optimize( IReadOnlyList<StateContext.Scope> scopes )
    {
        var references = new HashSet<LabelTarget>();

        foreach ( var scope in scopes )
        {
            OptimizeOrder( scope, references );
        }

        RemoveUnreferenced( scopes, references );
    }

    private static void OptimizeOrder( StateContext.Scope scope, HashSet<LabelTarget> references )
    {
        var nodes = scope.Nodes;

        var visited = new HashSet<NodeExpression>( nodes.Count );
        int stateOrder = 0;

        NodeExpression finalNode = null;

        // Add scope references to the set

        SetScopeReferences( scope, references );

        // Perform greedy DFS for each unvisited node
        for ( var index = 0; index < nodes.Count; index++ )
        {
            var node = nodes[index];

            if ( visited.Contains( node ) )
            {
                continue;
            }

            while ( node != null && visited.Add( node ) )
            {
                // Optimize transition, which may mutate node.Transition
                node.Transition?.OptimizeTransition( references );

                if ( node.Transition == null )
                {
                    finalNode = node;
                    break;
                }

                node.StateOrder = stateOrder++;
                node = node.Transition?.FallThroughNode;

                if ( node?.ScopeId != scope.ScopeId )
                    break;
            }
        }

        if ( finalNode != null )
        {
            finalNode.StateOrder = stateOrder;
        }

        // Sort nodes in-place based on StateOrder to reflect the DFS order
        nodes.Sort( ( x, y ) => x.StateOrder.CompareTo( y.StateOrder ) );

        return;

        static void SetScopeReferences( StateContext.Scope scope, HashSet<LabelTarget> references )
        {
            references.Add( scope.Nodes[0].NodeLabel ); // start node

            foreach ( var jumpCase in scope.JumpCases ) // jump cases
            {
                references.Add( jumpCase.ContinueLabel );
                references.Add( jumpCase.ResultLabel );
            }
        }
    }

    private static void RemoveUnreferenced( IReadOnlyList<StateContext.Scope> scopes, HashSet<LabelTarget> references )
    {
        // Remove any nodes that are not referenced

        for ( var i = 0; i < scopes.Count; i++ )
        {
            var scope = scopes[i];

            scope.Nodes.Remove( ( node, index ) =>
            {
                if ( !references.Contains( node.NodeLabel ) )
                    return true;

                node.StateOrder = index;

                return false;
            } );
        }
    }
}
