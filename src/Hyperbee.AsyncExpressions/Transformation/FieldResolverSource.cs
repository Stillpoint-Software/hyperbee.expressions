using System.Linq.Expressions;
using System.Reflection.Emit;

namespace Hyperbee.AsyncExpressions.Transformation;

internal record FieldResolverSource
{
    public Expression StateMachine { get; init; }
    public List<FieldBuilder> Fields { get; init; }
    public LabelTarget ReturnLabel { get; init; }
    public MemberExpression StateIdField { get; init; }
    public MemberExpression BuilderField { get; init; }

    public void Deconstruct( 
        out Expression stateMachine, 
        out List<FieldBuilder> fields, 
        out LabelTarget returnLabel, 
        out MemberExpression stateIdField, 
        out MemberExpression builderField )
    {
        stateMachine = StateMachine;
        fields = Fields;
        returnLabel = ReturnLabel;
        stateIdField = StateIdField;
        builderField = BuilderField;
    }
}
