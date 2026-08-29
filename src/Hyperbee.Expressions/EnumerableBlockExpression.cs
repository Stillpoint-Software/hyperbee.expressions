using System.Collections.ObjectModel;
using System.Linq.Expressions;
using Hyperbee.Collections;
using Hyperbee.Expressions.CompilerServices;
using Hyperbee.Expressions.CompilerServices.Lowering;

namespace Hyperbee.Expressions;

public class EnumerableBlockExpression : Expression
{
    private Type _enumerableType;
    public ReadOnlyCollection<Expression> Expressions { get; }
    public ReadOnlyCollection<ParameterExpression> Variables { get; }
    public ExpressionRuntimeOptions RuntimeOptions { get; }

    internal LinkedDictionary<ParameterExpression, ParameterExpression> ScopedVariables { get; set; }

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
        return YieldStateMachineBuilder.Create(
            EnumerableType,
            LoweringTransformer,
            RuntimeOptions,
            ExternVariables.Create( Variables, Expressions ) );
    }

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
