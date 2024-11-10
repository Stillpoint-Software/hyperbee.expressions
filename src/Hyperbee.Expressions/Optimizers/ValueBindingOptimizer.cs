using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Hyperbee.Expressions.Optimizers;

// VariableBindingOptimizer: Variable, Constant, and Member Access Optimization
//
// This optimizer handles optimizations related to variables, constants, and member accesses,
// performing constant folding, variable inlining, and simplifying member access.

public class ValueBindingOptimizer : ExpressionVisitor, IExpressionOptimizer
{
    public Expression Optimize( Expression expression )
    {
        // Order of optimization is important, as some optimizations benefit from previous optimizations.
        expression = new VariableInliningVisitor().Visit( expression );
        expression = new ConstantFoldingVisitor().Visit( expression );
        expression = new MemberAccessVisitor().Visit( expression );

        return expression;
    }

    public TExpr Optimize<TExpr>( TExpr expression ) where TExpr : LambdaExpression
    {
        var optimizedBody = Optimize( expression.Body );
        return !ReferenceEquals( expression.Body, optimizedBody )
            ? (TExpr) Expression.Lambda( expression.Type, optimizedBody, expression.Parameters )
            : expression;
    }

    // ConstantFoldingVisitor: Arithmetic, Logical, and Conditional Constant Folding
    //
    // This visitor evaluates constant expressions at compile-time, replacing them with their computed result.
    // It simplifies arithmetic, boolean, string, and conditional expressions when all operands are constants.
    //
    // Examples:
    //
    // Before:
    //   .Add(.Constant(3), .Constant(5))
    //
    // After:
    //   .Constant(8)
    //
    // Before:
    //   .IfThenElse(.Constant(true), .Constant("True Branch"), .Constant("False Branch"))
    //
    // After:
    //   .Constant("True Branch")
    //
    // Before:
    //   .Negate(.Constant(5))
    //
    // After:
    //   .Constant(-5)
    //
    private class ConstantFoldingVisitor : ExpressionVisitor
    {
        protected override Expression VisitBinary( BinaryExpression node )
        {
            if ( node.Left is not ConstantExpression leftConst || node.Right is not ConstantExpression rightConst )
            {
                return base.VisitBinary( node );
            }

            var folded = FoldConstants( node.NodeType, leftConst, rightConst );
            return folded ?? base.VisitBinary( node );
        }

        protected override Expression VisitUnary( UnaryExpression node )
        {
            if ( node.Operand is ConstantExpression constOperand )
            {
                return FoldUnaryOperation( node.NodeType, constOperand ) ?? base.VisitUnary( node );
            }

            return base.VisitUnary( node );
        }

        protected override Expression VisitConditional( ConditionalExpression node )
        {
            var test = Visit( node.Test );

            if ( test is not ConstantExpression testConst )
            {
                return base.VisitConditional( node );
            }

            var condition = (bool) testConst.Value!;
            var result = condition ? node.IfTrue : node.IfFalse;
            return Visit( result );

        }

        private static ConstantExpression FoldUnaryOperation( ExpressionType nodeType, ConstantExpression operand )
        {
            // Unary folding: Supports negation, logical NOT, and bitwise NOT for constants.
            //
            // Examples:
            //
            // Before:
            //   .Negate(.Constant(5))
            //
            // After:
            //   .Constant(-5)
            //
            return nodeType switch
            {
                ExpressionType.Negate when operand.Value is int intValue => Expression.Constant( -intValue ),
                ExpressionType.Not when operand.Value is bool boolValue => Expression.Constant( !boolValue ),
                ExpressionType.OnesComplement when operand.Value is int intValue => Expression.Constant( ~intValue ),
                _ => null
            };
        }

