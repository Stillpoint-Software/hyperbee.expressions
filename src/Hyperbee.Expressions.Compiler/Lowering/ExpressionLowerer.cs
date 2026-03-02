using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.Expressions.Compiler.IR;

namespace Hyperbee.Expressions.Compiler.Lowering;

/// <summary>
/// Lowers a System.Linq.Expressions expression tree into flat IR instructions.
/// Single traversal. Handles all Phase 1 node types.
/// </summary>
public class ExpressionLowerer
{
    private readonly IRBuilder _ir;
    private readonly Dictionary<ParameterExpression, int> _parameterMap = new();
    private readonly Dictionary<ParameterExpression, int> _localMap = new();
    private readonly Dictionary<LabelTarget, int> _labelMap = new();
    private readonly Dictionary<LabelTarget, int> _labelValueLocalMap = new();
    private int _argOffset;

    /// <summary>
    /// Creates a new expression lowerer targeting the given IR builder.
    /// </summary>
    public ExpressionLowerer( IRBuilder ir )
    {
        _ir = ir;
    }

    /// <summary>
    /// Lower a lambda expression into the IR builder.
    /// </summary>
    public void Lower( LambdaExpression lambda, int argOffset )
    {
        _argOffset = argOffset;

        for ( var i = 0; i < lambda.Parameters.Count; i++ )
        {
            _parameterMap[lambda.Parameters[i]] = i + _argOffset;
        }

        LowerExpression( lambda.Body );
        _ir.Emit( IROp.Ret );
    }

    private void LowerExpression( Expression node )
    {
        if ( node == null )
            return;

        switch ( node.NodeType )
        {
            case ExpressionType.Constant:
                LowerConstant( (ConstantExpression) node );
                break;

            case ExpressionType.Parameter:
                LowerParameter( (ParameterExpression) node );
                break;

            // Binary arithmetic
            case ExpressionType.Add:
            case ExpressionType.AddChecked:
            case ExpressionType.Subtract:
            case ExpressionType.SubtractChecked:
            case ExpressionType.Multiply:
            case ExpressionType.MultiplyChecked:
            case ExpressionType.Divide:
            case ExpressionType.Modulo:
            // Binary bitwise
            case ExpressionType.And:
            case ExpressionType.Or:
            case ExpressionType.ExclusiveOr:
            case ExpressionType.LeftShift:
            case ExpressionType.RightShift:
            // Binary comparison
            case ExpressionType.Equal:
            case ExpressionType.NotEqual:
            case ExpressionType.LessThan:
            case ExpressionType.GreaterThan:
            case ExpressionType.LessThanOrEqual:
            case ExpressionType.GreaterThanOrEqual:
                LowerBinary( (BinaryExpression) node );
                break;

            // Short-circuit logical
            case ExpressionType.AndAlso:
                LowerAndAlso( (BinaryExpression) node );
                break;

            case ExpressionType.OrElse:
                LowerOrElse( (BinaryExpression) node );
                break;

            // Unary
            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
            case ExpressionType.Not:
            case ExpressionType.UnaryPlus:
                LowerUnary( (UnaryExpression) node );
                break;

            // Conversions
            case ExpressionType.Convert:
            case ExpressionType.ConvertChecked:
                LowerConvert( (UnaryExpression) node );
                break;

            case ExpressionType.TypeAs:
                LowerTypeAs( (UnaryExpression) node );
                break;

            case ExpressionType.TypeIs:
                LowerTypeIs( (TypeBinaryExpression) node );
                break;

            // Method call
            case ExpressionType.Call:
                LowerMethodCall( (MethodCallExpression) node );
                break;

            // Conditional
            case ExpressionType.Conditional:
                LowerConditional( (ConditionalExpression) node );
                break;

            // Member access
            case ExpressionType.MemberAccess:
                LowerMemberAccess( (MemberExpression) node );
                break;

            // New object
            case ExpressionType.New:
                LowerNewObject( (NewExpression) node );
                break;

            // Block
            case ExpressionType.Block:
                LowerBlock( (BlockExpression) node );
                break;

            // Assignment
            case ExpressionType.Assign:
                LowerAssign( (BinaryExpression) node );
                break;

            // Default
            case ExpressionType.Default:
                LowerDefault( (DefaultExpression) node );
                break;

            // Exception handling
            case ExpressionType.Try:
                LowerTryCatch( (TryExpression) node );
                break;

            case ExpressionType.Throw:
                LowerThrow( (UnaryExpression) node );
                break;

            // Goto / Label
            case ExpressionType.Goto:
                LowerGoto( (GotoExpression) node );
                break;

            case ExpressionType.Label:
                LowerLabel( (LabelExpression) node );
                break;

            // Unsupported types that should throw
            case ExpressionType.Lambda:
            case ExpressionType.Loop:
            case ExpressionType.Switch:
                throw new NotSupportedException(
                    $"Expression type {node.NodeType} is not supported in this compiler phase." );

            default:
                if ( node.CanReduce )
                {
                    LowerExpression( node.Reduce() );
                }
                else
                {
                    throw new NotSupportedException(
                        $"Expression type {node.NodeType} is not supported." );
                }
                break;
        }
    }

