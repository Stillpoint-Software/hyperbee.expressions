using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Compiler.IR;

namespace Hyperbee.Expressions.Compiler.Lowering;

/// <summary>
/// Lowers a System.Linq.Expressions expression tree into flat IR instructions.
/// Single traversal. Handles constants, parameters, binary/unary, conversions,
/// method calls, conditionals, blocks, assignments, member access, new objects,
/// try/catch/finally/throw/goto/label, and nested lambdas with closures.
/// </summary>
public class ExpressionLowerer
{
    private readonly IRBuilder _ir;
    private readonly Dictionary<ParameterExpression, int> _parameterMap = new( 4 );
    private readonly Dictionary<ParameterExpression, int> _localMap = new( 8 );
    private readonly Dictionary<LabelTarget, int> _labelMap = new( 4 );
    private readonly Dictionary<LabelTarget, int> _labelValueLocalMap = new( 4 );
    private readonly HashSet<ParameterExpression> _capturedVariables;

    // Maps captured variable -> local index of its StrongBox<T>
    private readonly Dictionary<ParameterExpression, int> _strongBoxLocalMap = new();

    // Maps a nested lambda (by reference identity) to its closure info
    private readonly Dictionary<LambdaExpression, ClosureInfo> _closureInfoMap = new();

    private int _argOffset;

    /// <summary>
    /// Creates a new expression lowerer targeting the given IR builder.
    /// </summary>
    public ExpressionLowerer( IRBuilder ir )
        : this( ir, null )
    {
    }

