using System;
using System.Collections.Generic;
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

    internal LinkedDictionary<ParameterExpression, ParameterExpression> ScopedVariables { get; set; }

    private static YieldTypeVisitor TypeVisitor = new();

    public EnumerableBlockExpression(
        ReadOnlyCollection<ParameterExpression> variables,
        ReadOnlyCollection<Expression> expressions )
    {
        if ( expressions == null || expressions.Count == 0 )
            throw new ArgumentException( "YieldBlockExpression must contain at least one expression." );

        Variables = variables;
        Expressions = expressions;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public Type EnumerableType => _enumerableType ??= GetYieldType();
    public override Type Type => typeof( IEnumerable<> ).MakeGenericType( EnumerableType );
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        return YieldStateMachineBuilder.Create( EnumerableType, LoweringTransformer );
    }

    private YieldLoweringInfo LoweringTransformer()
    {
        try
        {
            var visitor = new YieldLoweringVisitor();

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
        return TypeVisitor.Find( [.. Expressions] );
    }


    private sealed class YieldTypeVisitor : ExpressionVisitor
    {
        private Type _type;

        public Type Find( Expression[] expressions )
        {
            foreach ( var expression in expressions )
            {
                Visit( expression );
                if ( _type != null )
                    return _type;
            }

            return typeof( void );
        }

        protected override Expression VisitExtension( Expression node )
        {
            if ( node is not YieldExpression { IsReturn: true } yieldExpression )
                return base.VisitExtension( node );

            _type = yieldExpression.Type;
            return node;
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
}
