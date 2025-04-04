﻿using System.Linq.Expressions;

namespace Hyperbee.Expressions;

public class WhileExpression : Expression
{
    public Expression Test { get; }
    public Expression Body { get; }

    public LabelTarget BreakLabel { get; } = Label( "break" );
    public LabelTarget ContinueLabel { get; } = Label( "continue" );

    internal WhileExpression( Expression test, Expression body )
    {
        ThrowIfInvalid( test, body );

        Test = test;
        Body = body;
    }

    internal WhileExpression( Expression test, LoopBody body )
    {
        ThrowIfInvalid( test, body );

        Test = test;
        Body = body( BreakLabel, ContinueLabel );
    }

    internal WhileExpression( Expression test, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        ThrowIfInvalid( test, body );

        ArgumentNullException.ThrowIfNull( breakLabel, nameof( breakLabel ) );
        ArgumentNullException.ThrowIfNull( continueLabel, nameof( continueLabel ) );

        Test = test;
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
        return
            Loop(
                IfThenElse(
                    Test,
                    Body,
                    Break( BreakLabel )
                ),
                BreakLabel,
                ContinueLabel
            );
    }

    protected override Expression VisitChildren( ExpressionVisitor visitor )
    {
        var newTest = visitor.Visit( Test );
        var newBody = visitor.Visit( Body );

        if ( newTest == Test && newBody == Body )
            return this;

        return new WhileExpression( newTest, newBody, BreakLabel, ContinueLabel );
    }
}

public static partial class ExpressionExtensions
{
    public static WhileExpression While( Expression test, Expression body )
    {
        return new WhileExpression( test, body );
    }

    public static WhileExpression While( Expression test, Expression body, LabelTarget breakLabel, LabelTarget continueLabel )
    {
        return new WhileExpression( test, body, breakLabel, continueLabel );
    }

    public static WhileExpression While( Expression test, LoopBody body )
    {
        return new WhileExpression( test, body );
    }
}

