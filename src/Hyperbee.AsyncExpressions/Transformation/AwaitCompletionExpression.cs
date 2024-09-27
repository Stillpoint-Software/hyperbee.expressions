using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Transformation;

internal class AwaitCompletionExpression : Expression
{
    private readonly ParameterExpression _awaiter;
    private readonly int _stateId;

    // initialize before reduce
    private Expression _stateMachine;
    private List<FieldBuilder> _fields;
    private LabelTarget _returnLabel;
    private MemberExpression _stateIdField;
    private MemberExpression _builderField;

    public AwaitCompletionExpression( ParameterExpression awaiter, int stateId )
    {
        _awaiter = awaiter;
        _stateId = stateId;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        var awaiterField = _fields.First( x => x.Name == _awaiter.Name );
        var awaiterFieldInfo = GetFieldInfo( _stateMachine.Type, awaiterField );
        var stateMachineAwaiterField = Field( _stateMachine, awaiterFieldInfo );

        return IfThen(
            IsFalse( Property( stateMachineAwaiterField, "IsCompleted" ) ),
            Block(
                Assign( _stateIdField, Constant( _stateId ) ),
                Call(
                    _builderField,
                    "AwaitUnsafeOnCompleted",
                    [awaiterField.FieldType, typeof(IAsyncStateMachine)],
                    stateMachineAwaiterField,
                    _stateMachine
                ),
                Return( _returnLabel )
            )
        );

        static FieldInfo GetFieldInfo( Type runtimeType, FieldBuilder field )
        {
            return runtimeType.GetField( field.Name, BindingFlags.Instance | BindingFlags.Public )!;
        }
    }

    public void Initialize( Expression stateMachine, List<FieldBuilder> fields, LabelTarget returnLabel, MemberExpression stateIdField, MemberExpression buildField )
    {
        _stateMachine = stateMachine;
        _fields = fields;
        _returnLabel = returnLabel;
        _stateIdField = stateIdField;
        _builderField = buildField;
    }
}
