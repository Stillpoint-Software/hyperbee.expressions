using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// InliningOptimizer: Expression Simplification
//
// This optimizer inlines functions and conditional expressions, and applies boolean short-circuiting.
// It replaces lambda expressions and known values directly in the expression tree,
// reducing unnecessary function calls and branching.

public class InliningOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression, OptimizationOptions options )
    {
        return options.EnableInlining ? Visit( expression ) : expression;
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        if ( node.Expression is not LambdaExpression lambda )
        {
            return base.VisitInvocation( node );
        }

        // Inline lambda body
        var inlined = new ParameterReplacer( lambda.Parameters, node.Arguments ).Visit( lambda.Body );
        return Visit( inlined );

    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        if ( node.NodeType != ExpressionType.AndAlso && node.NodeType != ExpressionType.OrElse )
        {
            return base.VisitBinary( node );
        }

        if ( node.Left is not ConstantExpression leftConst )
        {
            return base.VisitBinary( node );
        }

        // Short-circuit boolean expressions

        var leftValue = (bool) leftConst.Value!;

        return node.NodeType == ExpressionType.AndAlso && !leftValue
            ? leftConst
            : node.NodeType == ExpressionType.OrElse && leftValue
                ? leftConst
                : Visit( node.Right );

    }

    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly IReadOnlyList<ParameterExpression> _parameters;
        private readonly IReadOnlyList<Expression> _arguments;

        public ParameterReplacer( IReadOnlyList<ParameterExpression> parameters, IReadOnlyList<Expression> arguments )
        {
            _parameters = parameters;
            _arguments = arguments;
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            // Find the index of the parameter
            var index = -1;

            for ( var i = 0; i < _parameters.Count; i++ )
            {
                if ( _parameters[i] != node )
                {
                    continue;
                }

                index = i;
                break;
            }

            // Replace the parameter with the corresponding argument if found
            return index >= 0 ? _arguments[index] : node;
        }
    }
}
