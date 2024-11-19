using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

public interface INodeExpressionOptimizer
{
    public void Optimize( IReadOnlyList<StateContext.Scope> scopes );
}

internal sealed class NodeExpressionOptimizer : INodeExpressionOptimizer
{
    public void Optimize( IReadOnlyList<StateContext.Scope> scopes )
    {
        var references = new HashSet<LabelTarget>();
        int stateOrder = 0;

        foreach ( var scope in scopes )
        {
            OptimizeOrder( ref stateOrder, scope, references );
        }

        RemoveUnreferenced( scopes, references );
    }

    private static void OptimizeOrder( ref int stateOrder, StateContext.Scope scope, HashSet<LabelTarget> references )
    {
        var nodes = scope.Nodes;

        var visited = new HashSet<NodeExpression>( nodes.Count );


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
        foreach ( var scope in scopes )
        {
            scope.Nodes.RemoveAll( node => !references.Contains( node.NodeLabel ) );
        }
    }
}
