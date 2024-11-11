using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;


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
    public class BlockReducerVisitor : ExpressionVisitor, IExpressionTransformer
    {
    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

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





 
