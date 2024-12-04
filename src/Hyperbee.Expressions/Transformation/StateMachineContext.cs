
using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal sealed class StateMachineContext
{
    public StateInfo StateInfo { get; set; } //BF ME - Should this just be: IStateNode CurrentState { get; set; } ?? Feels like _maybe_??
    public StateMachineInfo StateMachineInfo { get; set; }
    public LoweringInfo LoweringInfo { get; set; }
}

internal record StateInfo(
    int StateOrder,
    StateResult Result
);

internal record StateMachineInfo(
    ParameterExpression StateMachine,
    LabelTarget ExitLabel,
    MemberExpression StateField,
    MemberExpression BuilderField,
    MemberExpression FinalResultField
);

internal record LoweringInfo
{
    public IReadOnlyList<StateContext.Scope> Scopes { get; init; }
    public IReadOnlyCollection<Expression> Variables { get; init; }
    public IReadOnlyCollection<ParameterExpression> ExternVariables { get; init; }
    public ParameterExpression ReturnValue { get; init; }
    public int AwaitCount { get; init; }
}
