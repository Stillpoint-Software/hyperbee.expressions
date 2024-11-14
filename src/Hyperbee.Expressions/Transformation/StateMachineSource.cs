//#define BUILD_STRUCT

using System.Linq.Expressions;

namespace Hyperbee.Expressions.Transformation;

internal record StateMachineSource( 
    ParameterExpression StateMachine,
    LabelTarget ExitLabel,
    MemberExpression StateIdField,
    MemberExpression BuilderField,
    MemberExpression ResultField,
    ParameterExpression ReturnValue
);
