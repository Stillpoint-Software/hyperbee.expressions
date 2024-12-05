using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Hyperbee.Expressions.Visitors;

// ExpressionFingerprinter: Expression Fingerprinting
//
// ExpressionFingerprinter is a general-purpose visitor for any system that needs to uniquely
// identify expressions.
//
// This visitor generates unique fingerprints for expression trees, providing a consistent and 
// reusable hash-based identifier for each node in the tree. It captures both structure and constant
// values, ensuring that each subexpression can be uniquely identified and compared. Fingerprinting
// enables the recognition of repeated patterns or identical subexpressions across expression trees.
//
// ExpressionFingerprinter works by traversing from the leaves of the tree to the root, ensuring
// that each subexpression's fingerprint is fully computed before it's included in the hash of a
// larger expression. This approach makes it ideal for identifying and reusing identical subexpressions
// at any depth of the tree.
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
// - Custom Hashing Support: The optional `GetHashBytes` delegate allows developers to define custom 
//   hashing for specific constants, offering fine-grained control over fingerprint generation. 
//   This flexibility is useful for optimizing performance or handling domain-specific requirements.

public delegate byte[] GetHashBytes( ConstantExpression node );

public class ExpressionFingerprinter : ExpressionVisitor, IDisposable
{
    private readonly MD5 _md5 = MD5.Create();
    private readonly Stack<byte[]> _hashStack = new();
    private readonly Dictionary<Expression, byte[]> _fingerprintCache = new();
    private readonly BufferWriter _bufferWriter = new();

    private static readonly byte[] NullBytes = BitConverter.GetBytes( 0 );

    public GetHashBytes CustomHashProvider { get; set; }

    public void Dispose()
    {
        GC.SuppressFinalize( this );

        _bufferWriter.Dispose();
        _md5.Dispose();
    }

    public byte[] ComputeFingerprint( Expression expression )
    {
        _md5.Initialize();
        Visit( expression );
        return _hashStack.Pop();
    }

    private void AddNodeHash( Expression node, Action<BufferWriter> writeAction )
    {
        if ( _fingerprintCache.TryGetValue( node, out var cachedHash ) )
        {
            _hashStack.Push( cachedHash );
            return;
        }

        _bufferWriter.Clear();
        writeAction( _bufferWriter );

        var hash = CombineHashes( _hashStack, _bufferWriter.GetBuffer(), _bufferWriter.Length );

        _fingerprintCache[node] = hash;
        _hashStack.Clear();
        _hashStack.Push( hash );
    }

    private byte[] CombineHashes( Stack<byte[]> hashStack, byte[] hashBuffer, int hashBufferLength )
    {
        _md5.Initialize();

        if ( hashBufferLength > 0 )
        {
            _md5.TransformBlock( hashBuffer, 0, hashBufferLength, null, 0 );
        }

        foreach ( var stackHash in hashStack )
        {
            _md5.TransformBlock( stackHash, 0, stackHash.Length, null, 0 );
        }

        _md5.TransformFinalBlock( [], 0, 0 );
        return _md5.Hash!;
    }

