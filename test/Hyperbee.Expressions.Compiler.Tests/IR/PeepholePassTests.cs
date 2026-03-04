using Hyperbee.Expressions.Compiler.IR;
using Hyperbee.Expressions.Compiler.Passes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperbee.Expressions.Compiler.Tests.IR;

[TestClass]
public class PeepholePassTests
{
    // --- Pattern 1: StoreLocal X; LoadLocal X -> Dup; StoreLocal X ---

    [TestMethod]
    public void StoreLocal_LoadLocal_SameIndex_BecomeDup_StoreLocal()
    {
        var ir = new IRBuilder();
        var local = ir.DeclareLocal( typeof( int ), "x" );

        // Simulate: some value on stack, StoreLocal 0, LoadLocal 0
        ir.Emit( IROp.StoreLocal, local );
        ir.Emit( IROp.LoadLocal, local );

        var modified = PeepholePass.Run( ir );

        Assert.IsTrue( modified );
        Assert.AreEqual( 2, ir.Instructions.Count );
        Assert.AreEqual( IROp.Dup, ir.Instructions[0].Op );
        Assert.AreEqual( IROp.StoreLocal, ir.Instructions[1].Op );
        Assert.AreEqual( local, ir.Instructions[1].Operand );
    }

    [TestMethod]
    public void StoreLocal_LoadLocal_DifferentIndex_NoChange()
    {
        var ir = new IRBuilder();
        var local0 = ir.DeclareLocal( typeof( int ), "x" );
        var local1 = ir.DeclareLocal( typeof( int ), "y" );

        ir.Emit( IROp.StoreLocal, local0 );
        ir.Emit( IROp.LoadLocal, local1 );

        var modified = PeepholePass.Run( ir );

        Assert.IsFalse( modified );
        Assert.AreEqual( 2, ir.Instructions.Count );
        Assert.AreEqual( IROp.StoreLocal, ir.Instructions[0].Op );
        Assert.AreEqual( IROp.LoadLocal, ir.Instructions[1].Op );
    }

    // --- Pattern 2: LoadConst; Pop -> removed ---

    [TestMethod]
    public void LoadConst_Pop_BothRemoved()
    {
        var ir = new IRBuilder();
        var operand = ir.AddOperand( 42 );

        ir.Emit( IROp.LoadConst, operand );
        ir.Emit( IROp.Pop );

        var modified = PeepholePass.Run( ir );

        Assert.IsTrue( modified );
        Assert.AreEqual( 0, ir.Instructions.Count );
    }

    // --- Pattern 3: LoadNull; Pop -> removed ---

    [TestMethod]
    public void LoadNull_Pop_BothRemoved()
    {
        var ir = new IRBuilder();

        ir.Emit( IROp.LoadNull );
        ir.Emit( IROp.Pop );

        var modified = PeepholePass.Run( ir );

        Assert.IsTrue( modified );
        Assert.AreEqual( 0, ir.Instructions.Count );
    }

    // --- Pattern 4: Box T; UnboxAny T -> removed (identity roundtrip) ---

    [TestMethod]
    public void Box_UnboxAny_SameOperand_BothRemoved()
    {
        var ir = new IRBuilder();
        var typeOperand = ir.AddOperand( typeof( int ) );

        ir.Emit( IROp.Box, typeOperand );
        ir.Emit( IROp.UnboxAny, typeOperand );

        var modified = PeepholePass.Run( ir );

        Assert.IsTrue( modified );
        Assert.AreEqual( 0, ir.Instructions.Count );
    }

    [TestMethod]
    public void Box_UnboxAny_DifferentOperand_NoChange()
    {
        var ir = new IRBuilder();
        var typeOperand1 = ir.AddOperand( typeof( int ) );
        var typeOperand2 = ir.AddOperand( typeof( double ) );

        ir.Emit( IROp.Box, typeOperand1 );
        ir.Emit( IROp.UnboxAny, typeOperand2 );

        var modified = PeepholePass.Run( ir );

        Assert.IsFalse( modified );
        Assert.AreEqual( 2, ir.Instructions.Count );
    }

    // --- Pattern 5: LoadLocal X; Pop -> removed ---

