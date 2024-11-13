//#define BUILD_STRUCT

using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal class HoistingSource : IHoistingSource
{
    public ParameterExpression StateMachine { get; init; }
    public LabelTarget ExitLabel { get; init; }
    public MemberExpression StateIdField { get; init; }
    public MemberExpression BuilderField { get; init; }
    public MemberExpression ResultField { get; init; }
    public ParameterExpression ReturnValue { get; init; }

    public HoistingSource(ParameterExpression stateMachine, LabelTarget exitLabel, MemberExpression stateField, MemberExpression builderField, MemberExpression finalResultField, ParameterExpression returnValue)
    {
        StateMachine = stateMachine;
        ExitLabel = exitLabel;
        StateIdField = stateField;
        BuilderField = builderField;
        ResultField = finalResultField;
        ReturnValue = returnValue;
    }

    public void Deconstruct(out ParameterExpression stateMachine, out LabelTarget exitLabel, out MemberExpression stateIdField, out MemberExpression builderField, out MemberExpression resultField, out ParameterExpression returnValue)
    {
        stateMachine = StateMachine;
        exitLabel = ExitLabel;
        stateIdField = StateIdField;
        builderField = BuilderField;
        resultField = ResultField;
        returnValue = ReturnValue;
    }
}
