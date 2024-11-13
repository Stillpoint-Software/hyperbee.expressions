using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;

// VariableReducerVisitor: Inlining and Memory Optimization
//
// This visitor combines inlining and memory optimization by inlining single-use variables, removing unused
// variables, and reusing parameters where possible. This helps in reducing both memory usage and
// unnecessary assignments, making the code more efficient.
//
// Example Transformations:
// Before:
//   .Block(
//       .Assign(.Parameter(x), .Constant(5)),
//       .Add(.Parameter(x), .Parameter(x))
//   )
//
// After:
//   .Add(.Constant(5), .Constant(5))
//
// Before:
//   .Block(
//       .Assign(.Parameter(temp), .Constant(42)),
//       .Parameter(temp)
//   )
//
// After:
//   .Constant(42)

public class VariableReducerVisitor : ExpressionVisitor, IExpressionTransformer
{
    private readonly Dictionary<ParameterExpression, Expression> _replacements = new();
    private readonly Dictionary<ParameterExpression, ParameterExpression> _reusedParameters = new();
    private readonly HashSet<ParameterExpression> _uniqueVariables = [];

    public int Priority => PriorityGroup.ControlFlowAndVariableSimplification + 40;

    public Expression Transform( Expression expression )
    {
        _replacements.Clear();
        _reusedParameters.Clear();
        _uniqueVariables.Clear();

        return Visit( expression );
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        //var variables = new List<ParameterExpression>();
        var expressions = new List<Expression>();
        var variableUsageCount = new Dictionary<ParameterExpression, int>();

        // Count usages and track replacements for inlining
        foreach ( var expr in node.Expressions )
        {
            if ( expr is BinaryExpression binaryExpr && binaryExpr.NodeType == ExpressionType.Assign &&
                binaryExpr.Left is ParameterExpression variable )
            {
                variableUsageCount.TryAdd( variable, 0 );
                variableUsageCount[variable]++;
                _replacements[variable] = binaryExpr.Right;
            }
        }

        // Inline or retain variables based on usage
        foreach ( var expr in node.Expressions )
        {
            switch ( expr )
            {
                case BinaryExpression binaryExpr when binaryExpr.NodeType == ExpressionType.Assign &&
                                                      binaryExpr.Left is ParameterExpression variable:
                    if ( variableUsageCount[variable] == 1 && _replacements[variable] is ConstantExpression )
                    {
                        // Inline single-use constant assignments
                        expressions.Add( Visit( _replacements[variable] ) );
                    }
                    else
                    {
                        _uniqueVariables.Add( variable );
                        //variables.Add(variable);
                        expressions.Add( Visit( expr ) );
                    }
                    break;
                default:
                    expressions.Add( Visit( expr ) );
                    break;
            }
        }

        // Remove unused variables and consolidate variable declarations
        var finalVariables = new List<ParameterExpression>( _uniqueVariables );

        return finalVariables.Count == 0 ? expressions.Last() : Expression.Block( finalVariables, expressions );
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        // Reuse parameters if already processed
        if ( _reusedParameters.TryGetValue( node, out var reusedParam ) )
        {
            return reusedParam;
        }

        if ( _replacements.TryGetValue( node, out var replacement ) )
        {
            return Visit( replacement );
        }

        _reusedParameters[node] = node;
        return base.VisitParameter( node );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        var visitedBody = Visit( node.Body );

        if ( ReferenceEquals( visitedBody, node.Body ) )
            return node;

        if ( visitedBody is ConstantExpression constantBody )
            return constantBody;

        return visitedBody;
    }
}

