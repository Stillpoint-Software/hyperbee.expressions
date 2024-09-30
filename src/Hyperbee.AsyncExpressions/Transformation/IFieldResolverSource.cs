using System.Linq.Expressions;

namespace Hyperbee.AsyncExpressions.Transformation;

internal interface IFieldResolverSource
{
    Expression StateMachine { get; init; }
    MemberExpression[] Fields { get; init; }
    LabelTarget ReturnLabel { get; init; }
    MemberExpression StateIdField { get; init; }
    MemberExpression BuilderField { get; init; }
}
