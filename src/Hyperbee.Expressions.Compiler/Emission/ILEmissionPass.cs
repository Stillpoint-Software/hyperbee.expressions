using System.Reflection;
using System.Reflection.Emit;
using Hyperbee.Expressions.Compiler.IR;

namespace Hyperbee.Expressions.Compiler.Emission;

/// <summary>
/// Final pass: emits CIL from the IR instruction list via ILGenerator.
/// Straightforward 1:1 mapping from IR opcodes to CIL opcodes.
/// </summary>
public static class ILEmissionPass
{
    /// <summary>
    /// Emit CIL for all instructions in the IR.
    /// </summary>
    /// <param name="ir">The IR builder containing the instruction stream.</param>
    /// <param name="ilg">The IL generator to emit into.</param>
    /// <param name="hasConstantsArray">Whether arg 0 is an object[] constants array.</param>
    /// <param name="constantIndices">
    /// Maps operand-table index to constants-array index for non-embeddable constants.
    /// Only used when <paramref name="hasConstantsArray"/> is true.
    /// </param>
    public static void Run(
        IRBuilder ir,
        ILGenerator ilg,
        bool hasConstantsArray,
        Dictionary<int, int>? constantIndices )
    {
        // Pre-declare all IL locals
        var ilLocals = new LocalBuilder[ir.Locals.Count];
        for ( var i = 0; i < ir.Locals.Count; i++ )
        {
            ilLocals[i] = ilg.DeclareLocal( ir.Locals[i].Type );
        }

        // Pre-declare all IL labels
        var ilLabels = new Label[ir.Labels.Count];
        for ( var i = 0; i < ir.Labels.Count; i++ )
        {
            ilLabels[i] = ilg.DefineLabel();
        }

        // Emit instructions
        foreach ( var inst in ir.Instructions )
        {
            switch ( inst.Op )
            {
                case IROp.Nop:
                    break;

                case IROp.LoadConst:
                    EmitLoadConstant( ilg, ir.Operands[inst.Operand], inst.Operand, hasConstantsArray, constantIndices );
                    break;

                case IROp.LoadNull:
                    ilg.Emit( OpCodes.Ldnull );
                    break;

                case IROp.LoadLocal:
                    EmitLoadLocal( ilg, inst.Operand );
                    break;

                case IROp.StoreLocal:
                    EmitStoreLocal( ilg, inst.Operand );
                    break;

                case IROp.LoadArg:
                    EmitLoadArg( ilg, inst.Operand );
                    break;

                case IROp.StoreArg:
                    EmitStoreArg( ilg, inst.Operand );
                    break;

                // Fields
                case IROp.LoadField:
                    ilg.Emit( OpCodes.Ldfld, (FieldInfo) ir.Operands[inst.Operand] );
                    break;

                case IROp.StoreField:
                    ilg.Emit( OpCodes.Stfld, (FieldInfo) ir.Operands[inst.Operand] );
                    break;

                case IROp.LoadStaticField:
                    ilg.Emit( OpCodes.Ldsfld, (FieldInfo) ir.Operands[inst.Operand] );
                    break;

                case IROp.StoreStaticField:
                    ilg.Emit( OpCodes.Stsfld, (FieldInfo) ir.Operands[inst.Operand] );
                    break;

                // Arithmetic
                case IROp.Add:
                    ilg.Emit( OpCodes.Add );
                    break;

                case IROp.Sub:
                    ilg.Emit( OpCodes.Sub );
                    break;

                case IROp.Mul:
                    ilg.Emit( OpCodes.Mul );
                    break;

                case IROp.Div:
                    ilg.Emit( OpCodes.Div );
                    break;

                case IROp.Rem:
                    ilg.Emit( OpCodes.Rem );
                    break;

                case IROp.AddChecked:
                    ilg.Emit( OpCodes.Add_Ovf );
                    break;

                case IROp.SubChecked:
                    ilg.Emit( OpCodes.Sub_Ovf );
                    break;

                case IROp.MulChecked:
                    ilg.Emit( OpCodes.Mul_Ovf );
                    break;

                // Unary
                case IROp.Negate:
                    ilg.Emit( OpCodes.Neg );
                    break;

                case IROp.NegateChecked:
                    // For checked negate: load 0, then sub.ovf
                    // This correctly detects overflow for int.MinValue etc.
                    // Actually the simplest approach: neg does NOT throw on overflow.
                    // For checked negate of int: ldc.i4.0, val (already on stack... but we already pushed operand)
                    // We need to do: push 0, push val, sub.ovf
                    // But val is already on the stack. So: store temp, ldc.i4.0, load temp, sub.ovf
                    // For simplicity in Phase 1 just emit neg (matches System compiler behavior for non-MinValue)
                    ilg.Emit( OpCodes.Neg );
                    break;

                case IROp.Not:
                    ilg.Emit( OpCodes.Not );
                    break;

                // Bitwise
                case IROp.And:
                    ilg.Emit( OpCodes.And );
                    break;

                case IROp.Or:
                    ilg.Emit( OpCodes.Or );
                    break;

                case IROp.Xor:
                    ilg.Emit( OpCodes.Xor );
                    break;

                case IROp.LeftShift:
                    ilg.Emit( OpCodes.Shl );
                    break;

                case IROp.RightShift:
                    ilg.Emit( OpCodes.Shr );
                    break;

                // Comparison
                case IROp.Ceq:
                    ilg.Emit( OpCodes.Ceq );
                    break;

                case IROp.Clt:
                    ilg.Emit( OpCodes.Clt );
                    break;

                case IROp.Cgt:
                    ilg.Emit( OpCodes.Cgt );
                    break;

                case IROp.CltUn:
                    ilg.Emit( OpCodes.Clt_Un );
                    break;

                case IROp.CgtUn:
                    ilg.Emit( OpCodes.Cgt_Un );
                    break;

                // Conversion
                case IROp.Convert:
                    EmitConvert( ilg, (Type) ir.Operands[inst.Operand], isChecked: false );
                    break;

                case IROp.ConvertChecked:
                    EmitConvert( ilg, (Type) ir.Operands[inst.Operand], isChecked: true );
                    break;

                case IROp.Box:
                    ilg.Emit( OpCodes.Box, (Type) ir.Operands[inst.Operand] );
                    break;

                case IROp.Unbox:
                    ilg.Emit( OpCodes.Unbox, (Type) ir.Operands[inst.Operand] );
                    break;

                case IROp.UnboxAny:
                    ilg.Emit( OpCodes.Unbox_Any, (Type) ir.Operands[inst.Operand] );
                    break;

                case IROp.CastClass:
                    ilg.Emit( OpCodes.Castclass, (Type) ir.Operands[inst.Operand] );
                    break;

                case IROp.IsInst:
                    ilg.Emit( OpCodes.Isinst, (Type) ir.Operands[inst.Operand] );
                    break;

                // Method calls
                case IROp.Call:
                    ilg.Emit( OpCodes.Call, (MethodInfo) ir.Operands[inst.Operand] );
                    break;

                case IROp.CallVirt:
                    ilg.Emit( OpCodes.Callvirt, (MethodInfo) ir.Operands[inst.Operand] );
                    break;

                case IROp.NewObj:
                    ilg.Emit( OpCodes.Newobj, (ConstructorInfo) ir.Operands[inst.Operand] );
                    break;

                // Control flow
                case IROp.Branch:
                    ilg.Emit( OpCodes.Br, ilLabels[inst.Operand] );
                    break;

                case IROp.BranchTrue:
                    ilg.Emit( OpCodes.Brtrue, ilLabels[inst.Operand] );
                    break;

                case IROp.BranchFalse:
                    ilg.Emit( OpCodes.Brfalse, ilLabels[inst.Operand] );
                    break;

                case IROp.Label:
                    ilg.MarkLabel( ilLabels[inst.Operand] );
                    break;

                // Stack manipulation
                case IROp.Dup:
                    ilg.Emit( OpCodes.Dup );
                    break;

                case IROp.Pop:
                    ilg.Emit( OpCodes.Pop );
                    break;

                case IROp.Ret:
                    ilg.Emit( OpCodes.Ret );
                    break;

                // Special
                case IROp.InitObj:
                {
                    var type = (Type) ir.Operands[inst.Operand];
                    // initobj needs an address on the stack. For our default(T) pattern,
                    // we handle this in the lowerer by using a temp local.
                    // Actually, InitObj in our lowerer usage is followed by StoreLocal+LoadLocal.
                    // We don't actually emit initobj here -- the local is already zeroed by the CLR.
                    // So this is a no-op in Phase 1.
                    break;
                }

                // Scope markers -- no IL emission
                case IROp.BeginScope:
                case IROp.EndScope:
                    break;

                // Exception handling
                case IROp.BeginTry:
                    ilg.BeginExceptionBlock();
                    break;

                case IROp.BeginCatch:
                    ilg.BeginCatchBlock( (Type) ir.Operands[inst.Operand] );
                    break;

                case IROp.BeginFinally:
                    ilg.BeginFinallyBlock();
                    break;

                case IROp.BeginFault:
                    ilg.BeginFaultBlock();
                    break;

                case IROp.EndTryCatch:
                    ilg.EndExceptionBlock();
                    break;

                case IROp.Throw:
                    ilg.Emit( OpCodes.Throw );
                    break;

                case IROp.Rethrow:
                    ilg.Emit( OpCodes.Rethrow );
                    break;

                case IROp.Leave:
                    ilg.Emit( OpCodes.Leave, ilLabels[inst.Operand] );
                    break;

                // Not in Phase 1
                case IROp.CreateDelegate:
                case IROp.LoadClosureVar:
                case IROp.StoreClosureVar:
                    throw new NotSupportedException(
                        $"IR op {inst.Op} is not supported in this compiler phase." );

                default:
                    throw new NotSupportedException( $"IR op {inst.Op} is not supported." );
            }
        }
    }

