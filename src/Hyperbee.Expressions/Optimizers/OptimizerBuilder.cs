
namespace Hyperbee.Expressions.Optimizers;

public class OptimizerBuilder
{
    private readonly List<IExpressionOptimizer> _optimizers = [];

    public OptimizerBuilder With<T>() where T : IExpressionOptimizer, new()
    {
        _optimizers.Add( new T() );
        return this;
    }

    public IExpressionOptimizer Build()
    {
        var dependencyGraph = BuildDependencyGraph( _optimizers );
        var sortedDependencies = TopologicallySortDependencies( dependencyGraph );
        return new CompositeOptimizer( sortedDependencies.ToArray() );
    }

    // Construct a dependency graph based on the relative order of visitors within each optimizer's dependencies.
    // Each visitor type becomes a node, and edges are added to maintain each optimizer’s internal order.
    // This ensures that all dependencies across optimizers are combined into a consistent order without duplication.
    //
    // Example:
    // Consider two optimizers with the following dependencies:
    //
    //     Optimizer 1: [A, B, C, D]
    //     Optimizer 2: [S, C, E, F, D]
    //
    // The resulting graph will include:
    // - Edges to maintain each optimizer’s internal order, e.g., A → B → C → D and S → C → E → F → D.
    // - A single node for each unique visitor type, even if used in multiple optimizers.
    //
    // Before Graph:
    //     No initial graph structure.
    //
    // After Graph:
    //     Nodes: [A, B, C, D, S, E, F]
    //     Edges: [A → B, B → C, C → D, S → C, C → E, E → F, F → D]

    private static Dictionary<Type, List<Type>> BuildDependencyGraph( IEnumerable<IExpressionOptimizer> optimizers )
    {
        var graph = new Dictionary<Type, List<Type>>();

        foreach ( var optimizer in optimizers )
        {
            var dependencies = optimizer.Dependencies.Select( d => d.GetType() ).ToList();
            for ( var i = 0; i < dependencies.Count - 1; i++ )
            {
                var current = dependencies[i];
                var next = dependencies[i + 1];

                if ( !graph.ContainsKey( current ) )
                    graph[current] = [];

                graph[current].Add( next );
            }
        }

        return graph;
    }

    // Sort the dependencies topologically based on the inferred dependency graph.
    // This ensures an order that respects all relative constraints within each optimizer, producing a consistent 
    // execution order for the visitors.
    //
    // Example:
    // Given a graph with the following edges:
    //
    //     A → B, B → C, C → D, S → C, C → E, E → F, F → D
    //
    // The topological sort will produce a consistent order, such as:
    //     
    //     [A, S, B, C, E, F, D]
    //
    // This order ensures:
    // - All dependencies within each optimizer are respected.
    // - The resulting order is valid across optimizers with no duplication.
    //
    // Before Sort:
    //     Graph Edges: [A → B, B → C, C → D, S → C, C → E, E → F, F → D]
    //
    // After Sort:
    //     Sorted Order: [A, S, B, C, E, F, D]

    private List<IExpressionTransformer> TopologicallySortDependencies( Dictionary<Type, List<Type>> graph )
    {
        var sorted = TopologicalSort( graph );
        return sorted
            .Select( type => _optimizers
                .SelectMany( opt => opt.Dependencies )
                .First( dep => dep.GetType() == type ) )
            .ToList();
    }

    // Core topological sort logic
    // Performs a topological sort on the graph to produce a globally consistent order that satisfies all 
    // constraints from each optimizer’s dependency list. If a cycle is detected, an exception is thrown to 
    // indicate a conflict.
    //
    // Example:
    // Given a graph with edges: [A → B, B → C, C → D]
    // Calling TopologicalSort on this graph would return the sorted order: [A, B, C, D]
    //
    // This ensures all relative constraints within the graph are preserved, and a global execution order is derived.

    private static List<Type> TopologicalSort( Dictionary<Type, List<Type>> graph )
    {
        var sorted = new List<Type>();
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>();

        foreach ( var node in graph.Keys )
        {
            if ( !Visit( node ) )
            {
                throw new InvalidOperationException( $"Cycle detected in dependencies involving {node}" );
            }
        }

        sorted.Reverse();
        return sorted;

        bool Visit( Type node )
        {
            if ( visited.Contains( node ) )
                return true;

            if ( !visiting.Add( node ) )
                return false;

            if ( graph.TryGetValue( node, out var neighbors ) )
            {
                foreach ( var neighbor in neighbors )
                {
                    if ( !Visit( neighbor ) )
                        return false;
                }
            }

            visiting.Remove( node );
            visited.Add( node );
            sorted.Add( node );

            return true;
        }
    }

    public class CompositeOptimizer : BaseOptimizer
    {
        public override IExpressionTransformer[] Dependencies { get; }

        internal CompositeOptimizer( IExpressionTransformer[] dependencies )
        {
            Dependencies = dependencies;
        }
    }
}

