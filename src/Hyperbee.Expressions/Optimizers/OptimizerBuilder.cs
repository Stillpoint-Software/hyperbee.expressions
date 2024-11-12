namespace Hyperbee.Expressions.Optimizers;

public class OptimizerBuilder
{
    private readonly List<IExpressionOptimizer> _optimizers = [];

    public OptimizerBuilder With<T>() where T : IExpressionOptimizer, new()
    {
        _optimizers.Add(new T());
        return this;
    }

    public IExpressionOptimizer Build()
    {
        var multiDependencyOptimizers = _optimizers.Where(opt => opt.Dependencies.Length > 1).ToList();
        var standaloneTransformers = _optimizers
            .Where(opt => opt.Dependencies.Length == 1)
            .SelectMany(opt => opt.Dependencies)
            .OrderBy(tran => tran.Priority)
            .ToList();

        var dependencyGraph = BuildDependencyGraph(multiDependencyOptimizers);
        var sortedDependencies = TopologicalSort(dependencyGraph);

        var wovenTransformers = WeaveStandaloneTransformers(
            sortedDependencies, 
            standaloneTransformers
        );

        return new CompositeOptimizer(wovenTransformers.ToArray());
    }

    private static Dictionary<Type, List<Type>> BuildDependencyGraph(IEnumerable<IExpressionOptimizer> optimizers)
    {
        var graph = new Dictionary<Type, List<Type>>();

        foreach (var optimizer in optimizers)
        {
            var dependencies = optimizer.Dependencies;
            for (var i = 0; i < dependencies.Length - 1; i++)
            {
                var current = dependencies[i].GetType();
                var next = dependencies[i + 1].GetType();

                if (!graph.ContainsKey(current))
                    graph[current] = [];

                if (!graph[current].Contains(next))
                    graph[current].Add(next);
            }

            foreach (var dep in dependencies.Select(d => d.GetType()))
            {
                if (!graph.ContainsKey(dep))
                    graph[dep] = [];
            }
        }

        return graph;
    }

    private List<IExpressionTransformer> TopologicalSort(Dictionary<Type, List<Type>> graph)
    {
        var sorted = new List<Type>();
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>();

        foreach (var node in graph.Keys)
        {
            if (!Visit(node))
                throw new InvalidOperationException($"Cycle detected in dependencies involving {node}.");
        }

        return sorted.Select(type => 
            _optimizers.SelectMany(opt => opt.Dependencies)
                .First(dep => dep.GetType() == type)
        ).ToList();

        bool Visit(Type node)
        {
            if (visited.Contains(node))
                return true;

            if (!visiting.Add(node))
                return false; // Cycle detected

            for ( var index = 0; index < graph[node].Count; index++ )
            {
                var neighbor = graph[node][index];
                if ( !Visit( neighbor ) )
                    return false;
            }

            visiting.Remove(node);
            visited.Add(node);
            sorted.Add(node);

            return true;
        }
    }

    private List<IExpressionTransformer> WeaveStandaloneTransformers( List<IExpressionTransformer> sortedTransformers, List<IExpressionTransformer> standaloneTransformers)
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

        while (standaloneIndex < standaloneTransformers.Count)
        {
            finalTransformers.Add(standaloneTransformers[standaloneIndex]);
            standaloneIndex++;
        }

        return finalTransformers;
    }

    public class CompositeOptimizer : BaseOptimizer
    {
        public override IExpressionTransformer[] Dependencies { get; }

        internal CompositeOptimizer(IExpressionTransformer[] dependencies)
        {
            Dependencies = dependencies;
        }
    }
}
