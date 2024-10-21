using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal interface IHoistingSource
{
    //public Type StateMachineType { get; init; }
    //Expression StateMachine { get; init; }
    public ParameterExpression StateMachine { get; init; }
    //public ParameterExpression StateMachineData { get; init; }

    LabelTarget ExitLabel { get; init; }
    MemberExpression StateIdField { get; init; }
    MemberExpression BuilderField { get; init; }
    MemberExpression ResultField { get; init; }
    ParameterExpression ReturnValue { get; init; }

    public void Deconstruct( 
        out ParameterExpression stateMachine,
        //out ParameterExpression stateMachineData, 
        out LabelTarget exitLabel,
        out MemberExpression stateIdField, out MemberExpression builderField,
        out MemberExpression resultField, out ParameterExpression returnValue )
    {
        stateMachine = StateMachine;
        //stateMachineData = StateMachineData;
        exitLabel = ExitLabel;
        stateIdField = StateIdField;
        builderField = BuilderField;
        resultField = ResultField;
        returnValue = ReturnValue;
    }

    //public void Deconstruct( out Type stateMachineType,
    //    out Expression stateMachine, out LabelTarget exitLabel,
    //    out MemberExpression stateIdField, out MemberExpression builderField,
    //    out MemberExpression resultField, out ParameterExpression returnValue )
    //{
    //    stateMachineType = StateMachineType;
    //    stateMachine = StateMachine;
    //    exitLabel = ExitLabel;
    //    stateIdField = StateIdField;
    //    builderField = BuilderField;
    //    resultField = ResultField;
    //    returnValue = ReturnValue;
    //}
}
