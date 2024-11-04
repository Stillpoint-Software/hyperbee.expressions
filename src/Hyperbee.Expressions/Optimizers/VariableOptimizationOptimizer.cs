using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// VariableOptimizationOptimizer: Variable Optimization
//
// This optimizer inlines single-use variables and eliminates redundant assignments.
// It also removes unused variables in blocks and avoids redundant assignments to the same variable.
// By simplifying variable usage, it reduces memory overhead and improves readability.

public class VariableOptimizationOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression, OptimizationOptions options )
    {
        return options.EnableVariableOptimization ? Visit( expression ) : expression;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        var variables = new List<ParameterExpression>();
        var expressions = new List<Expression>();
        var variableAssignments = new Dictionary<ParameterExpression, Expression>();

        // Track single-use and redundant variables
        var variableUsageCount = new Dictionary<ParameterExpression, int>();

        // First pass: Count variable usage and track last assignment to each variable
        foreach ( var expr in node.Expressions )
        {
            if ( expr is BinaryExpression binaryExpr && binaryExpr.NodeType == ExpressionType.Assign && binaryExpr.Left is ParameterExpression variable )
            {
                variableUsageCount.TryAdd( variable, 0 );
                variableUsageCount[variable]++;
                variableAssignments[variable] = binaryExpr.Right;
            }
        }

        // Second pass: Eliminate redundant assignments and inline single-use variables
        foreach ( var expr in node.Expressions )
        {
            if ( expr is not BinaryExpression binaryExpr || binaryExpr.NodeType != ExpressionType.Assign || binaryExpr.Left is not ParameterExpression variable )
            {
                expressions.Add( Visit( expr ) );
                continue;
            }

            if ( variableUsageCount[variable] == 1 )
            {
                // Inline single-use variable
                expressions.Add( Visit( binaryExpr.Right ) );
            }
            else if ( variableAssignments[variable] == binaryExpr.Right )
            {
                // Skip redundant assignment
            }
            else
            {
                // Keep assignment if variable is used multiple times
                variables.Add( variable );
                expressions.Add( expr );
            }
        }

        return Expression.Block( variables, expressions );
    }
}
