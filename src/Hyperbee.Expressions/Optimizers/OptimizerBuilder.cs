namespace Hyperbee.Expressions.Optimizers;

public class OptimizerBuilder
{
    private readonly List<ExpressionOptimizer> _optimizers = [];

    public OptimizerBuilder With<T>() where T : ExpressionOptimizer, new()
    {
        _optimizers.Add( new T() );
        return this;
    }

    public ExpressionOptimizer Build()
    {
        // Separate optimizers based on dependency count
        var multiDependencyOptimizers = _optimizers.Where( opt => opt.Dependencies.Length > 1 ).ToList();
        var standaloneTransformers = _optimizers
            .Where( opt => opt.Dependencies.Length == 1 )
            .SelectMany( opt => opt.Dependencies )
            .OrderBy( tran => tran.Priority )
            .ToList();

        // Build and sort the dependency graph
        var dependencyGraph = BuildDependencyGraph( multiDependencyOptimizers );
        var sortedDependencies = TopologicalSort( dependencyGraph );

        // Weave standalone transformers into sorted dependencies to achieve a unified execution order
        var wovenTransformers = WeaveStandaloneTransformers( sortedDependencies, standaloneTransformers );

        return new CompositeOptimizer( wovenTransformers.ToArray() );
    }

    // Construct a dependency graph based on relative order within each optimizer's dependencies.
    // Each visitor type becomes a node, and edges are added to maintain the internal order of each optimizer’s dependencies.
    // This process ensures that dependencies are combined into a consistent order across optimizers without duplication.
    //
    // Example:
    // Optimizer 1: [A, B, C, D]
    // Optimizer 2: [S, C, E, F, D]
    //
    // Resulting Graph:
    //     Nodes: [A, B, C, D, S, E, F]
    //     Edges: [A → B, B → C, C → D, S → C, C → E, E → F, F → D]
    //
    // This ensures all relative dependencies are respected, creating a coherent dependency graph.

    private static Dictionary<Type, List<Type>> BuildDependencyGraph( IEnumerable<IExpressionOptimizer> optimizers )
    {
        var graph = new Dictionary<Type, List<Type>>();

        foreach ( var optimizer in optimizers )
        {
            var dependencies = optimizer.Dependencies;
            for ( var i = 0; i < dependencies.Length - 1; i++ )
            {
                var current = dependencies[i].GetType();
                var next = dependencies[i + 1].GetType();

                if ( !graph.ContainsKey( current ) )
                    graph[current] = [];

                if ( !graph[current].Contains( next ) )
                    graph[current].Add( next );
            }

            // Ensure each dependency type is represented as a node in the graph
            foreach ( var dep in dependencies.Select( d => d.GetType() ) )
            {
                if ( !graph.ContainsKey( dep ) )
                    graph[dep] = [];
            }
        }

        return graph;
    }

    // Perform a topological sort on the dependency graph to ensure a globally consistent order that satisfies
    // all relative constraints from each optimizer's dependency list.
    // 
    // Example:
    // Given a graph with edges: [A → B, B → C, C → D, S → C, C → E, E → F, F → D]
    //
    // Calling TopologicalSort on this graph would yield the sorted order: [A, S, B, C, E, F, D].
    // This order preserves dependencies across optimizers and avoids duplication.
    //
    // Before Sort:
    // Graph Edges: [A → B, B → C, C → D, S → C, C → E, E → F, F → D]
    //
    // After Sort:
    // Sorted Order: [A, S, B, C, E, F, D]

    private List<IExpressionTransformer> TopologicalSort( Dictionary<Type, List<Type>> graph )
    {
        var sorted = new List<Type>();
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>();

        foreach ( var node in graph.Keys )
        {
            if ( !Visit( node ) )
                throw new InvalidOperationException( $"Cycle detected in dependencies involving {node}." );
        }

        return sorted.Select( type =>
            _optimizers.SelectMany( opt => opt.Dependencies )
                .First( dep => dep.GetType() == type )
        ).ToList();

        bool Visit( Type node )
        {
            if ( visited.Contains( node ) )
                return true;

            if ( !visiting.Add( node ) )
                return false; // Cycle detected

            foreach ( var neighbor in graph[node] )
            {
                if ( !Visit( neighbor ) )
                    return false;
            }

            visiting.Remove( node );
            visited.Add( node );
            sorted.Add( node );

            return true;
        }
    }

    // Weave standalone transformers into the sorted list of multi-dependency transformers, based on priority.
    // This ensures that standalone transformers with lower priority execute before higher-priority transformers,
    // while respecting the sorted execution order of multi-dependency transformers.
    //
    // Example:
    // Sorted Transformers: [A, B, C]
    // Standalone Transformers: [X (Priority: 1), Y (Priority: 2)]
    //
    // Resulting Order:
    // [X, A, B, Y, C]
    //
    // This interleaving ensures that the final list maintains dependency order while honoring standalone transformer priorities.

    private static List<IExpressionTransformer> WeaveStandaloneTransformers( List<IExpressionTransformer> sortedTransformers, List<IExpressionTransformer> standaloneTransformers )
    {
        var finalTransformers = new List<IExpressionTransformer>();
        int standaloneIndex = 0;

        for ( var index = 0; index < sortedTransformers.Count; index++ )
        {
            var transformer = sortedTransformers[index];
            while ( standaloneIndex < standaloneTransformers.Count && standaloneTransformers[standaloneIndex].Priority <= transformer.Priority )
            {
                finalTransformers.Add( standaloneTransformers[standaloneIndex] );
                standaloneIndex++;
            }

            finalTransformers.Add( transformer );
        }

        // Append any remaining standalone transformers
        while ( standaloneIndex < standaloneTransformers.Count )
        {
            finalTransformers.Add( standaloneTransformers[standaloneIndex] );
            standaloneIndex++;
        }

        return finalTransformers;
    }

    public class CompositeOptimizer : ExpressionOptimizer
    {
        public override IExpressionTransformer[] Dependencies { get; }

        internal CompositeOptimizer( IExpressionTransformer[] dependencies )
        {
            Dependencies = dependencies;
        }
    }
}