    private void LowerConstant( ConstantExpression node )
    {
        if ( node.Value == null )
        {
            _ir.Emit( IROp.LoadNull );
        }
        else
        {
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( node.Value ) );
        }
    }

    private void LowerParameter( ParameterExpression node )
    {
        if ( _parameterMap.TryGetValue( node, out var argIndex ) )
        {
            _ir.Emit( IROp.LoadArg, argIndex );
        }
        else if ( _localMap.TryGetValue( node, out var localIndex ) )
        {
            _ir.Emit( IROp.LoadLocal, localIndex );
        }
        else
        {
            // Variable not yet declared -- declare as local
            var local = _ir.DeclareLocal( node.Type, node.Name );
            _localMap[node] = local;
            _ir.Emit( IROp.LoadLocal, local );
        }
    }

    private void LowerBinary( BinaryExpression node )
    {
        // Operator overload -- emit as method call
        if ( node.Method != null )
        {
            LowerExpression( node.Left );
            LowerExpression( node.Right );
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            return;
        }

        LowerExpression( node.Left );
        LowerExpression( node.Right );

        switch ( node.NodeType )
        {
            case ExpressionType.Add:
                _ir.Emit( IROp.Add );
                break;
            case ExpressionType.AddChecked:
                _ir.Emit( IROp.AddChecked );
                break;
            case ExpressionType.Subtract:
                _ir.Emit( IROp.Sub );
                break;
            case ExpressionType.SubtractChecked:
                _ir.Emit( IROp.SubChecked );
                break;
            case ExpressionType.Multiply:
                _ir.Emit( IROp.Mul );
                break;
            case ExpressionType.MultiplyChecked:
                _ir.Emit( IROp.MulChecked );
                break;
            case ExpressionType.Divide:
                _ir.Emit( IROp.Div );
                break;
            case ExpressionType.Modulo:
                _ir.Emit( IROp.Rem );
                break;
            case ExpressionType.And:
                _ir.Emit( IROp.And );
                break;
            case ExpressionType.Or:
                _ir.Emit( IROp.Or );
                break;
            case ExpressionType.ExclusiveOr:
                _ir.Emit( IROp.Xor );
                break;
            case ExpressionType.LeftShift:
                _ir.Emit( IROp.LeftShift );
                break;
            case ExpressionType.RightShift:
                _ir.Emit( IROp.RightShift );
                break;
            case ExpressionType.Equal:
                _ir.Emit( IROp.Ceq );
                break;
            case ExpressionType.NotEqual:
                // ceq + ldc.i4.0 + ceq (negate equality)
                _ir.Emit( IROp.Ceq );
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( 0 ) );
                _ir.Emit( IROp.Ceq );
                break;
            case ExpressionType.LessThan:
                _ir.Emit( IROp.Clt );
                break;
            case ExpressionType.GreaterThan:
                _ir.Emit( IROp.Cgt );
                break;
            case ExpressionType.LessThanOrEqual:
                // cgt + ldc.i4.0 + ceq (negate greater-than)
                _ir.Emit( IROp.Cgt );
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( 0 ) );
                _ir.Emit( IROp.Ceq );
                break;
            case ExpressionType.GreaterThanOrEqual:
                // clt + ldc.i4.0 + ceq (negate less-than)
                _ir.Emit( IROp.Clt );
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( 0 ) );
                _ir.Emit( IROp.Ceq );
                break;
            default:
                throw new NotSupportedException( $"Binary op {node.NodeType} is not supported." );
        }
    }

    private void LowerAndAlso( BinaryExpression node )
    {
        // Operator overload
        if ( node.Method != null )
        {
            LowerExpression( node.Left );
            LowerExpression( node.Right );
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            return;
        }

        // Short-circuit: if left is false, skip right and push false
        var falseLabel = _ir.DefineLabel();
        var endLabel = _ir.DefineLabel();

        LowerExpression( node.Left );
        _ir.Emit( IROp.Dup );
        _ir.Emit( IROp.BranchFalse, falseLabel );
        _ir.Emit( IROp.Pop ); // discard the dup'd left value
        LowerExpression( node.Right );
        _ir.Emit( IROp.Branch, endLabel );

        _ir.MarkLabel( falseLabel );
        // The dup'd false value is still on the stack
        _ir.MarkLabel( endLabel );
    }

    private void LowerOrElse( BinaryExpression node )
    {
        // Operator overload
        if ( node.Method != null )
        {
            LowerExpression( node.Left );
            LowerExpression( node.Right );
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            return;
        }

        // Short-circuit: if left is true, skip right and push true
        var trueLabel = _ir.DefineLabel();
        var endLabel = _ir.DefineLabel();

        LowerExpression( node.Left );
        _ir.Emit( IROp.Dup );
        _ir.Emit( IROp.BranchTrue, trueLabel );
        _ir.Emit( IROp.Pop ); // discard the dup'd left value
        LowerExpression( node.Right );
        _ir.Emit( IROp.Branch, endLabel );

        _ir.MarkLabel( trueLabel );
        // The dup'd true value is still on the stack
        _ir.MarkLabel( endLabel );
    }

    private void LowerUnary( UnaryExpression node )
    {
        // Operator overload
        if ( node.Method != null )
        {
            LowerExpression( node.Operand );
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            return;
        }

        LowerExpression( node.Operand );

        switch ( node.NodeType )
        {
            case ExpressionType.Negate:
                _ir.Emit( IROp.Negate );
                break;
            case ExpressionType.NegateChecked:
                _ir.Emit( IROp.NegateChecked );
                break;
            case ExpressionType.Not:
                _ir.Emit( IROp.Not );
                break;
            case ExpressionType.UnaryPlus:
                // No-op: value is already on the stack
                break;
            default:
                throw new NotSupportedException( $"Unary op {node.NodeType} is not supported." );
        }
    }

    private void LowerConvert( UnaryExpression node )
    {
        LowerExpression( node.Operand );

        // Method-based conversion (e.g., user-defined implicit/explicit operators)
        if ( node.Method != null )
        {
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            return;
        }

        var sourceType = node.Operand.Type;
        var targetType = node.Type;

        // Identity conversion -- no-op
        if ( sourceType == targetType )
            return;

        // Reference type conversions
        if ( !targetType.IsValueType && !sourceType.IsValueType )
        {
            // Reference to reference: castclass
            _ir.Emit( IROp.CastClass, _ir.AddOperand( targetType ) );
            return;
        }

        // Unboxing: reference type -> value type
        if ( !sourceType.IsValueType && targetType.IsValueType )
        {
            _ir.Emit( IROp.UnboxAny, _ir.AddOperand( targetType ) );
            return;
        }

        // Boxing: value type -> reference type
        if ( sourceType.IsValueType && !targetType.IsValueType )
        {
            _ir.Emit( IROp.Box, _ir.AddOperand( sourceType ) );
            return;
        }

        // Primitive conversions: value type -> value type
        var op = node.NodeType == ExpressionType.ConvertChecked ? IROp.ConvertChecked : IROp.Convert;
        _ir.Emit( op, _ir.AddOperand( targetType ) );
    }

    private void LowerTypeAs( UnaryExpression node )
    {
        LowerExpression( node.Operand );
        _ir.Emit( IROp.IsInst, _ir.AddOperand( node.Type ) );
    }

    private void LowerTypeIs( TypeBinaryExpression node )
    {
        // Push instance, isinst, ldnull, cgt.un (produces bool)
        LowerExpression( node.Expression );

        // Box value types before isinst
        if ( node.Expression.Type.IsValueType )
        {
            _ir.Emit( IROp.Box, _ir.AddOperand( node.Expression.Type ) );
        }

        _ir.Emit( IROp.IsInst, _ir.AddOperand( node.TypeOperand ) );
        _ir.Emit( IROp.LoadNull );
        _ir.Emit( IROp.CgtUn );
    }

    private void LowerMethodCall( MethodCallExpression node )
    {
        if ( node.Object != null )
        {
            LowerExpression( node.Object );
        }

        for ( var i = 0; i < node.Arguments.Count; i++ )
        {
            LowerExpression( node.Arguments[i] );
        }

        _ir.Emit(
            node.Method.IsVirtual ? IROp.CallVirt : IROp.Call,
            _ir.AddOperand( node.Method ) );
    }

    private void LowerConditional( ConditionalExpression node )
    {
        var isVoidConditional = node.Type == typeof( void );

        if ( isVoidConditional && node.IfFalse is DefaultExpression { Type: var t } && t == typeof( void ) )
        {
            // IfThen pattern: void conditional with no else
            var endLabel = _ir.DefineLabel();

            LowerExpression( node.Test );
            _ir.Emit( IROp.BranchFalse, endLabel );

            LowerExpression( node.IfTrue );
            if ( node.IfTrue.Type != typeof( void ) )
            {
                _ir.Emit( IROp.Pop );
            }

            _ir.MarkLabel( endLabel );
        }
        else
        {
            // Full ternary or IfThenElse
            var falseLabel = _ir.DefineLabel();
            var endLabel = _ir.DefineLabel();

            LowerExpression( node.Test );
            _ir.Emit( IROp.BranchFalse, falseLabel );

            LowerExpression( node.IfTrue );
            _ir.Emit( IROp.Branch, endLabel );

            _ir.MarkLabel( falseLabel );
            LowerExpression( node.IfFalse );

            _ir.MarkLabel( endLabel );
        }
    }

    private void LowerMemberAccess( MemberExpression node )
    {
        if ( node.Member is FieldInfo field )
        {
            if ( field.IsStatic )
            {
                _ir.Emit( IROp.LoadStaticField, _ir.AddOperand( field ) );
            }
            else
            {
                LowerExpression( node.Expression! );
                _ir.Emit( IROp.LoadField, _ir.AddOperand( field ) );
            }
        }
        else if ( node.Member is PropertyInfo property )
        {
            var getter = property.GetGetMethod( true )
                ?? throw new InvalidOperationException( $"Property '{property.Name}' has no getter." );

            if ( getter.IsStatic )
            {
                _ir.Emit( IROp.Call, _ir.AddOperand( getter ) );
            }
            else
            {
                LowerExpression( node.Expression! );
                _ir.Emit(
                    getter.IsVirtual ? IROp.CallVirt : IROp.Call,
                    _ir.AddOperand( getter ) );
            }
        }
        else
        {
            throw new NotSupportedException( $"Member type {node.Member.GetType().Name} is not supported." );
        }
    }

    private void LowerNewObject( NewExpression node )
    {
        if ( node.Constructor == null )
        {
            throw new NotSupportedException( "NewExpression without constructor is not supported." );
        }

        for ( var i = 0; i < node.Arguments.Count; i++ )
        {
            LowerExpression( node.Arguments[i] );
        }

        _ir.Emit( IROp.NewObj, _ir.AddOperand( node.Constructor ) );
    }

    private void LowerBlock( BlockExpression node )
    {
        _ir.EnterScope();

        // Declare block variables
        foreach ( var variable in node.Variables )
        {
            var local = _ir.DeclareLocal( variable.Type, variable.Name );
            _localMap[variable] = local;
        }

        // Lower all expressions in the block
        for ( var i = 0; i < node.Expressions.Count; i++ )
        {
            LowerExpression( node.Expressions[i] );

            // All expressions except the last have their result discarded
            if ( i < node.Expressions.Count - 1
                && node.Expressions[i].Type != typeof( void ) )
            {
                _ir.Emit( IROp.Pop );
            }
        }

        _ir.ExitScope();
    }

    private void LowerAssign( BinaryExpression node )
    {
        // The left side must be a ParameterExpression (variable)
        if ( node.Left is ParameterExpression variable )
        {
            LowerExpression( node.Right );

            // Dup the value so it remains on the stack as the result of the assignment
            _ir.Emit( IROp.Dup );

            if ( _localMap.TryGetValue( variable, out var localIndex ) )
            {
                _ir.Emit( IROp.StoreLocal, localIndex );
            }
            else if ( _parameterMap.TryGetValue( variable, out var argIndex ) )
            {
                _ir.Emit( IROp.StoreArg, argIndex );
            }
            else
            {
                // Variable not yet declared -- declare as local
                var local = _ir.DeclareLocal( variable.Type, variable.Name );
                _localMap[variable] = local;
                _ir.Emit( IROp.StoreLocal, local );
            }
        }
        else if ( node.Left is MemberExpression member )
        {
            if ( member.Member is FieldInfo field )
            {
                if ( field.IsStatic )
                {
                    LowerExpression( node.Right );
                    _ir.Emit( IROp.Dup );
                    _ir.Emit( IROp.StoreStaticField, _ir.AddOperand( field ) );
                }
                else
                {
                    LowerExpression( member.Expression! );
                    LowerExpression( node.Right );
                    _ir.Emit( IROp.Dup );
                    _ir.Emit( IROp.StoreField, _ir.AddOperand( field ) );
                }
            }
            else if ( member.Member is PropertyInfo property )
            {
                var setter = property.GetSetMethod( true )
                    ?? throw new InvalidOperationException( $"Property '{property.Name}' has no setter." );

                if ( setter.IsStatic )
                {
                    LowerExpression( node.Right );
                    _ir.Emit( IROp.Dup );
                    _ir.Emit( IROp.Call, _ir.AddOperand( setter ) );
                }
                else
                {
                    LowerExpression( member.Expression! );
                    LowerExpression( node.Right );
                    _ir.Emit( IROp.Dup );
                    _ir.Emit(
                        setter.IsVirtual ? IROp.CallVirt : IROp.Call,
                        _ir.AddOperand( setter ) );
                }
            }
            else
            {
                throw new NotSupportedException( $"Cannot assign to member type {member.Member.GetType().Name}." );
            }
        }
        else
        {
            throw new NotSupportedException( $"Cannot assign to {node.Left.NodeType}." );
        }
    }

    private void LowerDefault( DefaultExpression node )
    {
        if ( node.Type == typeof( void ) )
        {
            // Void default -- nothing to push
            return;
        }

        if ( !node.Type.IsValueType )
        {
            // Reference type default is null
            _ir.Emit( IROp.LoadNull );
        }
        else
        {
            // Value type default: declare a temp, initobj, load
            var temp = _ir.DeclareLocal( node.Type );
            _ir.Emit( IROp.InitObj, _ir.AddOperand( node.Type ) );
            _ir.Emit( IROp.StoreLocal, temp );
            _ir.Emit( IROp.LoadLocal, temp );
        }
    }

    // --- Exception handling ---

    private void LowerTryCatch( TryExpression node )
    {
        var isVoid = node.Type == typeof( void );

        // For non-void try expressions, declare a temp to hold the result
        var resultLocal = -1;
        if ( !isVoid )
        {
            resultLocal = _ir.DeclareLocal( node.Type, "$tryResult" );
        }

        // Define the label for after EndTryCatch (leave target)
        var endLabel = _ir.DefineLabel();

        // Emit BeginTry
        _ir.Emit( IROp.BeginTry );

        // Lower try body
        LowerExpression( node.Body );

        // Store result if non-void
        if ( !isVoid )
        {
            _ir.Emit( IROp.StoreLocal, resultLocal );
        }

        // Leave the try block
        _ir.Emit( IROp.Leave, endLabel );

        // Lower catch handlers
        foreach ( var handler in node.Handlers )
        {
            _ir.Emit( IROp.BeginCatch, _ir.AddOperand( handler.Test ) );

            if ( handler.Variable != null )
            {
                // Declare a local for the caught exception and store it
                var exLocal = _ir.DeclareLocal( handler.Variable.Type, handler.Variable.Name );
                _localMap[handler.Variable] = exLocal;
                _ir.Emit( IROp.StoreLocal, exLocal );
            }
            else
            {
                // CLR pushes exception on stack at catch entry; discard it
                _ir.Emit( IROp.Pop );
            }

            // Lower handler body
            LowerExpression( handler.Body );

            // Store result if non-void
            if ( !isVoid )
            {
                _ir.Emit( IROp.StoreLocal, resultLocal );
            }

            // Leave the catch block
            _ir.Emit( IROp.Leave, endLabel );
        }

        // Lower finally block
        if ( node.Finally != null )
        {
            _ir.Emit( IROp.BeginFinally );
            LowerExpression( node.Finally );
            if ( node.Finally.Type != typeof( void ) )
            {
                _ir.Emit( IROp.Pop );
            }
            // endfinally is handled by ILGenerator at EndExceptionBlock
        }

        // Lower fault block
        if ( node.Fault != null )
        {
            _ir.Emit( IROp.BeginFault );
            LowerExpression( node.Fault );
            if ( node.Fault.Type != typeof( void ) )
            {
                _ir.Emit( IROp.Pop );
            }
        }

        // Emit EndTryCatch
        _ir.Emit( IROp.EndTryCatch );

        // Mark the end label (leave target)
        _ir.MarkLabel( endLabel );

        // Load result if non-void
        if ( !isVoid )
        {
            _ir.Emit( IROp.LoadLocal, resultLocal );
        }
    }

    private void LowerThrow( UnaryExpression node )
    {
        if ( node.Operand != null )
        {
            LowerExpression( node.Operand );
            _ir.Emit( IROp.Throw );
        }
        else
        {
            // Rethrow (throw without operand inside catch)
            _ir.Emit( IROp.Rethrow );
        }
    }

    // --- Goto / Label ---

    private int GetOrCreateLabel( LabelTarget target )
    {
        if ( !_labelMap.TryGetValue( target, out var labelIndex ) )
        {
            labelIndex = _ir.DefineLabel();
            _labelMap[target] = labelIndex;
        }
        return labelIndex;
    }

    private int GetOrCreateLabelValueLocal( LabelTarget target )
    {
        if ( !_labelValueLocalMap.TryGetValue( target, out var localIndex ) )
        {
            localIndex = _ir.DeclareLocal( target.Type, $"$label_{target.Name}" );
            _labelValueLocalMap[target] = localIndex;
        }
        return localIndex;
    }

    private void LowerGoto( GotoExpression node )
    {
        var labelIndex = GetOrCreateLabel( node.Target );

        // If the goto carries a value, store it in the label's value local
        if ( node.Value != null && node.Target.Type != typeof( void ) )
        {
            LowerExpression( node.Value );
            var valueLocal = GetOrCreateLabelValueLocal( node.Target );
            _ir.Emit( IROp.StoreLocal, valueLocal );
        }

        // Emit branch (or leave if inside try/catch -- StackSpillPass handles this)
        _ir.Emit( IROp.Branch, labelIndex );
    }

    private void LowerLabel( LabelExpression node )
    {
        var labelIndex = GetOrCreateLabel( node.Target );

        // If the label has a type (carries a value), we need to handle two arrival paths:
        //  1. Via Goto: the Goto already stored the value into the label's value local.
        //     The Goto branches directly to the "after default" point.
        //  2. Via fallthrough: the default value is stored into the value local.
        //
        // Pattern:
        //   [fallthrough default store]
        //   Branch afterDefaultLabel     -- skip for fallthrough (already stored default)
        //   -- Wait, simpler: Goto branches to afterDefaultLabel, fallthrough stores default.
        //
        // Actually the simplest correct pattern:
        //   [default value store]        -- fallthrough stores default
        //   afterDefaultLabel:           -- Goto jumps here (value already stored by Goto)
        //   LoadLocal valueLocal         -- load the value

        if ( node.Target.Type != typeof( void ) )
        {
            var valueLocal = GetOrCreateLabelValueLocal( node.Target );

            if ( node.DefaultValue != null )
            {
                // Store the default value for the fallthrough path.
                // Goto arrivals branch past this to the label mark point.
                LowerExpression( node.DefaultValue );
                _ir.Emit( IROp.StoreLocal, valueLocal );
            }

            // Mark the label (Goto targets arrive here, skipping default store)
            _ir.MarkLabel( labelIndex );

            // Load the value
            _ir.Emit( IROp.LoadLocal, valueLocal );
        }
        else
        {
            // Void label -- just mark the label
            _ir.MarkLabel( labelIndex );

            if ( node.DefaultValue != null && node.DefaultValue.Type != typeof( void ) )
            {
                // Void label but non-void default -- lower and discard
                LowerExpression( node.DefaultValue );
                _ir.Emit( IROp.Pop );
            }
        }
    }
}
