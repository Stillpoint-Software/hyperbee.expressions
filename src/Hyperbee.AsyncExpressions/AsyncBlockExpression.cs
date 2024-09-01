using System.Linq.Expressions;
using Hyperbee.AsyncExpressions;

public class AsyncBlockExpression : AsyncBaseExpression
{
    private readonly List<Expression> _expressions;

    public AsyncBlockExpression(List<Expression> expressions) : base(null)
    {
        _expressions = expressions;
    }

    protected override Type GetFinalResultType()
    {
        // Get the final result type from the last block
        var (_, finalResultType) = ReduceBlock(_expressions);
        return finalResultType;
    }

    protected override Expression BuildStateMachine<TResult>()
    {
        var (blocks, finalResultType) = ReduceBlock(_expressions);

        var builder = new StateMachineBuilder<TResult>();

        foreach (var block in blocks)
        {
            var lastExpr = block.Expressions.Last();
            if (IsTask(lastExpr.Type))
            {
                if (lastExpr.Type == typeof(Task))
                {
                    builder.AddTaskBlock(block); // Block with Task
                }
                else if (lastExpr.Type.IsGenericType && lastExpr.Type.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    builder.AddTaskResultBlock(block); // Block with Task<TResult>
                }
            }
            else
            {
                builder.AddBlock(block); // Regular code block
            }
        }

        return builder.Build();
    }

    // ReduceBlock method to split the block into sub-blocks
    private (List<BlockExpression> blocks, Type finalResultType) ReduceBlock(List<Expression> expressions)
    {
        var blocks = new List<BlockExpression>();
        var currentBlock = new List<Expression>();
        Type finalResultType = typeof(void);

        foreach (var expr in expressions)
        {
            currentBlock.Add(expr);

            if (expr is AwaitExpression)
            {
                // Finalize the current block and add it to the list
                blocks.Add(Expression.Block(currentBlock));
                currentBlock.Clear();
            }
        }

        // Add the last block if it exists
        if (currentBlock.Count > 0)
        {
            blocks.Add(Expression.Block(currentBlock));
            var lastExpr = currentBlock.Last();

            // Determine the final result type from the last expression
            if (IsTask(lastExpr.Type))
            {
                if (lastExpr.Type.IsGenericType)
                {
                    finalResultType = lastExpr.Type.GetGenericArguments()[0];
                }
                else
                {
                    finalResultType = typeof(void); // Task without a result
                }
            }
        }

        return (blocks, finalResultType);
    }
}

