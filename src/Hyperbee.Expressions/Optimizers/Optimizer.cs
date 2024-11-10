using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

public class Optimizer
{
    private readonly List<IExpressionOptimizer> _optimizers;

    // Optimizer Pipeline:
    //
    // The following pipeline applies a series of expression tree optimizations in a specific order.
    // Each optimizer focuses on a specific domain to streamline and enhance the expression tree.
    //
    // Order of optimizers is crucial to ensure that each optimization step builds on the previous one.

    public Optimizer( OptimizationOptions context )
    {
        _optimizers = [];

        // The following pipeline applies a series of expression tree optimizations in a specific order.
        // Each optimizer focuses on a specific domain to streamline and enhance the expression tree.
        //

        // InliningOptimizer
        //
        //    - Constant Inlining: Inline constants throughout the expression tree.
        //    - Function and Lambda Inlining: Inline invocation expressions by replacing parameters with arguments.
        //    - Conditional Inlining: Simplify `ConditionalExpression`s by directly selecting branches based on constant conditions.
        //    - Boolean Short-Circuiting: Simplify expressions like `x && true` or `x || false`.

        if ( context.EnableInliningOptimization )
            _optimizers.Add( new InliningOptimizer() );

        // ValueBindingOptimizer
        //
        //    - Constant Folding: Evaluate arithmetic and logical expressions with constant operands.
        //    - Variable Inlining: Replace variables with constant values where applicable.
        //    - Member Access Simplification: Simplify member access by precomputing values for constant fields and properties.

        if ( context.EnableValueBindingOptimization )
            _optimizers.Add( new ValueBindingOptimizer() );

        // FlowControlOptimizer
        //
        //    - Dead Code Elimination: Remove unreachable code and simplify conditional expressions.
        //    - Block Simplification: Flatten nested `BlockExpression`s and remove unreachable expressions.
        //    - TryCatch Simplification: Remove unreferenced labels and simplify empty `TryCatch`/`TryFinally` blocks.
        //    - Label Simplification: Remove unused labels and simplify `Goto` expressions.
        //    - Control Flow Simplification: Optimizes `ConditionalExpression` and `LoopExpression`.

        if ( context.EnableFlowControlOptimization )
            _optimizers.Add( new FlowControlOptimizer() );

        // ExpressionReductionOptimizer
        //
        //    - Arithmetic Reduction: Remove trivial operations like adding zero or multiplying by one.
        //    - Logical Reduction: Simplify expressions with logical identities (e.g., `x && true` or `x || false`).
        //    - Nested Expression Flattening: Simplify nested expressions, such as combining multiple additions or multiplications.

        if ( context.EnableExpressionReduction )
            _optimizers.Add( new ExpressionReductionOptimizer() );

        // ExpressionCachingOptimizer
        //
        //    - Subexpression Caching: Identify repeated subexpressions and cache their results.
        //    - Caching of Complex Expressions: Cache reusable results of nested or complex expressions to prevent redundant evaluation.

        if ( context.EnableExpressionResultCaching )
            _optimizers.Add( new ExpressionResultCachingOptimizer() );

        // MemoryOptimizationOptimizer
        //
        //    - Parameter Reuse: Reuse parameters where possible to reduce memory overhead.
        //    - Variable Cleanup: Remove unused variables, ensuring minimal memory usage for the final expression.
        //    - Allocation Reduction: Minimize memory allocations in expressions by optimizing variable usage.

        if ( context.EnableMemoryOptimization )
            _optimizers.Add( new MemoryOptimizationOptimizer() );
    }

    public Expression Optimize( Expression expression )
    {
        for ( var index = 0; index < _optimizers.Count; index++ )
        {
            expression = _optimizers[index].Optimize( expression );
        }

        return expression;
    }
}
