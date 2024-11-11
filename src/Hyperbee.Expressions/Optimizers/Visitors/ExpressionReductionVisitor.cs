using System.Linq.Expressions;

namespace Hyperbee.Expressions.Optimizers.Visitors;

// ExpressionReductionOptimizer: Arithmetic and Logical Reduction
//
// This visitor eliminates trivial arithmetic expressions (like adding zero, multiplying by one),
// simplifies logical identities (like x && true or x || false), and flattens nested expressions
// where applicable.
//
// Examples:
//
// Before:
//   .Add(.Parameter(x), .Constant(0))
//
// After:
//   .Parameter(x)
//
// Before:
//   .Multiply(.Parameter(x), .Constant(1))
//
// After:
//   .Parameter(x)
//
// Before:
//   .Add(.Add(.Parameter(x), .Parameter(y)), .Parameter(z))
//
// After:
//   .Add(.Parameter(x), .Parameter(y), .Parameter(z))
//
public class ExpressionReductionVisitor : ExpressionVisitor, IExpressionTransformer
{
    public Expression Transform( Expression expression )
    {
        return Visit( expression );
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        // Recursively visit left and right nodes to ensure optimizations
        // are applied in subexpressions first

        var left = Visit( node.Left );
        var right = Visit( node.Right );

        // Apply trivial arithmetic and logical simplifications

        switch ( node.NodeType )
        {
            // Arithmetic simplifications
            case ExpressionType.Add:
            case ExpressionType.Subtract:
                {
                    if ( right is ConstantExpression rightConst && rightConst.Value is int rightVal )
                    {
                        // x + 0 or x - 0
                        if ( rightVal == 0 )
                            return left;
                    }

                    if ( node.NodeType == ExpressionType.Add && left is ConstantExpression leftConst && leftConst.Value is int leftValue )
                    {
                        // 0 + x
                        if ( leftValue == 0 )
                            return right;
                    }

                    break;
                }

            case ExpressionType.Multiply:
                {
                    if ( right is ConstantExpression rightConst && rightConst.Value is int rightValue )
                    {
                        // x * 1
                        if ( rightValue == 1 )
                            return left;

                        // x * 0
                        if ( rightValue == 0 )
                            return Expression.Constant( 0 );
                    }

                    if ( left is ConstantExpression leftConst && leftConst.Value is int leftValue )
                    {
                        // 1 * x
                        if ( leftValue == 1 )
                            return right;

                        // 0 * x
                        if ( leftValue == 0 )
                            return Expression.Constant( 0 );
                    }

                    break;
                }

            // Logical short-circuiting for `&& true`, `|| false`, etc.
            case ExpressionType.AndAlso:
                {
                    // x && true or true && x
                    if ( right is ConstantExpression rightConst && rightConst.Value is bool rightValue && rightValue )
                        return left;

                    if ( left is ConstantExpression leftConst && leftConst.Value is bool leftValue && leftValue )
                        return right;

                    // false && x or x && false
                    if ( left is ConstantExpression lConstAndFalse && lConstAndFalse.Value is bool leftValAndFalse && !leftValAndFalse ||
                         right is ConstantExpression rConstAndFalse && rConstAndFalse.Value is bool rightValAndFalse && !rightValAndFalse )
                        return Expression.Constant( false );

                    break;
                }

            case ExpressionType.OrElse:
                {
                    // x || false or false || x
                    if ( right is ConstantExpression rightConst && rightConst.Value is bool rightValue && !rightValue )
                        return left;

                    if ( left is ConstantExpression leftConst && leftConst.Value is bool leftValue && !leftValue )
                        return right;

                    // true || x or x || true
                    if ( left is ConstantExpression lConstOrTrue && lConstOrTrue.Value is bool leftValOrTrue && leftValOrTrue ||
                         right is ConstantExpression rConstOrTrue && rConstOrTrue.Value is bool rightValOrTrue && rightValOrTrue )
                        return Expression.Constant( true );

                    break;
                }
        }

        if ( node.NodeType != ExpressionType.Add && node.NodeType != ExpressionType.Multiply )
        {
            return node.Update( left, node.Conversion, right );
        }

        // Handle nested expression flattening specifically for `Add` and `Multiply`

        // Collect terms for the node and recursively flatten if necessary
        var allTerms = FlattenBinaryExpression( node, node.NodeType ).ToList();

        // Aggregate constants in the list (e.g., sum all constants for `Add`)
        var constants = allTerms.OfType<ConstantExpression>().ToList();
        var otherTerms = allTerms.Except( constants ).ToList();

        if ( constants.Count <= 0 )
        {
            return otherTerms.Aggregate( ( acc, term ) => Expression.MakeBinary( node.NodeType, acc, term ) );
        }

        // Sum or multiply the constant values based on node type
        var constantResult = node.NodeType switch
        {
            ExpressionType.Add => constants.Sum( c => (int) c.Value! ),
            _ => constants.Aggregate( 1, ( acc, c ) => acc * (int) c.Value! )
        };

        otherTerms.Insert( 0, Expression.Constant( constantResult ) );

        return otherTerms.Aggregate( ( acc, term ) => Expression.MakeBinary( node.NodeType, acc, term ) );
    }

    private static IEnumerable<Expression> FlattenBinaryExpression( Expression expr, ExpressionType type )
    {
        if ( expr is BinaryExpression binary && binary.NodeType == type )
        {
            return FlattenBinaryExpression( binary.Left, type ).Concat( FlattenBinaryExpression( binary.Right, type ) );
        }

        return [expr];
    }
}
