using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public class ForExpression : Expression
{
    public Expression Initialization { get; }
    public Expression Test { get; }
    public Expression Iteration { get; }
    public Expression Body { get; }

    public LabelTarget BreakLabel { get; } = Label( "break" );
    public LabelTarget ContinueLabel { get; } = Label( "continue" );

    internal ForExpression( Expression initialization, Expression test, Expression iteration, Expression body )
    {
        ThrowIfInvalid( test, body );

        Initialization = initialization;
        Test = test;
        Iteration = iteration;
        Body = body;
    }

    internal ForExpression( Expression initialization, Expression test, Expression iteration, LoopBody body )
    {
        ThrowIfInvalid( test, body );

        Initialization = initialization;
        Test = test;
        Iteration = iteration;
        Body = body( BreakLabel, ContinueLabel );
    }

    internal ForExpression( Expression initialization, Expression test, Expression iteration, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        ThrowIfInvalid( test, body );

        ArgumentNullException.ThrowIfNull( breakLabel, nameof( breakLabel ) );
        ArgumentNullException.ThrowIfNull( continueLabel, nameof( continueLabel ) );

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
        var newInitialization = visitor.Visit( Initialization );
        var newTest = visitor.Visit( Test );
        var newIteration = visitor.Visit( Iteration );
        var newBody = visitor.Visit( Body );

        if ( newInitialization != Initialization || newTest != Test || newIteration != Iteration || newBody != Body )
        {
            return new ForExpression( newInitialization, newTest, newIteration, newBody, BreakLabel, ContinueLabel );
        }

        return this;
    }
}

public static partial class ExpressionExtensions
{
    public static ForExpression For( Expression initialization, Expression test, Expression iteration, Expression body )
    {
        return new ForExpression( initialization, test, iteration, body );
    }

    public static ForExpression For( Expression initialization, Expression test, Expression iteration, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        return new ForExpression( initialization, test, iteration, body, breakLabel, continueLabel );
    }

    public static ForExpression For( Expression initialization, Expression test, Expression iteration, LoopBody body )
    {
        return new ForExpression( initialization, test, iteration, body );
    }
}
