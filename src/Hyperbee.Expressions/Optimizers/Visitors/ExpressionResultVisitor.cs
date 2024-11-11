using System.Linq.Expressions;
using Hyperbee.Expressions.Visitors;

namespace Hyperbee.Expressions.Optimizers.Visitors;

// ExpressionCachingOptimizer: Expression Subexpression Caching
//
// This optimizer performs subexpression caching to reduce repeated computation in complex expressions. 
// By identifying and reusing common subexpressions, it improves execution efficiency, especially in cases
// where identical subexpressions are evaluated multiple times within an expression tree.
//
// This optimizer works in multiple phases:
//   * Fingerprinting: Each expression is traversed to create a unique "fingerprint" for subexpressions.
//   * Deferred Caching: Subexpressions deemed "complex enough" are queued for caching. This ensures
//     that only the necessary subexpressions are cached and that simple expressions remain unchanged.
//   * Block Wrapping: Cached expressions are assigned to variables in a `BlockExpression`, which groups
//     these assignments and ensures variables are reused wherever possible.
//
// Example: 
//
// Before Optimization:
//
// .Lambda #Lambda1<System.Func`1[System.Int32]> {
//     5 * (3 + 2) + 5 * (3 + 2)
// }
//
// After Optimization:
//
// .Lambda #Lambda1<System.Func`1[System.Int32]> {
//     .Block(System.Int32 $cacheVar) {
//         $cacheVar = 5 * (3 + 2);
//         $cacheVar + $cacheVar
//     }
// }
//
// In this example, the optimizer identifies the subexpression `5 * (3 + 2)` as a repeated, cacheable part.
// It creates a variable `$cacheVar` to hold the computed value of `5 * (3 + 2)`, and replaces occurrences
// of this subexpression with `$cacheVar` in the resulting `BlockExpression`. This optimization reduces
// redundant calculations, resulting in a more efficient expression execution.

public class ExpressionResultVisitor : ExpressionVisitor, IExpressionTransformer
{
    private readonly Dictionary<byte[], Expression> _fingerprintCache = new( new ByteArrayComparer() );
    private readonly Dictionary<byte[], ParameterExpression> _cacheVariables = new( new ByteArrayComparer() );
    private readonly Queue<(Expression Original, ParameterExpression Variable)> _deferredReplacements = new();

    private readonly ExpressionFingerprintVisitor _fingerprinter = new();

    public Expression Transform( Expression expression )
    {
        _deferredReplacements.Clear();
        var visitedExpression = Visit( expression );

        if ( _deferredReplacements.Count <= 0 )
        {
            return visitedExpression;
        }

        var blockExpressions = new List<Expression>();
        var variables = new List<ParameterExpression>();

        while ( _deferredReplacements.Count > 0 )
        {
            var (original, cacheVariable) = _deferredReplacements.Dequeue();
            variables.Add( cacheVariable );
            blockExpressions.Add( Expression.Assign( cacheVariable, original ) );
        }

        blockExpressions.Add( visitedExpression );
        visitedExpression = Expression.Block( variables, blockExpressions );

        return visitedExpression;
    }

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        var visitedNode = (LambdaExpression) base.VisitLambda( node );
        return ResolveExpression( node, visitedNode );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var visitedNode = base.VisitBinary( node );
        return ResolveExpression( node, visitedNode );
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        var visitedNode = base.VisitMethodCall( node );
        return ResolveExpression( node, visitedNode );
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        var visitedNode = base.VisitUnary( node );
        return ResolveExpression( node, visitedNode );
    }

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var visitedNode = base.VisitConditional( node );
        return ResolveExpression( node, visitedNode );
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        var visitedNode = base.VisitInvocation( node );
        return ResolveExpression( node, visitedNode );
    }

    protected override Expression VisitMember( MemberExpression node )
    {
        var visitedNode = base.VisitMember( node );
        return ResolveExpression( node, visitedNode );
    }

    protected override Expression VisitNew( NewExpression node )
    {
        var visitedNode = base.VisitNew( node );
        return ResolveExpression( node, visitedNode );
    }

    protected override Expression VisitNewArray( NewArrayExpression node )
    {
        var visitedNode = base.VisitNewArray( node );
        return ResolveExpression( node, visitedNode );
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        var visitedNode = base.VisitBlock( node );
        return ResolveExpression( node, visitedNode );
    }

    private Expression ResolveExpression( Expression original, Expression visitedNode )
    {
        if ( !ReferenceEquals( original, visitedNode ) || !IsComplexEnoughToCache( visitedNode ) )
        {
            return visitedNode;
        }

        var fingerprint = _fingerprinter.ComputeFingerprint( visitedNode );

        // Check if a cached node already exists
        if ( !_fingerprintCache.TryGetValue( fingerprint, out var cachedNode ) )
        {
            _fingerprintCache[fingerprint] = visitedNode;
            cachedNode = visitedNode;
        }

        // Ensure only one cached variable per unique subexpression
        if ( !_cacheVariables.TryGetValue( fingerprint, out var cacheVariable ) )
        {
            cacheVariable = Expression.Variable( cachedNode.Type, "cacheVar" );
            _cacheVariables[fingerprint] = cacheVariable;
            _deferredReplacements.Enqueue( (visitedNode, cacheVariable) );
        }

        return cacheVariable;
    }

    private static bool IsComplexEnoughToCache( Expression node )
    {
        return node switch
        {
            BinaryExpression binary => !(binary.Left is ConstantExpression && binary.Right is ConstantExpression) && IsComplexEnoughToCache( binary.Left ) && IsComplexEnoughToCache( binary.Right ),
            MethodCallExpression or MemberExpression or InvocationExpression => true,
            UnaryExpression unary => unary.Operand is not ConstantExpression && IsComplexEnoughToCache( unary.Operand ),
            BlockExpression block => block.Expressions.Count > 1,
            ConditionalExpression or NewExpression or NewArrayExpression => true,
            LambdaExpression lambda => lambda.Parameters.Count > 0 || IsComplexEnoughToCache( lambda.Body ),
            MemberInitExpression or ListInitExpression or IndexExpression => true,
            ConstantExpression => false,
            _ => false
        };
    }

    private class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals( byte[] x, byte[] y )
        {
            if ( ReferenceEquals( x, y ) )
                return true;

            if ( x == null || y == null )
                return false;

            if ( x.Length != y.Length )
                return false;

            for ( var i = 0; i < x.Length; i++ )
            {
                if ( x[i] != y[i] )
                    return false;
            }

            return true;
        }

        public int GetHashCode( byte[] bytes )
        {
            var hash = 17;

            foreach ( var x in bytes )
            {
                hash = hash * 31 + x;
            }

            return hash;
        }
    }


}
