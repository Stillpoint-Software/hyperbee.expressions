using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;

// VariableInliningVisitor: Variable Inlining and Elimination
//
// This visitor inlines variables where possible, removing unused variables and simplifying
// assignments when the variable is never accessed later in the code. It replaces variable references
// with their assigned values to simplify expressions.
//
// Before:
//   .Block(
//       .Assign(.Parameter(x), .Constant(5)),
//       .Add(.Parameter(x), .Parameter(x))
//   )
//
// After:
//   .Add(.Constant(5), .Constant(5))
//
public class VariableInliningVisitor : ExpressionVisitor, IExpressionTransformer
{
    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

    private readonly Dictionary<ParameterExpression, Expression> _replacements = new();

    protected override Expression VisitBlock( BlockExpression node )
    {
        var variables = new List<ParameterExpression>();
        var expressions = new List<Expression>();
        var variableAssignments = new Dictionary<ParameterExpression, Expression>();
        var variableUsageCount = new Dictionary<ParameterExpression, int>();

        for ( var index = 0; index < node.Expressions.Count; index++ )
        {
            var expr = node.Expressions[index];
            if ( expr is not BinaryExpression binaryExpr || binaryExpr.NodeType != ExpressionType.Assign || binaryExpr.Left is not ParameterExpression variable )
            {
                continue;
            }

            variableUsageCount.TryAdd( variable, 0 );
            variableUsageCount[variable]++;
            variableAssignments[variable] = binaryExpr.Right;
        }

        for ( var index = 0; index < node.Expressions.Count; index++ )
        {
            var expr = node.Expressions[index];
            if ( expr is not BinaryExpression binaryExpr || binaryExpr.NodeType != ExpressionType.Assign || binaryExpr.Left is not ParameterExpression variable )
            {
                expressions.Add( Visit( expr ) );
                continue;
            }

            if ( variableUsageCount[variable] != 1 )
            {
                if ( variableAssignments[variable] == binaryExpr.Right )
                {
                    continue;
                }

                variables.Add( variable );
                expressions.Add( expr );
            }
            else
            {
                _replacements[variable] = binaryExpr.Right;
                expressions.Add( Visit( binaryExpr.Right ) );
            }
        }

        return Expression.Block( variables, expressions );
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        return _replacements.TryGetValue( node, out var replacement ) ? Visit( replacement ) : base.VisitParameter( node );
    }
}