        private static ConstantExpression FoldConstants( ExpressionType nodeType, ConstantExpression leftConst, ConstantExpression rightConst )
        {
            if ( !typeof( IConvertible ).IsAssignableFrom( leftConst.Type ) || !typeof( IConvertible ).IsAssignableFrom( rightConst.Type ) )
                return null;

            // Arithmetic and logical operations on constants
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
                not null when type == typeof( bool ) => FoldBooleanOperation( nodeType, (bool) leftConst.Value!, (bool) rightConst.Value! ),
                not null when type == typeof( string ) => FoldStringOperation( nodeType, (string) leftConst.Value!, (string) rightConst.Value! ),
                not null when type == typeof( char ) => FoldCharOperation( nodeType, (char) leftConst.Value!, (char) rightConst.Value! ),
                not null when type == typeof( DateTime ) => FoldDateTimeOperation( nodeType, (DateTime) leftConst.Value!, rightConst.Value ),
                _ => null
            };
        }

        private static ConstantExpression FoldBooleanOperation( ExpressionType nodeType, bool left, bool right )
        {
            // Boolean folding: Simplifies operations like AND, OR, and comparisons.
            //
            // Examples:
            //
            // Before:
            //   .And(.Constant(true), .Constant(false))
            //
            // After:
            //   .Constant(false)
            //
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
            // Char folding: Supports addition and subtraction on char constants.
            //
            // Examples:
            //
            // Before:
            //   .Add(.Constant('A'), .Constant(1))
            //
            // After:
            //   .Constant('B')
            //
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
            // DateTime folding: Supports addition and subtraction of TimeSpan to DateTime.
            //
            // Examples:
            //
            // Before:
            //   .Add(.Constant(new DateTime(2024, 1, 1)), .Constant(TimeSpan.FromDays(1)))
            //
            // After:
            //   .Constant(new DateTime(2024, 1, 2))
            //
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
            // String folding: Concatenates strings for `Add` operation.
            //
            // Examples:
            //
            // Before:
            //   .Add(.Constant("Hello, "), .Constant("World!"))
            //
            // After:
            //   .Constant("Hello, World!")
            //
            return nodeType != ExpressionType.Add ? null : Expression.Constant( left + right );
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

            return commonType switch
            {
                not null when commonType == typeof( byte ) => ApplyOperation( nodeType, (byte) leftValue, (byte) rightValue ),
                not null when commonType == typeof( sbyte ) => ApplyOperation( nodeType, (sbyte) leftValue, (sbyte) rightValue ),
                not null when commonType == typeof( short ) => ApplyOperation( nodeType, (short) leftValue, (short) rightValue ),
                not null when commonType == typeof( ushort ) => ApplyOperation( nodeType, (ushort) leftValue, (ushort) rightValue ),
                not null when commonType == typeof( int ) => ApplyOperation( nodeType, (int) leftValue, (int) rightValue ),
                not null when commonType == typeof( uint ) => ApplyOperation( nodeType, (uint) leftValue, (uint) rightValue ),
                not null when commonType == typeof( long ) => ApplyOperation( nodeType, (long) leftValue, (long) rightValue ),
                not null when commonType == typeof( ulong ) => ApplyOperation( nodeType, (ulong) leftValue, (ulong) rightValue ),
                not null when commonType == typeof( float ) => ApplyOperation( nodeType, (float) leftValue, (float) rightValue ),
                not null when commonType == typeof( double ) => ApplyOperation( nodeType, (double) leftValue, (double) rightValue ),
                not null when commonType == typeof( decimal ) => ApplyOperation( nodeType, (decimal) leftValue, (decimal) rightValue ),
                _ => throw new InvalidOperationException( "Unsupported type for promoted operation." )
            };

            static ConstantExpression ApplyOperation<T>( ExpressionType nodeType, T left, T right ) where T : INumber<T>
            {
                var constant = nodeType switch
                {
                    ExpressionType.Add => left + right,
                    ExpressionType.Subtract => left - right,
                    ExpressionType.Multiply => left * right,
                    ExpressionType.Divide => Divide( left, right ),
                    _ => throw new InvalidOperationException( $"Unsupported operation {nodeType} for type {typeof( T )}." )
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

    // VariableInliningVisitor: Variable Inlining and Elimination
    //
    // This visitor inlines variables where possible, removing unused variables and simplifying
    // assignments when the variable is never accessed later in the code. It replaces variable references
    // with their assigned values to simplify expressions.
    //
    // Before:
    //   .Block(
    //       .Assign(.Parameter(x), .Constant(5)),
    //       .Add(.Parameter(x), .Parameter(x))
    //   )
    //
    // After:
    //   .Add(.Constant(5), .Constant(5))
    //
    private class VariableInliningVisitor : ExpressionVisitor
    {
        private readonly Dictionary<ParameterExpression, Expression> _replacements = new();

        protected override Expression VisitBlock( BlockExpression node )
        {
            var variables = new List<ParameterExpression>();
            var expressions = new List<Expression>();
            var variableAssignments = new Dictionary<ParameterExpression, Expression>();
            var variableUsageCount = new Dictionary<ParameterExpression, int>();

            for ( var index = 0; index < node.Expressions.Count; index++ )
            {
                var expr = node.Expressions[index];
                if ( expr is not BinaryExpression binaryExpr || binaryExpr.NodeType != ExpressionType.Assign || binaryExpr.Left is not ParameterExpression variable )
                {
                    continue;
                }

                variableUsageCount.TryAdd( variable, 0 );
                variableUsageCount[variable]++;
                variableAssignments[variable] = binaryExpr.Right;
            }

            for ( var index = 0; index < node.Expressions.Count; index++ )
            {
                var expr = node.Expressions[index];
                if ( expr is not BinaryExpression binaryExpr || binaryExpr.NodeType != ExpressionType.Assign || binaryExpr.Left is not ParameterExpression variable )
                {
                    expressions.Add( Visit( expr ) );
                    continue;
                }

                if ( variableUsageCount[variable] != 1 )
                {
                    if ( variableAssignments[variable] == binaryExpr.Right )
                    {
                        continue;
                    }

                    variables.Add( variable );
                    expressions.Add( expr );
                }
                else
                {
                    _replacements[variable] = binaryExpr.Right;
                    expressions.Add( Visit( binaryExpr.Right ) );
                }
            }

            return Expression.Block( variables, expressions );
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            return _replacements.TryGetValue( node, out var replacement ) ? Visit( replacement ) : base.VisitParameter( node );
        }
    }

    // MemberAccessVisitor: Constant Field and Property Access Simplification
    //
    // This visitor simplifies member access by precomputing values for constant fields or
    // properties. When a field or property is accessed through a constant object, it evaluates
    // the expression and replaces it with the value.
    //
    // Before:
    //   .MemberAccess(.Constant(obj), Property)
    //
    // After:
    //   .Constant(value)
    //
    private class MemberAccessVisitor : ExpressionVisitor
    {
        protected override Expression VisitMember( MemberExpression node )
        {
            node = (MemberExpression) base.VisitMember( node );

            if ( node.Expression is not ConstantExpression constExpr )
            {
                return node;
            }

            switch ( node.Member )
            {
                case FieldInfo fieldInfo:
                    var value = fieldInfo.GetValue( constExpr.Value );
                    return Expression.Constant( value, node.Type );

                case PropertyInfo propertyInfo when propertyInfo.CanRead && propertyInfo.GetMethod!.IsStatic:
                    var staticValue = propertyInfo.GetValue( null );
                    return Expression.Constant( staticValue, node.Type );

                default:
                    return node;
            }
        }

        protected override Expression VisitBinary( BinaryExpression node )
        {
            if ( node.NodeType != ExpressionType.Coalesce || node.Left is not ConstantExpression leftConst || leftConst.Value != null )
            {
                return base.VisitBinary( node );
            }

            return Visit( node.Right );
        }

        protected override Expression VisitIndex( IndexExpression node )
        {
            if ( node.Object is not ConstantExpression constantArray || constantArray.Value is not Array array || node.Arguments[0] is not ConstantExpression indexExpr )
            {
                return base.VisitIndex( node );
            }

            var index = (int) indexExpr.Value!;
            return Expression.Constant( array.GetValue( index ), node.Type );
        }
    }
}
