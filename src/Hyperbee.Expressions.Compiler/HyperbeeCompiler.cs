using System.Linq.Expressions;
using System.Reflection.Emit;
using Hyperbee.Expressions.Compiler.Emission;
using Hyperbee.Expressions.Compiler.IR;
using Hyperbee.Expressions.Compiler.Lowering;

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
        // Step 1: Create IR builder and lower expression tree
        var ir = new IRBuilder();
        var lowerer = new ExpressionLowerer( ir );

        // Step 2: Scan for non-embeddable constants need
        // We do a pre-scan by lowering first, then checking operands
        // But we need to know argOffset before lowering.
        // Solution: lower with argOffset=0 tentatively, then check if we need constants.
        // Actually, we need to lower twice or be smarter.
        // Better approach: do a quick pre-scan of the expression tree for non-embeddable constants.

        var needsConstantsArray = NeedsConstantsArray( lambda.Body );
        var argOffset = needsConstantsArray ? 1 : 0;

        lowerer.Lower( lambda, argOffset );

        // Step 3: Build the constants array and index mapping
        Dictionary<int, int>? constantIndices = null;
        object[]? constantsArray = null;

        if ( needsConstantsArray )
        {
            BuildConstantsMapping( ir, out constantIndices, out constantsArray );
        }

        // Step 4: Build DynamicMethod parameter types
        var paramTypes = BuildParameterTypes( lambda, needsConstantsArray );

        // Step 5: Create DynamicMethod
        var method = new DynamicMethod(
            string.Empty,
            lambda.ReturnType,
            paramTypes,
            typeof( HyperbeeCompiler ),
            skipVisibility: true );

        // Step 6: Emit IL from IR
        ILEmissionPass.Run( ir, method.GetILGenerator(), needsConstantsArray, constantIndices );

        // Step 7: Create delegate
        if ( needsConstantsArray )
        {
            return method.CreateDelegate( lambda.Type, constantsArray );
        }

        return method.CreateDelegate( lambda.Type );
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

    /// <summary>
    /// Pre-scan the expression tree for any constants that cannot be embedded directly in IL.
    /// </summary>
    private static bool NeedsConstantsArray( Expression body )
    {
        return ScanForNonEmbeddableConstants( body );
    }

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
        out Dictionary<int, int> constantIndices,
        out object[] constantsArray )
    {
        constantIndices = new Dictionary<int, int>();
        var constants = new List<object>();

        for ( var i = 0; i < ir.Operands.Count; i++ )
        {
            var operand = ir.Operands[i];

            // Only consider operands that are referenced by LoadConst instructions
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
