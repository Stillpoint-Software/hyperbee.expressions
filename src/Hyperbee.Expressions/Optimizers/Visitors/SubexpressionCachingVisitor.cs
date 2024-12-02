using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.Expressions.Visitors;

namespace Hyperbee.Expressions.Optimizers.Visitors;

public class SubexpressionCachingVisitor : ExpressionVisitor, IExpressionTransformer
{
    private readonly Dictionary<byte[], Expression> _fingerprintCache = new(new ByteArrayComparer());
    private readonly Dictionary<byte[], ParameterExpression> _cacheVariables = new(new ByteArrayComparer());
    private readonly Queue<(Expression Original, ParameterExpression Variable)> _deferredReplacements = new();
    private readonly ExpressionFingerprinter _fingerprinter = new();
    private readonly CacheableHelper _cacheHelper = new();

    private int _cacheVariableCounter;

    public int Priority => PriorityGroup.ExpressionReductionAndCaching + 70;

    public Expression Transform( Expression expression )
    {
        _deferredReplacements.Clear();
        var visitedExpression = Visit( expression );

        if ( _deferredReplacements.Count == 0 )
        {
            return visitedExpression;
        }

        var blockExpressions = new List<Expression>();
        var variables = new List<ParameterExpression>();

        if ( visitedExpression is BlockExpression originalBlock )
        {
            variables.AddRange( originalBlock.Variables );
            blockExpressions.AddRange( originalBlock.Expressions );
        }
        else
        {
            blockExpressions.Add( visitedExpression );
        }

        while ( _deferredReplacements.Count > 0 )
        {
            var (original, cacheVariable) = _deferredReplacements.Dequeue();

            if ( !variables.Contains( cacheVariable ) )
            {
                variables.Add( cacheVariable );
            }

            blockExpressions.Insert( 0, Expression.Assign( cacheVariable, original ) );
        }

        return Expression.Block( variables, blockExpressions );
    }

    public override Expression Visit( Expression node )
    {
        var visited = base.Visit( node );
        return ResolveExpression( node, visited );
    }

    private Expression ResolveExpression( Expression original, Expression visitedNode )
    {
        if ( !_cacheHelper.IsCacheable( original ) )
        {
            return visitedNode;
        }

        var fingerprint = _fingerprinter.ComputeFingerprint( visitedNode );

        if ( !_fingerprintCache.TryGetValue( fingerprint, out var cachedNode ) )
        {
            _fingerprintCache[fingerprint] = visitedNode;
            cachedNode = visitedNode;
        }

        if ( _cacheVariables.TryGetValue( fingerprint, out var cacheVariable ) )
        {
            return cacheVariable;
        }

        cacheVariable = Expression.Variable( cachedNode.Type, $"cacheVar{_cacheVariableCounter++}" );
        _cacheVariables[fingerprint] = cacheVariable;
        _deferredReplacements.Enqueue( (visitedNode, cacheVariable) );

        return cacheVariable;
    }

    private class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals( byte[] x, byte[] y )
        {
            if ( ReferenceEquals( x, y ) )
                return true;

            if ( x == null || y == null || x.Length != y.Length )
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

            foreach ( var b in bytes )
            {
                hash = hash * 31 + b;
            }

            return hash;
        }
    }

    protected class CacheableHelper
    {
        private readonly ImmutabilityVisitor _visitor = new();

        public bool IsCacheable( Expression expression )
        {
            return IsImmutable( expression ) && IsComplexEnoughToCache( expression );
        }

        private bool IsImmutable( Expression expression )
        {
            return _visitor.IsImmutable( expression );
        }

        public static bool IsComplexEnoughToCache( Expression node )
        {
            return node switch
            {
                BinaryExpression binary =>
                    (binary.Left is not ConstantExpression || binary.Right is not ConstantExpression) &&
                    (IsComplexEnoughToCache( binary.Left ) ||
                     IsComplexEnoughToCache( binary.Right ) ||
                     binary.NodeType is
                         ExpressionType.Add or
                         ExpressionType.Multiply or
                         ExpressionType.Divide or
                         ExpressionType.Subtract),

                MethodCallExpression or MemberExpression or InvocationExpression => true,
                UnaryExpression unary => IsComplexEnoughToCache( unary.Operand ),
                ConditionalExpression or NewExpression or NewArrayExpression => true,
                LambdaExpression lambda => lambda.Parameters.Count > 0 || IsComplexEnoughToCache( lambda.Body ),
                MemberInitExpression or ListInitExpression or IndexExpression => true,
                ConstantExpression => false,
                _ => false
            };
        }
    }

    private class ImmutabilityVisitor : ExpressionVisitor
    {
        private static readonly Dictionary<Type, bool> ImmutableTypeCache = new();

        private bool _isImmutable;

        public bool IsImmutable( Expression node )
        {
            _isImmutable = true;
            Visit( node );
            return _isImmutable;
        }

        protected override Expression VisitMember( MemberExpression node )
        {
            if ( !IsImmutableType( node.Type ) )
            {
                _isImmutable = false;
            }

            return base.VisitMember( node );
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            if ( !IsImmutableType( node.Type ) )
            {
                _isImmutable = false;
            }

            return base.VisitParameter( node );
        }

        private static bool IsImmutableType( Type type )
        {
            if ( ImmutableTypeCache.TryGetValue( type, out var isImmutable ) )
            {
                return isImmutable;
            }

            if ( type == typeof(Guid) || typeof(IConvertible).IsAssignableFrom( type ) )
            {
                return ImmutableTypeCache[type] = true;
            }

            if ( type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) )
            {
                return ImmutableTypeCache[type] = IsImmutableType( type.GetGenericArguments()[0] );
            }

            if ( type.IsValueType )
            {
                return ImmutableTypeCache[type] = type
                    .GetFields( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
                    .All( field => IsImmutableType( field.FieldType ) );
            }

            if ( type.IsClass )
            {
                return ImmutableTypeCache[type] = type
                    .GetProperties()
                    .All( property => property.CanRead && !property.CanWrite && IsImmutableType( property.PropertyType ) );
            }

            return ImmutableTypeCache[type] = false;
        }
    }
}
