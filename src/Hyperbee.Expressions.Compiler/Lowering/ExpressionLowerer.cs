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
    private readonly HashSet<ParameterExpression>? _capturedVariables;
    private readonly Func<LambdaExpression, Delegate>? _nestedCompiler;

    // Lazy-initialized maps: avoid allocation overhead for simple expressions
    private Dictionary<ParameterExpression, int>? _localMap;
    private Dictionary<LabelTarget, int>? _labelMap;
    private Dictionary<LabelTarget, int>? _labelValueLocalMap;
    private Dictionary<ParameterExpression, int>? _strongBoxLocalMap;
    private Dictionary<LambdaExpression, ClosureInfo>? _closureInfoMap;

    private int _argOffset;
    private bool _discardResult;

    /// <summary>
    /// Creates a new expression lowerer targeting the given IR builder.
    /// </summary>
    public ExpressionLowerer( IRBuilder ir )
        : this( ir, null, null )
    {
    }

    /// <summary>
    /// Creates a new expression lowerer targeting the given IR builder,
    /// with a set of captured variables that need StrongBox wrapping.
    /// </summary>
    public ExpressionLowerer( IRBuilder ir, HashSet<ParameterExpression>? capturedVariables )
        : this( ir, capturedVariables, null )
    {
    }

    /// <summary>
    /// Creates a new expression lowerer targeting the given IR builder,
    /// with a set of captured variables that need StrongBox wrapping,
    /// and an optional nested compiler for compiling nested lambdas.
    /// When <paramref name="nestedCompiler"/> is null, <see cref="LambdaExpression.Compile()"/> is used.
    /// </summary>
    public ExpressionLowerer( IRBuilder ir, HashSet<ParameterExpression>? capturedVariables, Func<LambdaExpression, Delegate>? nestedCompiler )
    {
        _ir = ir;
        _capturedVariables = capturedVariables;
        _nestedCompiler = nestedCompiler;
    }

    /// <summary>
    /// Lower a lambda expression into the IR builder.
    /// </summary>
    public void Lower( LambdaExpression lambda, int argOffset )
    {
        _argOffset = argOffset;

        for ( var i = 0; i < lambda.Parameters.Count; i++ )
        {
            var param = lambda.Parameters[i];
            _parameterMap[param] = i + _argOffset;

            // If this parameter is captured (e.g. by RuntimeVariables or a nested lambda),
            // it needs StrongBox wrapping. Load the arg value and store it into a StrongBox local.
            if ( IsCaptured( param ) )
            {
                var strongBoxType = typeof( StrongBox<> ).MakeGenericType( param.Type );
                var boxLocal = _ir.DeclareLocal( strongBoxType, $"$box_{param.Name}" );
                _strongBoxLocalMap ??= new( 2 );
                _strongBoxLocalMap[param] = boxLocal;

                // Emit: box = new StrongBox<T>(argValue)
                var ctor = strongBoxType.GetConstructor( [param.Type] )!;
                _ir.Emit( IROp.LoadArg, i + _argOffset );
                _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
                _ir.Emit( IROp.StoreLocal, boxLocal );
            }
        }

        var isVoidLambda = lambda.ReturnType == typeof( void );
        var bodyIsAssign = lambda.Body.NodeType == ExpressionType.Assign;

        // For void lambdas with a direct Assign body, suppress the result so the
        // Assign doesn't Dup+leave a value on the stack before Ret.
        if ( isVoidLambda && bodyIsAssign )
            _discardResult = true;

        LowerExpression( lambda.Body );
        _discardResult = false;

        // If a void lambda's non-Assign body produced a value, discard it.
        if ( isVoidLambda && lambda.Body.Type != typeof( void ) && !bodyIsAssign )
            _ir.Emit( IROp.Pop );

        _ir.Emit( IROp.Ret );
    }

    private bool IsCaptured( ParameterExpression variable )
    {
        return _capturedVariables != null && _capturedVariables.Contains( variable );
    }

    private void LowerExpression( Expression? node )
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
            case ExpressionType.OnesComplement:
            case ExpressionType.UnaryPlus:
            case ExpressionType.Increment:
            case ExpressionType.Decrement:
            case ExpressionType.IsTrue:
            case ExpressionType.IsFalse:
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

            // RuntimeVariables
            case ExpressionType.RuntimeVariables:
                LowerRuntimeVariables( (RuntimeVariablesExpression) node );
                break;

            // Dynamic expressions (DLR) are not supported.
            // This is a deliberate design choice: DynamicExpression requires the
            // Dynamic Language Runtime (DLR) infrastructure, which adds significant
            // overhead for a feature rarely used with compiled expression trees.
            case ExpressionType.Dynamic:
                throw new NotSupportedException(
                    "DynamicExpression is not supported by HyperbeeCompiler. " +
                    "Dynamic expressions require the DLR (Dynamic Language Runtime) " +
                    "infrastructure. Use Expression.Call() with explicit method " +
                    "bindings instead, or use the System compiler." );

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
            // Nullable<T> null constant → push default(Nullable<T>) (a zero-initialized struct), not ldnull.
            // ldnull produces an object ref; stelem and other value-type ops expect a struct on the stack.
            // CLR zeroes locals on declaration, so a fresh temp local already holds default(Nullable<T>).
            if ( Nullable.GetUnderlyingType( node.Type ) != null )
            {
                var tempLocal = _ir.DeclareLocal( node.Type, "$nullableDefault" );
                _ir.Emit( IROp.LoadLocal, tempLocal );
                return;
            }

            _ir.Emit( IROp.LoadNull );
            return;
        }

        // Expression.Constant(42, typeof(int?)) has Value=42 (int) but Type=int?.
        // We must push the underlying value then wrap it in Nullable<T>.
        var underlyingType = Nullable.GetUnderlyingType( node.Type );
        if ( underlyingType != null )
        {
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( node.Value ) );
            var ctor = node.Type.GetConstructor( [underlyingType] )!;
            _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
            return;
        }

        _ir.Emit( IROp.LoadConst, _ir.AddOperand( node.Value ) );
    }

    private void LowerParameter( ParameterExpression node )
    {
        // Captured variable -- load through StrongBox<T>.Value
        if ( IsCaptured( node ) && _strongBoxLocalMap?.ContainsKey( node ) == true )
        {
            EmitLoadCapturedValue( node );
            return;
        }

        if ( _parameterMap.TryGetValue( node, out var argIndex ) )
        {
            _ir.Emit( IROp.LoadArg, argIndex );
        }
        else if ( _localMap != null && _localMap.TryGetValue( node, out var localIndex ) )
        {
            _ir.Emit( IROp.LoadLocal, localIndex );
        }
        else
        {
            // Variable not yet declared -- declare as local
            var local = _ir.DeclareLocal( node.Type, node.Name );
            ( _localMap ??= new( 8 ) )[node] = local;
            _ir.Emit( IROp.LoadLocal, local );
        }
    }

    private void LowerBinary( BinaryExpression node )
    {
        // Check for lifted nullable operations first.
        // When IsLifted is true, even if node.Method is set (e.g. decimal operators, Math.Pow),
        // the operands are nullable and we must use the lifted null-propagation path.
        var leftUnderlying = Nullable.GetUnderlyingType( node.Left.Type );

        if ( leftUnderlying != null )
        {
            LowerLiftedBinary( node, leftUnderlying );
            return;
        }

        // Non-lifted operator overload -- emit as direct method call
        if ( node.Method != null )
        {
            LowerExpression( node.Left );
            LowerExpression( node.Right );
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            return;
        }

        LowerExpression( node.Left );
        LowerExpression( node.Right );
        EmitBinaryOp( node.NodeType, node.Left.Type );
    }

    private void EmitBinaryOp( ExpressionType nodeType, Type leftType )
    {
        switch ( nodeType )
        {
            case ExpressionType.Add:
                _ir.Emit( IROp.Add );
                break;
            case ExpressionType.AddChecked:
                _ir.Emit( IsUnsigned( leftType ) ? IROp.AddCheckedUn : IROp.AddChecked );
                break;
            case ExpressionType.Subtract:
                _ir.Emit( IROp.Sub );
                break;
            case ExpressionType.SubtractChecked:
                _ir.Emit( IsUnsigned( leftType ) ? IROp.SubCheckedUn : IROp.SubChecked );
                break;
            case ExpressionType.Multiply:
                _ir.Emit( IROp.Mul );
                break;
            case ExpressionType.MultiplyChecked:
                _ir.Emit( IsUnsigned( leftType ) ? IROp.MulCheckedUn : IROp.MulChecked );
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
                _ir.Emit( IsUnsigned( leftType ) ? IROp.RightShiftUn : IROp.RightShift );
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
                // For floats: clt (ordered) returns false when either operand is NaN — correct behavior
                // For unsigned: clt.un for proper unsigned comparison
                _ir.Emit( IsUnsigned( leftType ) ? IROp.CltUn : IROp.Clt );
                break;
            case ExpressionType.GreaterThan:
                // For floats: cgt (ordered) returns false when either operand is NaN — correct behavior
                // For unsigned: cgt.un for proper unsigned comparison
                _ir.Emit( IsUnsigned( leftType ) ? IROp.CgtUn : IROp.Cgt );
                break;
            case ExpressionType.LessThanOrEqual:
                // cgt.un: for float (NaN returns false) and unsigned types
                _ir.Emit( IsUnsignedOrFloat( leftType ) ? IROp.CgtUn : IROp.Cgt );
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( 0 ) );
                _ir.Emit( IROp.Ceq );
                break;
            case ExpressionType.GreaterThanOrEqual:
                // clt.un: for float (NaN returns false) and unsigned types
                _ir.Emit( IsUnsignedOrFloat( leftType ) ? IROp.CltUn : IROp.Clt );
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( 0 ) );
                _ir.Emit( IROp.Ceq );
                break;
            default:
                throw new NotSupportedException( $"Binary op {nodeType} is not supported." );
        }
    }

    private void LowerLiftedBinary( BinaryExpression node, Type underlyingType )
    {
        var leftNullableType = node.Left.Type;
        var hasValueGetterA = leftNullableType.GetProperty( "HasValue" )!.GetGetMethod()!;
        var getValueOrDefaultA = leftNullableType.GetMethod( "GetValueOrDefault", Type.EmptyTypes )!;

        // Right operand may have a different nullable type (e.g., shifts: long? << int?)
        var rightNullableType = node.Right.Type;
        var hasValueGetterB = rightNullableType.GetProperty( "HasValue" )!.GetGetMethod()!;
        var getValueOrDefaultB = rightNullableType.GetMethod( "GetValueOrDefault", Type.EmptyTypes )!;

        // Store operands into temp locals using their correct types
        var tempA = _ir.DeclareLocal( leftNullableType, "$liftA" );
        var tempB = _ir.DeclareLocal( rightNullableType, "$liftB" );

        LowerExpression( node.Left );
        _ir.Emit( IROp.StoreLocal, tempA );
        LowerExpression( node.Right );
        _ir.Emit( IROp.StoreLocal, tempB );

        var isComparison = node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
            or ExpressionType.LessThan or ExpressionType.GreaterThan
            or ExpressionType.LessThanOrEqual or ExpressionType.GreaterThanOrEqual;

        var isEqualityOp = node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual;

        if ( isComparison && !node.IsLiftedToNull )
        {
            // Lifted comparison returning bool (not bool?)
            LowerLiftedComparison( node, underlyingType, tempA, tempB,
                hasValueGetterA, getValueOrDefaultA,
                hasValueGetterB, getValueOrDefaultB,
                isEqualityOp );
        }
        else
        {
            // Lifted arithmetic returning Nullable<T>
            LowerLiftedArithmetic( node, underlyingType, leftNullableType, tempA, tempB,
                hasValueGetterA, getValueOrDefaultA,
                hasValueGetterB, getValueOrDefaultB );
        }
    }

    private void LowerLiftedComparison(
        BinaryExpression node, Type underlyingType,
        int tempA, int tempB,
        System.Reflection.MethodInfo hasValueGetterA,
        System.Reflection.MethodInfo getValueOrDefaultA,
        System.Reflection.MethodInfo hasValueGetterB,
        System.Reflection.MethodInfo getValueOrDefaultB,
        bool isEqualityOp )
    {
        var resultLocal = _ir.DeclareLocal( typeof( bool ), "$liftCmpResult" );
        var endLabel = _ir.DefineLabel();

        if ( isEqualityOp )
        {
            // Equality/inequality: null==null is true, null!=null is false
            var compareLabel = _ir.DefineLabel();
            var mismatchLabel = _ir.DefineLabel();
            var bothNullLabel = _ir.DefineLabel();
            var hasALocal = _ir.DeclareLocal( typeof( bool ), "$hasA" );
            var hasBLocal = _ir.DeclareLocal( typeof( bool ), "$hasB" );

            // hasA = tempA.HasValue
            _ir.Emit( IROp.LoadAddress, tempA );
            _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterA ) );
            _ir.Emit( IROp.StoreLocal, hasALocal );

            // hasB = tempB.HasValue
            _ir.Emit( IROp.LoadAddress, tempB );
            _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterB ) );
            _ir.Emit( IROp.StoreLocal, hasBLocal );

            // if (hasA != hasB) → one null, one not → mismatch
            _ir.Emit( IROp.LoadLocal, hasALocal );
            _ir.Emit( IROp.LoadLocal, hasBLocal );
            _ir.Emit( IROp.Ceq );
            _ir.Emit( IROp.BranchFalse, mismatchLabel );

            // hasA == hasB: if !hasA → both null
            _ir.Emit( IROp.LoadLocal, hasALocal );
            _ir.Emit( IROp.BranchFalse, bothNullLabel );

            // Both have values: compare
            _ir.MarkLabel( compareLabel );
            _ir.Emit( IROp.LoadAddress, tempA );
            _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultA ) );
            _ir.Emit( IROp.LoadAddress, tempB );
            _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultB ) );
            EmitBinaryOp( node.NodeType, underlyingType );
            _ir.Emit( IROp.StoreLocal, resultLocal );
            _ir.Emit( IROp.Branch, endLabel );

            // mismatchLabel: one null one not → Equal:false, NotEqual:true
            _ir.MarkLabel( mismatchLabel );
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( node.NodeType == ExpressionType.NotEqual ? 1 : 0 ) );
            _ir.Emit( IROp.StoreLocal, resultLocal );
            _ir.Emit( IROp.Branch, endLabel );

            // bothNullLabel: null==null → true, null!=null → false
            _ir.MarkLabel( bothNullLabel );
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( node.NodeType == ExpressionType.Equal ? 1 : 0 ) );
            _ir.Emit( IROp.StoreLocal, resultLocal );
        }
        else
        {
            // Relational: any null -> false (resultLocal starts as 0/false)
            var falseLabel = _ir.DefineLabel();

            _ir.Emit( IROp.LoadAddress, tempA );
            _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterA ) );
            _ir.Emit( IROp.BranchFalse, falseLabel );

            _ir.Emit( IROp.LoadAddress, tempB );
            _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterB ) );
            _ir.Emit( IROp.BranchFalse, falseLabel );

            // Both have values: compare
            _ir.Emit( IROp.LoadAddress, tempA );
            _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultA ) );
            _ir.Emit( IROp.LoadAddress, tempB );
            _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultB ) );
            EmitBinaryOp( node.NodeType, underlyingType );
            _ir.Emit( IROp.StoreLocal, resultLocal );

            // falseLabel: resultLocal is already 0 (false)
            _ir.MarkLabel( falseLabel );
        }

        // endLabel: push result
        _ir.MarkLabel( endLabel );
        _ir.Emit( IROp.LoadLocal, resultLocal );
    }

    private void LowerLiftedArithmetic(
        BinaryExpression node, Type underlyingType, Type nullableType,
        int tempA, int tempB,
        System.Reflection.MethodInfo hasValueGetterA,
        System.Reflection.MethodInfo getValueOrDefaultA,
        System.Reflection.MethodInfo hasValueGetterB,
        System.Reflection.MethodInfo getValueOrDefaultB )
    {
        // bool? & bool? and bool? | bool? use three-valued SQL-like logic:
        //   false & null = false,  null & false = false  (false dominates And)
        //   true  | null = true,   null | true  = true   (true  dominates Or)
        if ( underlyingType == typeof( bool ) && node.Method == null &&
             node.NodeType is ExpressionType.And or ExpressionType.Or )
        {
            LowerLiftedBoolLogic( node.NodeType, nullableType, tempA, tempB,
                hasValueGetterA, getValueOrDefaultA, hasValueGetterB, getValueOrDefaultB );
            return;
        }

        var endLabel = _ir.DefineLabel();
        var resultLocal = _ir.DeclareLocal( nullableType, "$liftResult" );

        // resultLocal starts as default(Nullable<T>) = null (CLR zero-init)

        // if (!tempA.HasValue) goto endLabel (result stays null)
        _ir.Emit( IROp.LoadAddress, tempA );
        _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterA ) );
        _ir.Emit( IROp.BranchFalse, endLabel );

        // if (!tempB.HasValue) goto endLabel (result stays null)
        _ir.Emit( IROp.LoadAddress, tempB );
        _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterB ) );
        _ir.Emit( IROp.BranchFalse, endLabel );

        // Both have values: extract, apply op, wrap
        _ir.Emit( IROp.LoadAddress, tempA );
        _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultA ) );
        _ir.Emit( IROp.LoadAddress, tempB );
        _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultB ) );

        // When a method override exists (e.g., decimal operators, Math.Pow), call it directly.
        // Otherwise use the standard IL opcode.
        if ( node.Method != null )
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
        else
            EmitBinaryOp( node.NodeType, underlyingType );

        // Wrap result: new Nullable<T>(result)
        var ctor = nullableType.GetConstructor( [underlyingType] )!;
        _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
        _ir.Emit( IROp.StoreLocal, resultLocal );

        // endLabel: push result (either computed or default null)
        _ir.MarkLabel( endLabel );
        _ir.Emit( IROp.LoadLocal, resultLocal );
    }

    private void LowerLiftedBoolLogic(
        ExpressionType nodeType,
        Type nullableType,
        int tempA, int tempB,
        System.Reflection.MethodInfo hasValueGetterA,
        System.Reflection.MethodInfo getValueOrDefaultA,
        System.Reflection.MethodInfo hasValueGetterB,
        System.Reflection.MethodInfo getValueOrDefaultB )
    {
        // Three-valued logic for bool? & bool? and bool? | bool?:
        //   And: if either is known-false  → result = false (dominates)
        //        else if both non-null     → result = a & b (must both be true)
        //        else                      → result = null
        //   Or:  if either is known-true   → result = true  (dominates)
        //        else if both non-null     → result = a | b (must both be false)
        //        else                      → result = null

        var resultLocal = _ir.DeclareLocal( nullableType, "$liftBoolResult" );
        var endLabel = _ir.DefineLabel();
        var checkBLabel = _ir.DefineLabel();
        var checkBothLabel = _ir.DefineLabel();
        var ctor = nullableType.GetConstructor( [typeof( bool )] )!;

        bool isAnd = nodeType == ExpressionType.And;

        // --- Phase 1: check if A is the dominating value ---
        // For And: dominating = false (HasValue && !GetValueOrDefault)
        // For Or:  dominating = true  (HasValue &&  GetValueOrDefault)

        // if (!tempA.HasValue) goto checkBLabel  (A is null, can't dominate)
        _ir.Emit( IROp.LoadAddress, tempA );
        _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterA ) );
        _ir.Emit( IROp.BranchFalse, checkBLabel );

        // Load A's value
        _ir.Emit( IROp.LoadAddress, tempA );
        _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultA ) );

        // For And: branch to checkBLabel if A is true (not dominating)
        // For Or:  branch to checkBLabel if A is false (not dominating)
        if ( isAnd )
            _ir.Emit( IROp.BranchTrue, checkBLabel );
        else
            _ir.Emit( IROp.BranchFalse, checkBLabel );

        // A is the dominating value → result = new bool?(dominatingBool)
        _ir.Emit( IROp.LoadConst, _ir.AddOperand( isAnd ? 0 : 1 ) );
        _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
        _ir.Emit( IROp.StoreLocal, resultLocal );
        _ir.Emit( IROp.Branch, endLabel );

        // --- Phase 2: check if B is the dominating value ---
        _ir.MarkLabel( checkBLabel );

        // if (!tempB.HasValue) goto checkBothLabel (B is null, can't dominate)
        _ir.Emit( IROp.LoadAddress, tempB );
        _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterB ) );
        _ir.Emit( IROp.BranchFalse, checkBothLabel );

        // Load B's value
        _ir.Emit( IROp.LoadAddress, tempB );
        _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultB ) );

        // For And: branch to checkBothLabel if B is true (not dominating)
        // For Or:  branch to checkBothLabel if B is false (not dominating)
        if ( isAnd )
            _ir.Emit( IROp.BranchTrue, checkBothLabel );
        else
            _ir.Emit( IROp.BranchFalse, checkBothLabel );

        // B is the dominating value → result = new bool?(dominatingBool)
        _ir.Emit( IROp.LoadConst, _ir.AddOperand( isAnd ? 0 : 1 ) );
        _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
        _ir.Emit( IROp.StoreLocal, resultLocal );
        _ir.Emit( IROp.Branch, endLabel );

        // --- Phase 3: neither is dominating ---
        // A is either null or non-dominating; B is either null or non-dominating.
        // If both have values → both are non-dominating → apply the op (result = a & b or a | b)
        // If either is null   → result = null (stays default)
        _ir.MarkLabel( checkBothLabel );

        // if (!tempA.HasValue) goto endLabel (result stays null)
        _ir.Emit( IROp.LoadAddress, tempA );
        _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterA ) );
        _ir.Emit( IROp.BranchFalse, endLabel );

        // if (!tempB.HasValue) goto endLabel (result stays null)
        _ir.Emit( IROp.LoadAddress, tempB );
        _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetterB ) );
        _ir.Emit( IROp.BranchFalse, endLabel );

        // Both non-null and non-dominating → compute a op b
        _ir.Emit( IROp.LoadAddress, tempA );
        _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultA ) );
        _ir.Emit( IROp.LoadAddress, tempB );
        _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefaultB ) );
        _ir.Emit( isAnd ? IROp.And : IROp.Or );
        _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
        _ir.Emit( IROp.StoreLocal, resultLocal );

        _ir.MarkLabel( endLabel );
        _ir.Emit( IROp.LoadLocal, resultLocal );
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

        // Short-circuit: if left is false, leave it on the stack and skip right.
        // Dup the left value so BranchFalse can consume one copy while the other remains.
        var endLabel = _ir.DefineLabel();

        LowerExpression( node.Left );
        _ir.Emit( IROp.Dup );                    // [left, left]
        _ir.Emit( IROp.BranchFalse, endLabel );  // false → [left] on stack, jump to end
        _ir.Emit( IROp.Pop );                    // left was true → discard it
        LowerExpression( node.Right );           // result is right

        _ir.MarkLabel( endLabel );
        // Result (left=false or right) is on the stack.
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

        // Short-circuit: if left is true, leave it on the stack and skip right.
        // Dup the left value so BranchTrue can consume one copy while the other remains.
        var endLabel = _ir.DefineLabel();

        LowerExpression( node.Left );
        _ir.Emit( IROp.Dup );                   // [left, left]
        _ir.Emit( IROp.BranchTrue, endLabel );  // true → [left] on stack, jump to end
        _ir.Emit( IROp.Pop );                   // left was false → discard it
        LowerExpression( node.Right );          // result is right

        _ir.MarkLabel( endLabel );
        // Result (left=true or right) is on the stack.
    }

    private void LowerUnary( UnaryExpression node )
    {
        // Check for lifted nullable operations BEFORE checking node.Method.
        // When the operand is nullable (e.g., Negate(decimal?)), node.Method may point
        // to the non-nullable underlying method (e.g., decimal.op_UnaryNegation(decimal)).
        // We must go through the lifted null-propagation path instead of calling the method directly.
        var operandUnderlying = Nullable.GetUnderlyingType( node.Operand.Type );

        if ( operandUnderlying != null && Nullable.GetUnderlyingType( node.Type ) != null )
        {
            LowerLiftedUnary( node, operandUnderlying );
            return;
        }

        // Non-nullable operator overload
        if ( node.Method != null )
        {
            LowerExpression( node.Operand );
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            return;
        }

        LowerExpression( node.Operand );
        EmitUnaryOp( node );
    }

    private void EmitUnaryOp( UnaryExpression node )
    {
        switch ( node.NodeType )
        {
            case ExpressionType.Negate:
                _ir.Emit( IROp.Negate );
                break;
            case ExpressionType.NegateChecked:
            {
                // Checked negate: 0 - value with overflow detection.
                // The operand is already on the stack. Store to temp, push 0, reload, sub.ovf.
                var temp = _ir.DeclareLocal( node.Type, "$neg_temp" );
                _ir.Emit( IROp.StoreLocal, temp );
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( GetZeroForType( node.Type ) ) );
                _ir.Emit( IROp.LoadLocal, temp );
                _ir.Emit( IROp.SubChecked );
                break;
            }
            case ExpressionType.Not:
                if ( node.Type == typeof( bool ) )
                {
                    // Boolean Not: ldc.i4.0 + ceq (true→false, false→true)
                    // Bitwise 'not' on bool(1) gives 0xFFFFFFFE which is still truthy.
                    _ir.Emit( IROp.LoadConst, _ir.AddOperand( 0 ) );
                    _ir.Emit( IROp.Ceq );
                }
                else
                {
                    _ir.Emit( IROp.Not );
                }
                break;
            case ExpressionType.OnesComplement:
                _ir.Emit( IROp.Not );
                break;
            case ExpressionType.UnaryPlus:
                // No-op: value is already on the stack
                break;
            case ExpressionType.Increment:
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( GetOneForType( node.Type ) ) );
                _ir.Emit( IROp.Add );
                break;
            case ExpressionType.Decrement:
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( GetOneForType( node.Type ) ) );
                _ir.Emit( IROp.Sub );
                break;
            case ExpressionType.IsTrue:
                // bool value is already on the stack as 0 or 1; no-op
                break;
            case ExpressionType.IsFalse:
                // Negate: false (0) → true (1), true (1) → false (0)
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( 0 ) );
                _ir.Emit( IROp.Ceq );
                break;
            default:
                throw new NotSupportedException( $"Unary op {node.NodeType} is not supported." );
        }
    }

    private void LowerLiftedUnary( UnaryExpression node, Type underlyingType )
    {
        var nullableType = node.Operand.Type;
        var hasValueGetter = nullableType.GetProperty( "HasValue" )!.GetGetMethod()!;
        var getValueOrDefault = nullableType.GetMethod( "GetValueOrDefault", Type.EmptyTypes )!;

        var endLabel = _ir.DefineLabel();
        var resultLocal = _ir.DeclareLocal( nullableType, "$liftResult" );
        var tempOperand = _ir.DeclareLocal( nullableType, "$liftOp" );

        // resultLocal starts as default(Nullable<T>) = null (CLR zero-init)

        // Store operand
        LowerExpression( node.Operand );
        _ir.Emit( IROp.StoreLocal, tempOperand );

        // if (!operand.HasValue) goto endLabel (result stays null)
        _ir.Emit( IROp.LoadAddress, tempOperand );
        _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetter ) );
        _ir.Emit( IROp.BranchFalse, endLabel );

        // Has value: extract, apply op, wrap
        _ir.Emit( IROp.LoadAddress, tempOperand );
        _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefault ) );

        // When a method override exists (e.g., decimal.op_UnaryNegation), call it directly
        // on the extracted underlying value. Otherwise use the standard IL opcode.
        if ( node.Method != null )
        {
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
        }
        else
        {
            // Emit the underlying unary operation on the extracted value
            switch ( node.NodeType )
            {
                case ExpressionType.Negate:
                    _ir.Emit( IROp.Negate );
                    break;
                case ExpressionType.NegateChecked:
                {
                    var temp = _ir.DeclareLocal( underlyingType, "$neg_temp" );
                    _ir.Emit( IROp.StoreLocal, temp );
                    _ir.Emit( IROp.LoadConst, _ir.AddOperand( GetZeroForType( underlyingType ) ) );
                    _ir.Emit( IROp.LoadLocal, temp );
                    _ir.Emit( IROp.SubChecked );
                    break;
                }
                case ExpressionType.Not:
                    if ( underlyingType == typeof( bool ) )
                    {
                        _ir.Emit( IROp.LoadConst, _ir.AddOperand( 0 ) );
                        _ir.Emit( IROp.Ceq );
                    }
                    else
                    {
                        _ir.Emit( IROp.Not );
                    }
                    break;
                case ExpressionType.OnesComplement:
                    _ir.Emit( IROp.Not );
                    break;
                case ExpressionType.UnaryPlus:
                    // No-op
                    break;
                case ExpressionType.IsTrue:
                    // bool value is already on stack (0 or 1); no-op
                    break;
                case ExpressionType.IsFalse:
                    // Negate: false (0) → true (1), true (1) → false (0)
                    _ir.Emit( IROp.LoadConst, _ir.AddOperand( 0 ) );
                    _ir.Emit( IROp.Ceq );
                    break;
                default:
                    throw new NotSupportedException( $"Lifted unary op {node.NodeType} is not supported." );
            }
        }

        // Wrap result: new Nullable<T>(result)
        var ctor = nullableType.GetConstructor( [underlyingType] )!;
        _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
        _ir.Emit( IROp.StoreLocal, resultLocal );

        // endLabel: push result (either computed or default null)
        _ir.MarkLabel( endLabel );
        _ir.Emit( IROp.LoadLocal, resultLocal );
    }

    private void LowerConvert( UnaryExpression node )
    {
        var sourceType = node.Operand.Type;
        var targetType = node.Type;

        var sourceUnderlying = Nullable.GetUnderlyingType( sourceType );
        var targetUnderlying = Nullable.GetUnderlyingType( targetType );

        // Nullable<S> -> Nullable<T>: null-propagating conversion
        if ( sourceUnderlying != null && targetUnderlying != null )
        {
            LowerNullableToNullableConvert( node, sourceType, targetType, sourceUnderlying, targetUnderlying );
            return;
        }

        LowerExpression( node.Operand );

        // Method-based conversion (e.g., user-defined implicit/explicit operators).
        // If the method returns the underlying type but target is Nullable<T>, wrap the result.
        if ( node.Method != null )
        {
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
            if ( targetUnderlying != null && node.Method.ReturnType == targetUnderlying )
            {
                var ctor = targetType.GetConstructor( [targetUnderlying] )!;
                _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
            }
            return;
        }

        // Identity conversion -- no-op
        if ( sourceType == targetType )
            return;

        // Nullable<T> -> T: call Nullable<T>.get_Value()
        if ( sourceUnderlying != null && targetType == sourceUnderlying )
        {
            var temp = _ir.DeclareLocal( sourceType, "$nullable_temp" );
            _ir.Emit( IROp.StoreLocal, temp );
            _ir.Emit( IROp.LoadAddress, temp );
            var getValueMethod = sourceType.GetProperty( "Value" )!.GetGetMethod()!;
            _ir.Emit( IROp.Call, _ir.AddOperand( getValueMethod ) );
            return;
        }

        // T -> Nullable<T>: call new Nullable<T>(T)
        if ( targetUnderlying != null && sourceType == targetUnderlying )
        {
            var ctor = targetType.GetConstructor( [targetUnderlying] )!;
            _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
            return;
        }

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

        // Enum conversions: resolve to underlying type for primitive conversion
        var effectiveSource = sourceType.IsEnum ? Enum.GetUnderlyingType( sourceType ) : sourceType;
        var effectiveTarget = targetType.IsEnum ? Enum.GetUnderlyingType( targetType ) : targetType;

        if ( effectiveSource == effectiveTarget )
            return; // Same underlying representation (e.g., int <-> DayOfWeek)

        // Primitive conversions: value type -> value type.
        // For ConvertChecked from unsigned source, use Conv_Ovf_X_Un opcodes.
        IROp op;
        if ( node.NodeType == ExpressionType.ConvertChecked )
            op = IsUnsigned( effectiveSource ) ? IROp.ConvertCheckedUn : IROp.ConvertChecked;
        else
            op = IROp.Convert;

        _ir.Emit( op, _ir.AddOperand( effectiveTarget ) );
    }

    private void LowerNullableToNullableConvert(
        UnaryExpression node,
        Type sourceNullable, Type targetNullable,
        Type sourceUnderlying, Type targetUnderlying )
    {
        var endLabel = _ir.DefineLabel();
        var srcLocal = _ir.DeclareLocal( sourceNullable, "$convSrc" );
        var resultLocal = _ir.DeclareLocal( targetNullable, "$convResult" );

        LowerExpression( node.Operand );
        _ir.Emit( IROp.StoreLocal, srcLocal );

        var hasValueGetter = sourceNullable.GetProperty( "HasValue" )!.GetGetMethod()!;
        var getValueOrDefault = sourceNullable.GetMethod( "GetValueOrDefault", Type.EmptyTypes )!;

        // if (!src.HasValue) goto endLabel (result stays null)
        _ir.Emit( IROp.LoadAddress, srcLocal );
        _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetter ) );
        _ir.Emit( IROp.BranchFalse, endLabel );

        // Has value: extract, convert, wrap
        _ir.Emit( IROp.LoadAddress, srcLocal );
        _ir.Emit( IROp.Call, _ir.AddOperand( getValueOrDefault ) );

        if ( node.Method != null )
        {
            _ir.Emit( IROp.Call, _ir.AddOperand( node.Method ) );
        }
        else if ( sourceUnderlying != targetUnderlying )
        {
            IROp op;
            if ( node.NodeType == ExpressionType.ConvertChecked )
                op = IsUnsigned( sourceUnderlying ) ? IROp.ConvertCheckedUn : IROp.ConvertChecked;
            else
                op = IROp.Convert;
            _ir.Emit( op, _ir.AddOperand( targetUnderlying ) );
        }

        // Wrap in Nullable<T>
        var ctor = targetNullable.GetConstructor( [targetUnderlying] )!;
        _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
        _ir.Emit( IROp.StoreLocal, resultLocal );

        _ir.MarkLabel( endLabel );
        _ir.Emit( IROp.LoadLocal, resultLocal );
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
        var isValueTypeInstance = node.Object != null && node.Object.Type.IsValueType;
        var needsConstrained = isValueTypeInstance && node.Method.IsVirtual;

        if ( node.Object != null )
        {
            if ( isValueTypeInstance )
            {
                // All value-type instance calls need a managed pointer (byref) on stack
                EmitLoadAddress( node.Object );
            }
            else
            {
                LowerExpression( node.Object );
            }
        }

        var parameters = node.Method.GetParameters();
        for ( var i = 0; i < node.Arguments.Count; i++ )
        {
            var arg = node.Arguments[i];
            var isByRef = i < parameters.Length && parameters[i].ParameterType.IsByRef;
            if ( isByRef )
                EmitLoadAddress( arg );
            else
                LowerExpression( arg );
        }

        if ( needsConstrained )
        {
            _ir.Emit( IROp.Constrained, _ir.AddOperand( node.Object!.Type ) );
            _ir.Emit( IROp.CallVirt, _ir.AddOperand( node.Method ) );
        }
        else
        {
            _ir.Emit(
                node.Method.IsVirtual ? IROp.CallVirt : IROp.Call,
                _ir.AddOperand( node.Method ) );
        }
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

            if ( !isVoidConditional )
            {
                // Both branches leave their result on the stack at endLabel.
                // CIL allows a consistent non-zero stack depth at merge points.
                _ir.Emit( IROp.Branch, endLabel );

                _ir.MarkLabel( falseLabel );
                LowerExpression( node.IfFalse );

                _ir.MarkLabel( endLabel );
                // Result is on the stack from whichever branch was taken.
            }
            else
            {
                _ir.Emit( IROp.Branch, endLabel );

                _ir.MarkLabel( falseLabel );
                LowerExpression( node.IfFalse );
                if ( node.IfFalse.Type != typeof( void ) )
                {
                    _ir.Emit( IROp.Pop );
                }

                _ir.MarkLabel( endLabel );
            }
        }
    }

    /// <summary>
    /// Emit the address of an expression onto the evaluation stack, suitable for passing
    /// as a <c>ref</c>/<c>out</c>/<c>in</c> argument or for constrained virtual calls.
    /// </summary>
    private void EmitLoadAddress( Expression node )
    {
        switch ( node )
        {
            case ParameterExpression param when _localMap != null && _localMap.TryGetValue( param, out var localIndex ):
                _ir.Emit( IROp.LoadAddress, localIndex );
                return;

            case ParameterExpression param when _parameterMap.TryGetValue( param, out var argIndex ):
                // Byref parameters already hold a managed pointer — ldarg loads it directly.
                // Non-byref: ldarga loads the address of the value in the argument slot.
                if ( param.IsByRef )
                    _ir.Emit( IROp.LoadArg, argIndex );
                else
                    _ir.Emit( IROp.LoadArgAddress, argIndex );
                return;

            case MemberExpression { Member: FieldInfo fieldInfo } memberExpr when !fieldInfo.IsStatic:
                // Push instance pointer then ldflda to get managed pointer to the field.
                EmitInstancePointer( memberExpr.Expression! );
                _ir.Emit( IROp.LoadFieldAddress, _ir.AddOperand( fieldInfo ) );
                return;

            case BinaryExpression { NodeType: ExpressionType.ArrayIndex } ai when ai.Type.IsValueType:
                // Struct array element: emit ldelema (managed pointer) instead of ldelem (value copy).
                // Necessary for both instance method calls and field assignments on the element.
                LowerExpression( ai.Left );
                LowerExpression( ai.Right );
                _ir.Emit( IROp.LoadElementAddress, _ir.AddOperand( ai.Type ) );
                return;

            default:
                // Complex expression: lower it, store to a temp local, load address of temp.
                LowerExpression( node );
                var temp = _ir.DeclareLocal( node.Type, "$addr_temp" );
                _ir.Emit( IROp.StoreLocal, temp );
                _ir.Emit( IROp.LoadAddress, temp );
                return;
        }
    }

    /// <summary>
    /// Emit a managed pointer or object reference for use as the instance operand of a
    /// field-address instruction (<c>ldflda</c>). For value types this is a managed pointer;
    /// for reference types this is the object reference.
    /// </summary>
    private void EmitInstancePointer( Expression instance )
    {
        switch ( instance )
        {
            case ParameterExpression param when _localMap != null && _localMap.TryGetValue( param, out var localIndex ):
                if ( param.Type.IsValueType )
                    _ir.Emit( IROp.LoadAddress, localIndex );  // ldloca — managed pointer to struct
                else
                    _ir.Emit( IROp.LoadLocal, localIndex );    // ldloc — object reference
                return;

            case ParameterExpression param when _parameterMap.TryGetValue( param, out var argIndex ):
                // Byref args carry a managed pointer; reference-type args carry an object reference.
                // In both cases ldarg is correct. Only non-byref value-type args need ldarga.
                if ( param.IsByRef || !param.Type.IsValueType )
                    _ir.Emit( IROp.LoadArg, argIndex );
                else
                    _ir.Emit( IROp.LoadArgAddress, argIndex );
                return;

            default:
                // For reference types: lowering yields the object reference — ldflda works on it.
                // For value types: spill to a temp and take its address.
                LowerExpression( instance );
                if ( instance.Type.IsValueType )
                {
                    var temp = _ir.DeclareLocal( instance.Type, "$inst_ptr" );
                    _ir.Emit( IROp.StoreLocal, temp );
                    _ir.Emit( IROp.LoadAddress, temp );
                }
                return;
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
            else if ( node.Expression!.Type.IsValueType )
            {
                // Value-type instance calls need a managed pointer (byref).
                // Virtual calls use constrained prefix; non-virtual use plain call.
                EmitLoadAddress( node.Expression! );

                if ( getter.IsVirtual )
                {
                    _ir.Emit( IROp.Constrained, _ir.AddOperand( node.Expression!.Type ) );
                    _ir.Emit( IROp.CallVirt, _ir.AddOperand( getter ) );
                }
                else
                {
                    _ir.Emit( IROp.Call, _ir.AddOperand( getter ) );
                }
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
            // Value type default construction (no constructor).
            // CLR zeros locals on declaration.
            var temp = _ir.DeclareLocal( node.Type, "$newDefault" );
            _ir.Emit( IROp.LoadLocal, temp );
            return;
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
                _strongBoxLocalMap ??= new( 2 );
                _strongBoxLocalMap[variable] = boxLocal;

                // Emit: new StrongBox<T>() and store
                var ctor = strongBoxType.GetConstructor( Type.EmptyTypes )!;
                _ir.Emit( IROp.NewObj, _ir.AddOperand( ctor ) );
                _ir.Emit( IROp.StoreLocal, boxLocal );
            }
            else
            {
                var local = _ir.DeclareLocal( variable.Type, variable.Name );
                ( _localMap ??= new( 8 ) )[variable] = local;
            }
        }

        // Lower all expressions in the block
        for ( var i = 0; i < node.Expressions.Count; i++ )
        {
            var isLast = i == node.Expressions.Count - 1;
            var expr = node.Expressions[i];

            // Suppress the result value for:
            //   - any non-last Assign (result is immediately discarded)
            //   - the last Assign in a void block (result is not the block's return value)
            // This avoids the Dup that LowerAssign emits when needsResult=true, saving
            // the otherwise-redundant Dup + Pop pair.
            var suppressAssign = expr.NodeType == ExpressionType.Assign &&
                ( !isLast || node.Type == typeof( void ) );

            if ( suppressAssign )
            {
                _discardResult = true;
                LowerExpression( expr );
                _discardResult = false;
            }
            else
            {
                LowerExpression( expr );

                // All expressions except the last have their result discarded
                if ( !isLast && expr.Type != typeof( void ) )
                {
                    _ir.Emit( IROp.Pop );
                }
            }
        }

        // If the block has an explicit void type but the last non-Assign expression produces a value,
        // discard it so the stack stays balanced. (Assigns in void blocks are suppressed above.)
        var lastExpr = node.Expressions.Count > 0 ? node.Expressions[^1] : null;
        if ( node.Type == typeof( void ) && lastExpr != null
            && lastExpr.Type != typeof( void )
            && lastExpr.NodeType != ExpressionType.Assign )
        {
            _ir.Emit( IROp.Pop );
        }

        _ir.ExitScope();
    }

    private void LowerAssign( BinaryExpression node )
    {
        var needsResult = !_discardResult;

        // Reset _discardResult so that nested assignments used as the RHS correctly produce
        // a value on the stack. The outer discard decision is already captured in needsResult.
        _discardResult = false;

        // The left side must be a ParameterExpression (variable)
        if ( node.Left is ParameterExpression variable )
        {
            // Captured variable -- store through StrongBox<T>.Value
            if ( IsCaptured( variable ) && _strongBoxLocalMap?.ContainsKey( variable ) == true )
            {
                EmitStoreCapturedValue( variable, node.Right, needsResult );
                return;
            }

            LowerExpression( node.Right );

            if ( needsResult )
                _ir.Emit( IROp.Dup );

            if ( _localMap != null && _localMap.TryGetValue( variable, out var localIndex ) )
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
                ( _localMap ??= new( 8 ) )[variable] = local;
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
                    if ( needsResult )
                        _ir.Emit( IROp.Dup );
                    _ir.Emit( IROp.StoreStaticField, _ir.AddOperand( field ) );
                }
                else
                {
                    // For struct (value-type) array elements, we need a managed pointer
                    // (ldelema) rather than a value (ldelem) so that stfld can write back.
                    EmitInstanceForFieldAssign( member.Expression! );
                    LowerExpression( node.Right );

                    if ( needsResult )
                    {
                        // Need the result: use temp to preserve value across stfld
                        var temp = _ir.DeclareLocal( node.Right.Type, "$fld_assign" );
                        _ir.Emit( IROp.Dup );
                        _ir.Emit( IROp.StoreLocal, temp );
                        _ir.Emit( IROp.StoreField, _ir.AddOperand( field ) );
                        _ir.Emit( IROp.LoadLocal, temp );
                    }
                    else
                    {
                        // Statement position: just store, no result needed
                        _ir.Emit( IROp.StoreField, _ir.AddOperand( field ) );
                    }
                }
            }
            else if ( member.Member is PropertyInfo property )
            {
                var setter = property.GetSetMethod( true )
                    ?? throw new InvalidOperationException( $"Property '{property.Name}' has no setter." );

                if ( setter.IsStatic )
                {
                    LowerExpression( node.Right );
                    if ( needsResult )
                        _ir.Emit( IROp.Dup );
                    _ir.Emit( IROp.Call, _ir.AddOperand( setter ) );
                }
                else
                {
                    EmitInstanceForFieldAssign( member.Expression! );
                    LowerExpression( node.Right );

                    if ( needsResult )
                    {
                        // Need the result: use temp to preserve value across setter call
                        var temp = _ir.DeclareLocal( node.Right.Type, "$prop_assign" );
                        _ir.Emit( IROp.Dup );
                        _ir.Emit( IROp.StoreLocal, temp );
                        _ir.Emit(
                            setter.IsVirtual ? IROp.CallVirt : IROp.Call,
                            _ir.AddOperand( setter ) );
                        _ir.Emit( IROp.LoadLocal, temp );
                    }
                    else
                    {
                        // Statement position: just call setter, no result needed
                        _ir.Emit(
                            setter.IsVirtual ? IROp.CallVirt : IROp.Call,
                            _ir.AddOperand( setter ) );
                    }
                }
            }
            else
            {
                throw new NotSupportedException( $"Cannot assign to member type {member.Member.GetType().Name}." );
            }
        }
        else if ( node.Left is IndexExpression indexExpr )
        {
            if ( indexExpr.Indexer != null )
            {
                // Property indexer: call the set accessor
                var setter = indexExpr.Indexer.GetSetMethod( true )
                    ?? throw new InvalidOperationException( $"Indexer '{indexExpr.Indexer.Name}' has no setter." );

                LowerExpression( indexExpr.Object );
                foreach ( var arg in indexExpr.Arguments )
                    LowerExpression( arg );
                LowerExpression( node.Right );

                if ( needsResult )
                {
                    var temp = _ir.DeclareLocal( node.Right.Type, "$idx_assign" );
                    _ir.Emit( IROp.Dup );
                    _ir.Emit( IROp.StoreLocal, temp );
                    _ir.Emit(
                        setter.IsVirtual ? IROp.CallVirt : IROp.Call,
                        _ir.AddOperand( setter ) );
                    _ir.Emit( IROp.LoadLocal, temp );
                }
                else
                {
                    _ir.Emit(
                        setter.IsVirtual ? IROp.CallVirt : IROp.Call,
                        _ir.AddOperand( setter ) );
                }
            }
            else if ( indexExpr.Arguments.Count > 1 )
            {
                // Multi-dimensional array: call the runtime-generated Set(i1, i2, ..., value) method.
                // Save the value first so we can restore it as the assignment result if needed.
                var setMethod = indexExpr.Object!.Type.GetMethod( "Set" )!;

                if ( needsResult )
                {
                    var temp = _ir.DeclareLocal( node.Right.Type, "$arr_assign" );
                    LowerExpression( node.Right );
                    _ir.Emit( IROp.StoreLocal, temp );

                    LowerExpression( indexExpr.Object );
                    foreach ( var arg in indexExpr.Arguments )
                        LowerExpression( arg );
                    _ir.Emit( IROp.LoadLocal, temp );
                    _ir.Emit( IROp.Call, _ir.AddOperand( setMethod ) );
                    _ir.Emit( IROp.LoadLocal, temp );
                }
                else
                {
                    LowerExpression( indexExpr.Object );
                    foreach ( var arg in indexExpr.Arguments )
                        LowerExpression( arg );
                    LowerExpression( node.Right );
                    _ir.Emit( IROp.Call, _ir.AddOperand( setMethod ) );
                }
            }
            else
            {
                // 1D array element: stelem
                LowerExpression( indexExpr.Object );
                foreach ( var arg in indexExpr.Arguments )
                    LowerExpression( arg );
                LowerExpression( node.Right );

                if ( needsResult )
                {
                    var temp = _ir.DeclareLocal( node.Right.Type, "$arr_assign" );
                    _ir.Emit( IROp.Dup );
                    _ir.Emit( IROp.StoreLocal, temp );
                    _ir.Emit( IROp.StoreElement, _ir.AddOperand( indexExpr.Type ) );
                    _ir.Emit( IROp.LoadLocal, temp );
                }
                else
                {
                    _ir.Emit( IROp.StoreElement, _ir.AddOperand( indexExpr.Type ) );
                }
            }
        }
        else
        {
            throw new NotSupportedException( $"Cannot assign to {node.Left.NodeType}." );
        }
    }

    /// <summary>
    /// Emits the instance for a field or property assignment onto the stack.
    /// For value types, emits a managed pointer (byref) so that stfld/setter writes back
    /// through the pointer. For struct array elements this uses ldelema rather than ldelem.
    /// For reference types, loads the object reference normally.
    /// </summary>
    private void EmitInstanceForFieldAssign( Expression instance )
    {
        if ( instance.Type.IsValueType )
        {
            // Value types need a managed pointer — use EmitLoadAddress which handles
            // ArrayIndex (ldelema), locals (ldloca), args (ldarga), and the general case.
            EmitLoadAddress( instance );
        }
        else
        {
            LowerExpression( instance );
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
            // Value type default: CLR zeros locals on declaration,
            // so just declare a temp and load it.
            var temp = _ir.DeclareLocal( node.Type );
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
            if ( handler.Filter != null )
            {
                // Exception filter: emit BeginFilter, filter expression, then BeginFilteredCatch
                _ir.Emit( IROp.BeginFilter );

                if ( handler.Variable != null )
                {
                    // Declare the exception variable and store for use in filter
                    var exLocal = _ir.DeclareLocal( handler.Variable.Type, handler.Variable.Name );
                    ( _localMap ??= new( 8 ) )[handler.Variable] = exLocal;
                    _ir.Emit( IROp.StoreLocal, exLocal );
                }
                else
                {
                    // Discard the exception pushed by BeginFilter
                    _ir.Emit( IROp.Pop );
                }

                // Lower the filter expression (must evaluate to bool)
                LowerExpression( handler.Filter );

                // BeginFilteredCatch: transitions from filter to catch handler
                _ir.Emit( IROp.BeginFilteredCatch );

                // CLR pushes exception on stack again at catch entry; discard it
                _ir.Emit( IROp.Pop );
            }
            else
            {
                _ir.Emit( IROp.BeginCatch, _ir.AddOperand( handler.Test ) );

                if ( handler.Variable != null )
                {
                    // Declare a local for the caught exception and store it
                    var exLocal = _ir.DeclareLocal( handler.Variable.Type, handler.Variable.Name );
                    ( _localMap ??= new( 8 ) )[handler.Variable] = exLocal;
                    _ir.Emit( IROp.StoreLocal, exLocal );
                }
                else
                {
                    // CLR pushes exception on stack at catch entry; discard it
                    _ir.Emit( IROp.Pop );
                }
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
        _labelMap ??= new( 4 );
        if ( !_labelMap.TryGetValue( target, out var labelIndex ) )
        {
            labelIndex = _ir.DefineLabel();
            _labelMap[target] = labelIndex;
        }
        return labelIndex;
    }

    private int GetOrCreateLabelValueLocal( LabelTarget target )
    {
        _labelValueLocalMap ??= new( 4 );
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

        // For non-void switches, use a result local so stack is empty at labels
        var resultLocal = !isVoid ? _ir.DeclareLocal( node.Type, "$switchResult" ) : -1;

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
                _ir.Emit( IROp.Pop );
            }
            else if ( !isVoid && node.Cases[i].Body.Type == typeof( void ) )
            {
                LowerDefault( Expression.Default( node.Type ) );
                _ir.Emit( IROp.StoreLocal, resultLocal );
            }
            else if ( !isVoid )
            {
                _ir.Emit( IROp.StoreLocal, resultLocal );
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
            else if ( !isVoid )
            {
                _ir.Emit( IROp.StoreLocal, resultLocal );
            }

            _ir.Emit( IROp.Branch, endLabel );
        }

        _ir.MarkLabel( endLabel );

        if ( !isVoid )
        {
            _ir.Emit( IROp.LoadLocal, resultLocal );
        }
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

            // Push element type using ldtoken + Type.GetTypeFromHandle (Type objects cannot be
            // embedded directly in IL as constants).
            _ir.Emit( IROp.LoadToken, _ir.AddOperand( elementType ) );
            var getTypeFromHandle = typeof( Type ).GetMethod(
                nameof( Type.GetTypeFromHandle ),
                [typeof( RuntimeTypeHandle )] )!;
            _ir.Emit( IROp.Call, _ir.AddOperand( getTypeFromHandle ) );

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

            // Cast to the actual multi-dimensional array type for IL type safety
            _ir.Emit( IROp.CastClass, _ir.AddOperand( node.Type ) );
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
        else if ( node.Arguments.Count > 1 )
        {
            // Multi-dimensional array access: call the runtime-generated Get(i1, i2, ...) method
            var getMethod = node.Object!.Type.GetMethod( "Get" )!;
            _ir.Emit( IROp.Call, _ir.AddOperand( getMethod ) );
        }
        else
        {
            // 1D array element access
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

        var leftType = node.Left.Type;

        if ( leftType.IsValueType && Nullable.GetUnderlyingType( leftType ) != null )
        {
            // Nullable value type: check HasValue. Must store to a local to call address-based HasValue/Value.
            var resultLocal = _ir.DeclareLocal( node.Type, "$coalesce" );
            var leftLocal = _ir.DeclareLocal( leftType, "$coalesceLeft" );

            LowerExpression( node.Left );
            _ir.Emit( IROp.StoreLocal, leftLocal );

            // Call HasValue
            _ir.Emit( IROp.LoadAddress, leftLocal );
            var hasValueGetter = leftType.GetProperty( "HasValue" )!.GetGetMethod()!;
            _ir.Emit( IROp.Call, _ir.AddOperand( hasValueGetter ) );
            _ir.Emit( IROp.BranchFalse, useRightLabel );

            // Has value -- get the underlying value
            _ir.Emit( IROp.LoadAddress, leftLocal );
            var getValueGetter = leftType.GetProperty( "Value" )!.GetGetMethod()!;
            _ir.Emit( IROp.Call, _ir.AddOperand( getValueGetter ) );

            // Apply conversion if present
            if ( node.Conversion != null )
            {
                var convDelegate = node.Conversion.Compile();
                var delLocal = _ir.DeclareLocal( convDelegate.GetType(), "$coalesceDel" );
                var valLocal = _ir.DeclareLocal( Nullable.GetUnderlyingType( leftType )!, "$coalesceVal" );
                _ir.Emit( IROp.StoreLocal, valLocal );
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( convDelegate ) );
                _ir.Emit( IROp.StoreLocal, delLocal );
                _ir.Emit( IROp.LoadLocal, delLocal );
                _ir.Emit( IROp.LoadLocal, valLocal );
                var invokeMethod = convDelegate.GetType().GetMethod( "Invoke" )!;
                _ir.Emit( IROp.CallVirt, _ir.AddOperand( invokeMethod ) );
            }

            _ir.Emit( IROp.StoreLocal, resultLocal );
            _ir.Emit( IROp.Branch, endLabel );

            _ir.MarkLabel( useRightLabel );
            LowerExpression( node.Right );
            _ir.Emit( IROp.StoreLocal, resultLocal );

            _ir.MarkLabel( endLabel );
            _ir.Emit( IROp.LoadLocal, resultLocal );
        }
        else
        {
            // Reference type: Dup + branch to avoid temp locals.
            // Both paths leave their result on the stack at endLabel.
            LowerExpression( node.Left );
            _ir.Emit( IROp.Dup );                           // [left, left]

            if ( node.Conversion == null )
            {
                // No conversion: BranchTrue leaves left on stack when non-null.
                _ir.Emit( IROp.BranchTrue, endLabel );      // non-null → [left], jump to end
                _ir.Emit( IROp.Pop );                       // null → discard [left_null]
                LowerExpression( node.Right );
                _ir.MarkLabel( endLabel );
            }
            else
            {
                // With conversion: BranchFalse to right path; BranchFalse pops one copy,
                // leaving the original on stack for the non-null conversion path.
                _ir.Emit( IROp.BranchFalse, useRightLabel ); // null → [left_null] on stack, jump

                // Non-null path: [left] is on the stack; apply conversion.
                var convDelegate = node.Conversion.Compile();
                var delLocal = _ir.DeclareLocal( convDelegate.GetType(), "$coalesceDel" );
                var valLocal = _ir.DeclareLocal( node.Left.Type, "$coalesceVal" );
                _ir.Emit( IROp.StoreLocal, valLocal );
                _ir.Emit( IROp.LoadConst, _ir.AddOperand( convDelegate ) );
                _ir.Emit( IROp.StoreLocal, delLocal );
                _ir.Emit( IROp.LoadLocal, delLocal );
                _ir.Emit( IROp.LoadLocal, valLocal );
                var invokeMethod = convDelegate.GetType().GetMethod( "Invoke" )!;
                _ir.Emit( IROp.CallVirt, _ir.AddOperand( invokeMethod ) );
                _ir.Emit( IROp.Branch, endLabel );

                // Right path: pop the null left, then load right.
                _ir.MarkLabel( useRightLabel );
                _ir.Emit( IROp.Pop );                       // discard [left_null]
                LowerExpression( node.Right );
                _ir.MarkLabel( endLabel );
                // Result on stack from both paths.
            }
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

        // Load the Type via ldtoken + Type.GetTypeFromHandle (embeddable in IL)
        _ir.Emit( IROp.LoadToken, _ir.AddOperand( node.TypeOperand ) );
        var getTypeFromHandle = typeof( Type ).GetMethod( nameof( Type.GetTypeFromHandle ) )!;
        _ir.Emit( IROp.Call, _ir.AddOperand( getTypeFromHandle ) );

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
        // Nullable operands must go through the lifted null-propagation path.
        // LowerLiftedArithmetic will extract underlying values, call Math.Pow, then wrap.
        var leftUnderlying = Nullable.GetUnderlyingType( node.Left.Type );
        if ( leftUnderlying != null )
        {
            LowerLiftedBinary( node, leftUnderlying );
            return;
        }

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

    // --- RuntimeVariables ---

    private void LowerRuntimeVariables( RuntimeVariablesExpression node )
    {
        var variables = node.Variables;
        var count = variables.Count;

        // Create IStrongBox[] array
        _ir.Emit( IROp.LoadConst, _ir.AddOperand( count ) );
        _ir.Emit( IROp.NewArray, _ir.AddOperand( typeof( IStrongBox ) ) );

        // Store each variable's StrongBox into the array
        for ( var i = 0; i < count; i++ )
        {
            _ir.Emit( IROp.Dup ); // keep array reference on stack
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( i ) );

            // Load the StrongBox local for this variable
            var boxLocal = _strongBoxLocalMap![variables[i]];
            _ir.Emit( IROp.LoadLocal, boxLocal );

            _ir.Emit( IROp.StoreElement, _ir.AddOperand( typeof( IStrongBox ) ) );
        }

        // Call RuntimeVariablesHelper.Create(IStrongBox[]) → IRuntimeVariables
        var createMethod = typeof( RuntimeVariablesHelper ).GetMethod(
            nameof( RuntimeVariablesHelper.Create ),
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public )!;
        _ir.Emit( IROp.Call, _ir.AddOperand( createMethod ) );
    }

    // --- Lambda / Invoke (Phase 3: Closures) ---

    /// <summary>
    /// Emit code to load a captured variable's value through its StrongBox&lt;T&gt;.
    /// Stack effect: pushes the value of the captured variable.
    /// </summary>
    private void EmitLoadCapturedValue( ParameterExpression variable )
    {
        var boxLocal = _strongBoxLocalMap![variable];
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
    private void EmitStoreCapturedValue( ParameterExpression variable, Expression rightSide, bool needsResult = true )
    {
        var boxLocal = _strongBoxLocalMap![variable];
        var strongBoxType = typeof( StrongBox<> ).MakeGenericType( variable.Type );
        var valueField = strongBoxType.GetField( "Value" )!;

        _ir.Emit( IROp.LoadLocal, boxLocal );
        LowerExpression( rightSide );

        if ( needsResult )
        {
            // Need the result: use temp to preserve value across stfld
            var tempLocal = _ir.DeclareLocal( variable.Type, $"$temp_{variable.Name}" );
            _ir.Emit( IROp.Dup );
            _ir.Emit( IROp.StoreLocal, tempLocal );
            _ir.Emit( IROp.StoreField, _ir.AddOperand( valueField ) );
            _ir.Emit( IROp.LoadLocal, tempLocal );
        }
        else
        {
            // Statement position: just store, no result needed
            _ir.Emit( IROp.StoreField, _ir.AddOperand( valueField ) );
        }
    }

    /// <summary>
    /// Lower a nested lambda expression. For lambdas without captures, compile
    /// directly with the System compiler and push on stack. For lambdas with
    /// captures, build a binder delegate that partially applies the StrongBox
    /// locals to produce a correctly-typed delegate at runtime.
    /// </summary>
    private void LowerNestedLambda( LambdaExpression nestedLambda )
    {
        // Ensure closure info is prepared (shared with LowerInvoke)
        var closureInfo = GetOrBuildClosureInfo( nestedLambda );

        if ( closureInfo == null )
        {
            // No captures -- compile directly
            var compiledDelegate = _nestedCompiler != null ? _nestedCompiler( nestedLambda ) : nestedLambda.Compile();
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( compiledDelegate ) );
        }
        else
        {
            // Has captures -- this lambda is used as a value (not via Invoke).
            // Build a "binder" delegate that takes StrongBox locals and returns
            // a correctly-typed delegate.
            //
            // Example: inner = Func<int, StrongBox<int>, int>
            //   binder = Func<StrongBox<int>, Func<int, int>>
            //     = (box) => (x) => inner(x, box)
            //
            // At runtime: load binder, load StrongBox locals, invoke binder.
            var binder = BuildClosureBinder( nestedLambda, closureInfo );

            // Load the binder delegate
            _ir.Emit( IROp.LoadConst, _ir.AddOperand( binder ) );

            // Load each StrongBox local as arguments to the binder
            foreach ( var capture in closureInfo.Captures )
            {
                var boxLocal = _strongBoxLocalMap![capture];
                _ir.Emit( IROp.LoadLocal, boxLocal );
            }

            // Invoke the binder to produce the correctly-typed delegate
            var binderInvoke = binder.GetType().GetMethod( "Invoke" )!;
            _ir.Emit( IROp.CallVirt, _ir.AddOperand( binderInvoke ) );
        }
    }

    /// <summary>
    /// Build a binder delegate that takes StrongBox locals and returns a delegate
    /// with the original lambda's signature. The binder partially applies the
    /// captured variables to the inner compiled delegate.
    /// </summary>
    private static Delegate BuildClosureBinder(
        LambdaExpression nestedLambda,
        ClosureInfo closureInfo )
    {
        // Create parameter expressions for StrongBox parameters (binder's args)
        var boxParams = new ParameterExpression[closureInfo.Captures.Count];
        for ( var i = 0; i < closureInfo.Captures.Count; i++ )
        {
            var captureType = closureInfo.Captures[i].Type;
            var strongBoxType = typeof( StrongBox<> ).MakeGenericType( captureType );
            boxParams[i] = Expression.Parameter( strongBoxType, $"box_{closureInfo.Captures[i].Name}" );
        }

        // Create parameter expressions for the original lambda's parameters
        var originalParams = new ParameterExpression[nestedLambda.Parameters.Count];
        for ( var i = 0; i < nestedLambda.Parameters.Count; i++ )
        {
            originalParams[i] = Expression.Parameter(
                nestedLambda.Parameters[i].Type, nestedLambda.Parameters[i].Name );
        }

        // Build the inner call: closureInfo.CompiledDelegate(originalParams..., boxParams...)
        var invokeArgs = new Expression[originalParams.Length + boxParams.Length];
        for ( var i = 0; i < originalParams.Length; i++ )
            invokeArgs[i] = originalParams[i];
        for ( var i = 0; i < boxParams.Length; i++ )
            invokeArgs[originalParams.Length + i] = boxParams[i];

        var innerCall = Expression.Invoke(
            Expression.Constant( closureInfo.CompiledDelegate ),
            invokeArgs );

        // Build the inner wrapper lambda with the original signature:
        //   (originalParams...) => compiledDelegate(originalParams..., boxParams...)
        var innerWrapper = Expression.Lambda( nestedLambda.Type, innerCall, originalParams );

        // Build the outer binder lambda that takes StrongBox params and returns
        // the correctly-typed delegate:
        //   (boxParams...) => innerWrapper
        //
        // Determine the binder delegate type: Func<StrongBox<T1>, ..., OriginalDelegateType>
        var binderParamTypes = new Type[boxParams.Length + 1];
        for ( var i = 0; i < boxParams.Length; i++ )
            binderParamTypes[i] = boxParams[i].Type;
        binderParamTypes[^1] = nestedLambda.Type; // return type = original delegate type

        var binderDelegateType = Expression.GetFuncType( binderParamTypes );
        var binderLambda = Expression.Lambda( binderDelegateType, innerWrapper, boxParams );

        // Compile the binder using System compiler (one-time cost at lowering time)
        return binderLambda.Compile();
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
                    var boxLocal = _strongBoxLocalMap![capture];
                    _ir.Emit( IROp.LoadLocal, boxLocal );
                }

                // Call Invoke on the rewritten delegate type (original params + StrongBox params)
                var invokeMethod = closureInfo.CompiledDelegate.GetType().GetMethod( "Invoke" )!;
                _ir.Emit( IROp.CallVirt, _ir.AddOperand( invokeMethod ) );
                return;
            }

            // No captures -- compile the lambda directly and invoke
            var compiledDelegate = _nestedCompiler != null ? _nestedCompiler( lambdaExpr ) : lambdaExpr.Compile();
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
        if ( _closureInfoMap?.TryGetValue( lambda, out var existing ) == true )
        {
            return existing;
        }

        // Find which captured variables this nested lambda references
        var innerCaptures = new List<ParameterExpression>( _capturedVariables!.Count );
        foreach ( var capturedVar in _capturedVariables )
        {
            if ( _strongBoxLocalMap?.ContainsKey( capturedVar ) == true
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
        var replacements = new Dictionary<ParameterExpression, Expression>( innerCaptures.Count );
        for ( var i = 0; i < innerCaptures.Count; i++ )
        {
            var valueField = boxTypes[i].GetField( "Value" )!;
            replacements[innerCaptures[i]] = Expression.Field( boxParams[i], valueField );
        }

        // Rewrite the inner lambda body to use StrongBox parameters
        var rewrittenBody = (Expression) new CaptureRewriter( replacements ).Visit( lambda.Body )!;

        // Build a new lambda that takes the original params + StrongBox params
        var allParams = new List<ParameterExpression>( lambda.Parameters.Count + innerCaptures.Count );
        allParams.AddRange( lambda.Parameters );
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
        var allParamTypes = new Type[allParams.Count];
        for ( var i = 0; i < allParams.Count; i++ )
            allParamTypes[i] = allParams[i].Type;
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
        var compiledInner = _nestedCompiler != null ? _nestedCompiler( rewrittenLambda ) : rewrittenLambda.Compile();

        var closureInfo = new ClosureInfo( compiledInner, innerCaptures );
        _closureInfoMap ??= new( 2 );
        _closureInfoMap[lambda] = closureInfo;
        return closureInfo;
    }

    /// <summary>
    /// Check if an expression tree references a specific parameter variable.
    /// </summary>
    private static bool ReferencesVariable( Expression? node, ParameterExpression variable )
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

    // --- Helpers ---

    /// <summary>
    /// Returns the zero constant for a numeric type, used for checked negation (0 - value).
    /// </summary>
    private static object GetZeroForType( Type type )
    {
        if ( type == typeof( int ) ) return 0;
        if ( type == typeof( long ) ) return 0L;
        if ( type == typeof( short ) ) return (short) 0;
        if ( type == typeof( sbyte ) ) return (sbyte) 0;
        if ( type == typeof( float ) ) return 0f;
        if ( type == typeof( double ) ) return 0d;
        if ( type == typeof( decimal ) ) return 0m;
        throw new NotSupportedException( $"NegateChecked is not supported for type {type.Name}." );
    }

    private static object GetOneForType( Type type )
    {
        if ( type == typeof( int ) ) return 1;
        if ( type == typeof( long ) ) return 1L;
        if ( type == typeof( uint ) ) return 1U;
        if ( type == typeof( ulong ) ) return 1UL;
        if ( type == typeof( short ) ) return (short) 1;
        if ( type == typeof( ushort ) ) return (ushort) 1;
        if ( type == typeof( sbyte ) ) return (sbyte) 1;
        if ( type == typeof( byte ) ) return (byte) 1;
        if ( type == typeof( float ) ) return 1f;
        if ( type == typeof( double ) ) return 1d;
        if ( type == typeof( decimal ) ) return 1m;
        throw new NotSupportedException( $"Increment/Decrement is not supported for type {type.Name}." );
    }

    private static bool IsFloatingPoint( Type type )
    {
        return type == typeof( float ) || type == typeof( double );
    }

    private static bool IsUnsigned( Type type )
    {
        return type == typeof( uint ) || type == typeof( ulong )
            || type == typeof( byte ) || type == typeof( ushort );
    }

    private static bool IsUnsignedOrFloat( Type type )
    {
        return IsUnsigned( type ) || IsFloatingPoint( type );
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
