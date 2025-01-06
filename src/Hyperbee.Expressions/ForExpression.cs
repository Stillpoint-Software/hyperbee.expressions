using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public class ForExpression : Expression
{
    public IEnumerable<ParameterExpression> Variables { get; }
    public Expression Initialization { get; }
    public Expression Test { get; }
    public Expression Iteration { get; }
    public Expression Body { get; }

    public LabelTarget BreakLabel { get; } = Label( "break" );
    public LabelTarget ContinueLabel { get; } = Label( "continue" );

    internal ForExpression( IEnumerable<ParameterExpression> variables, Expression initialization, Expression test, Expression iteration, Expression body )
    {
        ThrowIfInvalid( test, body );

        Variables = variables;
        Initialization = initialization;
        Test = test;
        Iteration = iteration;
        Body = body;
    }

    internal ForExpression( IEnumerable<ParameterExpression> variables, Expression initialization, Expression test, Expression iteration, LoopBody body )
    {
        ThrowIfInvalid( test, body );

        Variables = variables;
        Initialization = initialization;
        Test = test;
        Iteration = iteration;
        Body = body( BreakLabel, ContinueLabel );
    }

    internal ForExpression( IEnumerable<ParameterExpression> variables, Expression initialization, Expression test, Expression iteration, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        ThrowIfInvalid( test, body );

        ArgumentNullException.ThrowIfNull( breakLabel, nameof( breakLabel ) );
        ArgumentNullException.ThrowIfNull( continueLabel, nameof( continueLabel ) );

        Variables = variables;
        Initialization = initialization;
        Test = test;
        Iteration = iteration;
        Body = body;
        BreakLabel = breakLabel;
        ContinueLabel = continueLabel;
    }

    private static void ThrowIfInvalid( Expression test, object body )
    {
        ArgumentNullException.ThrowIfNull( test, nameof( test ) );
        ArgumentNullException.ThrowIfNull( body, nameof( body ) );

        if ( test.Type != typeof( bool ) )
            throw new ArgumentException( "Test expression must return a boolean.", nameof( test ) );
    }

    public override Type Type => typeof( void );
    public override ExpressionType NodeType => ExpressionType.Extension;

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        return Block(
            Variables,
            Initialization,
            Loop(
                IfThenElse(
                    Test,
                    Block(
                        Body,
                        Iteration
                    ),
                    Break( BreakLabel )
                ),
                BreakLabel,
                ContinueLabel
            )
        );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newVariables = VisitCollection( visitor, Variables.ToArray() );
        var newInitialization = visitor.Visit( Initialization );
        var newTest = visitor.Visit( Test );
        var newIteration = visitor.Visit( Iteration );
        var newBody = visitor.Visit( Body );

        if ( newVariables == Variables && newInitialization == Initialization && newTest == Test && newIteration == Iteration && newBody == Body )
            return this;

        return new ForExpression( newVariables, newInitialization, newTest, newIteration, newBody, BreakLabel, ContinueLabel );

    }

    private static IEnumerable<T> VisitCollection<T>( ExpressionVisitor visitor, T[] nodes ) where T : Expression
    {
        T[] newNodes = null;

        for ( int i = 0, n = nodes.Length; i < n; i++ )
        {
            var node = visitor.Visit( nodes[i] );

            if ( newNodes != null )
            {
                newNodes[i] = (T) node;
            }
            else if ( !ReferenceEquals( node, nodes[i] ) )
            {
                newNodes = new T[n];
                for ( int j = 0; j < i; j++ )
                {
                    newNodes[j] = nodes[j];
                }
                newNodes[i] = (T) node;
            }
        }

        if ( newNodes == null )
        {
            return nodes;
        }

        return newNodes;
    }
}

public static partial class ExpressionExtensions
{
    public static ForExpression For( Expression initialization, Expression test, Expression iteration, Expression body )
    {
        return new ForExpression( [], initialization, test, iteration, body );
    }

    public static ForExpression For( Expression initialization, Expression test, Expression iteration, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        return new ForExpression( [], initialization, test, iteration, body, breakLabel, continueLabel );
    }

    public static ForExpression For( Expression initialization, Expression test, Expression iteration, LoopBody body )
    {
        return new ForExpression( [], initialization, test, iteration, body );
    }

    public static ForExpression For( IEnumerable<ParameterExpression> variables, Expression initialization, Expression test, Expression iteration, Expression body )
    {
        return new ForExpression( variables, initialization, test, iteration, body );
    }

    public static ForExpression For( IEnumerable<ParameterExpression> variables, Expression initialization, Expression test, Expression iteration, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        return new ForExpression( variables, initialization, test, iteration, body, breakLabel, continueLabel );
    }

    public static ForExpression For( IEnumerable<ParameterExpression> variables, Expression initialization, Expression test, Expression iteration, LoopBody body )
    {
        return new ForExpression( variables, initialization, test, iteration, body );
    }
}
