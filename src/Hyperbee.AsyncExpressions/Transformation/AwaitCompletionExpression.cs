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
    private FieldResolverSource _resolverSource;

    public AwaitCompletionExpression( ParameterExpression awaiter, int stateId )
    {
        _awaiter = awaiter;
        _stateId = stateId;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    public Expression Reduce( FieldResolverSource resolverSource )
    {
        _resolverSource = resolverSource;
        return Reduce();
    }

    public override Expression Reduce()
    {
        var (stateMachine, fields, returnLabel, stateIdField, builderField) = _resolverSource;

        var awaiterField = fields.First( x => x.Name == _awaiter.Name );
        var awaiterFieldInfo = GetFieldInfo( stateMachine.Type, awaiterField );
        var stateMachineAwaiterField = Field( stateMachine, awaiterFieldInfo );

        return IfThen(
            IsFalse( Property( stateMachineAwaiterField, "IsCompleted" ) ),
            Block(
                Assign( stateIdField, Constant( _stateId ) ),
                Call(
                    builderField,
                    "AwaitUnsafeOnCompleted",
                    [awaiterField.FieldType, typeof(IAsyncStateMachine)],
                    stateMachineAwaiterField,
                    stateMachine
                ),
                Return( returnLabel )
            )
        );
    }

    private static FieldInfo GetFieldInfo( Type runtimeType, FieldBuilder field )
    {
        return runtimeType.GetField( field.Name, BindingFlags.Instance | BindingFlags.Public )!;
    }
}
