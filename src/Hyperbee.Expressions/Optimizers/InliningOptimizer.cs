using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// InliningOptimizer: Inlining Functions, Constants, and Conditional Values
//
// This optimizer inlines constants and simplifies expressions by propagating known values.
// It focuses on inlining lambda expressions, boolean short-circuiting, and simplifying conditional
// expressions based on constant values.
//
// Before:
//   .Invoke(lambda, .Constant(5))
//
// After:
//   .LambdaExpression(
//       .Parameter(x),
//       .Add(.Constant(5), .Constant(5))
//   )
//
// Before:
//   .IfThenElse(.Constant(true), .Constant("True Branch"), .Constant("False Branch"))
//
// After:
//   .Constant("True Branch")
//
public class InliningOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize(Expression expression)
    {
        return Visit(expression);
    }

    public TExpr Optimize<TExpr>(TExpr expression) where TExpr : LambdaExpression
    {
        var optimizedBody = Optimize(expression.Body);

        if ( ReferenceEquals( expression.Body, optimizedBody ) )
        {
            return expression;
        }

        return (TExpr) Expression.Lambda( expression.Type, optimizedBody, expression.Parameters );
    }

    // Extended Constant Propagation: Simplifies conditionals with constant conditions
    //
    // This visitor expands constant propagation by simplifying `ConditionalExpression`s
    // when the condition is constant.
    //
    // Before:
    //   .IfThenElse(.Constant(true), .Constant("True Branch"), .Constant("False Branch"))
    //
    // After:
    //   .Constant("True Branch")
    //
    protected override Expression VisitConditional(ConditionalExpression node)
    {
        var test = Visit(node.Test);

        if ( test is not ConstantExpression constantTest )
        {
            return base.VisitConditional( node );
        }

        var condition = (bool)constantTest.Value!;
        var result = condition ? node.IfTrue : node.IfFalse;
        return Visit(result);

    }

    // BlockExpression Inlining: Replace parameters in blocks with constants where possible
    //
    // This visitor handles inlining constants within blocks, ensuring single-use variables
    // are replaced with constants, enabling further simplification of expressions.
    //
    // Before:
    //   .Block(
    //       .Assign(.Parameter(x), .Constant(5)),
    //       .Add(.Parameter(x), .Constant(2))
    //   )
    //
    // After:
    //   .Block(
    //       .Add(.Constant(5), .Constant(2))
    //   )
    //
    protected override Expression VisitBlock(BlockExpression node)
    {
        var variableReplacements = new Dictionary<ParameterExpression, Expression>();
        var variables = new List<ParameterExpression>(node.Variables);
        var expressions = new List<Expression>();

        foreach (var expr in node.Expressions)
        {
            if (expr is BinaryExpression assignExpr && assignExpr.NodeType == ExpressionType.Assign &&
                assignExpr.Left is ParameterExpression variable && assignExpr.Right is ConstantExpression constant)
            {
                // Record the constant assignment for inlining later in the block
                variableReplacements[variable] = constant;
                continue;
            }

            var replacedExpression = ReplaceVariables(expr, variableReplacements);
            expressions.Add(Visit(replacedExpression));
        }

        return Expression.Block(variables, expressions);
    }

    private static Expression ReplaceVariables(Expression expression, Dictionary<ParameterExpression, Expression> replacements)
    {
        return new VariableReplacementVisitor(replacements).Visit(expression);
    }

    // Short-Circuiting and Invocation Inlining: Optimizes boolean expressions and lambdas
    //
    // This visitor performs short-circuiting for `AndAlso` and `OrElse` expressions, and
    // inlines invocation expressions by replacing parameters in lambdas with invocation arguments.
    //
    // Before:
    //   .AndAlso(.Constant(true), expr)
    //
    // After:
    //   expr
    //
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if ( (node.NodeType != ExpressionType.AndAlso && node.NodeType != ExpressionType.OrElse) ||
             (node.Left is not ConstantExpression leftConst) )
        {
            return base.VisitBinary( node );
        }

        var leftValue = (bool)leftConst.Value!;
        return (node.NodeType, leftValue) switch
        {
            (ExpressionType.AndAlso, true) => Visit(node.Right),
            (ExpressionType.AndAlso, false) => Expression.Constant(false),
            (ExpressionType.OrElse, true) => Expression.Constant(true),
            (ExpressionType.OrElse, false) => Visit(node.Right),
            _ => base.VisitBinary(node)
        };

    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        if ( node.Expression is not LambdaExpression lambda )
        {
            return base.VisitInvocation( node );
        }

        var replacements = new Dictionary<ParameterExpression, Expression>();

        for (int i = 0; i < lambda.Parameters.Count; i++)
        {
            replacements[lambda.Parameters[i]] = Visit(node.Arguments[i]);
        }

        return ReplaceVariables(lambda.Body, replacements);
    }

    private class VariableReplacementVisitor : ExpressionVisitor
    {
        private readonly Dictionary<ParameterExpression, Expression> _replacements;

        public VariableReplacementVisitor(Dictionary<ParameterExpression, Expression> replacements)
        {
            _replacements = replacements;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return _replacements.TryGetValue(node, out var replacement) ? replacement : base.VisitParameter(node);
        }
    }
}
