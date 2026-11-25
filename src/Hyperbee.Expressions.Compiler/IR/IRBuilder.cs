namespace Hyperbee.Expressions.Compiler.IR;

/// <summary>
/// Builds a flat IR instruction stream with side tables for operands, locals, and labels.
/// </summary>
public class IRBuilder
{
    // Only the instruction list is certain to be used. The three side tables are not:
    // measured over the benchmark tiers, a two-parameter `a + b` body declares no local,
    // defines no label and -- now that int constants ride in the instruction itself --
    // fills no operand table either. Allocating all three up front cost every compile a
    // List object and its backing array for tables that stayed empty.
    private readonly List<IRInstruction> _instructions = new( 16 );
    private List<object>? _operands;
    private Dictionary<int, Type>? _operandTypes;
    private List<LocalInfo>? _locals;
    private List<LabelInfo>? _labels;

    // --- Public read-only accessors ---

    /// <summary>The instruction stream.</summary>
    public IReadOnlyList<IRInstruction> Instructions => _instructions;

    /// <summary>The operand table (constants, MethodInfo, Type, etc.).</summary>
    public IReadOnlyList<object> Operands => _operands ?? (IReadOnlyList<object>) Array.Empty<object>();

    /// <summary>The local variable table.</summary>
    public IReadOnlyList<LocalInfo> Locals => _locals ?? (IReadOnlyList<LocalInfo>) Array.Empty<LocalInfo>();

    /// <summary>The label table.</summary>
    public IReadOnlyList<LabelInfo> Labels => _labels ?? (IReadOnlyList<LabelInfo>) Array.Empty<LabelInfo>();

    // --- Instruction emission ---

    /// <summary>Emit an instruction with no operand.</summary>
    public void Emit( IROp op )
        => _instructions.Add( new IRInstruction( op ) );

    /// <summary>Emit an instruction with an integer operand.</summary>
    public void Emit( IROp op, int operand )
        => _instructions.Add( new IRInstruction( op, operand ) );

    /// <summary>Emit a constant load, keeping int constants out of the operand table.</summary>
    /// <remarks>
    /// An int goes in the instruction's own operand field, which is already an int. Routing
    /// it through the operand table boxes it -- once per constant per compile, for a value
    /// the emitter embeds in the IL anyway and never reads back as an object. Only an int
    /// whose declared type is int qualifies. A declared type drives the cast that the
    /// constants-array path emits, so an int boxed as object, or one standing in for an
    /// enum, has to keep going through the table.
    /// </remarks>
    public void EmitLoadConst( object? value, Type? declaredType = null )
    {
        if ( value is int i && ( declaredType == null || declaredType == typeof( int ) ) )
        {
            Emit( IROp.LoadInt, i );
            return;
        }

        Emit( IROp.LoadConst, AddOperand( value!, declaredType ) );
    }

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
        _operands ??= new( 4 );

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
        _locals ??= new( 2 );

        var index = _locals.Count;
        _locals.Add( new LocalInfo( type, name ) );
        return index;
    }

    // --- Labels ---

    /// <summary>Define a new label and return its index.</summary>
    public int DefineLabel()
    {
        _labels ??= new( 2 );

        var index = _labels.Count;
        _labels.Add( new LabelInfo() );
        return index;
    }

    /// <summary>Mark the label at the current instruction position.</summary>
    public void MarkLabel( int labelIndex )
    {
        _labels![labelIndex] = _labels[labelIndex] with
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