    public override Expression Visit( Expression node )
    {
        if ( node == null )
            return null;

        if ( !HasOverride( node ) )
        {
            AddNodeHash( node, writer => writer.Write( node.NodeType ) );
        }

        return base.Visit( node );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        var visitedNode = base.VisitBinary( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        AddNodeHash( node, writer =>
        {
            writer.Write( node.NodeType );
            writer.Write( node.IsLifted ? 1 : 0 );

            if ( node.Method == null )
                return;

            writer.Write( node.Method.Name );
            writer.Write( node.Method.DeclaringType );
        } );

        return node;
    }

    protected override Expression VisitMember( MemberExpression node )
    {
        var visitedNode = base.VisitMember( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        AddNodeHash( node, writer =>
        {
            writer.Write( node.NodeType );
            writer.Write( node.Member.MemberType );
            writer.Write( node.Member.Name );
        } );

        return node;
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        var visitedNode = base.VisitMethodCall( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        AddNodeHash( node, writer =>
        {
            writer.Write( node.NodeType );
            writer.Write( node.Method.Name );
            writer.Write( ':' );

            if ( node.Method.IsGenericMethod )
            {
                var genericArgs = node.Method.GetGenericArguments();

                for ( var index = 0; index < genericArgs.Length; index++ )
                {
                    var arg = genericArgs[index];
                    writer.Write( arg );
                }

                writer.Write( ':' );
            }

            foreach ( var param in node.Method.GetParameters() )
            {
                writer.Write( param.ParameterType );
            }
        } );

        return node;
    }

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        var visitedNode = base.VisitLambda( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        AddNodeHash( node, writer =>
        {
            writer.Write( node.NodeType );
            for ( var index = 0; index < node.Parameters.Count; index++ )
            {
                var param = node.Parameters[index];
                writer.Write( param.Type );
            }

            writer.Write( ':' );
            writer.Write( node.ReturnType );
        } );

        return node;
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        var visitedNode = base.VisitInvocation( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        AddNodeHash( node, writer =>
        {
            writer.Write( node.NodeType );

            foreach ( var arg in node.Arguments )
            {
                writer.Write( arg.Type );
            }

            writer.Write( ':' );
            writer.Write( node.Type );
        } );

        return node;
    }

    protected override Expression VisitBlock( BlockExpression node )
    {
        var visitedNode = base.VisitBlock( node );

        if ( !ReferenceEquals( visitedNode, node ) )
            return visitedNode;

        AddNodeHash( node, writer =>
        {
            writer.Write( node.NodeType );

            foreach ( var variable in node.Variables )
            {
                writer.Write( variable.Name );
                writer.Write( variable.Type );
            }

            writer.Write( ':' );
            writer.Write( node.Type );
        } );

        return node;
    }

    protected override Expression VisitConstant( ConstantExpression node )
    {
        AddNodeHash( node, writer =>
        {
            switch ( node.Value )
            {
                case null:
                    writer.Write( NullBytes );
                    return;
                case Guid guid:
                    writer.Write( guid );
                    return;
                case IConvertible convertible:
                    writer.Write( convertible );
                    return;
                default:
                    if ( CustomHashProvider != null )
                    {
                        var hashBytes = CustomHashProvider( node );
                        writer.Write( hashBytes );
                    }
                    else
                    {
                        ConstantHasher.AddToHash( _md5, node.Value );
                    }

                    break;
            }
        } );

        return node;
    }

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
            }

            var type = value.GetType();
            var typeBytesHeader = Encoding.UTF8.GetBytes( type.FullName! );
            md5.TransformBlock( typeBytesHeader, 0, typeBytesHeader.Length, null, 0 );

            var tuples = GetPropertyAccessors( type );

            for ( var index = 0; index < tuples.Length; index++ )
            {
                var (propName, accessor) = tuples[index];
                md5.TransformBlock( propName, 0, propName.Length, null, 0 );
                AddToHash( accessor( value ), md5, visited );
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

    private class BufferWriter
    {
        private byte[] _buffer;

        private readonly Dictionary<Type, int> _typeId = new();
        private int _nextTypeId;

        public byte[] GetBuffer() => _buffer;
        public int Length => Position;

        private int Position { get; set; }

        public BufferWriter( int initialSize = 512 )
        {
            _buffer = ArrayPool<byte>.Shared.Rent( initialSize );
        }

        public void Clear()
        {
            Position = 0;
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return( _buffer );
            _buffer = null!;
        }

        public void Write( Type type )
        {
            if ( type == null )
            {
                Write( NullBytes );
                return;
            }

            if ( !_typeId.TryGetValue( type, out var typeId ) )
            {
                typeId = _nextTypeId++;
                _typeId[type] = typeId;
            }

            Write( typeId );
        }

        public void Write( Guid value )
        {
            EnsureCapacity( 16 ); // Guid is 16 bytes
            value.TryWriteBytes( _buffer.AsSpan( Position ) );
            Position += 16;
        }

        public void Write( ReadOnlySpan<byte> data )
        {
            EnsureCapacity( data.Length );
            data.CopyTo( _buffer.AsSpan( Position ) );
            Position += data.Length;
        }

        public void Write( string value )
        {
            if ( value == null )
            {
                Write( NullBytes );
                return;
            }

            var byteCount = Encoding.UTF8.GetByteCount( value );
            EnsureCapacity( byteCount );
            Encoding.UTF8.GetBytes( value, _buffer.AsSpan( Position ) );
            Position += byteCount;
        }

        public void Write( ExpressionType type )
        {
            Write( (int) type );
        }

        public void Write( IConvertible convertible )
        {
            switch ( Type.GetTypeCode( convertible.GetType() ) )
            {
                case TypeCode.Boolean:
                    EnsureCapacity( 1 );
                    _buffer[Position++] = convertible.ToBoolean( CultureInfo.InvariantCulture ) ? (byte) 1 : (byte) 0;
                    break;

                case TypeCode.Char:
                    EnsureCapacity( 2 );
                    BitConverter.TryWriteBytes( _buffer.AsSpan( Position ), convertible.ToChar( CultureInfo.InvariantCulture ) );
                    Position += 2;
                    break;

                case TypeCode.Byte:
                case TypeCode.SByte:
                    EnsureCapacity( 1 );
                    _buffer[Position++] = convertible.ToByte( CultureInfo.InvariantCulture );
                    break;

                case TypeCode.Int16:
                case TypeCode.UInt16:
                    EnsureCapacity( 2 );
                    BitConverter.TryWriteBytes( _buffer.AsSpan( Position ), convertible.ToInt16( CultureInfo.InvariantCulture ) );
                    Position += 2;
                    break;

                case TypeCode.Int32:
                case TypeCode.UInt32:
                    EnsureCapacity( 4 );
                    BitConverter.TryWriteBytes( _buffer.AsSpan( Position ), convertible.ToInt32( CultureInfo.InvariantCulture ) );
                    Position += 4;
                    break;

                case TypeCode.Int64:
                case TypeCode.UInt64:
                    EnsureCapacity( 8 );
                    BitConverter.TryWriteBytes( _buffer.AsSpan( Position ), convertible.ToInt64( CultureInfo.InvariantCulture ) );
                    Position += 8;
                    break;

                case TypeCode.Single:
                    EnsureCapacity( 4 );
                    BitConverter.TryWriteBytes( _buffer.AsSpan( Position ), convertible.ToSingle( CultureInfo.InvariantCulture ) );
                    Position += 4;
                    break;

                case TypeCode.Double:
                    EnsureCapacity( 8 );
                    BitConverter.TryWriteBytes( _buffer.AsSpan( Position ), convertible.ToDouble( CultureInfo.InvariantCulture ) );
                    Position += 8;
                    break;

                case TypeCode.DateTime:
                    EnsureCapacity( 8 );
                    BitConverter.TryWriteBytes( _buffer.AsSpan( Position ), convertible.ToDateTime( CultureInfo.InvariantCulture ).ToBinary() );
                    Position += 8;
                    break;

                case TypeCode.String:
                case TypeCode.Decimal:
                default:
                    var strValue = convertible.ToString( CultureInfo.InvariantCulture );
                    var utf8Length = Encoding.UTF8.GetByteCount( strValue );
                    EnsureCapacity( utf8Length );
                    Encoding.UTF8.GetBytes( strValue, _buffer.AsSpan( Position ) );
                    Position += utf8Length;
                    break;
            }
        }

        private void EnsureCapacity( int requiredBytes )
        {
            if ( Position + requiredBytes <= _buffer.Length )
                return;

            var buffer = ArrayPool<byte>.Shared.Rent( Position + requiredBytes );
            _buffer.AsSpan( 0, Position ).CopyTo( buffer );
            ArrayPool<byte>.Shared.Return( _buffer );
            _buffer = buffer;
        }
    }
}

