using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Hyperbee.Expressions.Visitors;

public delegate byte[] GetHashBytes( ConstantExpression node );

// ExpressionFingerprintVisitor: Expression Fingerprinting
//
// This visitor generates unique fingerprints for expression trees, providing a consistent and 
// reusable hash-based identifier for each node in the tree. It captures both structure and values, 
// ensuring that each subexpression can be uniquely identified and compared. Fingerprinting enables 
// the recognition of repeated patterns or identical subexpressions across expression trees.
//
// Designed to operate in a bottom-up manner, the `ExpressionFingerprintVisitor` works by traversing 
// from the leaves of the tree to the root, ensuring that each subexpression's fingerprint is fully 
// computed before it's included in the hash of a larger expression. This approach makes it ideal for 
// identifying and reusing identical subexpressions at any depth of the tree.
//
// Key Features and Constraints:
//
// - Bottom-Up Traversal: By visiting nodes from the leaves to the root, each subexpression is 
//   fully resolved and fingerprinted before it is integrated into the larger context. This ensures 
//   consistent and precise identification of all parts of the tree.
//
// - Consistent Fingerprints: Each expression node, from constants to complex method calls, is assigned 
//   a unique fingerprint. This allows for the comparison of nodes based solely on their fingerprints, 
//   regardless of their original position in the expression tree.
//
// - Complexity-Based Heuristics: Through `IsComplexEnoughToCache` (or equivalent logic), this class 
//   supports the selective fingerprinting of complex nodes, filtering out nodes where fingerprinting 
//   may not add value.
//
// - Custom Hashing Support: The optional `GetHashBytes` delegate allows developers to define custom 
//   hashing for specific constants, offering fine-grained control over fingerprint generation. 
//   This flexibility is useful for optimizing performance or handling domain-specific requirements.
//
// Use Cases:
//
// The `ExpressionFingerprintVisitor` is an essential utility for applications that rely on consistent 
// identification of subexpressions, such as expression caching, optimization, or comparison. By 
// creating repeatable and unique identifiers, it supports scenarios where recognizing identical 
// expressions across complex trees is beneficial.
//
// Example Scenario: Expression Caching Optimization
//
// Original Expression:
//
// .Lambda #Lambda1<System.Func<int>>() {
//     5 * (3 + 2) + 5 * (3 + 2)
// }
//
// Fingerprinting enables caching or deduplication as follows:
//
// .Lambda #Lambda1<System.Func<int>>() {
//     .Block(System.Int32 $cacheVar) {
//         $cacheVar = 5 * (3 + 2);
//         $cacheVar + $cacheVar
//     }
// }
//
// This transformation, achieved with the help of consistent fingerprinting. The expression caching
// optimizer is able to eliminate redundant computations by recognizing repeated patterns by using
// expression fingerprinting.
//
// The `ExpressionFingerprintVisitor` is a general-purpose utility for any system that requires
// structured and reusable hashing of expression trees.

public class ExpressionFingerprintVisitor : ExpressionVisitor
{
    private readonly MD5 _md5 = MD5.Create();
    private readonly Stack<byte[]> _hashStack = new();
    private readonly Dictionary<Expression, byte[]> _fingerprintCache = new();

    private static readonly byte[] NullBytes = BitConverter.GetBytes( 0 );

    public GetHashBytes CustomHashProvider { get; set; }

    public byte[] ComputeFingerprint( Expression expression )
    {
        _md5.Initialize();
        Visit( expression );
        return _hashStack.Pop();
    }

    private void AddNodeHash( Expression node, byte[] hashValue, Action<MD5> hashAction = null )
    {
        if ( _fingerprintCache.TryGetValue( node, out var cachedHash ) )
        {
            _hashStack.Push( cachedHash );
            return;
        }

        var combinedHash = CombineHashes( _hashStack, hashValue, hashAction );

        _fingerprintCache[node] = combinedHash;
        _hashStack.Clear();
        _hashStack.Push( combinedHash );
    }

    private void AddNodeHash( Expression node, StringBuilder signature )
    {
        AddNodeHash( node, Encoding.UTF8.GetBytes( signature.ToString() ) );
    }

