using System.Diagnostics;
using System.Linq.Expressions;
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
    private IFieldResolverSource _resolverSource;
    internal static class Constants
    {
        internal const string AwaiterIsCompleted = "IsCompleted";
        internal const string BuilderAwaitUnsafeOnCompleted = "AwaitUnsafeOnCompleted";
    }

    public AwaitCompletionExpression( ParameterExpression awaiter, int stateId )
    {
        _awaiter = awaiter;
        _stateId = stateId;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof( void );
    public override bool CanReduce => true;

    public Expression Reduce( IFieldResolverSource resolverSource )
    {
        _resolverSource = resolverSource;
        return Reduce();
    }

    public override Expression Reduce()
    {
        if ( _isReduced )
            return _expression;

        if ( _resolverSource == null )
            throw new InvalidOperationException( $"Reduce requires an {nameof(IFieldResolverSource)} instance." );

        var awaiterField = _resolverSource.Fields
            .First( x => x.Member.Name == _awaiter.Name );

        _expression = IfThen(
            IsFalse( Property( awaiterField, Constants.AwaiterIsCompleted ) ),
            Block(
                Assign( _resolverSource.StateIdField, Constant( _stateId ) ),
                Call(
                    _resolverSource.BuilderField,
                    Constants.BuilderAwaitUnsafeOnCompleted,
                    [awaiterField.Type, typeof(IAsyncStateMachine)],
                    awaiterField,
                    _resolverSource.StateMachine
                ),
                Return( _resolverSource.ReturnLabel )
            )
        );

        _isReduced = true;
        return _expression;
    }

    private class AwaitCompletionExpressionDebuggerProxy( AwaitCompletionExpression node )
    {
        public int StateId => node._stateId;
        public ParameterExpression Awaiter => node._awaiter;
        public bool IsReduced => node._isReduced;
        public Expression Expression => node._expression;
        public IFieldResolverSource ResolverSource => node._resolverSource;
    }

}
