namespace Hyperbee.Expressions.Compiler.IR;

/// <summary>
/// Builds a flat IR instruction stream with side tables for operands, locals, and labels.
/// </summary>
public class IRBuilder
{
    private readonly List<IRInstruction> _instructions = new( 16 );
    private readonly List<object> _operands = new( 4 );
    private readonly List<LocalInfo> _locals = new( 2 );
    private readonly List<LabelInfo> _labels = new( 2 );
    private int _currentScope;

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
    public int AddOperand( object value )
    {
        var index = _operands.Count;
        _operands.Add( value );
        return index;
    }

    // --- Local variables ---

    /// <summary>Declare a new local variable and return its index.</summary>
    public int DeclareLocal( Type type, string? name = null )
    {
        var index = _locals.Count;
        _locals.Add( new LocalInfo( type, name, _currentScope ) );
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

    // --- Scope tracking ---

    /// <summary>Enter a new scope.</summary>
    public void EnterScope()
    {
        _currentScope++;
        Emit( IROp.BeginScope );
    }

    /// <summary>Exit the current scope.</summary>
    public void ExitScope()
    {
        Emit( IROp.EndScope );
        _currentScope--;
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