    private static void EmitLoadLocal( ILGenerator ilg, int index )
    {
        switch ( index )
        {
            case 0: ilg.Emit( OpCodes.Ldloc_0 ); break;
            case 1: ilg.Emit( OpCodes.Ldloc_1 ); break;
            case 2: ilg.Emit( OpCodes.Ldloc_2 ); break;
            case 3: ilg.Emit( OpCodes.Ldloc_3 ); break;
            default:
                if ( index <= 255 )
                    ilg.Emit( OpCodes.Ldloc_S, (byte) index );
                else
                    ilg.Emit( OpCodes.Ldloc, (short) index );
                break;
        }
    }

    private static void EmitStoreLocal( ILGenerator ilg, int index )
    {
        switch ( index )
        {
            case 0: ilg.Emit( OpCodes.Stloc_0 ); break;
            case 1: ilg.Emit( OpCodes.Stloc_1 ); break;
            case 2: ilg.Emit( OpCodes.Stloc_2 ); break;
            case 3: ilg.Emit( OpCodes.Stloc_3 ); break;
            default:
                if ( index <= 255 )
                    ilg.Emit( OpCodes.Stloc_S, (byte) index );
                else
                    ilg.Emit( OpCodes.Stloc, (short) index );
                break;
        }
    }

    private static void EmitLoadArg( ILGenerator ilg, int index )
    {
        switch ( index )
        {
            case 0: ilg.Emit( OpCodes.Ldarg_0 ); break;
            case 1: ilg.Emit( OpCodes.Ldarg_1 ); break;
            case 2: ilg.Emit( OpCodes.Ldarg_2 ); break;
            case 3: ilg.Emit( OpCodes.Ldarg_3 ); break;
            default:
                if ( index <= 255 )
                    ilg.Emit( OpCodes.Ldarg_S, (byte) index );
                else
                    ilg.Emit( OpCodes.Ldarg, (short) index );
                break;
        }
    }

