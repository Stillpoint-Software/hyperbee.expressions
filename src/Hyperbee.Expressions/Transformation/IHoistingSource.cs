using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal interface IHoistingSource
{
    Expression StateMachine { get; init; }
    LabelTarget ExitLabel { get; init; }
    MemberExpression StateIdField { get; init; }
    MemberExpression BuilderField { get; init; }
    MemberExpression ResultField { get; init; }
    ParameterExpression ReturnValue { get; init; }

    public void Deconstruct(
        out Expression stateMachine, out LabelTarget exitLabel,
        out MemberExpression stateIdField, out MemberExpression builderField,
        out MemberExpression resultField, out ParameterExpression returnValue )
    {
        stateMachine = StateMachine;
        exitLabel = ExitLabel;
        stateIdField = StateIdField;
        builderField = BuilderField;
        resultField = ResultField;
        returnValue = ReturnValue;
    }
}
