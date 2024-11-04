using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

public class Optimizer
{
    private readonly List<IExpressionOptimizer> _optimizers;
    private readonly OptimizationOptions _context;

    public Optimizer( OptimizationOptions context )
    {
        _context = context;
        _optimizers = [];

        // 1. Constant Simplification: Perform constant folding and constant propagation first.
        //    This simplifies expressions by precomputing values, which makes subsequent optimizations easier.

        if ( context.EnableConstantSimplification )
            _optimizers.Add( new ConstantSimplificationOptimizer() );

        // 2. Inlining: Inline functions, conditional expressions, and apply boolean short-circuiting.
        //    Inlining removes function calls and known values, which can further simplify expressions.

        if ( context.EnableInlining )
            _optimizers.Add( new InliningOptimizer() );

        // 3. Control Flow Simplification: Eliminate dead code and simplify conditionals.
        //    By removing unreachable code, this prepares the expression tree for more targeted optimizations.

        if ( context.EnableControlFlowSimplification )
            _optimizers.Add( new ControlFlowSimplificationOptimizer() );

        // 4. Variable Optimization: Inline single-use variables and remove redundant assignments.
        //    This reduces memory usage and simplifies variable handling before structural changes.

        if ( context.EnableVariableOptimization )
            _optimizers.Add( new VariableOptimizationOptimizer() );

        // 5. Structural Simplification: Flatten nested blocks and remove redundant `BlockExpression` nodes.
        //    By simplifying the tree structure, this makes subsequent optimizations more efficient.

        if ( context.EnableStructuralSimplification )
            _optimizers.Add( new StructuralSimplificationOptimizer() );

        // 6. Flow Control Optimization: Remove unreferenced labels and simplify empty `TryCatch`/`TryFinally` blocks.
        //    This cleans up control flow structures and reduces unnecessary jumps.

        if ( context.EnableFlowControlOptimization )
            _optimizers.Add( new FlowControlOptimizationOptimizer() );

        // 7. Expression Caching: Cache reusable sub-expressions to avoid redundant evaluations.
        //    This improves performance by reusing results of previously computed expressions.

        if ( context.EnableExpressionCaching )
            _optimizers.Add( new ExpressionCachingOptimizer() );

        // 8. Expression Simplification: Simplify arithmetic expressions and combine adjacent expressions.
        //    This reduces the number of operations, making the expression tree more efficient.

        if ( context.EnableExpressionSimplification )
            _optimizers.Add( new ExpressionSimplificationOptimizer() );

        // 9. Access Simplification: Remove unnecessary null propagation checks and simplify constant indexing.
        //    This makes accesses to collections or arrays more efficient and eliminates redundant null checks.

        if ( context.EnableAccessSimplification )
            _optimizers.Add( new AccessSimplificationOptimizer() );

        // 10. Memory Optimization: Reuse parameters, remove unused temporary variables, and consolidate variable declarations.
        //     This reduces memory usage and optimizes variable handling for efficient memory management.

        if ( context.EnableMemoryOptimization )
            _optimizers.Add( new MemoryOptimizationOptimizer() );
    }

    public Expression Optimize( Expression expression )
    {
        for ( var index = 0; index < _optimizers.Count; index++ )
        {
            expression = _optimizers[index].Optimize( expression, _context );
        }

        return expression;
    }
}
