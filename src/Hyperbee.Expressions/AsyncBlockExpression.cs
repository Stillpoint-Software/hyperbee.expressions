using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using Hyperbee.Collections;
using Hyperbee.Expressions.CompilerServices;
using Hyperbee.Expressions.CompilerServices.Lowering;

namespace Hyperbee.Expressions;

[DebuggerTypeProxy( typeof( AsyncBlockExpressionDebuggerProxy ) )]
public class AsyncBlockExpression : Expression
{
    private Expression _stateMachine;

    public ReadOnlyCollection<Expression> Expressions { get; }
    public ReadOnlyCollection<ParameterExpression> Variables { get; }
    public ExpressionRuntimeOptions RuntimeOptions { get; }

    internal LinkedDictionary<ParameterExpression, ParameterExpression> ScopedVariables { get; set; }

    public Expression Result => Expressions[^1];

    internal AsyncBlockExpression( ReadOnlyCollection<ParameterExpression> variables, ReadOnlyCollection<Expression> expressions, ExpressionRuntimeOptions options = null )
        : this( variables, expressions, null, options )
    {
    }

    internal AsyncBlockExpression(
        ReadOnlyCollection<ParameterExpression> variables,
        ReadOnlyCollection<Expression> expressions,
        LinkedDictionary<ParameterExpression, ParameterExpression> scopedVariables,
        ExpressionRuntimeOptions options = null
    )
    {
        if ( expressions == null || expressions.Count == 0 )
            throw new ArgumentException( $"{nameof( AsyncBlockExpression )} must contain at least one expression.", nameof( expressions ) );

        Variables = variables;
        Expressions = expressions;
        ScopedVariables = scopedVariables;
        RuntimeOptions = options;

        Type = GetTaskType( Result.Type );
    }

    public override bool CanReduce => true;

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type { get; }

    public override Expression Reduce()
    {
        // Compiler choice flows through CoroutineBuilderContext.Current (ambient or global default),
        // not through RuntimeOptions. RuntimeOptions carries behavioral options only.
        // Two threads reducing the same node both build a state machine type, and whichever
        // loses publishes nothing -- but every caller has to see the same one, or a tree
        // rewritten against one machine ends up running another.

        if ( _stateMachine != null )
            return _stateMachine;

        var stateMachine = AsyncStateMachineBuilder.Create(
            Result.Type,
            LoweringTransformer,
            RuntimeOptions,
            ExternVariables.Create( Variables, Expressions ),
            CanEmitIntoType() );

        return Interlocked.CompareExchange( ref _stateMachine, stateMachine, null ) ?? stateMachine;
    }

    // The body may be emitted into the machine's own method unless it reaches a non-public
    // member -- only a DynamicMethod is created with visibility checks skipped. The scan is a
    // full walk, so the switch is read first and short-circuits it.

    private bool CanEmitIntoType() =>
        (RuntimeOptions?.EmitMoveNextIntoType ?? true)
        && !VisibilityScanner.HasNonPublicReferences( Expressions );

    private AsyncLoweringInfo LoweringTransformer()
    {
        try
        {
            var visitor = new AsyncLoweringVisitor { Optimize = RuntimeOptions?.Optimize ?? true };

            return visitor.Transform(
                Result.Type,
                [.. Variables],
                [.. Expressions],
                ScopedVariables ?? []
            );
        }
        catch ( LoweringException ex )
        {
            throw new InvalidOperationException( $"Unable to lower {nameof( AsyncBlockExpression )}.", ex );
        }
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newVariables = visitor.VisitAndConvert( Variables, nameof( VisitChildren ) );
        var newExpressions = visitor.Visit( Expressions );

        if ( Compare( newVariables, Variables ) && Compare( newExpressions, Expressions ) )
            return this;

        return new AsyncBlockExpression( newVariables, newExpressions, ScopedVariables, RuntimeOptions );
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

    private static Type GetTaskType( Type resultType )
    {
        return resultType == typeof( void )
            ? typeof( Task )
            : typeof( Task<> ).MakeGenericType( resultType );
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

    public static AsyncBlockExpression BlockAsync( Expression[] expressions, ExpressionRuntimeOptions options )
    {
        return new AsyncBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, new ReadOnlyCollection<Expression>( expressions ), options );
    }

    public static AsyncBlockExpression BlockAsync( ParameterExpression[] variables, Expression[] expressions, ExpressionRuntimeOptions options )
    {
        return new AsyncBlockExpression( new ReadOnlyCollection<ParameterExpression>( variables ), new ReadOnlyCollection<Expression>( expressions ), options );
    }

    public static AsyncBlockExpression BlockAsync( ReadOnlyCollection<Expression> expressions, ExpressionRuntimeOptions options )
    {
        return new AsyncBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, expressions, options );
    }

    public static AsyncBlockExpression BlockAsync( ReadOnlyCollection<ParameterExpression> variables, ReadOnlyCollection<Expression> expressions, ExpressionRuntimeOptions options )
    {
        return new AsyncBlockExpression( variables, expressions, options );
    }
}