    /// <summary>
    /// Creates a new expression lowerer targeting the given IR builder,
    /// with a set of captured variables that need StrongBox wrapping.
    /// </summary>
    public ExpressionLowerer( IRBuilder ir, HashSet<ParameterExpression>? capturedVariables )
    {
        _ir = ir;
        _capturedVariables = capturedVariables ?? new HashSet<ParameterExpression>();
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

    private bool IsCaptured( ParameterExpression variable )
    {
        return _capturedVariables.Contains( variable );
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

            // Lambda (nested)
            case ExpressionType.Lambda:
                LowerNestedLambda( (LambdaExpression) node );
                break;

            // Invoke (delegate invocation)
            case ExpressionType.Invoke:
                LowerInvoke( (InvocationExpression) node );
                break;

            // Loop
            case ExpressionType.Loop:
                LowerLoop( (LoopExpression) node );
                break;

            // Switch
            case ExpressionType.Switch:
                LowerSwitch( (SwitchExpression) node );
                break;

            // Array operations
            case ExpressionType.NewArrayInit:
                LowerNewArrayInit( (NewArrayExpression) node );
                break;

            case ExpressionType.NewArrayBounds:
                LowerNewArrayBounds( (NewArrayExpression) node );
                break;

            case ExpressionType.ArrayIndex:
                LowerArrayIndex( (BinaryExpression) node );
                break;

            case ExpressionType.ArrayLength:
                LowerArrayLength( (UnaryExpression) node );
                break;

            // Index (indexer property or array access)
            case ExpressionType.Index:
                LowerIndex( (IndexExpression) node );
                break;

            // Collection initializers
            case ExpressionType.ListInit:
                LowerListInit( (ListInitExpression) node );
                break;

            case ExpressionType.MemberInit:
                LowerMemberInit( (MemberInitExpression) node );
                break;

            // Null coalescing
            case ExpressionType.Coalesce:
                LowerCoalesce( (BinaryExpression) node );
                break;

            // Type equality (exact match)
            case ExpressionType.TypeEqual:
                LowerTypeEqual( (TypeBinaryExpression) node );
                break;

            // Quote (expression as data)
            case ExpressionType.Quote:
                LowerQuote( (UnaryExpression) node );
                break;

            // Power (Math.Pow)
            case ExpressionType.Power:
                LowerPower( (BinaryExpression) node );
                break;

            // Unbox
            case ExpressionType.Unbox:
                LowerUnbox( (UnaryExpression) node );
                break;

            // DebugInfo (no-op)
            case ExpressionType.DebugInfo:
                break;

            // RuntimeVariables and Dynamic are low priority
            case ExpressionType.RuntimeVariables:
            case ExpressionType.Dynamic:
                throw new NotSupportedException(
                    $"Expression type {node.NodeType} is not supported." );

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
        // Captured variable -- load through StrongBox<T>.Value
        if ( IsCaptured( node ) && _strongBoxLocalMap.ContainsKey( node ) )
        {
            EmitLoadCapturedValue( node );
            return;
        }

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
            if ( isVoidConditional && node.IfTrue.Type != typeof( void ) )
            {
                _ir.Emit( IROp.Pop );
            }
            _ir.Emit( IROp.Branch, endLabel );

            _ir.MarkLabel( falseLabel );
            LowerExpression( node.IfFalse );
            if ( isVoidConditional && node.IfFalse.Type != typeof( void ) )
            {
                _ir.Emit( IROp.Pop );
            }

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
            if ( IsCaptured( variable ) )
            {
                // Captured variable: allocate a StrongBox<T> local
                var strongBoxType = typeof( StrongBox<> ).MakeGenericType( variable.Type );
                var boxLocal = _ir.DeclareLocal( strongBoxType, $"$box_{variable.Name}" );
                _strongBoxLocalMap[variable] = boxLocal;

                // Emit: new StrongBox<T>() and store
                var ctor = strongBoxType.GetConstructor( Type.EmptyTypes )!;
                _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
                _ir.Emit( IROp.StoreLocal, boxLocal );
            }
            else
            {
                var local = _ir.DeclareLocal( variable.Type, variable.Name );
                _localMap[variable] = local;
            }
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
            // Captured variable -- store through StrongBox<T>.Value
            if ( IsCaptured( variable ) && _strongBoxLocalMap.ContainsKey( variable ) )
            {
                EmitStoreCapturedValue( variable, node.Right );
                return;
            }

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

    // --- Loop ---

    private void LowerLoop( LoopExpression node )
    {
        // Allocate labels for continue (top of loop) and break (after loop)
        int continueLabel;
        if ( node.ContinueLabel != null )
        {
            continueLabel = GetOrCreateLabel( node.ContinueLabel );
        }
        else
        {
            continueLabel = _ir.DefineLabel();
        }

        int breakLabel;
        if ( node.BreakLabel != null )
        {
            breakLabel = GetOrCreateLabel( node.BreakLabel );
        }
        else
        {
            breakLabel = _ir.DefineLabel();
        }

        // continueLabel:
        _ir.MarkLabel( continueLabel );

        // Lower body
        LowerExpression( node.Body );

        // If body produces a value, discard it
        if ( node.Body.Type != typeof( void ) )
        {
            _ir.Emit( IROp.Pop );
        }

        // Branch back to continue label
        _ir.Emit( IROp.Branch, continueLabel );

        // breakLabel:
        _ir.MarkLabel( breakLabel );

        // If the loop has a non-void break label, load the value
        if ( node.BreakLabel != null && node.BreakLabel.Type != typeof( void ) )
        {
            var valueLocal = GetOrCreateLabelValueLocal( node.BreakLabel );
            _ir.Emit( IROp.LoadLocal, valueLocal );
        }
    }

    // --- Switch ---

    private void LowerSwitch( SwitchExpression node )
    {
        var isVoid = node.Type == typeof( void );

        // Lower the switch value and store in a temp
        LowerExpression( node.SwitchValue );
        var switchValueLocal = _ir.DeclareLocal( node.SwitchValue.Type, "$switchValue" );
        _ir.Emit( IROp.StoreLocal, switchValueLocal );

        var endLabel = _ir.DefineLabel();
        var caseLabels = new int[node.Cases.Count];
        for ( var i = 0; i < node.Cases.Count; i++ )
        {
            caseLabels[i] = _ir.DefineLabel();
        }

        var defaultLabel = node.DefaultBody != null ? _ir.DefineLabel() : endLabel;

        // Emit test conditions
        for ( var i = 0; i < node.Cases.Count; i++ )
        {
            var switchCase = node.Cases[i];

            foreach ( var testValue in switchCase.TestValues )
            {
                _ir.Emit( IROp.LoadLocal, switchValueLocal );
                LowerExpression( testValue );

                if ( node.Comparison != null )
                {
                    // Use custom comparison method
                    _ir.Emit( IROp.Call, _ir.AddOperand( node.Comparison ) );
                }
                else
                {
                    _ir.Emit( IROp.Ceq );
                }

                _ir.Emit( IROp.BranchTrue, caseLabels[i] );
            }
        }

        // Branch to default or end
        _ir.Emit( IROp.Branch, defaultLabel );

        // Emit case bodies
        for ( var i = 0; i < node.Cases.Count; i++ )
        {
            _ir.MarkLabel( caseLabels[i] );
            LowerExpression( node.Cases[i].Body );

            if ( isVoid && node.Cases[i].Body.Type != typeof( void ) )
            {
                // Non-void body in void switch -- discard result
                _ir.Emit( IROp.Pop );
            }
            else if ( !isVoid && node.Cases[i].Body.Type == typeof( void ) )
            {
                // Void body in non-void switch -- push default
                LowerDefault( Expression.Default( node.Type ) );
            }

            _ir.Emit( IROp.Branch, endLabel );
        }

        // Default body
        if ( node.DefaultBody != null )
        {
            _ir.MarkLabel( defaultLabel );
            LowerExpression( node.DefaultBody );

            if ( isVoid && node.DefaultBody.Type != typeof( void ) )
            {
                _ir.Emit( IROp.Pop );
            }

            _ir.Emit( IROp.Branch, endLabel );
        }

        _ir.MarkLabel( endLabel );
    }

    // --- Array operations ---

    private void LowerNewArrayInit( NewArrayExpression node )
    {
        var elementType = node.Type.GetElementType()!;

        // Push array length
        _ir.Emit( IROp.LoadConst, _ir.AddOperand( node.Expressions.Count ) );

        // newarr elementType
        _ir.Emit( IROp.NewArray, _ir.AddOperand( elementType ) );

        // For each element: dup, ldc.i4 index, lower element, stelem
        for ( var i = 0; i < node.Expressions.Count; i++ )
        {
            _ir.Emit( IROp.Dup );
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( i ) );
            LowerExpression( node.Expressions[i] );
            _ir.Emit( IROp.StoreElement, _ir.AddOperand( elementType ) );
        }
    }

    private void LowerNewArrayBounds( NewArrayExpression node )
    {
        var elementType = node.Type.GetElementType()!;

        if ( node.Expressions.Count == 1 )
        {
            // Single dimension: push length, newarr
            LowerExpression( node.Expressions[0] );
            _ir.Emit( IROp.NewArray, _ir.AddOperand( elementType ) );
        }
        else
        {
            // Multi-dimensional: call Array.CreateInstance(Type, int[])
            // Build the bounds array
            var boundsCount = node.Expressions.Count;

            // Push element type
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( elementType ) );

            // Create int[] for bounds
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( boundsCount ) );
            _ir.Emit( IROp.NewArray, _ir.AddOperand( typeof( int ) ) );

            for ( var i = 0; i < boundsCount; i++ )
            {
                _ir.Emit( IROp.Dup );
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( i ) );
                LowerExpression( node.Expressions[i] );
                _ir.Emit( IROp.StoreElement, _ir.AddOperand( typeof( int ) ) );
            }

