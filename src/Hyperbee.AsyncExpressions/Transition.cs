namespace Hyperbee.AsyncExpressions;

public enum TransitionType
{
    None, // Represents a no-op or default transition
    Conditional, // Conditional transitions (e.g., if-else)
    Switch, // Switch case transitions
    TryCatch, // Try-catch-finally transitions
    Loop, // Loop transitions
    Await, // Await transition
    Goto, // Goto transition
    Label, // Label transition
}

public abstract class TransitionNode
{
    public TransitionType TransitionType { get; }
    protected TransitionNode( TransitionType transitionType )
    {
        TransitionType = transitionType;
    }
}

public class ConditionalTransition : TransitionNode
{
    public StateNode IfTrue { get; set; }
    public StateNode IfFalse { get; set; }

    public ConditionalTransition()
        : base( TransitionType.Conditional )
    {
    }
}

public class SwitchTransition : TransitionNode
{
    public List<StateNode> CaseNodes { get; set; } = [];
    public StateNode DefaultNode { get; set; }

    public SwitchTransition()
        : base(TransitionType.Switch )
    {
    }
}

public class TryCatchTransition : TransitionNode
{
    public StateNode TryNode { get; set; }
    public List<StateNode> CatchNodes { get; set; } = [];
    public StateNode FinallyNode { get; set; }

    public TryCatchTransition()
        : base(TransitionType.TryCatch )
    {
    }
}

public class AwaitTransition : TransitionNode
{
    public StateNode CompletionNode { get; set; }

    public AwaitTransition()
        : base( TransitionType.Await )
    {
    }
}

public class AwaitResultTransition : TransitionNode
{
    public StateNode TargetNode { get; set; }

    public AwaitResultTransition()
        : base( TransitionType.Await )
    {
    }
}

public class GotoTransition : TransitionNode
{
    public StateNode TargetNode { get; set; }

    public GotoTransition()
        : base( TransitionType.Goto )
    {
    }
}

public class LabelTransition : TransitionNode
{
    public LabelTransition()
        : base( TransitionType.Label )
    {
    }
}
