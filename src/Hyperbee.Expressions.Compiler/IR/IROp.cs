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
    LoadClosureVar,         // Push variable from closure (post closure-analysis)
    StoreClosureVar,        // Pop and store to closure variable

    // Fields and properties
    LoadField,              // Push field value (instance on stack)
    StoreField,             // Store to field (instance and value on stack)
    LoadStaticField,        // Push static field value
    StoreStaticField,       // Pop and store to static field

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
    Negate, 
    NegateChecked,
    And, 
    Or, 
    Xor, 
    Not,
    LeftShift, 
    RightShift,

    // Comparison
    Ceq, 
    Clt, 
    Cgt,
    CltUn, 
    CgtUn,

    // Conversion
    Convert,                // Type conversion (operand -> Type in operand table)
    ConvertChecked,
    Box, 
    Unbox, 
    UnboxAny,
    CastClass, 
    IsInst,

    // Method calls
    Call,                   // Static/non-virtual call
    CallVirt,               // Virtual/interface call
    NewObj,                 // Constructor call

    // Control flow
    Branch,                 // Unconditional branch
    BranchTrue,             // Branch if true
    BranchFalse,            // Branch if false
    Label,                  // Branch target marker

    // Exception handling
    BeginTry,               // Enter try block
    BeginCatch,             // Enter catch handler
    BeginFinally,           // Enter finally handler
    BeginFault,             // Enter fault handler
    EndTryCatch,            // End exception handling block
    Throw,                  // Throw exception
    Rethrow,                // Rethrow current exception

    // Stack manipulation
    Dup,                    // Duplicate top of stack
    Pop,                    // Discard top of stack
    Ret,                    // Return

    // Scope markers (for variable lifetime tracking)
    BeginScope,             // Enter a new variable scope
    EndScope,               // Exit variable scope

    // Delegate creation (high-level, expanded during closure pass)
    CreateDelegate,         // Create delegate from nested lambda IR

    // Special
    InitObj,                // Initialize value type
    LoadAddress,            // Load address of local/arg/field
    LoadToken,              // Load runtime type/method/field token
    Switch,                 // Switch table branch
}
