namespace Hyperbee.Expressions.Compiler.IR;

/// <summary>
/// Builds a flat IR instruction stream with side tables for operands, locals, and labels.
/// </summary>
public class IRBuilder
{
    private readonly List<IRInstruction> _instructions = new( 16 );
    private readonly List<object> _operands = new( 4 );
    private Dictionary<int, Type>? _operandTypes;
    private readonly List<LocalInfo> _locals = new( 2 );
    private readonly List<LabelInfo> _labels = new( 2 );

    // --- Public read-only accessors ---

    /// <summary>The instruction stream.</summary>
    public IReadOnlyList<IRInstruction> Instructions => _instructions;

    /// <summary>The operand table (constants, MethodInfo, Type, etc.).</summary>
    public IReadOnlyList<object> Operands => _operands;

    /// <summary>The local variable table.</summary>
    public IReadOnlyList<LocalInfo> Locals => _locals;

    /// <summary>The label table.</summary>
    public IReadOnlyList<LabelInfo> Labels => _labels;

    // --- Instruction emission ---

    /// <summary>Emit an instruction with no operand.</summary>
    public void Emit( IROp op )
        => _instructions.Add( new IRInstruction( op ) );

    /// <summary>Emit an instruction with an integer operand.</summary>
    public void Emit( IROp op, int operand )
        => _instructions.Add( new IRInstruction( op, operand ) );

    // --- Operand table ---

    /// <summary>Add a value to the operand table and return its index.</summary>
    /// <param name="declaredType">
    /// The type the expression gave the value, when that differs from its runtime type. A
    /// constant is reached through the constants array as <c>object</c> and cast back, and
    /// the cast has to name the declared type: the runtime type is over-specific and may be
    /// one the emitting context cannot see, such as the concrete type behind a Task.
    /// </param>
    public int AddOperand( object value, Type? declaredType = null )
    {
        var index = _operands.Count;
        _operands.Add( value );

        if ( declaredType != null && declaredType != value?.GetType() )
            ( _operandTypes ??= new( 2 ) )[index] = declaredType;

        return index;
    }

    /// <summary>The declared type of an operand, or null to use its runtime type.</summary>
    public Type? GetOperandType( int index )
    {
        return _operandTypes != null && _operandTypes.TryGetValue( index, out var type ) ? type : null;
    }

    // --- Local variables ---

    /// <summary>Declare a new local variable and return its index.</summary>
    public int DeclareLocal( Type type, string? name = null )
    {
        var index = _locals.Count;
        _locals.Add( new LocalInfo( type, name ) );
        return index;
    }

    // --- Labels ---

    /// <summary>Define a new label and return its index.</summary>
    public int DefineLabel()
    {
        var index = _labels.Count;
        _labels.Add( new LabelInfo() );
        return index;
    }

    /// <summary>Mark the label at the current instruction position.</summary>
    public void MarkLabel( int labelIndex )
    {
        _labels[labelIndex] = _labels[labelIndex] with
        {
            InstructionIndex = _instructions.Count
        };
        Emit( IROp.Label, labelIndex );
    }

    // --- Instruction list manipulation (for passes) ---

    /// <summary>Insert an instruction at the given position.</summary>
    public void InsertAt( int position, IRInstruction instruction )
        => _instructions.Insert( position, instruction );

    /// <summary>Remove the instruction at the given position.</summary>
    public void RemoveAt( int position )
        => _instructions.RemoveAt( position );

    /// <summary>Replace the instruction at the given position.</summary>
    public void ReplaceAt( int position, IRInstruction instruction )
        => _instructions[position] = instruction;
}
