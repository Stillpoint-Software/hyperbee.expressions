using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// MemoryOptimizationOptimizer: Memory Optimization
//
// This optimizer reuses parameters, removes unused temporary variables, and consolidates variable declarations.
// By pooling common parameters and eliminating unused variables, it reduces memory overhead and optimizes variable usage.

public class MemoryOptimizationOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    private readonly Dictionary<ParameterExpression, ParameterExpression> _reusedParameters = new();

    public Expression Optimize( Expression expression, OptimizationOptions options )
    {
        return options.EnableMemoryOptimization ? Visit( expression ) : expression;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        // Reuse parameters if they already exist
        if ( _reusedParameters.TryGetValue( node, out var reusedParam ) )
        {
            return reusedParam;
        }

        _reusedParameters[node] = node;
        return base.VisitParameter( node );
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        var uniqueVariables = new HashSet<ParameterExpression>( node.Variables );
        var newVariables = new List<ParameterExpression>( uniqueVariables );
        var expressions = new List<Expression>();

        // Remove unused temporary variables and consolidate variable declarations
        foreach ( var expr in node.Expressions )
        {
            if ( expr is BinaryExpression binary && binary.NodeType == ExpressionType.Assign && binary.Left is ParameterExpression param )
            {
                // Only add used parameters to the newVariables list
                if ( uniqueVariables.Contains( param ) )
                {
                    expressions.Add( expr );
                }
            }
            else
            {
                expressions.Add( Visit( expr ) );
            }
        }

        return Expression.Block( newVariables, expressions );
    }
}