    private byte[] CombineHashes( Stack<byte[]> hashStack, byte[] hashValue, Action<MD5> hashAction = null )
    {
        _md5.Initialize();

        if ( hashValue == null && hashAction == null )
            throw new InvalidOperationException( "Must provide a hash action or a hash value." );

        if ( hashValue != null && hashAction != null )
            throw new InvalidOperationException( "Cannot write both a hash action and a hash value." );

        if ( hashAction != null )
            hashAction( _md5 );
        else
            _md5.TransformBlock( hashValue, 0, hashValue.Length, null, 0 );

        foreach ( var stackHash in hashStack )
            _md5.TransformBlock( stackHash, 0, stackHash.Length, null, 0 );

        _md5.TransformFinalBlock( [], 0, 0 );

        return _md5.Hash;
    }

    public override Expression Visit( Expression node )
    {
        if ( node == null )
            return null;

        if ( !HasOverride( node ) )
        {
            AddNodeHash( node, Encoding.UTF8.GetBytes( $"{node.NodeType}" ) );
        }

        return base.Visit( node );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var visitedNode = base.VisitBinary( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        var signature = SignatureBuilder( node )
            .Append( node.IsLifted ? "Lifted" : "NotLifted" );

        if ( node.Method != null )
        {
            signature.Append( ":Method=" ).Append( node.Method.Name )
                .Append( ":DeclaringType=" ).Append( node.Method.DeclaringType?.FullName ?? "null" );
        }

        AddNodeHash( node, signature );
        return node;
    }

    protected override Expression VisitMember( MemberExpression node )
    {
        var visitedNode = base.VisitMember( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        AddNodeHash( node, Encoding.UTF8.GetBytes( node.Member.Name ) );
        return node;
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        var visitedNode = base.VisitMethodCall( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        var signature = SignatureBuilder( node )
            .Append( node.Method.Name );

        // method generic arguments

        if ( node.Method.IsGenericMethod )
        {
            signature.Append( '<' );

            var genericArgs = node.Method.GetGenericArguments();

            for ( var index = 0; index < genericArgs.Length; index++ )
            {
                var argType = genericArgs[index];
                signature.Append( argType.FullName );

                if ( index < genericArgs.Length - 1 )
                    signature.Append( ", " );
            }

            signature.Append( '>' );
        }

        // method parameters

        signature.Append( '(' );
        var parameters = node.Method.GetParameters();

        for ( var index = 0; index < parameters.Length; index++ )
        {
            var parameter = parameters[index];
            signature.Append( parameter.ParameterType.FullName );

            if ( index < parameters.Length - 1 )
                signature.Append( ", " );
        }

        signature.Append( ')' );

        AddNodeHash( node, signature );
        return node;
    }

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        var visitedNode = base.VisitLambda( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        var signature = SignatureBuilder( node )
            .Append( ".Lambda(" );

        for ( var index = 0; index < node.Parameters.Count; index++ )
        {
            var parameter = node.Parameters[index];
            signature.Append( parameter.Type.FullName );

            if ( index < node.Parameters.Count - 1 )
                signature.Append( ", " );
        }

        signature
            .Append( ") => " )
            .Append( node.ReturnType.FullName );

        AddNodeHash( node, signature );
        return node;
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        var visitedNode = base.VisitInvocation( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        var signature = SignatureBuilder( node )
            .Append( ".Invocation(" );

        for ( var index = 0; index < node.Arguments.Count; index++ )
        {
            var arg = node.Arguments[index];
            signature.Append( arg.Type.FullName );

            if ( index < node.Arguments.Count - 1 )
                signature.Append( ", " );
        }

        signature.Append( ')' );

        AddNodeHash( node, signature );
        return node;
    }


    protected override Expression VisitBlock( BlockExpression node )
    {
        var visitedNode = base.VisitBlock( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        var signature = SignatureBuilder( node )
            .Append( ".Block(" );

        for ( var index = 0; index < node.Variables.Count; index++ )
        {
            var variable = node.Variables[index];
            signature.Append( $"{variable}:{variable.Type.FullName!}" );

            if ( index < node.Variables.Count - 1 )
                signature.Append( ", " );
        }

        signature.Append( ')' );

        AddNodeHash( node, signature );
        return node;
    }

    protected override Expression VisitConstant( ConstantExpression node )
    {
        byte[] hashValue;
        Action<MD5> hashAction = null;

        switch ( node.Value )
        {
            case null:
                hashValue = NullBytes;
                break;
            case string str:
                hashValue = Encoding.UTF8.GetBytes( str );
                break;
            case Guid guid:
                hashValue = guid.ToByteArray();
                break;
            case DateTime dateTime:
                hashValue = BitConverter.GetBytes( dateTime.ToBinary() );
                break;
            case IConvertible convertible:
                hashValue = Encoding.UTF8.GetBytes( convertible.ToString( CultureInfo.InvariantCulture ) );
                break;
            default:
                hashValue = CustomHashProvider?.Invoke( node );

                if ( hashValue == null )
                    hashAction = md5 => ConstantHasher.AddToHash( md5, node.Value );
                break;
        }

        AddNodeHash( node, hashValue, hashAction );
        return node;
    }

    private static StringBuilder SignatureBuilder( Expression node ) => new( $"{node.NodeType}: " );

    private static bool HasOverride( Expression node )
    {
        return node switch
        {
            BinaryExpression or
                ConstantExpression or
                MemberExpression or
                MethodCallExpression or
                InvocationExpression or
                LambdaExpression or
                BlockExpression => true,
            _ => false
        };
    }

    private static class ConstantHasher
    {
        private static readonly ConcurrentDictionary<Type, (byte[] NameBytes, Func<object, object> Accessor)[]> __propertyCache
            = new();

        public static void AddToHash( MD5 md5, object value )
        {
            var visited = new HashSet<object>();
            AddToHash( value, md5, visited );
        }

        private static void AddToHash( object value, HashAlgorithm md5, HashSet<object> visited )
        {
            if ( value == null )
            {
                md5.TransformBlock( NullBytes, 0, NullBytes.Length, null, 0 );
                return;
            }

            if ( !visited.Add( value ) )
                return;

            switch ( value )
            {
                case DateTime dateTime:
                    {
                        var dateBytes = BitConverter.GetBytes( dateTime.ToBinary() );
                        md5.TransformBlock( dateBytes, 0, dateBytes.Length, null, 0 );
                        return;
                    }
                case IConvertible convertible:
                    {
                        var bytes = Encoding.UTF8.GetBytes( convertible.ToString( CultureInfo.InvariantCulture ) );
                        md5.TransformBlock( bytes, 0, bytes.Length, null, 0 );
                        return;
                    }
                case Guid guid:
                    {
                        var guidBytes = guid.ToByteArray();
                        md5.TransformBlock( guidBytes, 0, guidBytes.Length, null, 0 );
                        return;
                    }
                case nint:
                case nuint:
                    {
                        var typeBytes = Encoding.UTF8.GetBytes( value.GetType().FullName! );
                        md5.TransformBlock( typeBytes, 0, typeBytes.Length, null, 0 );
                        return;
                    }
            }

            // complex object

            var type = value.GetType();
            var typeBytesHeader = Encoding.UTF8.GetBytes( type.FullName! );

            md5.TransformBlock( typeBytesHeader, 0, typeBytesHeader.Length, null, 0 );

            var tuples = GetPropertyAccessors( type );

            for ( var index = 0; index < tuples.Length; index++ )
            {
                var (propName, accessor) = tuples[index];

                md5.TransformBlock( propName, 0, propName.Length, null, 0 );

                var propValue = accessor( value );
                AddToHash( propValue, md5, visited );
            }

            visited.Remove( value );
        }

        private static (byte[] NameBytes, Func<object, object> Accessor)[] GetPropertyAccessors( Type type )
        {
            return __propertyCache.GetOrAdd( type, x =>
            {
                return x.GetProperties( BindingFlags.Public | BindingFlags.Instance )
                    .Where( p => p.CanRead && p.GetIndexParameters().Length == 0 )
                    .Select( p =>
                    {
                        var propName = Encoding.UTF8.GetBytes( p.Name );
                        return (propName, CreatePropertyAccessor( p ));
                    } )
                    .ToArray();
            } );
        }

        private static Func<object, object> CreatePropertyAccessor( PropertyInfo propertyInfo )
        {
            var instance = Expression.Parameter( typeof( object ), "instance" );
            var castInstance = Expression.Convert( instance, propertyInfo.DeclaringType! );
            var propertyAccess = Expression.Property( castInstance, propertyInfo );
            var castResult = Expression.Convert( propertyAccess, typeof( object ) );

            return Expression.Lambda<Func<object, object>>( castResult, instance ).Compile();
        }
    }
}
