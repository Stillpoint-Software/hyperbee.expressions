namespace Hyperbee.Expressions.Compiler.IR;

/// <summary>
/// IR operation codes. Maps closely to CIL but at a slightly higher abstraction level.
/// </summary>
public enum IROp : byte
{
    // Constants and variables
    Nop,
    LoadConst,              // Push constant from operand table
    LoadNull,               // Push null
    LoadLocal,              // Push local variable
    StoreLocal,             // Pop and store to local variable
    LoadArg,                // Push argument
    StoreArg,               // Pop and store to argument

    // Fields and properties
    LoadField,              // Push field value (instance on stack)
    StoreField,             // Store to field (instance and value on stack)
    LoadStaticField,        // Push static field value
    StoreStaticField,       // Pop and store to static field
    LoadFieldAddress,       // Push managed pointer to instance field (ldflda)

    // Array operations
    LoadElement,            // Push array element
    StoreElement,           // Store to array element
    LoadArrayLength,        // Push array length
    NewArray,               // Create new array

    // Arithmetic
    Add, 
    Sub, 
    Mul, 
    Div, 
    Rem,
    AddChecked,
    SubChecked,
    MulChecked,
    AddCheckedUn,           // Unsigned checked add (add.ovf.un)
    SubCheckedUn,           // Unsigned checked subtract (sub.ovf.un)
    MulCheckedUn,           // Unsigned checked multiply (mul.ovf.un)
    Negate, 
    NegateChecked,
    And, 
    Or, 
    Xor, 
    Not,
    LeftShift,
    RightShift,
    RightShiftUn,           // Unsigned/logical right shift (shr.un)

    // Comparison
    Ceq, 
    Clt, 
    Cgt,
    CltUn, 
    CgtUn,

    // Conversion
    Convert,                // Type conversion (operand -> Type in operand table)
    ConvertChecked,         // Checked conversion from signed source
    ConvertCheckedUn,       // Checked conversion from unsigned source (conv.ovf.X.un)
    Box, 
    Unbox, 
    UnboxAny,
    CastClass, 
    IsInst,

    // Method calls
    Call,                   // Static/non-virtual call
    CallVirt,               // Virtual/interface call
    Constrained,            // Constrained prefix for value-type virtual calls (operand -> Type)
    NewObj,                 // Constructor call

    // Control flow
    Branch,                 // Unconditional branch
    BranchTrue,             // Branch if true
    BranchFalse,            // Branch if false
    Label,                  // Branch target marker

    // Exception handling
    BeginTry,               // Enter try block
    BeginCatch,             // Enter catch handler
    BeginFilter,            // Enter exception filter block (evaluates to bool)
    BeginFilteredCatch,     // Enter catch handler after filter (operand unused)
    BeginFinally,           // Enter finally handler
    BeginFault,             // Enter fault handler
    EndTryCatch,            // End exception handling block
    Throw,                  // Throw exception
    Rethrow,                // Rethrow current exception
    Leave,                  // Leave try/catch block (branch target label)

    // Stack manipulation
    Dup,                    // Duplicate top of stack
    Pop,                    // Discard top of stack
    Ret,                    // Return

    // Scope markers (for variable lifetime tracking)
    BeginScope,             // Enter a new variable scope
    EndScope,               // Exit variable scope

    // Special
    InitObj,                // Initialize value type
    LoadAddress,            // Load address of local variable
    LoadArgAddress,         // Load address of argument
    LoadToken,              // Load runtime type/method/field token
    Switch,                 // Switch table branch
}
