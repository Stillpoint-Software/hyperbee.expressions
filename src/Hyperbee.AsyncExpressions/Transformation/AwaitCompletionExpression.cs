using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Hyperbee.AsyncExpressions.Transformation;

[DebuggerTypeProxy( typeof(AwaitCompletionExpressionDebuggerProxy) )]
internal class AwaitCompletionExpression : Expression
{
    private readonly ParameterExpression _awaiter;
    private readonly int _stateId;

    private bool _isReduced;
    private Expression _expression;

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
        if ( _isReduced )
            return _expression;

        if ( _resolverSource == null )
            throw new InvalidOperationException( $"Reduce requires a {nameof(FieldResolverSource)} instance." );

        var (stateMachine, fields, returnLabel, stateIdField, builderField) = _resolverSource;

        var awaiterField = fields.First( x => x.Name == _awaiter.Name );
        var awaiterFieldInfo = GetFieldInfo( stateMachine.Type, awaiterField );
        var stateMachineAwaiterField = Field( stateMachine, awaiterFieldInfo );

        _expression = IfThen(
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

        _isReduced = true;
        return _expression;
    }

    private static FieldInfo GetFieldInfo( Type runtimeType, FieldBuilder field )
    {
        return runtimeType.GetField( field.Name, BindingFlags.Instance | BindingFlags.Public )!;
    }

    private class AwaitCompletionExpressionDebuggerProxy( AwaitCompletionExpression node )
    {
        public int StateId => node._stateId;
        public ParameterExpression Awaiter => node._awaiter;
        public bool IsReduced => node._isReduced;
        public Expression Expression => node._expression;
        public FieldResolverSource ResolverSource => node._resolverSource;
    }
}
