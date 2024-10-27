using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

public interface INodeOptimizer
{
    public void Optimize( IReadOnlyList<StateScope> scopes );
}

internal sealed class NodeOptimizer : INodeOptimizer
{
    public void Optimize( IReadOnlyList<StateScope> scopes )
    {
        var references = new HashSet<LabelTarget>();

        OptimizeOrder( scopes, references );
        OptimizeNodes( scopes, references );
    }

    private static void OptimizeNodes( IReadOnlyList<StateScope> scopes, HashSet<LabelTarget> references )
    {
        for ( var i = 0; i < scopes.Count; i++ )
        {
            var scope = scopes[i];

            scope.Nodes = scope.Nodes
                .Where( node =>
                {
                    var contains = references.Contains( node.NodeLabel );
                    return contains;
                } )
                .Select( ( node, index ) =>
                {
                    node.MachineOrder = index;
                    return node;
                } )
                .ToList();
        }
    }

    private static void OptimizeOrder( IReadOnlyList<StateScope> scopes, HashSet<LabelTarget> references )
    {
        for ( var i = 0; i < scopes.Count; i++ )
        {
            var scope = scopes[i];

            SetScopeReferences( scope, references );
            scope.Nodes = OptimizeOrder( scope.ScopeId, scope.Nodes, references );
        }

        return;

        static void SetScopeReferences( StateScope scope, HashSet<LabelTarget> references )
        {
            references.Add( scope.Nodes[0].NodeLabel ); // start node

            foreach ( var jumpCase in scope.JumpCases ) // jump cases
            {
                references.Add( jumpCase.ContinueLabel );
                references.Add( jumpCase.ResultLabel );
            }
        }
    }

    private static List<NodeExpression> OptimizeOrder( int currentScopeId, List<NodeExpression> nodes, HashSet<LabelTarget> references )
    {
        // Optimize node order for better performance by performing a greedy depth-first
        // search to find the best order of execution for each node.
        //
        // Doing this will allow us to reduce the number of goto calls in the final machine.
        //
        // The first node is always the start node, and the last node is always the final node.

        var ordered = new List<NodeExpression>( nodes.Count );
        var visited = new HashSet<NodeExpression>( nodes.Count );

        // Perform greedy DFS for every unvisited node

        for ( var index = 0; index < nodes.Count; index++ )
        {
            var node = nodes[index];

            if ( !visited.Contains( node ) )
                Visit( node );
        }

        // Make sure the final state is last

        var finalNode = nodes.FirstOrDefault( x => x.Transition == null );

        if ( finalNode == null || ordered.Last() == finalNode )
            return ordered;

        ordered.Remove( finalNode );
        ordered.Add( finalNode );

        return ordered;

        void Visit( NodeExpression node )
        {
            while ( node != null && visited.Add( node ) )
            {
                node.Transition?.OptimizeTransition( references );

                ordered.Add( node );
                node = node.Transition?.FallThroughNode;

                if ( node?.ScopeId != currentScopeId )
                    return;
            }
        }
    }
}
