
using System.Linq.Expressions;
using Hyperbee.Expressions.CompilerServices.Collections;

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
    MemberExpression FinalResultField
);

internal record LoweringInfo
{
    public IReadOnlyList<StateContext.Scope> Scopes { get; init; }
    public IReadOnlyCollection<Expression> Variables { get; init; }
    public LinkedDictionary<ParameterExpression, ParameterExpression> ExternScopes { get; init; }

    public int AwaitCount { get; init; }
    public bool HasFinalResultVariable { get; init; }

}
