using System.Linq.Expressions;
using System.Reflection;
using Hyperbee.Collections;

namespace Hyperbee.Expressions.CompilerServices;

internal sealed class StateMachineContext
{
    public StateNode StateNode { get; set; }
    public StateMachineInfo StateMachineInfo { get; set; }
    public LoweringInfo LoweringInfo { get; set; }

    // Maps a hoisted variable to its state-machine field. Keyed by ParameterExpression
    // instance: distinct instances may share a name, so names cannot identify a variable.

    public IReadOnlyDictionary<ParameterExpression, FieldInfo> VariableFields { get; set; }

    // Variables the body reads from the enclosing scope, carried by field so the body stays closed.

    public ExternVariables ExternVariables { get; set; }
}

internal record StateMachineInfo(
    ParameterExpression StateMachine,
    LabelTarget ExitLabel,
    MemberExpression StateField
);

internal record AsyncStateMachineInfo(
    ParameterExpression StateMachine,
    LabelTarget ExitLabel,
    MemberExpression StateField,
    MemberExpression BuilderField,
    MemberExpression FinalResultField
) : StateMachineInfo( StateMachine, ExitLabel, StateField );

internal record EnumerableStateMachineInfo(
    ParameterExpression StateMachine,
    LabelTarget ExitLabel,
    MemberExpression StateField,
    MemberExpression CurrentField,
    ParameterExpression Success
) : StateMachineInfo( StateMachine, ExitLabel, StateField );

internal record LoweringInfo
{
    public IReadOnlyList<StateContext.Scope> Scopes { get; init; }

    public LinkedDictionary<ParameterExpression, ParameterExpression> ScopedVariables { get; init; }
}

internal record AsyncLoweringInfo : LoweringInfo
{
    public int AwaitCount { get; init; }
    public bool HasFinalResultVariable { get; init; }
}

internal record EnumerableLoweringInfo : LoweringInfo
{
    public IReadOnlyCollection<ParameterExpression> Variables { get; init; }
}
