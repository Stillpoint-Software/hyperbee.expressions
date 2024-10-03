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
}
