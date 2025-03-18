using System.Linq.Expressions;
using System.Numerics;
using Hyperbee.Expressions.Interpreter.Core;

namespace Hyperbee.Expressions.Interpreter.Evaluators;

internal sealed class UnaryEvaluator
{
    public static object Unary( InterpretContext context, UnaryExpression unary )
    {
        object operand = null;

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        // Rethrows can have a null / unvisited operand
        if ( unary.Operand != null )
            operand = context.Results.Pop();

        switch ( unary.NodeType )
        {
            case ExpressionType.Throw:
                return ThrowOperation( context, operand as Exception );

            case ExpressionType.Convert:
                return ConvertOperation( unary, operand );

            case ExpressionType.TypeAs:
                return operand is null || unary.Type.IsAssignableFrom( operand.GetType() ) ? operand : null;

            case ExpressionType.Not:
            case ExpressionType.IsFalse:
            case ExpressionType.IsTrue:
                return LogicalOperation( unary, (bool) operand );

            case ExpressionType.Negate:
            case ExpressionType.PreIncrementAssign:
            case ExpressionType.PreDecrementAssign:
            case ExpressionType.PostIncrementAssign:
            case ExpressionType.PostDecrementAssign:
                return NumericOperation( context, unary, operand );

            case ExpressionType.OnesComplement:
                return OnesComplement( unary, operand );

            default:
                throw new InterpreterException( $"Unsupported unary operation: {unary.NodeType}", unary );
        }
    }

    private static Exception ThrowOperation( InterpretContext context, Exception exception )
    {
        if ( context.Transition is TransitionException { Exception: not null } transitionException )
        {
            exception = transitionException.Exception;
        }

        context.Transition = new TransitionException( exception );
        context.TransitionChildIndex = 0;

        return exception;
    }

    private static object ConvertOperation( UnaryExpression unary, object operand )
    {
        if ( operand is not IConvertible convertible )
            throw new InterpreterException( $"Cannot convert {operand.GetType()} to {unary.Type}", unary );

        return Convert.ChangeType( convertible, unary.Type );
    }

    private static bool LogicalOperation( UnaryExpression unary, bool operand )
    {
        return unary.NodeType switch
        {
            ExpressionType.Not => !operand,
            ExpressionType.IsFalse => !operand,
            ExpressionType.IsTrue => operand,
            _ => throw new InterpreterException( $"Unsupported boolean unary operation: {unary.NodeType}", unary )
        };
    }

    private static object NumericOperation( InterpretContext context, UnaryExpression unary, object operand )
    {
        return operand switch
        {
            int intValue => NumericOperation( context.Scope, unary, intValue ),
            long longValue => NumericOperation( context.Scope, unary, longValue ),
            short shortValue => NumericOperation( context.Scope, unary, shortValue ),
            float floatValue => NumericOperation( context.Scope, unary, floatValue ),
            double doubleValue => NumericOperation( context.Scope, unary, doubleValue ),
            decimal decimalValue => NumericOperation( context.Scope, unary, decimalValue ),
            _ => throw new InterpreterException( $"Unsupported unary operation for type {operand.GetType()}", unary )
        };
    }

    private static object NumericOperation<T>( InterpretScope scope, UnaryExpression unary, T operand )
        where T : INumber<T>
    {
        if ( unary.NodeType == ExpressionType.Negate )
            return -operand;

        if ( unary.Operand is not ParameterExpression variable )
            throw new InterpreterException( "Unary target must be a variable.", unary );

        T newValue;
        switch ( unary.NodeType )
        {
            case ExpressionType.PreIncrementAssign:
                newValue = operand + T.One;
                scope[Collections.LinkedNode.Single, variable] = newValue;
                return newValue;

            case ExpressionType.PreDecrementAssign:
                newValue = operand - T.One;
                scope[Collections.LinkedNode.Single, variable] = newValue;
                return newValue;

            case ExpressionType.PostIncrementAssign:
                newValue = operand + T.One;
                scope[Collections.LinkedNode.Single, variable] = newValue;
                return operand;

            case ExpressionType.PostDecrementAssign:
                newValue = operand - T.One;
                scope[Collections.LinkedNode.Single, variable] = newValue;
                return operand;

            default:
                throw new InterpreterException( $"Unsupported numeric unary operation: {unary.NodeType}", unary );
        }
    }

    private static object OnesComplement( UnaryExpression unary, object operand )
    {
        return operand switch
        {
            int intValue => ~intValue,
            long longValue => ~longValue,
            short shortValue => ~shortValue,
            byte byteValue => (byte) ~byteValue,
            sbyte sbyteValue => (sbyte) ~sbyteValue,
            uint uintValue => ~uintValue,
            ulong ulongValue => ~ulongValue,
            ushort ushortValue => (ushort) ~ushortValue,
            _ => throw new InterpreterException( $"Unsupported type for OnesComplement: {operand.GetType()}", unary )
        };
    }
}
