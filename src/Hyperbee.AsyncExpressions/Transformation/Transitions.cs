using System.Diagnostics;

namespace Hyperbee.AsyncExpressions.Transformation;

[DebuggerDisplay( "Transition = {GetType().Name,nq}" )]

public abstract class Transition;

public class AwaitResultTransition : Transition
{
    public StateNode TargetNode { get; set; }
}

public class AwaitTransition : Transition
{
    public StateNode CompletionNode { get; set; }
}

public class ConditionalTransition : Transition
{
    public StateNode IfTrue { get; set; }
    public StateNode IfFalse { get; set; }
}

public class GotoTransition : Transition
{
    public StateNode TargetNode { get; set; }
}

public class SwitchTransition : Transition
{
    public List<StateNode> CaseNodes { get; set; } = [];
    public StateNode DefaultNode { get; set; }
}

public class TryCatchTransition : Transition
{
    public StateNode TryNode { get; set; }
    public List<StateNode> CatchNodes { get; set; } = [];
    public StateNode FinallyNode { get; set; }
}