    [TestMethod]
    public void LoadLocal_Pop_BothRemoved()
    {
        var ir = new IRBuilder();
        var local = ir.DeclareLocal( typeof( int ), "x" );

        ir.Emit( IROp.LoadLocal, local );
        ir.Emit( IROp.Pop );

        var modified = PeepholePass.Run( ir );

        Assert.IsTrue( modified );
        Assert.AreEqual( 0, ir.Instructions.Count );
    }

    // --- Pattern 6: Dup; Pop -> removed ---

    [TestMethod]
    public void Dup_Pop_BothRemoved()
    {
        var ir = new IRBuilder();

        ir.Emit( IROp.Dup );
        ir.Emit( IROp.Pop );

        var modified = PeepholePass.Run( ir );

        Assert.IsTrue( modified );
        Assert.AreEqual( 0, ir.Instructions.Count );
    }

    // --- Pattern 7: Branch to next label -> removed ---

    [TestMethod]
    public void Branch_ToNextLabel_Removed()
    {
        var ir = new IRBuilder();
        var label = ir.DefineLabel();

        ir.Emit( IROp.Branch, label );
        ir.MarkLabel( label );

        var modified = PeepholePass.Run( ir );

        Assert.IsTrue( modified );
        // Only the Label instruction should remain
        Assert.AreEqual( 1, ir.Instructions.Count );
        Assert.AreEqual( IROp.Label, ir.Instructions[0].Op );
        Assert.AreEqual( label, ir.Instructions[0].Operand );
    }

    [TestMethod]
    public void Branch_ToDistantLabel_NoChange()
    {
        var ir = new IRBuilder();
        var label = ir.DefineLabel();

        ir.Emit( IROp.Branch, label );
        ir.Emit( IROp.Nop ); // something between branch and label
        ir.MarkLabel( label );

        var modified = PeepholePass.Run( ir );

        Assert.IsFalse( modified );
        Assert.AreEqual( 3, ir.Instructions.Count );
        Assert.AreEqual( IROp.Branch, ir.Instructions[0].Op );
    }

    // --- No modification when patterns don't match ---

    [TestMethod]
    public void UnrelatedInstructions_NoChange()
    {
        var ir = new IRBuilder();
        var local = ir.DeclareLocal( typeof( int ), "x" );

        ir.Emit( IROp.LoadArg, 0 );
        ir.Emit( IROp.StoreLocal, local );
        ir.Emit( IROp.LoadArg, 1 );
        ir.Emit( IROp.Ret );

        var modified = PeepholePass.Run( ir );

        Assert.IsFalse( modified );
        Assert.AreEqual( 4, ir.Instructions.Count );
    }

    // --- Multiple patterns in sequence ---

    [TestMethod]
    public void MultiplePatterns_AllApplied()
    {
        var ir = new IRBuilder();
        var local = ir.DeclareLocal( typeof( int ), "x" );
        var operand = ir.AddOperand( 99 );

        // Pattern 5: LoadLocal; Pop (dead local load)
        ir.Emit( IROp.LoadLocal, local );
        ir.Emit( IROp.Pop );

        // Pattern 2: LoadConst; Pop (dead constant load)
        ir.Emit( IROp.LoadConst, operand );
        ir.Emit( IROp.Pop );

        // Something that stays
        ir.Emit( IROp.LoadArg, 0 );
        ir.Emit( IROp.Ret );

        var modified = PeepholePass.Run( ir );

        Assert.IsTrue( modified );
        Assert.AreEqual( 2, ir.Instructions.Count );
        Assert.AreEqual( IROp.LoadArg, ir.Instructions[0].Op );
        Assert.AreEqual( IROp.Ret, ir.Instructions[1].Op );
    }

    [TestMethod]
    public void SingleInstruction_NoChange()
    {
        var ir = new IRBuilder();
        ir.Emit( IROp.Ret );

        var modified = PeepholePass.Run( ir );

        Assert.IsFalse( modified );
        Assert.AreEqual( 1, ir.Instructions.Count );
    }

    [TestMethod]
    public void EmptyInstructionStream_NoChange()
    {
        var ir = new IRBuilder();

        var modified = PeepholePass.Run( ir );

        Assert.IsFalse( modified );
        Assert.AreEqual( 0, ir.Instructions.Count );
    }
}
