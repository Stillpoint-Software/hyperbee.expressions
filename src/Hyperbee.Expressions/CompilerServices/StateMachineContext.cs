
using System.Linq.Expressions;
using Hyperbee.Collections;

namespace Hyperbee.Expressions.CompilerServices;

internal sealed class StateMachineContext
{
    public StateNode StateNode { get; set; }
    public StateMachineInfo StateMachineInfo { get; set; }
    public LoweringInfo LoweringInfo { get; set; }
}

internal record StateMachineInfo(
    ParameterExpression StateMachine,
    LabelTarget ExitLabel,
    MemberExpression StateField,
    MemberExpression BuilderField,
    MemberExpression FinalResultField,
    MemberExpression CurrentField
);

internal record LoweringInfo
{
    public IReadOnlyList<StateContext.Scope> Scopes { get; init; }

    public LinkedDictionary<ParameterExpression, ParameterExpression> ScopedVariables { get; init; }

    public int AwaitCount { get; init; }
    public bool HasFinalResultVariable { get; init; }
}
