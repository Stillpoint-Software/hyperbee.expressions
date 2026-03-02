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
    {
        return (TDelegate) Compile( (LambdaExpression) lambda );
    }

    /// <summary>Compiles the expression. Throws on unsupported patterns.</summary>
    public static Delegate Compile( LambdaExpression lambda )
    {
        // Fast-path: skip capture scanning when no nested lambdas exist (common case)
        var capturedVariables = ContainsNestedLambda( lambda.Body )
            ? CaptureScanner.FindCapturedVariables( lambda )
            : null;

        var ir = LowerToIR( lambda, capturedVariables, out var needsConstantsArray );

        TransformIR( ir, lambda.ReturnType == typeof( void ) );

        return EmitDelegate( ir, lambda, needsConstantsArray );
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
    {
        return (TDelegate) CompileWithFallback( (LambdaExpression) lambda );
    }

    /// <summary>Compiles the expression. Falls back to system compiler on failure.</summary>
    public static Delegate CompileWithFallback( LambdaExpression lambda )
    {
        return TryCompile( lambda ) ?? lambda.Compile();
    }

    // --- Compilation steps ---

    private static IRBuilder LowerToIR(
        LambdaExpression lambda,
        HashSet<ParameterExpression>? capturedVariables,
        out bool needsConstantsArray )
    {
        needsConstantsArray = ScanForNonEmbeddableConstants( lambda.Body )
            || ( capturedVariables != null && capturedVariables.Count > 0 );

        var ir = new IRBuilder();
        var lowerer = new ExpressionLowerer( ir, capturedVariables );
        var argOffset = needsConstantsArray ? 1 : 0;

        lowerer.Lower( lambda, argOffset );

        return ir;
    }

    private static void TransformIR( IRBuilder ir, bool isVoidReturn )
    {
        StackSpillPass.Run( ir );  // Handle stack spilling for complex expressions and try/catch blocks
        PeepholePass.Run( ir );    // Remove redundant instructions
        DeadCodePass.Run( ir );    // Remove unreachable instructions after terminators
        IRValidator.Validate( ir, isVoidReturn ); // Structural validation (DEBUG only, zero cost in Release)
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

    // --- Private helpers ---

    /// <summary>
    /// Quick check: does the expression tree contain any nested LambdaExpression?
    /// If not, there can be no captured variables and CaptureScanner can be skipped.
    /// </summary>
    private static bool ContainsNestedLambda( Expression? node )
    {
        if ( node == null )
            return false;

        switch ( node )
        {
            case LambdaExpression:
                return true;

            case BinaryExpression b:
                return ContainsNestedLambda( b.Left ) || ContainsNestedLambda( b.Right );

            case UnaryExpression u:
                return ContainsNestedLambda( u.Operand );

            case ConditionalExpression c:
                return ContainsNestedLambda( c.Test )
                    || ContainsNestedLambda( c.IfTrue )
                    || ContainsNestedLambda( c.IfFalse );

            case MethodCallExpression m:
            {
                if ( ContainsNestedLambda( m.Object ) )
                    return true;
                foreach ( var arg in m.Arguments )
                    if ( ContainsNestedLambda( arg ) )
                        return true;
                return false;
            }

            case BlockExpression b:
            {
                foreach ( var expr in b.Expressions )
                    if ( ContainsNestedLambda( expr ) )
                        return true;
                return false;
            }

            case InvocationExpression inv:
            {
                if ( ContainsNestedLambda( inv.Expression ) )
                    return true;
                foreach ( var arg in inv.Arguments )
                    if ( ContainsNestedLambda( arg ) )
                        return true;
                return false;
            }

            case MemberExpression m:
                return ContainsNestedLambda( m.Expression );

            case NewExpression n:
            {
                foreach ( var arg in n.Arguments )
                    if ( ContainsNestedLambda( arg ) )
                        return true;
                return false;
            }

            case TryExpression t:
            {
                if ( ContainsNestedLambda( t.Body ) )
                    return true;
                foreach ( var h in t.Handlers )
                    if ( ContainsNestedLambda( h.Body ) || ContainsNestedLambda( h.Filter ) )
                        return true;
                return ContainsNestedLambda( t.Finally ) || ContainsNestedLambda( t.Fault );
            }

            case LoopExpression l:
                return ContainsNestedLambda( l.Body );

            case SwitchExpression s:
            {
                if ( ContainsNestedLambda( s.SwitchValue ) )
                    return true;
                foreach ( var c in s.Cases )
                    if ( ContainsNestedLambda( c.Body ) )
                        return true;
                return ContainsNestedLambda( s.DefaultBody );
            }

            case GotoExpression g:
                return ContainsNestedLambda( g.Value );

            case LabelExpression l:
                return ContainsNestedLambda( l.DefaultValue );

            case TypeBinaryExpression t:
                return ContainsNestedLambda( t.Expression );

            default:
                return false;
        }
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

            case LambdaExpression:
                // Nested lambda -- always needs constants array (delegate is non-embeddable)
                return true;

            case InvocationExpression invocation:
            {
                if ( ScanForNonEmbeddableConstants( invocation.Expression ) )
                    return true;
                foreach ( var arg in invocation.Arguments )
                {
                    if ( ScanForNonEmbeddableConstants( arg ) )
                        return true;
                }
                return false;
            }

            case LoopExpression loop:
                return ScanForNonEmbeddableConstants( loop.Body );

            case SwitchExpression switchExpr:
            {
                if ( ScanForNonEmbeddableConstants( switchExpr.SwitchValue ) )
                    return true;
                foreach ( var switchCase in switchExpr.Cases )
                {
                    foreach ( var testValue in switchCase.TestValues )
                    {
                        if ( ScanForNonEmbeddableConstants( testValue ) )
                            return true;
                    }
                    if ( ScanForNonEmbeddableConstants( switchCase.Body ) )
                        return true;
                }
                if ( switchExpr.DefaultBody != null && ScanForNonEmbeddableConstants( switchExpr.DefaultBody ) )
                    return true;
                return false;
            }

            case IndexExpression indexExpr:
            {
                if ( indexExpr.Object != null && ScanForNonEmbeddableConstants( indexExpr.Object ) )
                    return true;
                foreach ( var arg in indexExpr.Arguments )
                {
                    if ( ScanForNonEmbeddableConstants( arg ) )
                        return true;
                }
                return false;
            }

            case ListInitExpression listInit:
            {
                foreach ( var arg in listInit.NewExpression.Arguments )
                {
                    if ( ScanForNonEmbeddableConstants( arg ) )
                        return true;
                }
                foreach ( var init in listInit.Initializers )
                {
                    foreach ( var arg in init.Arguments )
                    {
                        if ( ScanForNonEmbeddableConstants( arg ) )
                            return true;
                    }
                }
                return false;
            }

            case MemberInitExpression memberInit:
            {
                foreach ( var arg in memberInit.NewExpression.Arguments )
                {
                    if ( ScanForNonEmbeddableConstants( arg ) )
                        return true;
                }
                return ScanMemberBindings( memberInit.Bindings );
            }

            case NewArrayExpression newArray:
            {
                foreach ( var expr in newArray.Expressions )
                {
                    if ( ScanForNonEmbeddableConstants( expr ) )
                        return true;
                }
                return false;
            }

            default:
                return false;
        }
    }

    private static bool ScanMemberBindings( IEnumerable<MemberBinding> bindings )
    {
        foreach ( var binding in bindings )
        {
            switch ( binding )
            {
                case MemberAssignment assignment:
                    if ( ScanForNonEmbeddableConstants( assignment.Expression ) )
                        return true;
                    break;
                case MemberListBinding listBinding:
                    foreach ( var init in listBinding.Initializers )
                    {
                        foreach ( var arg in init.Arguments )
                        {
                            if ( ScanForNonEmbeddableConstants( arg ) )
                                return true;
                        }
                    }
                    break;
                case MemberMemberBinding memberBinding:
                    if ( ScanMemberBindings( memberBinding.Bindings ) )
                        return true;
                    break;
            }
        }
        return false;
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

        // Build a set of operand indices referenced by LoadConst instructions
        // in a single pass, avoiding O(operands * instructions) scan.
        var operandCount = ir.Operands.Count;
        var loadConstOperands = new HashSet<int>( operandCount );
        foreach ( var inst in ir.Instructions )
        {
            if ( inst.Op == IROp.LoadConst )
                loadConstOperands.Add( inst.Operand );
        }

        constantIndices = new Dictionary<int, int>( operandCount );
        var constants = new List<object>( operandCount );

        for ( var i = 0; i < ir.Operands.Count; i++ )
        {
            if ( loadConstOperands.Contains( i ) && !IsEmbeddable( ir.Operands[i] ) )
            {
                constantIndices[i] = constants.Count;
                constants.Add( ir.Operands[i] );
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
