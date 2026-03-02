using System.Linq.Expressions;
using System.Reflection.Emit;
using Hyperbee.Expressions.Compiler.Emission;
using Hyperbee.Expressions.Compiler.IR;
using Hyperbee.Expressions.Compiler.Lowering;
using Hyperbee.Expressions.Compiler.Passes;

namespace Hyperbee.Expressions.Compiler;

/// <summary>
/// High-performance IR-based expression compiler.
/// Drop-in replacement for Expression.Compile().
/// </summary>
public static class HyperbeeCompiler
{
    /// <summary>Compiles the expression. Throws on unsupported patterns.</summary>
    public static TDelegate Compile<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate
        => (TDelegate) Compile( (LambdaExpression) lambda );

    /// <summary>Compiles the expression. Throws on unsupported patterns.</summary>
    public static Delegate Compile( LambdaExpression lambda )
    {
        var ir = LowerToIR( lambda, out var needsConstantsArray );

        TransformIR( ir );

        return EmitDelegate( ir, lambda, needsConstantsArray );
    }

    private static IRBuilder LowerToIR( LambdaExpression lambda, out bool needsConstantsArray )
    {
        needsConstantsArray = ScanForNonEmbeddableConstants( lambda.Body );

        var ir = new IRBuilder();
        var lowerer = new ExpressionLowerer( ir );
        var argOffset = needsConstantsArray ? 1 : 0;

        lowerer.Lower( lambda, argOffset );

        return ir;
    }

    private static void TransformIR( IRBuilder ir )
    {
        StackSpillPass.Run( ir );
    }

    private static Delegate EmitDelegate( IRBuilder ir, LambdaExpression lambda, bool needsConstantsArray )
    {
        BuildConstantsMapping( ir, needsConstantsArray, out var constantIndices, out var constantsArray );

        var paramTypes = BuildParameterTypes( lambda, needsConstantsArray );

        var method = new DynamicMethod(
            string.Empty,
            lambda.ReturnType,
            paramTypes,
            typeof( HyperbeeCompiler ),
            skipVisibility: true );

        ILEmissionPass.Run( ir, method.GetILGenerator(), needsConstantsArray, constantIndices );

        return needsConstantsArray
            ? method.CreateDelegate( lambda.Type, constantsArray )
            : method.CreateDelegate( lambda.Type );
    }