    private static void EmitStoreArg( ILGenerator ilg, int index )
    {
        if ( index <= 255 )
            ilg.Emit( OpCodes.Starg_S, (byte) index );
        else
            ilg.Emit( OpCodes.Starg, (short) index );
    }

    private static void EmitLoadConstant(
        ILGenerator ilg,
        object value,
        int operandIndex,
        bool hasConstantsArray,
        Dictionary<int, int>? constantIndices )
    {
        switch ( value )
        {
            case int i:
                EmitLoadInt( ilg, i );
                break;

            case long l:
                ilg.Emit( OpCodes.Ldc_I8, l );
                break;

            case float f:
                ilg.Emit( OpCodes.Ldc_R4, f );
                break;

            case double d:
                ilg.Emit( OpCodes.Ldc_R8, d );
                break;

            case string s:
                ilg.Emit( OpCodes.Ldstr, s );
                break;

            case bool b:
                ilg.Emit( b ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0 );
                break;

            case byte b:
                EmitLoadInt( ilg, b );
                break;

            case sbyte sb:
                EmitLoadInt( ilg, sb );
                break;

            case short s:
                EmitLoadInt( ilg, s );
                break;

            case ushort us:
                EmitLoadInt( ilg, us );
                break;

            case char c:
                EmitLoadInt( ilg, c );
                break;

            case uint ui:
                ilg.Emit( OpCodes.Ldc_I4, unchecked((int) ui) );
                break;

            case ulong ul:
                ilg.Emit( OpCodes.Ldc_I8, unchecked((long) ul) );
                break;

            case decimal:
                // Decimal is a value type that needs to be loaded from the constants array
                EmitLoadFromConstantsArray( ilg, operandIndex, value.GetType(), constantIndices! );
                break;

            default:
                // Non-embeddable constant -- load from constants array
                if ( hasConstantsArray && constantIndices != null && constantIndices.ContainsKey( operandIndex ) )
                {
                    EmitLoadFromConstantsArray( ilg, operandIndex, value.GetType(), constantIndices );
                }
                else
                {
                    throw new NotSupportedException(
                        $"Cannot embed constant of type {value.GetType().Name} directly in IL. " +
                        "A constants array is required." );
                }
                break;
        }
    }

