using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;

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
public class TryCatchSimplifierVisitor : ExpressionVisitor, IExpressionTransformer
{
    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

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
