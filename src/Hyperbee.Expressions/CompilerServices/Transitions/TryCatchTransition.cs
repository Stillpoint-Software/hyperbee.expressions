﻿using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.Expressions.CompilerServices.Transitions;

internal class TryCatchTransition : Transition
{
    internal List<CatchBlockDefinition> CatchBlocks = [];
    public StateNode TryNode { get; set; }
    public StateNode FinallyNode { get; set; }

    public Expression TryStateVariable { get; set; }
    public Expression ExceptionVariable { get; set; }

    public StateContext.Scope StateScope { get; init; }
    public List<StateContext.Scope> Scopes { get; init; }

    internal override StateNode FallThroughNode => TryNode;

    public override void AddExpressions( List<Expression> expressions, StateMachineContext context )
    {
        base.AddExpressions( expressions, context );
        expressions.AddRange( Expressions() );
        return;

        List<Expression> Expressions()
        {
            var body = new List<Expression>
            {
                JumpTableBuilder.Build(
                    StateScope,
                    Scopes,
                    context.StateMachineInfo.StateField
                )
            };

            body.AddRange( StateScope.GetExpressions( context ) );

            MapCatchBlock( context.StateNode.StateOrder, out var catches, out var switchCases );

            return [
                TryCatch(
                    body.Count == 1
                        ? body[0]
                        : Block( body ),
                    catches
                ),
                Switch( // Handle error
                    TryStateVariable,
                    switchCases
                )
            ];
        }
    }

    internal override void Optimize( HashSet<LabelTarget> references )
    {
        references.Add( TryNode.NodeLabel );

        if ( FinallyNode != null )
            references.Add( FinallyNode.NodeLabel );

        for ( var index = 0; index < CatchBlocks.Count; index++ )
        {
            references.Add( CatchBlocks[index].UpdateBody.NodeLabel );
        }
    }

    private void MapCatchBlock( int order, out CatchBlock[] catches, out SwitchCase[] switchCases )
    {
        var includeFinal = FinallyNode != null;
        var size = CatchBlocks.Count + (includeFinal ? 1 : 0);

        catches = new CatchBlock[size];
        switchCases = new SwitchCase[size];

        for ( var index = 0; index < CatchBlocks.Count; index++ )
        {
            var catchBlock = CatchBlocks[index];

            catches[index] = catchBlock.Reduce( ExceptionVariable, TryStateVariable );

            switchCases[index] = SwitchCase(
                GotoOrFallThrough( order, catchBlock.UpdateBody ),
                Constant( catchBlock.CatchState ) );
        }

        if ( !includeFinal )
            return;

        catches[^1] = Catch(
            typeof( Exception ),
            Block(
                typeof( void ),
                Assign( TryStateVariable, Constant( catches.Length ) )
            )
        );

        switchCases[^1] = SwitchCase(
            Goto( FinallyNode.NodeLabel ),
            Constant( catches.Length )
        );
    }

    public void AddCatchBlock( CatchBlock handler, StateNode updateBody, int catchState )
    {
        CatchBlocks.Add( new CatchBlockDefinition( handler, updateBody, catchState ) );
    }

    internal class CatchBlockDefinition( CatchBlock handler, StateNode updateBody, int catchState )
    {
        public CatchBlock Reduce( Expression exceptionVariable, Expression tryStateVariable )
        {
            if ( Handler.Variable == null )
            {
                return Catch(
                    Handler.Test,
                    Block(
                        typeof( void ),
                        Assign( tryStateVariable, Constant( CatchState ) )
                    ) );
            }

            return Catch(
                Handler.Test,
                Block(
                    typeof( void ),
                    Assign( exceptionVariable, Constant( Handler.Variable ) ),
                    Assign( tryStateVariable, Constant( CatchState ) )
                ) );
        }

        public CatchBlock Handler { get; init; } = handler;
        public StateNode UpdateBody { get; internal set; } = updateBody;
        public int CatchState { get; init; } = catchState;
    }
}