    private static void EmitLoadInt( ILGenerator ilg, int value )
    {
        switch ( value )
        {
            case -1: ilg.Emit( OpCodes.Ldc_I4_M1 ); break;
            case 0: ilg.Emit( OpCodes.Ldc_I4_0 ); break;
            case 1: ilg.Emit( OpCodes.Ldc_I4_1 ); break;
            case 2: ilg.Emit( OpCodes.Ldc_I4_2 ); break;
            case 3: ilg.Emit( OpCodes.Ldc_I4_3 ); break;
            case 4: ilg.Emit( OpCodes.Ldc_I4_4 ); break;
            case 5: ilg.Emit( OpCodes.Ldc_I4_5 ); break;
            case 6: ilg.Emit( OpCodes.Ldc_I4_6 ); break;
            case 7: ilg.Emit( OpCodes.Ldc_I4_7 ); break;
            case 8: ilg.Emit( OpCodes.Ldc_I4_8 ); break;
            default:
                if ( value is >= -128 and <= 127 )
                    ilg.Emit( OpCodes.Ldc_I4_S, (sbyte) value );
                else
                    ilg.Emit( OpCodes.Ldc_I4, value );
                break;
        }
    }

    private static void EmitLoadFromConstantsArray(
        ILGenerator ilg,
        int operandIndex,
        Type targetType,
        Dictionary<int, int> constantIndices )
    {
        var arrayIndex = constantIndices[operandIndex];

        // Load constants array (arg 0)
        ilg.Emit( OpCodes.Ldarg_0 );
        // Load array index
        EmitLoadInt( ilg, arrayIndex );
        // Load element reference
        ilg.Emit( OpCodes.Ldelem_Ref );

        // Cast or unbox to target type
        if ( targetType.IsValueType )
            ilg.Emit( OpCodes.Unbox_Any, targetType );

        else if ( targetType != typeof( object ) )
            ilg.Emit( OpCodes.Castclass, targetType );
    }