    /// <summary>Compiles the expression. Returns null on unsupported patterns.</summary>
    public static TDelegate? TryCompile<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate
    {
        try
        {
            return Compile( lambda );
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Compiles the expression. Returns null on unsupported patterns.</summary>
    public static Delegate? TryCompile( LambdaExpression lambda )
    {
        try
        {
            return Compile( lambda );
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Compiles the expression. Falls back to system compiler on failure.</summary>
    public static TDelegate CompileWithFallback<TDelegate>( Expression<TDelegate> lambda )
        where TDelegate : Delegate
        => (TDelegate) CompileWithFallback( (LambdaExpression) lambda );

    /// <summary>Compiles the expression. Falls back to system compiler on failure.</summary>
    public static Delegate CompileWithFallback( LambdaExpression lambda )
        => TryCompile( lambda ) ?? lambda.Compile();

    // --- Private helpers ---

    private static bool ScanForNonEmbeddableConstants( Expression node )
    {
        if ( node == null )
            return false;

        if ( node is ConstantExpression constant && constant.Value != null )
        {
            if ( !IsEmbeddable( constant.Value ) )
                return true;
        }

        // Recursively scan children
        switch ( node )
        {
            case BinaryExpression binary:
                return ScanForNonEmbeddableConstants( binary.Left )
                    || ScanForNonEmbeddableConstants( binary.Right );

            case UnaryExpression unary:
                return ScanForNonEmbeddableConstants( unary.Operand );

            case ConditionalExpression conditional:
                return ScanForNonEmbeddableConstants( conditional.Test )
                    || ScanForNonEmbeddableConstants( conditional.IfTrue )
                    || ScanForNonEmbeddableConstants( conditional.IfFalse );

            case MethodCallExpression methodCall:
            {
                if ( methodCall.Object != null && ScanForNonEmbeddableConstants( methodCall.Object ) )
                    return true;
                foreach ( var arg in methodCall.Arguments )
                {
                    if ( ScanForNonEmbeddableConstants( arg ) )
                        return true;
                }
                return false;
            }

            case MemberExpression member:
                return member.Expression != null && ScanForNonEmbeddableConstants( member.Expression );

            case NewExpression newExpr:
            {
                foreach ( var arg in newExpr.Arguments )
                {
                    if ( ScanForNonEmbeddableConstants( arg ) )
                        return true;
                }
                return false;
            }

            case BlockExpression block:
            {
                foreach ( var expr in block.Expressions )
                {
                    if ( ScanForNonEmbeddableConstants( expr ) )
                        return true;
                }
                return false;
            }

            case TypeBinaryExpression typeBinary:
                return ScanForNonEmbeddableConstants( typeBinary.Expression );

            case TryExpression tryExpr:
            {
                if ( ScanForNonEmbeddableConstants( tryExpr.Body ) )
                    return true;
                foreach ( var handler in tryExpr.Handlers )
                {
                    if ( handler.Filter != null && ScanForNonEmbeddableConstants( handler.Filter ) )
                        return true;
                    if ( ScanForNonEmbeddableConstants( handler.Body ) )
                        return true;
                }
                if ( tryExpr.Finally != null && ScanForNonEmbeddableConstants( tryExpr.Finally ) )
                    return true;
                if ( tryExpr.Fault != null && ScanForNonEmbeddableConstants( tryExpr.Fault ) )
                    return true;
                return false;
            }

            case GotoExpression gotoExpr:
                return gotoExpr.Value != null && ScanForNonEmbeddableConstants( gotoExpr.Value );

            case LabelExpression labelExpr:
                return labelExpr.DefaultValue != null && ScanForNonEmbeddableConstants( labelExpr.DefaultValue );

            default:
                return false;
        }
    }

    /// <summary>
    /// Returns true if the constant value can be embedded directly in IL.
    /// </summary>
    private static bool IsEmbeddable( object value )
    {
        return value is int or long or float or double or string or bool
            or byte or sbyte or short or ushort or char or uint or ulong;
    }

    /// <summary>
    /// Build the mapping from operand-table indices to constants-array indices
    /// for non-embeddable constants.
    /// </summary>
    private static void BuildConstantsMapping(
        IRBuilder ir,
        bool needsConstantsArray,
        out Dictionary<int, int>? constantIndices,
        out object[]? constantsArray )
    {
        if ( !needsConstantsArray )
        {
            constantIndices = null;
            constantsArray = null;
            return;
        }

        constantIndices = new Dictionary<int, int>();
        var constants = new List<object>();

        for ( var i = 0; i < ir.Operands.Count; i++ )
        {
            var operand = ir.Operands[i];

            var isConstant = false;
            foreach ( var inst in ir.Instructions )
            {
                if ( inst.Op == IROp.LoadConst && inst.Operand == i )
                {
                    isConstant = true;
                    break;
                }
            }

            if ( isConstant && !IsEmbeddable( operand ) )
            {
                constantIndices[i] = constants.Count;
                constants.Add( operand );
            }
        }

        constantsArray = constants.ToArray();
    }

    /// <summary>
    /// Build the parameter types array for the DynamicMethod.
    /// </summary>
    private static Type[] BuildParameterTypes( LambdaExpression lambda, bool hasConstantsArray )
    {
        var offset = hasConstantsArray ? 1 : 0;
        var types = new Type[lambda.Parameters.Count + offset];

        if ( hasConstantsArray )
        {
            types[0] = typeof( object[] );
        }

        for ( var i = 0; i < lambda.Parameters.Count; i++ )
        {
            var p = lambda.Parameters[i];
            types[i + offset] = p.IsByRef ? p.Type.MakeByRefType() : p.Type;
        }

        return types;
    }
}
