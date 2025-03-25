using System.Linq.Expressions;
using Hyperbee.Collections;

namespace Hyperbee.Expressions.CompilerServices.YieldSupport;

//internal sealed class YieldStateMachineContext
//{
//    public StateNode StateNode { get; set; }
//    public YieldStateMachineInfo StateMachineInfo { get; set; }
//    public YieldLoweringInfo LoweringInfo { get; set; }
//}

//internal record YieldStateMachineInfo(
//    ParameterExpression StateMachine,
//    LabelTarget ExitLabel,
//    MemberExpression StateField,
//    MemberExpression CurrentField
//);

//internal record YieldLoweringInfo
//{
//    public IReadOnlyList<StateContext.Scope> Scopes { get; init; }

//    public LinkedDictionary<ParameterExpression, ParameterExpression> ScopedVariables { get; init; }
//}
