using System.Linq.Expressions;
using System.Numerics;

namespace Hyperbee.Expressions.Optimizers;

// ConstantSimplificationOptimizer: Expression Simplification
//
// This optimizer performs constant folding and constant propagation.
// It simplifies expressions by precomputing constant values and replacing variables with known constants.
// For example, expressions like "2 + 3" are reduced to "5".

public class ConstantFoldingOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression )
    {
        return Visit( expression );
    }

    public TExpr Optimize<TExpr>( TExpr expression ) where TExpr : LambdaExpression
    {
        return (TExpr) Visit( expression );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        if ( node.Left is ConstantExpression leftConst && node.Right is ConstantExpression rightConst )
        {
            var folded = FoldConstants( node.NodeType, leftConst, rightConst );

            if ( folded != null )
            {
                return folded;
            }
        }

        return base.VisitBinary( node );
    }

    private static ConstantExpression FoldConstants( ExpressionType nodeType, ConstantExpression leftConst, ConstantExpression rightConst )
    {
        if ( !typeof(IConvertible).IsAssignableFrom( leftConst.Type ) || !typeof(IConvertible).IsAssignableFrom( rightConst.Type ) )
            return null;

        if ( IsNumericType( leftConst.Type ) && IsNumericType( rightConst.Type ) )
        {
            return FoldNumericOperation( nodeType, leftConst, rightConst );
        }

        if ( leftConst.Type != rightConst.Type )
        {
            return null;
        }

        var type = leftConst.Type;

        return type switch
        {
            not null when type == typeof(bool) => FoldBooleanOperation( nodeType, (bool) leftConst.Value!, (bool) rightConst.Value! ),
            not null when type == typeof(string) => FoldStringOperation( nodeType, (string) leftConst.Value!, (string) rightConst.Value! ),
            not null when type == typeof(char) => FoldCharOperation( nodeType, (char) leftConst.Value!, (char) rightConst.Value! ),
            not null when type == typeof(DateTime) => FoldDateTimeOperation( nodeType, (DateTime) leftConst.Value!, rightConst.Value ),
            _ => null
        };
    }

    private static ConstantExpression FoldBooleanOperation( ExpressionType nodeType, bool left, bool right )
    {
        return nodeType switch
        {
            ExpressionType.And => Expression.Constant( left && right ),
            ExpressionType.Or => Expression.Constant( left || right ),
            ExpressionType.Equal => Expression.Constant( left == right ),
            ExpressionType.NotEqual => Expression.Constant( left != right ),
            _ => null
        };
    }

    private static ConstantExpression FoldCharOperation( ExpressionType nodeType, char left, char right )
    {
        int leftInt = left;
        int rightInt = right;

        return nodeType switch
        {
            ExpressionType.Add => Expression.Constant( (char) (leftInt + rightInt) ),
            ExpressionType.Subtract => Expression.Constant( (char) (leftInt - rightInt) ),
            _ => null
        };
    }

    private static ConstantExpression FoldDateTimeOperation( ExpressionType nodeType, DateTime left, object rightValue )
    {
        if ( rightValue is not TimeSpan rightTimeSpan )
            return null;

        return nodeType switch
        {
            ExpressionType.Add => Expression.Constant( left + rightTimeSpan ),
            ExpressionType.Subtract => Expression.Constant( left - rightTimeSpan ),
            _ => null
        };
    }

    private static ConstantExpression FoldStringOperation( ExpressionType nodeType, string left, string right )
    {
        if ( nodeType != ExpressionType.Add )
            return null;

        if ( left == null || right == null )
            return null;

        return Expression.Constant( left + right );
    }

    private static ConstantExpression FoldNumericOperation( ExpressionType nodeType, ConstantExpression leftConst, ConstantExpression rightConst )
    {
        Type commonType;
        object leftValue;
        object rightValue;

        if ( leftConst.Type == rightConst.Type )
        {
            commonType = leftConst.Type;
            leftValue = leftConst.Value!;
            rightValue = rightConst.Value!;
        }
        else
        {
            commonType = GetNumericType( leftConst.Type, rightConst.Type );

            if ( commonType == null )
                throw new InvalidOperationException( "Incompatible numeric types." );

            leftValue = Convert.ChangeType( leftConst.Value, commonType )!;
            rightValue = Convert.ChangeType( rightConst.Value, commonType )!;
        }

        // Use a generic method to apply the operation based on the common type
        return commonType switch
        {
            not null when commonType == typeof(byte) => ApplyOperation( nodeType, (byte) leftValue, (byte) rightValue ),
            not null when commonType == typeof(sbyte) => ApplyOperation( nodeType, (sbyte) leftValue, (sbyte) rightValue ),
            not null when commonType == typeof(short) => ApplyOperation( nodeType, (short) leftValue, (short) rightValue ),
            not null when commonType == typeof(ushort) => ApplyOperation( nodeType, (ushort) leftValue, (ushort) rightValue ),
            not null when commonType == typeof(int) => ApplyOperation( nodeType, (int) leftValue, (int) rightValue ),
            not null when commonType == typeof(uint) => ApplyOperation( nodeType, (uint) leftValue, (uint) rightValue ),
            not null when commonType == typeof(long) => ApplyOperation( nodeType, (long) leftValue, (long) rightValue ),
            not null when commonType == typeof(ulong) => ApplyOperation( nodeType, (ulong) leftValue, (ulong) rightValue ),
            not null when commonType == typeof(float) => ApplyOperation( nodeType, (float) leftValue, (float) rightValue ),
            not null when commonType == typeof(double) => ApplyOperation( nodeType, (double) leftValue, (double) rightValue ),
            not null when commonType == typeof(decimal) => ApplyOperation( nodeType, (decimal) leftValue, (decimal) rightValue ),
            _ => throw new InvalidOperationException( "Unsupported type for promoted operation." )
        };

        // Operation helper

        static ConstantExpression ApplyOperation<T>( ExpressionType nodeType, T left, T right ) where T : INumber<T>
        {
            var constant = nodeType switch
            {
                ExpressionType.Add => left + right,
                ExpressionType.Subtract => left - right,
                ExpressionType.Multiply => left * right,
                ExpressionType.Divide => Divide( left, right ),
                _ => throw new InvalidOperationException( $"Unsupported operation {nodeType} for type {typeof(T)}." )
            };

            return Expression.Constant( constant );

            static T Divide( T left, T right )
            {
                if ( right == T.Zero )
                    throw new DivideByZeroException();

                return left / right;
            }
        }
    }
    
    private static bool IsNumericType( Type type )
    {
        if ( type == null )
            return false;

        return Type.GetTypeCode( type ) switch
        {
            TypeCode.Byte or 
            TypeCode.SByte or
            TypeCode.Int16 or 
            TypeCode.UInt16 or
            TypeCode.Int32 or 
            TypeCode.UInt32 or
            TypeCode.Int64 or 
            TypeCode.UInt64 or
            TypeCode.Single or 
            TypeCode.Double or
            TypeCode.Decimal => true,
            _ => false
        };
    }

    private static readonly TypeCode[] TypeCodePromotionOrder = 
    [
        TypeCode.Byte, 
        TypeCode.SByte, 
        TypeCode.Int16, 
        TypeCode.UInt16, 
        TypeCode.Int32, 
        TypeCode.UInt32, 
        TypeCode.Int64, 
        TypeCode.UInt64, 
        TypeCode.Single, 
        TypeCode.Double, 
        TypeCode.Decimal
    ];
    
    private static Type GetNumericType( Type leftType, Type rightType )
    {
        var leftCode = Type.GetTypeCode( leftType );
        var rightCode = Type.GetTypeCode( rightType );

        if ( leftCode == rightCode )
            return leftType;

        var leftIndex = Array.IndexOf( TypeCodePromotionOrder, leftCode );
        var rightIndex = Array.IndexOf( TypeCodePromotionOrder, rightCode );

        if ( leftIndex < 0 || rightIndex < 0 )
            return null;

        var commonTypeCode = TypeCodePromotionOrder[Math.Max( leftIndex, rightIndex )];

        return Type.GetType( "System." + commonTypeCode );
    }
}