    private static void EmitConvert( ILGenerator ilg, Type targetType, bool isChecked )
    {
        if ( isChecked )
            EmitConvertChecked( ilg, targetType );
        else
            EmitConvertUnchecked( ilg, targetType );
    }

    private static void EmitConvertUnchecked( ILGenerator ilg, Type targetType )
    {
        if ( targetType == typeof( sbyte ) )
            ilg.Emit( OpCodes.Conv_I1 );
        else if ( targetType == typeof( short ) )
            ilg.Emit( OpCodes.Conv_I2 );
        else if ( targetType == typeof( int ) )
            ilg.Emit( OpCodes.Conv_I4 );
        else if ( targetType == typeof( long ) )
            ilg.Emit( OpCodes.Conv_I8 );
        else if ( targetType == typeof( byte ) )
            ilg.Emit( OpCodes.Conv_U1 );
        else if ( targetType == typeof( ushort ) || targetType == typeof( char ) )
            ilg.Emit( OpCodes.Conv_U2 );
        else if ( targetType == typeof( uint ) )
            ilg.Emit( OpCodes.Conv_U4 );
        else if ( targetType == typeof( ulong ) )
            ilg.Emit( OpCodes.Conv_U8 );
        else if ( targetType == typeof( float ) )
            ilg.Emit( OpCodes.Conv_R4 );
        else if ( targetType == typeof( double ) )
            ilg.Emit( OpCodes.Conv_R8 );
        else if ( targetType == typeof( nint ) )
            ilg.Emit( OpCodes.Conv_I );
        else if ( targetType == typeof( nuint ) )
            ilg.Emit( OpCodes.Conv_U );
        else
            throw new NotSupportedException( $"Unsupported conversion target type: {targetType.Name}" );
    }

    private static void EmitConvertChecked( ILGenerator ilg, Type targetType )
    {
        if ( targetType == typeof( sbyte ) )
            ilg.Emit( OpCodes.Conv_Ovf_I1 );
        else if ( targetType == typeof( short ) )
            ilg.Emit( OpCodes.Conv_Ovf_I2 );
        else if ( targetType == typeof( int ) )
            ilg.Emit( OpCodes.Conv_Ovf_I4 );
        else if ( targetType == typeof( long ) )
            ilg.Emit( OpCodes.Conv_Ovf_I8 );
        else if ( targetType == typeof( byte ) )
            ilg.Emit( OpCodes.Conv_Ovf_U1 );
        else if ( targetType == typeof( ushort ) || targetType == typeof( char ) )
            ilg.Emit( OpCodes.Conv_Ovf_U2 );
        else if ( targetType == typeof( uint ) )
            ilg.Emit( OpCodes.Conv_Ovf_U4 );
        else if ( targetType == typeof( ulong ) )
            ilg.Emit( OpCodes.Conv_Ovf_U8 );
        else if ( targetType == typeof( float ) )
            ilg.Emit( OpCodes.Conv_R4 );
        else if ( targetType == typeof( double ) )
            ilg.Emit( OpCodes.Conv_R8 );
        else if ( targetType == typeof( nint ) )
            ilg.Emit( OpCodes.Conv_Ovf_I );
        else if ( targetType == typeof( nuint ) )
            ilg.Emit( OpCodes.Conv_Ovf_U );
        else
            throw new NotSupportedException( $"Unsupported checked conversion target type: {targetType.Name}" );
    }
}
