using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

internal interface IFieldResolverSource
{
    Expression StateMachine { get; init; }
    MemberExpression StateIdField { get; init; }
    MemberExpression BuilderField { get; init; }
    MemberExpression ResultField { get; init; }
    ParameterExpression ReturnValue { get; init; }
}
