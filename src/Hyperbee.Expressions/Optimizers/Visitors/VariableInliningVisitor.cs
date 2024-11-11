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
        var variableUsageCount = new Dictionary<ParameterExpression, int>();

        // Track usage counts and assignments
        for ( var index = 0; index < node.Expressions.Count; index++ )
        {
            var expr = node.Expressions[index];

            if ( expr is not BinaryExpression binaryExpr ||
                 binaryExpr.NodeType != ExpressionType.Assign ||
                 binaryExpr.Left is not ParameterExpression variable )
            {
                continue;
            }

            variableUsageCount.TryAdd( variable, 0 );
            variableUsageCount[variable]++;
            _replacements[variable] = binaryExpr.Right;
        }

        // Inline or retain expressions based on usage
        for ( var index = 0; index < node.Expressions.Count; index++ )
        {
            var expr = node.Expressions[index];
            switch ( expr )
            {
                case BinaryExpression binaryExpr 
                    when binaryExpr.NodeType == ExpressionType.Assign && binaryExpr.Left is ParameterExpression variable:
                {
                    if ( variableUsageCount[variable] == 1 && _replacements[variable] is ConstantExpression )
                    {
                        // Inline single-use constant assignments
                        expressions.Add( Visit( _replacements[variable] ) );
                    }
                    else
                    {
                        variables.Add( variable );
                        expressions.Add( Visit( expr ) );
                    }

                    break;
                }
                default:
                {
                    expressions.Add( Visit( expr ) );
                    break;
                }
            }
        }

        return variables.Count == 0 ? expressions.Last() : Expression.Block( variables, expressions );
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        // Inline the variable if a replacement is available
        return _replacements.TryGetValue( node, out var replacement )
            ? Visit( replacement )
            : base.VisitParameter( node );
    }
}
