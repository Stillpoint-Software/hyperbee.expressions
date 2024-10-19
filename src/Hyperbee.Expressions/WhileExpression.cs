using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public delegate Expression LoopBodyFactory( LabelTarget breakLabel, LabelTarget continueLabel );

public class WhileExpression : Expression
{
    public Expression Test { get; }
    public Expression Body { get; }

    public LabelTarget BreakLabel { get; } = Label( "break" );
    public LabelTarget ContinueLabel { get; } = Label( "continue" );

    public WhileExpression( Expression test, LoopBodyFactory body )
    {
        ThrowIfInvalidArguments( test, body );

        Test = test;
        Body = body( BreakLabel, ContinueLabel );
    }

    public WhileExpression( Expression test, Expression body )
    {
        ThrowIfInvalidArguments( test, body );

        Test = test;
        Body = body;
    }

    private WhileExpression( Expression test, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        Test = test;
        Body = body;

        BreakLabel = breakLabel;
        ContinueLabel = continueLabel;
    }

    private static void ThrowIfInvalidArguments( Expression test, object body )
    {
        ArgumentNullException.ThrowIfNull( test, nameof(test) );
        ArgumentNullException.ThrowIfNull( body, nameof(body) );

        if ( test.Type != typeof(bool) )
            throw new ArgumentException( "Test expression must return a boolean.", nameof(test) );
    }

    public override Type Type => typeof(void); 
    public override ExpressionType NodeType => ExpressionType.Extension;

    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        return Block(
            Loop(
                Block(
                    Label( ContinueLabel ),
                    IfThenElse(
                        Test,
                        Body, 
                        Break( BreakLabel )
                    )
                )
            ),
            Label( BreakLabel )
        );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newTest = visitor.Visit( Test );
        var newBody = visitor.Visit( Body );

        if ( newTest != Test || newBody != Body )
        {
            return new WhileExpression( newTest, newBody, BreakLabel, ContinueLabel );
        }

        return this;
    }
}
