using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// ExpressionCachingOptimizer: Performance Optimization
//
// This optimizer eliminates redundant sub-expressions and applies memoization by caching reusable expressions.
// It replaces repeated sub-expressions with a single cached variable to avoid redundant evaluations,
// improving performance in large or frequently used expression trees.

public class ExpressionCachingOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    private readonly Dictionary<Expression, ParameterExpression> _cache = new();
    private readonly List<ParameterExpression> _variables = [];
    private readonly List<Expression> _assignments = [];

    public Expression Optimize( Expression expression, OptimizationOptions options )
    {
        if ( !options.EnableExpressionCaching ) return expression;

        // Reset cache for each optimization run
        _cache.Clear();
        _variables.Clear();
        _assignments.Clear();

        var optimizedExpression = Visit( expression );

        // If no caching was applied, return the original expression
        // Else return a block with cached assignments and the optimized expression

        return _variables.Count == 0 
            ? optimizedExpression 
            : Expression.Block( _variables, _assignments.Append( optimizedExpression ) );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        // Check if the expression has already been cached
        if ( _cache.TryGetValue( node, out var cachedVariable ) )
        {
            return cachedVariable;
        }

        var visitedNode = base.VisitBinary( node );

        // Cache the expression result
        var variable = Expression.Variable( visitedNode.Type );
        _variables.Add( variable );
        _assignments.Add( Expression.Assign( variable, visitedNode ) );
        _cache[visitedNode] = variable;

        return variable;
    }
}
