using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace Hyperbee.AsyncExpressions.Transformation.Transitions;

public class TryCatchTransition : Transition
{
    internal readonly List<CatchBlockDefinition> CatchBlocks = [];
    public NodeExpression TryNode { get; set; }
    public NodeExpression FinallyNode { get; set; }
    public NodeScope NodeScope { get; set; }
    internal override NodeExpression FallThroughNode => TryNode;
    public ParameterExpression TryStateVariable { get; set; }
    public ParameterExpression ExceptionVariable { get; set; }

    internal override Expression Reduce( int order, NodeExpression expression, IFieldResolverSource resolverSource )
    {
        var jumpTableExpression = Expression.Switch(
            resolverSource.StateIdField,
            Empty(),
            NodeScope.GetJumpCases().Select( c =>
                SwitchCase(
                    Block(
                        Assign( resolverSource.StateIdField, Constant( -1 ) ),
                        Goto( c.Key )
                    ),
                    Constant( c.Value )
                ) ).ToArray() );

        var expressions = new List<Expression>( NodeScope.Nodes.Count + 1 ) { jumpTableExpression };
        expressions.AddRange( NodeScope.Nodes.Select( x => x.Reduce( resolverSource ) ) );

        var tryBody = Block(
            expressions
        );

        var includeFinal = FinallyNode != null;
        var catches = new CatchBlock[CatchBlocks.Count + (includeFinal ? 1 : 0)];
        var switchCases = new SwitchCase[CatchBlocks.Count + (includeFinal ? 1 : 0)];

        for ( var index = 0; index < CatchBlocks.Count; index++ )
        {
            var catchBlock = CatchBlocks[index];
            catches[index] = catchBlock.Reduce( ExceptionVariable, TryStateVariable );
            switchCases[index] = SwitchCase(
                (catchBlock.UpdateBody is NodeExpression nodeExpression ) 
                    ? Goto( nodeExpression.NodeLabel )
                    : Block( typeof(void), catchBlock.UpdateBody),
                Constant( catchBlock.CatchState ) );
        }

        if ( includeFinal )
        {
            catches[^1] = Catch(
                typeof(Exception),
                Block(
                    typeof(void),
                    Assign( TryStateVariable, Constant( catches.Length ) )
                ) );
            switchCases[^1] = SwitchCase(
                Goto( FinallyNode.NodeLabel ),
                Constant( catches.Length ) );
        }
        
        var handleError = Switch(
            TryStateVariable,
            Empty(),
            switchCases );

        return Block(
            TryCatch(
                tryBody,
                catches
            ),
            handleError );


        /*

        switch (state) {

        case 2:
           __state = -1:
           goto ST_0004;  // GetResult

        case 5:
        case 6:
          goto ST_0005; // try {}

        }

        ST_0000:
          .. blah
          goto ST_0002;

        ST_0001:
          __finalResult.SetResult...
          return exit

        ST_0002:
          __awaiter<2> == getawaiter...
          goto ST_0004;

        ST_0004:
          __awaiter<2>.GetResult()
          goto ST_0005

        //?
          try
          {

               ST_0005:
                 __awaiter<2> == getawaiter...
                 goto ST_0007;

               ST_0007:
                 __awaiter<2>.GetResult()
                 goto ST_0006
          }
          catch( ex1 )
          {
            __ex_obj<5> = ex1;
            __ex<5> = 1
          }
          catch( ex2 )
          {
            __ex_obj<5> = ex2;
            ex<5> = 2
          }
          //finally?

          switch ( __ex<5> ){
            case 1:
            case 2:
          }
          goto ST_0001;


        ST_0008:



        */

        // var finallyBody = FinallyNode != null
        //     ? Goto( FinallyNode.NodeLabel )
        //     : null;
        //
        // // TODO: FallThrough is removing the rest of the body and replacing with Empty()
        // return TryCatchFinally(
        //     GotoOrFallThrough( order, TryNode ),
        //     finallyBody,
        //     [.. catches]
        // );
    }

    public void AddCatchBlock( CatchBlock handler, Expression updateBody, int catchState )
    {
        CatchBlocks.Add( new CatchBlockDefinition( handler, updateBody, catchState ) );
    }

    internal record CatchBlockDefinition( CatchBlock Handler, Expression UpdateBody, int CatchState )
    {
        public CatchBlock Reduce( ParameterExpression exceptionVariable, ParameterExpression tryStateVariable )
        {
            return Catch(
                Handler.Test,
                Block(
                    typeof(void),
                    Assign( exceptionVariable, Constant( Handler.Variable ) ),
                    Assign( tryStateVariable, Constant( CatchState ) ) //Goto( Body.NodeLabel )
                ) );
        }
    }
}