            // Call Array.CreateInstance(Type, int[])
            var createInstanceMethod = typeof( Array ).GetMethod(
                nameof( Array.CreateInstance ),
                [typeof( Type ), typeof( int[] )] )!;
            _ir.Emit( IROp.Call, _ir.AddOperand( createInstanceMethod ) );
        }
    }

    private void LowerArrayIndex( BinaryExpression node )
    {
        // Lower array, lower index, ldelem
        LowerExpression( node.Left );
        LowerExpression( node.Right );
        _ir.Emit( IROp.LoadElement, _ir.AddOperand( node.Type ) );
    }

    private void LowerArrayLength( UnaryExpression node )
    {
        LowerExpression( node.Operand );
        _ir.Emit( IROp.LoadArrayLength );
    }

    // --- Index expression (indexer or array access) ---

    private void LowerIndex( IndexExpression node )
    {
        LowerExpression( node.Object! );

        foreach ( var arg in node.Arguments )
        {
            LowerExpression( arg );
        }

        if ( node.Indexer != null )
        {
            // Indexer property access -- call the getter
            var getter = node.Indexer.GetGetMethod( true )
                ?? throw new InvalidOperationException( $"Indexer '{node.Indexer.Name}' has no getter." );
            _ir.Emit(
                getter.IsVirtual ? IROp.CallVirt : IROp.Call,
                _ir.AddOperand( getter ) );
        }
        else
        {
            // Array element access
            _ir.Emit( IROp.LoadElement, _ir.AddOperand( node.Type ) );
        }
    }

    // --- ListInit ---

    private void LowerListInit( ListInitExpression node )
    {
        // Lower the new expression
        LowerNewObject( node.NewExpression );

        // For each initializer: dup, lower args, call Add method
        foreach ( var initializer in node.Initializers )
        {
            _ir.Emit( IROp.Dup );

            foreach ( var arg in initializer.Arguments )
            {
                LowerExpression( arg );
            }

            _ir.Emit(
                initializer.AddMethod.IsVirtual ? IROp.CallVirt : IROp.Call,
                _ir.AddOperand( initializer.AddMethod ) );

            // If Add returns a value, discard it (most Add methods return void, but some like HashSet.Add return bool)
            if ( initializer.AddMethod.ReturnType != typeof( void ) )
            {
                _ir.Emit( IROp.Pop );
            }
        }
    }

    // --- MemberInit ---

    private void LowerMemberInit( MemberInitExpression node )
    {
        // Lower the new expression
        LowerNewObject( node.NewExpression );

        // Process each binding
        foreach ( var binding in node.Bindings )
        {
            LowerMemberBinding( binding );
        }
    }

    private void LowerMemberBinding( MemberBinding binding )
    {
        switch ( binding )
        {
            case MemberAssignment assignment:
            {
                _ir.Emit( IROp.Dup );
                LowerExpression( assignment.Expression );

                if ( assignment.Member is FieldInfo field )
                {
                    _ir.Emit( IROp.StoreField, _ir.AddOperand( field ) );
                }
                else if ( assignment.Member is PropertyInfo property )
                {
                    var setter = property.GetSetMethod( true )
                        ?? throw new InvalidOperationException( $"Property '{property.Name}' has no setter." );
                    _ir.Emit(
                        setter.IsVirtual ? IROp.CallVirt : IROp.Call,
                        _ir.AddOperand( setter ) );
                }
                break;
            }

            case MemberListBinding listBinding:
            {
                _ir.Emit( IROp.Dup );

                // Load the member value
                if ( listBinding.Member is FieldInfo field )
                {
                    _ir.Emit( IROp.LoadField, _ir.AddOperand( field ) );
                }
                else if ( listBinding.Member is PropertyInfo property )
                {
                    var getter = property.GetGetMethod( true )!;
                    _ir.Emit(
                        getter.IsVirtual ? IROp.CallVirt : IROp.Call,
                        _ir.AddOperand( getter ) );
                }

                // For each initializer: dup, lower args, call Add method
                foreach ( var initializer in listBinding.Initializers )
                {
                    _ir.Emit( IROp.Dup );

                    foreach ( var arg in initializer.Arguments )
                    {
                        LowerExpression( arg );
                    }

                    _ir.Emit(
                        initializer.AddMethod.IsVirtual ? IROp.CallVirt : IROp.Call,
                        _ir.AddOperand( initializer.AddMethod ) );

                    if ( initializer.AddMethod.ReturnType != typeof( void ) )
                    {
                        _ir.Emit( IROp.Pop );
                    }
                }

                // Pop the member value (list reference)
                _ir.Emit( IROp.Pop );
                break;
            }

            case MemberMemberBinding memberBinding:
            {
                _ir.Emit( IROp.Dup );

                // Load the member value
                if ( memberBinding.Member is FieldInfo field )
                {
                    _ir.Emit( IROp.LoadField, _ir.AddOperand( field ) );
                }
                else if ( memberBinding.Member is PropertyInfo property )
                {
                    var getter = property.GetGetMethod( true )!;
                    _ir.Emit(
                        getter.IsVirtual ? IROp.CallVirt : IROp.Call,
                        _ir.AddOperand( getter ) );
                }

                // Recursively process inner bindings
                foreach ( var innerBinding in memberBinding.Bindings )
                {
                    LowerMemberBinding( innerBinding );
                }

                // Pop the member value
                _ir.Emit( IROp.Pop );
                break;
            }

            default:
                throw new NotSupportedException( $"Member binding type {binding.BindingType} is not supported." );
        }
    }

    // --- Coalesce (null coalescing ??) ---

    private void LowerCoalesce( BinaryExpression node )
    {
        // Method-based coalescing
        if ( node.Method != null )
        {
            LowerExpression( node.Left );
            LowerExpression( node.Right );
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            return;
        }

        var endLabel = _ir.DefineLabel();
        var useRightLabel = _ir.DefineLabel();

        LowerExpression( node.Left );
        _ir.Emit( IROp.Dup );

        // For nullable value types, we need HasValue check
        var leftType = node.Left.Type;
        if ( leftType.IsValueType && Nullable.GetUnderlyingType( leftType ) != null )
        {
            // Store the nullable in a temp for HasValue check
            var temp = _ir.DeclareLocal( leftType, "$coalesceTemp" );
            _ir.Emit( IROp.StoreLocal, temp );
            _ir.Emit( IROp.Pop ); // pop the dup'd value

            // Load address and call HasValue
            _ir.Emit( IROp.LoadLocal, temp );

            var hasValueGetter = leftType.GetProperty( "HasValue" )!.GetGetMethod()!;
            // For value types we need to store and load address
            _ir.Emit( IROp.StoreLocal, temp );
            _ir.Emit( IROp.LoadAddress, temp );
            _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetter ) );
            _ir.Emit( IROp.BranchFalse, useRightLabel );

            // Has value -- get the value
            _ir.Emit( IROp.LoadAddress, temp );
            var getValueGetter = leftType.GetProperty( "Value" )!.GetGetMethod()!;
            _ir.Emit( IROp.Call, _ir.AddOperand( getValueGetter ) );

            // If the coalesce conversion exists, apply it
            if ( node.Conversion != null )
            {
                var convDelegate = node.Conversion.Compile();
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( convDelegate ) );
                // Stack: [value] [delegate]
                // Need to swap -- use temp
                var valTemp = _ir.DeclareLocal( Nullable.GetUnderlyingType( leftType )!, "$coalesceVal" );
                var delTemp = _ir.DeclareLocal( convDelegate.GetType(), "$coalesceDel" );
                _ir.Emit( IROp.StoreLocal, delTemp );
                _ir.Emit( IROp.StoreLocal, valTemp );
                _ir.Emit( IROp.LoadLocal, delTemp );
                _ir.Emit( IROp.LoadLocal, valTemp );
                var invokeMethod = convDelegate.GetType().GetMethod( "Invoke" )!;
                _ir.Emit( IROp.CallVirt, _ir.AddOperand( invokeMethod ) );
            }

            _ir.Emit( IROp.Branch, endLabel );

            _ir.MarkLabel( useRightLabel );
            LowerExpression( node.Right );
            _ir.MarkLabel( endLabel );
        }
        else
        {
            // Reference type -- use brfalse (null check)
            _ir.Emit( IROp.BranchFalse, useRightLabel );

            // If there is a conversion lambda, apply it
            if ( node.Conversion != null )
            {
                var convDelegate = node.Conversion.Compile();
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( convDelegate ) );
                // Stack: [left] [delegate] -- need swap
                var leftTemp = _ir.DeclareLocal( node.Left.Type, "$coalesceLeft" );
                var delTemp = _ir.DeclareLocal( convDelegate.GetType(), "$coalesceDel" );
                _ir.Emit( IROp.StoreLocal, delTemp );
                _ir.Emit( IROp.StoreLocal, leftTemp );
                _ir.Emit( IROp.LoadLocal, delTemp );
                _ir.Emit( IROp.LoadLocal, leftTemp );
                var invokeMethod = convDelegate.GetType().GetMethod( "Invoke" )!;
                _ir.Emit( IROp.CallVirt, _ir.AddOperand( invokeMethod ) );
            }

            _ir.Emit( IROp.Branch, endLabel );

            _ir.MarkLabel( useRightLabel );
            _ir.Emit( IROp.Pop ); // discard the dup'd null
            LowerExpression( node.Right );

            _ir.MarkLabel( endLabel );
        }
    }

    // --- TypeEqual (exact type match) ---

    private void LowerTypeEqual( TypeBinaryExpression node )
    {
        // expr.GetType() == typeof(T)
        LowerExpression( node.Expression );

        // If value type, box it first
        if ( node.Expression.Type.IsValueType )
        {
            _ir.Emit( IROp.Box, _ir.AddOperand( node.Expression.Type ) );
        }

        // Call object.GetType()
        var getTypeMethod = typeof( object ).GetMethod( nameof( object.GetType ) )!;
        _ir.Emit( IROp.CallVirt, _ir.AddOperand( getTypeMethod ) );

        // Load the Type token
        _ir.Emit( IROp.LoadConst, _ir.AddOperand( node.TypeOperand ) );

        // Compare
        _ir.Emit( IROp.Ceq );
    }

    // --- Quote ---

    private void LowerQuote( UnaryExpression node )
    {
        // Quote wraps an expression tree as data.
        // Store the inner expression as a non-embeddable constant.
        _ir.Emit( IROp.LoadConst, _ir.AddOperand( node.Operand ) );
    }

    // --- Power ---

    private void LowerPower( BinaryExpression node )
    {
        if ( node.Method != null )
        {
            LowerExpression( node.Left );
            LowerExpression( node.Right );
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            return;
        }

        LowerExpression( node.Left );
        LowerExpression( node.Right );

        var mathPow = typeof( Math ).GetMethod( nameof( Math.Pow ), [typeof( double ), typeof( double )] )!;
        _ir.Emit( IROp.Call, _ir.AddOperand( mathPow ) );
    }

    // --- Unbox ---

    private void LowerUnbox( UnaryExpression node )
    {
        LowerExpression( node.Operand );
        _ir.Emit( IROp.UnboxAny, _ir.AddOperand( node.Type ) );
    }

    // --- Lambda / Invoke (Phase 3: Closures) ---

    /// <summary>
    /// Emit code to load a captured variable's value through its StrongBox&lt;T&gt;.
    /// Stack effect: pushes the value of the captured variable.
    /// </summary>
    private void EmitLoadCapturedValue( ParameterExpression variable )
    {
        var boxLocal = _strongBoxLocalMap[variable];
        var strongBoxType = typeof( StrongBox<> ).MakeGenericType( variable.Type );
        var valueField = strongBoxType.GetField( "Value" )!;

        _ir.Emit( IROp.LoadLocal, boxLocal );
        _ir.Emit( IROp.LoadField, _ir.AddOperand( valueField ) );
    }

    /// <summary>
    /// Emit code to store a value into a captured variable through its StrongBox&lt;T&gt;.
    /// The right-hand side expression is lowered and the result is dup'd so the
    /// assignment expression still has a value on the stack.
    /// </summary>
    private void EmitStoreCapturedValue( ParameterExpression variable, Expression rightSide )
    {
        var boxLocal = _strongBoxLocalMap[variable];
        var strongBoxType = typeof( StrongBox<> ).MakeGenericType( variable.Type );
        var valueField = strongBoxType.GetField( "Value" )!;

        // Pattern: LoadLocal box, LowerExpression right, Dup, StoreLocal temp, StoreField Value, LoadLocal temp
        // This leaves the assigned value on the stack as the expression result.
        _ir.Emit( IROp.LoadLocal, boxLocal );
        LowerExpression( rightSide );
        _ir.Emit( IROp.Dup );

        // Stack: [box] [value] [value]
        // stfld expects [box][value], but the dup'd value is on top.
        // Use a temp to hold the result.
        var tempLocal = _ir.DeclareLocal( variable.Type, $"$temp_{variable.Name}" );
        _ir.Emit( IROp.StoreLocal, tempLocal ); // Stack: [box] [value]
        _ir.Emit( IROp.StoreField, _ir.AddOperand( valueField ) ); // Stack: empty
        _ir.Emit( IROp.LoadLocal, tempLocal ); // Stack: [value] (assignment result)
    }

    /// <summary>
    /// Lower a nested lambda expression. For lambdas without captures, compile
    /// directly with the System compiler and push on stack. For lambdas with
    /// captures, prepare closure info and push on stack.
    /// </summary>
    private void LowerNestedLambda( LambdaExpression nestedLambda )
    {
        // Ensure closure info is prepared (shared with LowerInvoke)
        var closureInfo = GetOrBuildClosureInfo( nestedLambda );

        if ( closureInfo == null )
        {
            // No captures -- compile directly with System compiler
            var compiledDelegate = nestedLambda.Compile();
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( compiledDelegate ) );
        }
        else
        {
            // Has captures -- this lambda is used as a value (not via Invoke).
            // We can't easily represent a partially-applied delegate in IL,
            // so emit the compiled inner delegate as a constant.
            // Note: standalone closure lambdas (not invoked) are an edge case.
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( closureInfo.CompiledDelegate ) );
        }
    }

    /// <summary>
    /// Lower an invocation expression (Expression.Invoke).
    /// For closure lambdas, passes the StrongBox locals as extra arguments.
    /// For non-closure delegates, calls Invoke normally.
    /// </summary>
    private void LowerInvoke( InvocationExpression node )
    {
        // Check if this invocation targets a nested lambda that may have closures
        if ( node.Expression is LambdaExpression lambdaExpr )
        {
            var closureInfo = GetOrBuildClosureInfo( lambdaExpr );

            if ( closureInfo != null )
            {
                // Closure lambda: load compiled delegate, args, and StrongBox locals
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( closureInfo.CompiledDelegate ) );

                // Lower the original arguments
                foreach ( var arg in node.Arguments )
                {
                    LowerExpression( arg );
                }

                // Load the StrongBox locals for captured variables
                foreach ( var capture in closureInfo.Captures )
                {
                    var boxLocal = _strongBoxLocalMap[capture];
                    _ir.Emit( IROp.LoadLocal, boxLocal );
                }

                // Call Invoke on the rewritten delegate type (original params + StrongBox params)
                var invokeMethod = closureInfo.CompiledDelegate.GetType().GetMethod( "Invoke" )!;
                _ir.Emit( IROp.CallVirt, _ir.AddOperand( invokeMethod ) );
                return;
            }

            // No captures -- compile the lambda directly and invoke
            var compiledDelegate = lambdaExpr.Compile();
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( compiledDelegate ) );

            foreach ( var arg in node.Arguments )
            {
                LowerExpression( arg );
            }

            var delegateInvokeMethod = compiledDelegate.GetType().GetMethod( "Invoke" )!;
            _ir.Emit( IROp.CallVirt, _ir.AddOperand( delegateInvokeMethod ) );
            return;
        }

        // Generic delegate invocation -- lower the target and arguments
        LowerExpression( node.Expression );

        foreach ( var arg in node.Arguments )
        {
            LowerExpression( arg );
        }

        // Call Invoke on the delegate type
        var delegateType = node.Expression.Type;
        var invokeMethod2 = delegateType.GetMethod( "Invoke" )!;
        _ir.Emit( IROp.CallVirt, _ir.AddOperand( invokeMethod2 ) );
    }

    /// <summary>
    /// Get or build the closure info for a nested lambda. Returns null if the
    /// lambda has no captured variables and doesn't need closure treatment.
    /// </summary>
    private ClosureInfo? GetOrBuildClosureInfo( LambdaExpression lambda )
    {
        // Check if already built
        if ( _closureInfoMap.TryGetValue( lambda, out var existing ) )
        {
            return existing;
        }

        // Find which captured variables this nested lambda references
        var innerCaptures = new List<ParameterExpression>();
        foreach ( var capturedVar in _capturedVariables )
        {
            if ( _strongBoxLocalMap.ContainsKey( capturedVar )
                && ReferencesVariable( lambda.Body, capturedVar ) )
            {
                innerCaptures.Add( capturedVar );
            }
        }

        if ( innerCaptures.Count == 0 )
        {
            return null;
        }

        // Build the closure: rewrite inner lambda to take StrongBox<T> parameters
        var boxParams = new ParameterExpression[innerCaptures.Count];
        var boxTypes = new Type[innerCaptures.Count];

        for ( var i = 0; i < innerCaptures.Count; i++ )
        {
            var strongBoxType = typeof( StrongBox<> ).MakeGenericType( innerCaptures[i].Type );
            boxParams[i] = Expression.Parameter( strongBoxType, $"box_{innerCaptures[i].Name}" );
            boxTypes[i] = strongBoxType;
        }

        // Build a mapping from captured variable to StrongBox<T>.Value access
        var replacements = new Dictionary<ParameterExpression, Expression>();
        for ( var i = 0; i < innerCaptures.Count; i++ )
        {
            var valueField = boxTypes[i].GetField( "Value" )!;
            replacements[innerCaptures[i]] = Expression.Field( boxParams[i], valueField );
        }

        // Rewrite the inner lambda body to use StrongBox parameters
        var rewrittenBody = (Expression) new CaptureRewriter( replacements ).Visit( lambda.Body )!;

        // Build a new lambda that takes the original params + StrongBox params
        var allParams = new List<ParameterExpression>( lambda.Parameters );
        allParams.AddRange( boxParams );

        // If the original lambda has a void return type but the rewritten body has
        // a non-void type, wrap in a void block to discard the value.
        var originalReturnType = lambda.ReturnType;
        if ( originalReturnType == typeof( void ) && rewrittenBody.Type != typeof( void ) )
        {
            rewrittenBody = Expression.Block( typeof( void ), rewrittenBody );
        }

        // Build the correct delegate type that matches the original return type
        // but includes the extra StrongBox parameters.
        var allParamTypes = allParams.Select( p => p.Type ).ToArray();
        Type delegateType;

        if ( originalReturnType == typeof( void ) )
        {
            delegateType = Expression.GetActionType( allParamTypes );
        }
        else
        {
            var funcTypes = new Type[allParamTypes.Length + 1];
            Array.Copy( allParamTypes, funcTypes, allParamTypes.Length );
            funcTypes[^1] = originalReturnType;
            delegateType = Expression.GetFuncType( funcTypes );
        }

        // Create and compile the rewritten lambda with explicit delegate type
        var rewrittenLambda = Expression.Lambda( delegateType, rewrittenBody, allParams );
        var compiledInner = rewrittenLambda.Compile();

        var closureInfo = new ClosureInfo( compiledInner, innerCaptures );
        _closureInfoMap[lambda] = closureInfo;
        return closureInfo;
    }

    /// <summary>
    /// Check if an expression tree references a specific parameter variable.
    /// </summary>
    private static bool ReferencesVariable( Expression node, ParameterExpression variable )
    {
        if ( node == null )
            return false;

        if ( node is ParameterExpression param && param == variable )
            return true;

        switch ( node )
        {
            case BinaryExpression binary:
                return ReferencesVariable( binary.Left, variable )
                    || ReferencesVariable( binary.Right, variable );

            case UnaryExpression unary:
                return ReferencesVariable( unary.Operand, variable );

            case ConditionalExpression conditional:
                return ReferencesVariable( conditional.Test, variable )
                    || ReferencesVariable( conditional.IfTrue, variable )
                    || ReferencesVariable( conditional.IfFalse, variable );

            case MethodCallExpression methodCall:
            {
                if ( methodCall.Object != null && ReferencesVariable( methodCall.Object, variable ) )
                    return true;
                foreach ( var arg in methodCall.Arguments )
                {
                    if ( ReferencesVariable( arg, variable ) )
                        return true;
                }
                return false;
            }

            case BlockExpression block:
            {
                foreach ( var expr in block.Expressions )
                {
                    if ( ReferencesVariable( expr, variable ) )
                        return true;
                }
                return false;
            }

            case MemberExpression member:
                return ReferencesVariable( member.Expression, variable );

            case InvocationExpression invocation:
            {
                if ( ReferencesVariable( invocation.Expression, variable ) )
                    return true;
                foreach ( var arg in invocation.Arguments )
                {
                    if ( ReferencesVariable( arg, variable ) )
                        return true;
                }
                return false;
            }

            case LambdaExpression lambda:
                return ReferencesVariable( lambda.Body, variable );

            case NewExpression newExpr:
            {
                foreach ( var arg in newExpr.Arguments )
                {
                    if ( ReferencesVariable( arg, variable ) )
                        return true;
                }
                return false;
            }

            case TryExpression tryExpr:
            {
                if ( ReferencesVariable( tryExpr.Body, variable ) )
                    return true;
                foreach ( var handler in tryExpr.Handlers )
                {
                    if ( ReferencesVariable( handler.Filter, variable )
                        || ReferencesVariable( handler.Body, variable ) )
                        return true;
                }
                if ( ReferencesVariable( tryExpr.Finally, variable ) )
                    return true;
                if ( ReferencesVariable( tryExpr.Fault, variable ) )
                    return true;
                return false;
            }

            case GotoExpression gotoExpr:
                return ReferencesVariable( gotoExpr.Value, variable );

            case LabelExpression labelExpr:
                return ReferencesVariable( labelExpr.DefaultValue, variable );

            case TypeBinaryExpression typeBinary:
                return ReferencesVariable( typeBinary.Expression, variable );

            default:
                return false;
        }
    }

    // --- Closure infrastructure ---

    /// <summary>
    /// Contains info about a compiled closure: the inner delegate and which
    /// captured variables it needs as StrongBox arguments.
    /// </summary>
    private record ClosureInfo( Delegate CompiledDelegate, List<ParameterExpression> Captures );

    /// <summary>
    /// An ExpressionVisitor that replaces captured ParameterExpression references
    /// with StrongBox&lt;T&gt;.Value field accesses.
    /// </summary>
    private class CaptureRewriter : ExpressionVisitor
    {
        private readonly Dictionary<ParameterExpression, Expression> _replacements;

        public CaptureRewriter( Dictionary<ParameterExpression, Expression> replacements )
        {
            _replacements = replacements;
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            return _replacements.TryGetValue( node, out var replacement )
                ? replacement
                : base.VisitParameter( node );
        }

        protected override Expression VisitBinary( BinaryExpression node )
        {
            if ( node.NodeType == ExpressionType.Assign && node.Left is ParameterExpression param
                && _replacements.TryGetValue( param, out var replacement ) )
            {
                // Rewrite: Assign(param, value) -> Assign(box.Value, value)
                var newRight = Visit( node.Right );
                return Expression.Assign( replacement, newRight! );
            }

            return base.VisitBinary( node );
        }
    }
}
