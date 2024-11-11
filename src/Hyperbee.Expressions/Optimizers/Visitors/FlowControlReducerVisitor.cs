using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;

// FlowControlReducerVisitor: Loop and Conditional Simplification
//
// This visitor optimizes control flow expressions, specifically focusing on loops and conditional branches.
// It removes non-operational or non-entering loops and simplifies conditionals with constant expressions.
// Infinite loops with constant expressions (e.g., `Loop(Constant(1))`) are treated as redundant if they perform
// no meaningful action and have no side effects or terminating conditions.
//
// Transformation Patterns:
//
// 1. Non-Entering Loop (Loop with a `false` condition):
//    Before: .Loop(.IfThenElse(.Constant(false), .Break()))
//    After:  .Empty()
//
// 2. No-Op Loop (Loop body performs no action):
//    Before: .Loop(.Constant(0))  // No-op as it doesn’t affect program state
//    After:  .Empty()
//
// 3. Infinite Loop with Constant Expression (No effect):
//    Before: .Loop(.Constant(1))
//    After:  .Empty()
//
// 4. Unreachable Conditional (Condition is a constant):
//    Before: .IfThenElse(.Constant(true), .Constant(1), .Constant(0))
//    After:  .Constant(1)
//
// 5. Intentional Infinite Loop (Loop without break and meaningful action):
//    Before: .Loop(.Constant(1) with meaningful break or continue logic)
//    After:  (Unchanged - Infinite loop is preserved as it may be intentional)
//
public class FlowControlReducerVisitor : ExpressionVisitor, IExpressionTransformer
{
    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var test = Visit( node.Test );

        if ( test is ConstantExpression testConst )
        {
            var condition = (bool) testConst.Value!;
            var result = condition ? node.IfTrue : node.IfFalse;
            return Visit( result );
        }

        return base.VisitConditional( node );
    }

    protected override Expression VisitLoop( LoopExpression node )
    {
        var body = Visit( node.Body );

        // Case 1: Remove non-entering loops or loops with effectively no meaningful effect
        if ( IsFalsyConstant( body ) || IsNoOpExpression( body ) || IsEffectivelyInfiniteNoOpLoop( body ) )
        {
            return node.Type == typeof(void) ? Expression.Empty() : Expression.Default( node.Type );
        }

        return node.Update( node.BreakLabel, node.ContinueLabel, body );
    }

    private static bool IsEffectivelyInfiniteNoOpLoop( Expression expression )
    {
        // Detects infinite loops with no operational effect (e.g., loops with static constants like Constant(1))
        return expression is ConstantExpression && !HasControlFlowOrSideEffects( expression );
    }

    private static bool HasControlFlowOrSideEffects( Expression expression )
    {
        // This method can be expanded if we define what constitutes a meaningful action in loop context
        // For now, it returns false for any ConstantExpression (no side effects).
        return expression is not ConstantExpression;
    }


    private static bool IsFalsyConstant( Expression expression )
    {
        // Detects constants with "falsy" values: null, false, 0, 0.0, etc.
        return expression is ConstantExpression constant && constant.Value switch
        {
            null => true,
            bool boolValue => !boolValue,
            int intValue => intValue == 0,
            double doubleValue => doubleValue == 0.0,
            float floatValue => floatValue == 0.0f,
            _ => false
        };
    }

    private static bool IsNoOpExpression( Expression expression )
    {
        // Checks if an expression is effectively a no-op, such as an empty block or default expression
        return expression is DefaultExpression ||
               (expression is BlockExpression block && block.Expressions.All( IsNoOpExpression ));
    }
}

