using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;

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
public class InliningVisitor : ExpressionVisitor, IExpressionTransformer
{
    private ConstantFoldingVisitor ConstantFoldingVisitor { get; } = new();

    public int Priority => PriorityGroup.ControlFlowAndVariableSimplification + 30;

    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        // Visit left and right nodes to apply inlining or other optimizations
        var left = Visit( node.Left );
        var right = Visit( node.Right );

        // Handle short-circuiting for `AndAlso` and `OrElse`
        if ( node.NodeType == ExpressionType.AndAlso || node.NodeType == ExpressionType.OrElse )
        {
            if ( left is ConstantExpression leftConst && leftConst.Value is bool leftBool )
            {
                // Short-circuit based on left value
                return (node.NodeType, leftBool) switch
                {
                    (ExpressionType.AndAlso, true ) => ConstantFoldingVisitor.Visit( right ),
                    (ExpressionType.AndAlso, false ) => Expression.Constant( false ),
                    (ExpressionType.OrElse, true ) => Expression.Constant( true ),
                    (ExpressionType.OrElse, false ) => ConstantFoldingVisitor.Visit( right ),
                    _ => node.Update( left, node.Conversion, right )
                };
            }
        }

        // If both sides are constants, and it's not a logical short-circuit case, try folding directly
        if ( left is ConstantExpression && right is ConstantExpression )
        {
            return ConstantFoldingVisitor.Visit( Expression.MakeBinary( node.NodeType, left, right ) );
        }

        return node.Update( left, node.Conversion, right );
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        if ( node.Expression is LambdaExpression lambda )
        {
            // Inline lambda expressions by replacing parameters with arguments
            var argumentMap = lambda.Parameters
                .Zip( node.Arguments, ( parameter, argument ) => (parameter, argument) )
                .ToDictionary( pair => pair.parameter, pair => pair.argument );

            var inlinedBody = new ParameterReplacer( argumentMap ).Visit( lambda.Body );
            return ConstantFoldingVisitor.Visit( inlinedBody ); // Apply folding after inlining
        }

        return base.VisitInvocation( node );
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        // Evaluate conditional expressions with constant test conditions
        var test = Visit( node.Test );
        var ifTrue = Visit( node.IfTrue );
        var ifFalse = Visit( node.IfFalse );

        if ( test is ConstantExpression constTest && constTest.Value is bool condition )
        {
            var result = condition ? ifTrue : ifFalse;
            return ConstantFoldingVisitor.Visit( result ); // Apply folding after resolving the condition
        }

        return node.Update( test, ifTrue, ifFalse );
    }

    // Helper class for inlining: replaces parameters with arguments
    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly Dictionary<ParameterExpression, Expression> _replacements;

        public ParameterReplacer( Dictionary<ParameterExpression, Expression> replacements )
        {
            _replacements = replacements;
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            return _replacements.TryGetValue( node, out var replacement ) ? replacement : base.VisitParameter( node );
        }
    }
}

