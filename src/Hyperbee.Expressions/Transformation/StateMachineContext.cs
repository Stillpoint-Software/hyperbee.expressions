
using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal sealed class StateMachineContext
{
    public NodeInfo NodeInfo { get; set; }
    public StateMachineInfo StateMachineInfo { get; set; }
    public LoweringInfo LoweringInfo { get; set; }
}

internal record NodeInfo(
    int StateOrder,
    Expression ResultVariable,
    Expression ResultValue
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
    public List<StateContext.Scope> Scopes { get; init; }
    public ParameterExpression ReturnValue { get; init; }
    public int AwaitCount { get; init; }
    public IReadOnlyCollection<Expression> Variables { get; init; }
    public ParameterExpression[] ExternVariables { get; internal set; }

    public IEnumerable<NodeExpression> Nodes => Scopes.SelectMany( scope => scope.Nodes );
}
