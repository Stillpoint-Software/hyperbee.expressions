using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers;

// FlowControlOptimizer: Control Flow Simplification
//
// This optimizer manages blocks, labels, loops, and conditionals, removing unreachable
// code and dead branches based on control flow and constant conditions.

public class FlowControlOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression )
    {
        // Run optimization in multiple phases for maximal simplification
        expression = new BlockSimplifierVisitor().Visit( expression );
        expression = new LabelSimplifierVisitor().Visit( expression );
        expression = new ControlFlowSimplifierVisitor().Visit( expression );
        expression = new TryCatchSimplifierVisitor().Visit( expression );

        return expression;
    }

    public TExpr Optimize<TExpr>( TExpr expression ) where TExpr : LambdaExpression
    {
        var optimizedBody = Optimize( expression.Body );

        return !ReferenceEquals( expression.Body, optimizedBody )
            ? (TExpr) Expression.Lambda( expression.Type, optimizedBody, expression.Parameters )
            : expression;
    }

    // BlockSimplifier: Block Expression Flattening and Dead Code Elimination
    //
    // This visitor flattens nested `BlockExpression`s and removes unreachable expressions
    // following control flow exits such as `return`, `goto`, or `throw`.
    //
    // Before:
    //
    //   .Block(
    //       .Block(
    //           .Constant(2),
    //           .Goto(label1)
    //       ),
    //       .Constant(3)  // Unreachable
    //   )
    //
    // After:
    //
    //   .Block(
    //       .Constant(2),
    //       .Goto(label1)
    //   )
    //
    private class BlockSimplifierVisitor : ExpressionVisitor
    {
        protected override Expression VisitBlock( BlockExpression node )
        {
            var flattenedExpressions = new List<Expression>();
            var variables = new List<ParameterExpression>( node.Variables );

            for ( var index = 0; index < node.Expressions.Count; index++ )
            {
                var expr = node.Expressions[index];
                if ( expr is BlockExpression innerBlock )
                {
                    variables.AddRange( innerBlock.Variables );
                    flattenedExpressions.AddRange( innerBlock.Expressions.Select( Visit ) );
                }
                else
                {
                    flattenedExpressions.Add( Visit( expr ) );
                }
            }

            var optimizedExpressions = new List<Expression>();
            var hasControlFlowExit = false;

            for ( var index = 0; index < flattenedExpressions.Count; index++ )
            {
                var expr = flattenedExpressions[index];
                if ( hasControlFlowExit )
                    continue;

                // Check for control flow exits like `goto` or `throw`
                if ( expr is GotoExpression || (expr is UnaryExpression unary && unary.NodeType == ExpressionType.Throw) )
                {
                    hasControlFlowExit = true;
                }

                optimizedExpressions.Add( expr );
            }

            return optimizedExpressions.Count == 1 && variables.Count == 0
                ? optimizedExpressions[0]
                : Expression.Block( variables, optimizedExpressions );
        }
    }


    // ControlFlowSimplifier: Conditional and Loop Expression Optimization
    //
    // This visitor simplifies `ConditionalExpression` and `LoopExpression` nodes by reducing
    // unnecessary conditions and loop constructs. For conditionals, it pre-evaluates static
    // conditions, replacing them with the appropriate branch. For loops, it removes loops with
    // a `false` condition.
    //
    // Before:
    //
    //   .IfThenElse(.Constant(true), .Constant(1), .Constant(0))
    //
    // After:
    //
    //   .Constant(1)
    //
    // Before:
    //
    //   .Loop(
    //       .IfThenElse(.Constant(false), .Break(loop))
    //   )
    //
    // After:
    //
    //   .Empty()
    //
    private class ControlFlowSimplifierVisitor : ExpressionVisitor
    {
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

            if ( body is ConditionalExpression conditional && IsConstantFalse( conditional.Test ) )
            {
                return node.Type == typeof( void ) ? Expression.Empty() : Expression.Default( node.Type );
            }

            return node.Update( node.BreakLabel, node.ContinueLabel, body );
        }

        private static bool IsConstantFalse( Expression expression )
        {
            return expression is ConstantExpression constant &&
                   constant.Type == typeof( bool ) &&
                   !(bool) constant.Value!;
        }
    }

    // LabelSimplifier: Label and Goto Simplification
    //
    // This visitor removes unreferenced `LabelExpression`s and optimizes `GotoExpression`s
    // by eliminating unnecessary gotos, typically those that do not have a corresponding label
    // or are never used. It scans for labels in the expression tree to determine which ones
    // are required and removes those that are unreferenced.
    //
    // Before:
    //
    //   .Block(
    //       .Label(label1),
    //       .Goto(label1),
    //       .Constant(5)
    //   )
    //
    // After:
    //
    //   .Block(
    //       .Constant(5)
    //   )
    //
    private class LabelSimplifierVisitor : ExpressionVisitor
    {
        private readonly HashSet<LabelTarget> _usedLabels = new();

        protected override Expression VisitLabel( LabelExpression node )
        {
            return _usedLabels.Contains( node.Target ) ? node : Expression.Empty();
        }

        protected override Expression VisitGoto( GotoExpression node )
        {
            _usedLabels.Add( node.Target );

            if ( node.Target.Name == null )
            {
                return Expression.Empty();
            }

            var expression = Visit( node.Value );
            return Expression.MakeGoto( node.Kind, node.Target, expression, node.Type );
        }
    }

    // TryCatchSimplifier: Try-Catch-Finally Optimization
    //
    // This visitor removes empty `TryExpression` blocks and redundant catch handlers or
    // finally blocks. If the body of a `try` block and its `catch` handlers and `finally`
    // blocks are empty or have no effect, they are eliminated to streamline the control flow.
    //
    // Before:
    //
    //   .Try(
    //       .Empty(),
    //       .Catch(
    //           .Parameter(ex, typeof(Exception)),
    //           .Empty()
    //       ),
    //       .Finally(.Empty())
    //   )
    //
    // After:
    //
    //   .Empty()
    //
    private class TryCatchSimplifierVisitor : ExpressionVisitor
    {
        protected override Expression VisitTry( TryExpression node )
        {
            var body = Visit( node.Body );

            if ( IsEmpty( body ) )
            {
                return Expression.Empty();
            }

            var final = node.Finally != null ? Visit( node.Finally ) : null;
            var handlers = VisitCatchBlocks( node.Handlers );

            if ( (final == null || IsEmpty( final )) && handlers.Count == 0 )
            {
                return body;
            }

            return Expression.MakeTry( node.Type, body, final, node.Fault, handlers );
        }

        private ReadOnlyCollection<CatchBlock> VisitCatchBlocks( ReadOnlyCollection<CatchBlock> handlers )
        {
            var newHandlers = new List<CatchBlock>();

            foreach ( var handler in handlers )
            {
                var body = Visit( handler.Body );
                var filter = handler.Filter != null ? Visit( handler.Filter ) : null;

                if ( IsEmpty( body ) && filter == null )
                {
                    continue;
                }

                if ( handler.Variable != null )
                {
                    var variable = (ParameterExpression) Visit( handler.Variable );
                    newHandlers.Add( Expression.Catch( variable, body, filter ) );
                }
                else
                {
                    newHandlers.Add( Expression.Catch( handler.Test, body, filter ) );
                }
            }

            return newHandlers.AsReadOnly();
        }

        private static bool IsEmpty( Expression expression )
        {
            return expression switch
            {
                null => true,
                DefaultExpression => true,
                BlockExpression block when block.Expressions.Count == 0 => true,
                _ => false,
            };
        }
    }
}
