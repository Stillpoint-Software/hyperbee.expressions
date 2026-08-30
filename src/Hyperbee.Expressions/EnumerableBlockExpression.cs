using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Hyperbee.Collections;
using Hyperbee.Expressions.CompilerServices;
using Hyperbee.Expressions.CompilerServices.Lowering;

namespace Hyperbee.Expressions;

public class EnumerableBlockExpression : Expression
{
    private Type _enumerableType;
    private Expression _stateMachine;
    public ReadOnlyCollection<Expression> Expressions { get; }
    public ReadOnlyCollection<ParameterExpression> Variables { get; }
    public ExpressionRuntimeOptions RuntimeOptions { get; }

    private LinkedDictionary<ParameterExpression, ParameterExpression> _scopedVariables;

    internal LinkedDictionary<ParameterExpression, ParameterExpression> ScopedVariables
    {
        get => _scopedVariables;

        // Reduce() caches, and this is an input to it, so a later assignment has to drop
        // what was cached rather than leave a machine built from the old scope.
        set
        {
            _scopedVariables = value;
            _stateMachine = null;
        }
    }

    public EnumerableBlockExpression(
        ReadOnlyCollection<ParameterExpression> variables,
        ReadOnlyCollection<Expression> expressions,
        ExpressionRuntimeOptions options = null )
    {
        if ( expressions == null || expressions.Count == 0 )
            throw new ArgumentException( "YieldBlockExpression must contain at least one expression." );

        Variables = variables;
        Expressions = expressions;
        RuntimeOptions = options;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public Type EnumerableType => _enumerableType ??= GetYieldType();
    public override Type Type => typeof( IEnumerable<> ).MakeGenericType( EnumerableType );
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        // Cached because reducing builds a state machine type, and the compilation pipeline
        // reduces a node more than once. Without this each pass emitted its own type,
        // compiled its own MoveNext, and discarded all but the last -- three times over for
        // a single compile, which was most of what BlockEnumerable cost to compile.

        if ( _stateMachine != null )
            return _stateMachine;

        var stateMachine = YieldStateMachineBuilder.Create(
            EnumerableType,
            LoweringTransformer,
            RuntimeOptions,
            ExternVariables.Create( Variables, Expressions ),
            CanEmitIntoType() );

        return Interlocked.CompareExchange( ref _stateMachine, stateMachine, null ) ?? stateMachine;
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        // Without this the base implementation reduces the block and visits the state
        // machine instead of the block's own children -- so merely walking the tree built a
        // state machine type, and a visitor rewriting a variable rewrote lowered code rather
        // than the block. AsyncBlockExpression has always done this.

        var newVariables = visitor.VisitAndConvert( Variables, nameof( VisitChildren ) );
        var newExpressions = visitor.Visit( Expressions );

        if ( AsyncBlockExpression.Compare( newVariables, Variables ) && AsyncBlockExpression.Compare( newExpressions, Expressions ) )
            return this;

        return new EnumerableBlockExpression( newVariables, newExpressions, RuntimeOptions )
        {
            ScopedVariables = ScopedVariables
        };
    }

    // The body may be emitted into the machine's own method unless it reaches a non-public
    // member -- only a DynamicMethod is created with visibility checks skipped. The scan is a
    // full walk, so the switch is read first and short-circuits it.

    private bool CanEmitIntoType() =>
        (RuntimeOptions?.EmitMoveNextIntoType ?? true)
        && !VisibilityScanner.HasNonPublicReferences( Expressions );

    private EnumerableLoweringInfo LoweringTransformer()
    {
        try
        {
            var visitor = new EnumerableLoweringVisitor();

            return visitor.Transform(
                EnumerableType,
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

    private Type GetYieldType()
    {
        return YieldTypeVisitor.Find( Expressions );
    }

    private sealed class YieldTypeVisitor : ExpressionVisitor
    {
        // The visitor carries the type it found, so an instance is used per search. A
        // shared instance would leak the type it resolved into the next block, and a
        // block whose first expression holds no yield would take that stale type.

        private Type _type;

        public static Type Find( IReadOnlyList<Expression> expressions )
        {
            var visitor = new YieldTypeVisitor();

            for ( var index = 0; index < expressions.Count; index++ )
            {
                visitor.Visit( expressions[index] );

                if ( visitor._type != null )
                    return visitor._type;
            }

            return typeof( void );
        }

        protected override Expression VisitExtension( Expression node )
        {
            switch ( node )
            {
                case YieldExpression { IsReturn: true } yieldExpression:
                    _type = yieldExpression.Type;
                    return node;

                // A nested coroutine block yields into its own sequence.

                case EnumerableBlockExpression:
                case AsyncBlockExpression:
                    return node;

                default:
                    return base.VisitExtension( node );
            }
        }
    }


}

public static partial class ExpressionExtensions
{
    public static EnumerableBlockExpression BlockEnumerable( params Expression[] expressions )
    {
        return new EnumerableBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, new ReadOnlyCollection<Expression>( expressions ) );
    }

    public static EnumerableBlockExpression BlockEnumerable( ParameterExpression[] variables, params Expression[] expressions )
    {
        return new EnumerableBlockExpression( new ReadOnlyCollection<ParameterExpression>( variables ), new ReadOnlyCollection<Expression>( expressions ) );
    }

    public static EnumerableBlockExpression BlockEnumerable( ReadOnlyCollection<Expression> expressions )
    {
        return new EnumerableBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, expressions );
    }

    public static EnumerableBlockExpression BlockEnumerable( ReadOnlyCollection<ParameterExpression> variables, ReadOnlyCollection<Expression> expressions )
    {
        return new EnumerableBlockExpression( variables, expressions );
    }

    public static EnumerableBlockExpression BlockEnumerable( Expression[] expressions, ExpressionRuntimeOptions options )
    {
        return new EnumerableBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, new ReadOnlyCollection<Expression>( expressions ), options );
    }

    public static EnumerableBlockExpression BlockEnumerable( ParameterExpression[] variables, Expression[] expressions, ExpressionRuntimeOptions options )
    {
        return new EnumerableBlockExpression( new ReadOnlyCollection<ParameterExpression>( variables ), new ReadOnlyCollection<Expression>( expressions ), options );
    }

    public static EnumerableBlockExpression BlockEnumerable( ReadOnlyCollection<Expression> expressions, ExpressionRuntimeOptions options )
    {
        return new EnumerableBlockExpression( ReadOnlyCollection<ParameterExpression>.Empty, expressions, options );
    }

    public static EnumerableBlockExpression BlockEnumerable( ReadOnlyCollection<ParameterExpression> variables, ReadOnlyCollection<Expression> expressions, ExpressionRuntimeOptions options )
    {
        return new EnumerableBlockExpression( variables, expressions, options );
    }
}
