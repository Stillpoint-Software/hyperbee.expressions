using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace Hyperbee.AsyncExpressions;

[DebuggerDisplay( "{InnerVariable}" )]
[DebuggerTypeProxy( typeof( AwaitResultExpressionProxy ) )]
public class AwaitResultExpression( Expression variable ) : Expression
{
    private Expression _instance;
    private FieldInfo _fieldInfo;

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => variable.Type;
    public Expression InnerVariable => variable;
    public override bool CanReduce => true;

    public override Expression Reduce()
    {
        return Assign( variable, Call(
            Field( _instance, _fieldInfo ),
            "GetResult", Type.EmptyTypes )
        );
    }

    public void InitializeAwaiter( Expression stateMachineInstance, FieldInfo fieldInfo )
    {
        _instance = stateMachineInstance;
        _fieldInfo = fieldInfo;
    }
    private class AwaitResultExpressionProxy( AwaitResultExpression node )
    {
        public Expression InnerVariable => node.InnerVariable;
        public Type ReturnType => node.Type;
    }
}
