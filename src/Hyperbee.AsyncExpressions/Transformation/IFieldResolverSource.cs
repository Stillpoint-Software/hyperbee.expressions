using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

internal interface IFieldResolverSource
{
    public Type StateMachineType { get; init; }
    Expression StateMachine { get; init; }
    LabelTarget ReturnLabel { get; init; }
    MemberExpression StateIdField { get; init; }
    MemberExpression BuilderField { get; init; }
    MemberExpression ResultField { get; init; }
    ParameterExpression ReturnValue { get; init; }

    public void Deconstruct( out Type stateMachineType, 
        out Expression stateMachine, out LabelTarget returnLabel, 
        out MemberExpression stateIdField, out MemberExpression builderField,
        out MemberExpression resultField, out ParameterExpression returnValue )
    {
        stateMachineType = StateMachineType;
        stateMachine = StateMachine;
        returnLabel = ReturnLabel;
        stateIdField = StateIdField;
        builderField = BuilderField;
        resultField = ResultField;
        returnValue = ReturnValue;
    }
}
