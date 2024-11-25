using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Expressions.Transformation;

namespace Hyperbee.Expressions;

[DebuggerTypeProxy( typeof( AsyncBlockExpressionDebuggerProxy ) )]
public class AsyncBlockExpression : Expression
{
    private readonly Type _taskType;
    private Expression _stateMachine;
    private List<ParameterExpression> _sharedScopeVariables;

    public ReadOnlyCollection<Expression> Expressions { get; }
    public ReadOnlyCollection<ParameterExpression> Variables { get; }
    public Expression Result => Expressions[^1];

    internal AsyncBlockExpression( ReadOnlyCollection<ParameterExpression> variables, ReadOnlyCollection<Expression> expressions )
        : this( variables, expressions, null ) { }

    internal AsyncBlockExpression( ReadOnlyCollection<ParameterExpression> variables, ReadOnlyCollection<Expression> expressions, List<ParameterExpression> sharedScopeVariables )
    {
        if ( expressions == null || expressions.Count == 0 )
            throw new ArgumentException( $"{nameof( AsyncBlockExpression )} must contain at least one expression.", nameof( expressions ) );

        Variables = variables;
        Expressions = expressions;
        _sharedScopeVariables = sharedScopeVariables;

        _taskType = GetTaskType( Result.Type );
    }

    public override bool CanReduce => true;

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => _taskType;

    public override Expression Reduce()
    {
        if ( _stateMachine != null )
            return _stateMachine;

        // create state-machine

        var visitor = new LoweringVisitor();

        var source = visitor.Transform( [.. Variables], [.. Expressions], _sharedScopeVariables != null ? [.. _sharedScopeVariables] : [] );

        if ( source.AwaitCount == 0 )
            throw new InvalidOperationException( $"{nameof( AsyncBlockExpression )} must contain at least one await." );

        _stateMachine = StateMachineBuilder.Create( Result.Type, source );

        return _stateMachine;
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newVariables = visitor.VisitAndConvert( Variables, nameof( VisitChildren ) );
        var newExpressions = visitor.Visit( Expressions );

        if ( Compare( newVariables, Variables ) && Compare( newExpressions, Expressions ) )
            return this;

        return new AsyncBlockExpression( newVariables, newExpressions, _sharedScopeVariables );
    }

    internal static bool Compare<T>( ICollection<T> compare, IReadOnlyList<T> current )
        where T : class
    {
        if ( ReferenceEquals( compare, current ) )
            return true;

        if ( compare == null )
            return current.Count == 0;

        if ( compare.Count != current.Count )
            return false;

        using var comparand = compare.GetEnumerator();

        for ( var i = 0; i < current.Count; i++ )
        {
            comparand.MoveNext();

            if ( !ReferenceEquals( comparand.Current, current[i] ) )
                return false;
        }

        return true;
    }

    internal void SetSharedScopeVariables( List<ParameterExpression> shared )
    {
        _sharedScopeVariables = shared;
    }


    private static Type GetTaskType( Type resultType )
    {
        return resultType == typeof( void ) ? typeof( Task ) : typeof( Task<> ).MakeGenericType( resultType );
    }

    private class AsyncBlockExpressionDebuggerProxy( AsyncBlockExpression node )
    {
        public Expression StateMachine => node._stateMachine;

        public ReadOnlyCollection<Expression> Expressions => node.Expressions;
        public ReadOnlyCollection<ParameterExpression> Variables => node.Variables;
        public Expression Result => node.Result;
    }
}

public static partial class ExpressionExtensions
{
    public static AsyncBlockExpression BlockAsync( params Expression[] expressions )
    {
        return new AsyncBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, new ReadOnlyCollection<Expression>( expressions ) );
    }

    public static AsyncBlockExpression BlockAsync( ParameterExpression[] variables, params Expression[] expressions )
    {
        return new AsyncBlockExpression( new ReadOnlyCollection<ParameterExpression>( variables ), new ReadOnlyCollection<Expression>( expressions ) );
    }

    public static AsyncBlockExpression BlockAsync( ReadOnlyCollection<Expression> expressions )
    {
        return new AsyncBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, expressions );
    }

    public static AsyncBlockExpression BlockAsync( ReadOnlyCollection<ParameterExpression> variables, ReadOnlyCollection<Expression> expressions )
    {
        return new AsyncBlockExpression( variables, expressions );
    }
}
