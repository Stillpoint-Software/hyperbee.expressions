using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hyperbee.Expressions.Interpreter.Core;
using Hyperbee.Expressions.Interpreter.Evaluators;

namespace Hyperbee.Expressions.Interpreter;

public sealed class XsInterpreter : ExpressionVisitor
{
    private readonly Evaluator _evaluator;
    private readonly Dictionary<Expression, Expression> _extensions = new();
    private readonly InterpretSynchronizationContext _syncContext;

    private LambdaExpression _lowered;
    private Dictionary<GotoExpression, Navigation> _navigation;

    internal InterpretContext CurrentContext => InterpretContext.Current;

    internal InterpretScope Scope => CurrentContext.Scope;
    internal Stack<object> Results => CurrentContext.Results;
    internal Navigation Navigation
    {
        get => CurrentContext.Navigation;
        set => CurrentContext.Navigation = value;
    }

    public XsInterpreter()
    {
        _evaluator = new Evaluator( this );
        _syncContext = new InterpretSynchronizationContext();
    }

    public TDelegate Interpreter<TDelegate>( LambdaExpression expression )
        where TDelegate : Delegate
    {
        if ( _lowered == null )
        {
            AnalyzeExpression( expression );
            expression = _lowered;
        }

        return EvaluateDelegateFactory.CreateDelegate<TDelegate>( this, expression );
    }

    private void AnalyzeExpression( Expression expression )
    {
        var analyzer = new AnalyzerVisitor();
        analyzer.Analyze( expression, _extensions );

        _navigation = analyzer.Navigation;
        _lowered = (LambdaExpression) analyzer.Lowered;
    }

    internal T Evaluate<T>( LambdaExpression lambda, params object[] values )
    {
        var prevContext = SynchronizationContext.Current;

        try
        {
            SynchronizationContext.SetSynchronizationContext( _syncContext );

            var (scope, _) = CurrentContext;

            scope.EnterScope();

            try
            {
                for ( var i = 0; i < lambda.Parameters.Count; i++ )
                    scope.Values[lambda.Parameters[i]] = values[i];

                Visit( lambda.Body );
                (scope, var results) = CurrentContext;

                ThrowIfNavigating();

                return (T) results.Pop();
            }
            finally
            {
                scope.ExitScope();
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext( prevContext );
        }

    }

    internal void Evaluate( LambdaExpression lambda, params object[] values )
    {
        var prevContext = SynchronizationContext.Current;

        try
        {
            SynchronizationContext.SetSynchronizationContext( _syncContext );

            var (scope, _) = CurrentContext;
            scope.EnterScope();

            try
            {
                for ( var i = 0; i < lambda.Parameters.Count; i++ )
                    scope.Values[lambda.Parameters[i]] = values[i];

                Visit( lambda.Body );
                (scope, var results) = CurrentContext;

                ThrowIfNavigating();

                results.Pop();
            }
            finally
            {
                scope.ExitScope();
            }
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(prevContext );
        }
    }

    private void ThrowIfNavigating()
    {
        if ( !CurrentContext.IsNavigating )
            return;

        if ( Navigation.Exception != null )
            throw new InvalidOperationException( "Interpreter failed because of an unhandled exception.", Navigation.Exception );

        throw new InvalidOperationException( "Interpreter failed to navigate to next expression." );
    }

    // Goto

    protected override Expression VisitGoto( GotoExpression node )
    {
        if ( !_navigation.TryGetValue( node, out var navigation ) )
            throw new InterpreterException( $"Undefined label target: {node.Target.Name}", node );

        object lastResult = null;

        if ( node.Kind == GotoExpressionKind.Return && node.Value != null )
        {
            Visit( node.Value );
            lastResult = Results.Pop();
        }

        //Mode = InterpreterMode.Navigating;
        Navigation = navigation;

        Results.Push( lastResult );

        return node;
    }

    protected override Expression VisitLabel( LabelExpression node )
    {
        if ( CurrentContext.IsNavigating && Navigation.TargetLabel == node.Target )
        {
            //Mode = InterpreterMode.Evaluating;
            //Navigation.Reset();
            Navigation = null;
        }

        Results.Push( null );

        return node;
    }

    // Block

    private enum BlockState
    {
        InitializeVariables,
        HandleStatements,
        Complete
    };

    protected override Expression VisitBlock( BlockExpression node )
    {
        var state = BlockState.InitializeVariables;
        var statementIndex = 0;
        object lastResult = null;

        Scope.EnterScope();

        try
        {
Navigate:

            if ( CurrentContext.IsNavigating )
            {
                var nextStep = Navigation.GetNextStep();
                statementIndex = node.Expressions.IndexOf( nextStep );
            }

            while ( true )
            {
                switch ( state )
                {
                    case BlockState.InitializeVariables:
                        foreach ( var variable in node.Variables )
                        {
                            var name = variable.Name ?? variable.ToString();
                            Scope.Variables[name] = variable;
                            Scope.Values[variable] = Default( variable.Type );
                        }

                        state = BlockState.HandleStatements;
                        break;

                    case BlockState.HandleStatements:
                        if ( statementIndex >= node.Expressions.Count )
                        {
                            state = BlockState.Complete;
                            break;
                        }

                        Visit( node.Expressions[statementIndex] );

                        lastResult = Results.Pop();

                        if ( CurrentContext.IsNavigating )
                        {
                            if ( Navigation.CommonAncestor == node )
                                goto Navigate;

                            Results.Push( lastResult );
                            return node!;
                        }

                        statementIndex++;
                        break;

                    case BlockState.Complete:
                        Results.Push( lastResult );
                        return node;
                }
            }
        }
        catch ( Exception ex )
        {
            throw;
        }
        finally
        {
            Scope.ExitScope();
        }

        static object Default( Type type ) =>
            type == typeof( string ) ? string.Empty :
            type.IsValueType ? RuntimeHelpers.GetUninitializedObject( type ) : null;
    }

    // Conditional

    private enum ConditionalState
    {
        Test,
        HandleTest,
        Visit,
        Complete
    };

    protected override Expression VisitConditional( ConditionalExpression node )
    {
        var state = ConditionalState.Test;
        var continuation = ConditionalState.Complete;
        Expression expr = null;
        object lastResult = null;

Navigate:

        if ( CurrentContext.IsNavigating )
        {
            expr = Navigation.GetNextStep();
            state = ConditionalState.Visit;
            continuation = expr == node.Test ? ConditionalState.HandleTest : ConditionalState.Complete;
        }

        while ( true )
        {
            switch ( state )
            {
                case ConditionalState.Test:
                    expr = node.Test;
                    state = ConditionalState.Visit;
                    continuation = ConditionalState.HandleTest;
                    break;

                case ConditionalState.HandleTest:
                    var conditionValue = (bool) lastResult!;  
                    expr = conditionValue ? node.IfTrue : node.IfFalse;
                    state = ConditionalState.Visit;
                    continuation = ConditionalState.Complete;
                    break;

                case ConditionalState.Visit:
                    Visit( expr );

                    lastResult = Results.Pop();

                    if ( CurrentContext.IsNavigating )
                    {
                        if ( Navigation.CommonAncestor == node )
                            goto Navigate;

                        Results.Push( lastResult );
                        return node;
                    }

                    state = continuation;
                    break;

                case ConditionalState.Complete:
                    Results.Push( lastResult );
                    return node;
            }
        }
    }

    // Switch

    private enum SwitchState
    {
        SwitchValue,
        HandleSwitchValue,
        MatchCase,
        HandleMatchCase,
        Visit,
        Complete
    }

    protected override Expression VisitSwitch( SwitchExpression node )
    {
        var state = SwitchState.SwitchValue;
        var continuation = SwitchState.Complete;
        var caseIndex = 0;
        var testIndex = 0;
        object switchValue = null;
        Expression expr = null;
        object lastResult = null;

Navigate:

        if ( CurrentContext.IsNavigating )
        {
            expr = Navigation.GetNextStep();

            if ( expr == node.SwitchValue )
            {
                state = SwitchState.Visit;
                continuation = SwitchState.HandleSwitchValue;
            }
            else
            {
                var matchedCase = node.Cases.FirstOrDefault( c => c.Body == expr );
                expr = matchedCase?.Body ?? node.DefaultBody;
                state = expr != null ? SwitchState.Visit : SwitchState.Complete;
                continuation = SwitchState.Complete;
            }
        }

        while ( true )
        {
            switch ( state )
            {
                case SwitchState.SwitchValue:
                    expr = node.SwitchValue;
                    state = SwitchState.Visit;
                    continuation = SwitchState.HandleSwitchValue;
                    break;

                case SwitchState.HandleSwitchValue:
                    switchValue = lastResult; 
                    caseIndex = 0;
                    testIndex = 0;
                    state = SwitchState.MatchCase;
                    break;

                case SwitchState.MatchCase:
                    if ( caseIndex >= node.Cases.Count )
                    {
                        expr = node.DefaultBody;
                        state = expr != null ? SwitchState.Visit : SwitchState.Complete;
                        continuation = SwitchState.Complete;
                        break;
                    }

                    var testValues = node.Cases[caseIndex].TestValues;
                    if ( testIndex >= testValues.Count )
                    {
                        caseIndex++;
                        testIndex = 0;
                        state = SwitchState.MatchCase;
                        break;
                    }

                    expr = testValues[testIndex];
                    state = SwitchState.Visit;
                    continuation = SwitchState.HandleMatchCase;
                    break;

                case SwitchState.HandleMatchCase:

                    if ( (switchValue != null && !switchValue.Equals( lastResult )) || (switchValue == null && lastResult != null) )
                    {
                        testIndex++;
                        state = SwitchState.MatchCase;
                        break;
                    }

                    expr = node.Cases[caseIndex].Body;
                    state = SwitchState.Visit;
                    continuation = SwitchState.Complete;
                    break;

                case SwitchState.Visit:
                    Visit( expr! );

                    lastResult = Results.Pop();

                    if ( CurrentContext.IsNavigating )
                    {
                        if ( Navigation.CommonAncestor == node )
                            goto Navigate;

                        Results.Push( lastResult );
                        return node;
                    }

                    state = continuation;
                    break;

                case SwitchState.Complete:
                    Results.Push( lastResult );
                    return node;
            }
        }
    }

    // Try/Catch

    private enum TryCatchState
    {
        Try,
        Catch,
        HandleCatch,
        Finally,
        HandleFinally,
        Visit,
        Complete
    }

    protected override Expression VisitTry( TryExpression node )
    {
        var state = TryCatchState.Try;
        var continuation = TryCatchState.Complete;
        var catchIndex = 0;

        Expression expr = null;
        ParameterExpression exceptionVariable = null;
        object lastResult = null;

Navigate:

        if ( CurrentContext.IsNavigating )
        {
            expr = Navigation.GetNextStep();

            if ( expr == node.Body )
            {
                state = TryCatchState.Visit;
                continuation = TryCatchState.Finally;
            }
            else if ( expr == node.Finally )
            {
                state = TryCatchState.Visit;
                continuation = TryCatchState.Complete;
            }
            else
            {
                var exceptionHandler = node.Handlers.FirstOrDefault( c => c.Body == expr );

                if ( exceptionHandler != null )
                {
                    expr = exceptionHandler.Body;
                    state = TryCatchState.Visit;
                    continuation = TryCatchState.Finally;
                }
            }
        }

        while ( true )
        {
            switch ( state )
            {
                case TryCatchState.Try:
                    expr = node.Body;
                    state = TryCatchState.Visit;
                    continuation = TryCatchState.Finally;
                    break;

                case TryCatchState.Catch:
                    if ( catchIndex >= node.Handlers.Count )
                    {
                        state = TryCatchState.Finally;
                        break;
                    }

                    var handler = node.Handlers[catchIndex];
                    var exceptionType = Navigation.Exception?.GetType();

                    if ( handler.Test.IsAssignableFrom( exceptionType ) )
                    {
                        exceptionVariable = handler.Variable;
                        expr = handler.Body;
                        state = TryCatchState.HandleCatch;
                        continuation = TryCatchState.Finally;
                    }
                    else
                    {
                        catchIndex++;
                        state = TryCatchState.Catch;
                    }
                    break;

                case TryCatchState.HandleCatch:

                    // found matching catch, clear navigation
                    //Mode = InterpreterMode.Evaluating;

                    var exception = Navigation.Exception; //BF
                    Navigation = null; //BF

                    try
                    {
                        Scope.EnterScope();
                        Scope.Values[exceptionVariable] = exception; // Navigation.Exception; //BF

                        Visit( expr! );

                        //Navigation.Exception = null; //BF
                    }
                    finally
                    {
                        Scope.ExitScope();
                    }

                    lastResult = Results.Pop();

                    if ( CurrentContext.IsNavigating )
                    {
                        if ( Navigation.CommonAncestor == node )
                            goto Navigate;
                    }

                    state = continuation;
                    break;

                case TryCatchState.Finally:
                    if ( node.Finally != null )
                    {
                        expr = node.Finally;
                        state = TryCatchState.HandleFinally;
                        continuation = TryCatchState.Complete;
                    }
                    else
                    {
                        state = TryCatchState.Complete;
                    }
                    break;

                case TryCatchState.HandleFinally:

                    var currentException = Navigation?.Exception;
                    
                    if ( currentException != null )
                    {
                        //Mode = InterpreterMode.Evaluating;
                        Navigation = null; //BF
                    }

                    Visit( expr! );
                    Results.Pop();

                    if ( currentException != null )
                        throw currentException;

                    if ( CurrentContext.IsNavigating )
                    {
                        if ( Navigation.CommonAncestor == node )
                            goto Navigate;

                        Results.Push( lastResult );
                        return node;
                    }

                    state = continuation;
                    break;

                case TryCatchState.Visit:
                    Visit( expr! );

                    if ( Navigation?.Exception == null )
                        lastResult = Results.Pop();

                    if ( CurrentContext.IsNavigating )
                    {
                        if ( Navigation.CommonAncestor == node )
                            goto Navigate;

                        if ( Navigation.Exception != null )
                        {
                            state = TryCatchState.Catch;
                            break;
                        }

                        Results.Push( lastResult );
                        return node;
                    }

                    state = continuation;
                    break;

                case TryCatchState.Complete:
                    Results.Push( lastResult );
                    return node;
            }
        }
    }

    // Loop

    protected override Expression VisitLoop( LoopExpression node )
    {
        Scope.EnterScope();

        try
        {
            object lastResult;

            while ( true )
            {
                Visit( node.Body );
                lastResult = Results.Pop();

                if ( !CurrentContext.IsNavigating )
                {
                    continue;
                }

                if ( Navigation.TargetLabel == node.BreakLabel || Navigation.TargetLabel == node.ContinueLabel )
                {
                    Navigation = null;
                    break;
                }

                //if ( Navigation.TargetLabel == node.BreakLabel || Navigation.TargetLabel == node.ContinueLabel )
                //{
                //    Mode = InterpreterMode.Evaluating;
                //    break;
                //}

                Results.Push( lastResult );
                return node;
            }

            Results.Push( lastResult );
        }
        catch( Exception ex )
        {
            throw;
        }
        finally
        {
            Scope.ExitScope();
        }

        return node;
    }

    // Lambda

    protected override Expression VisitLambda<T>( Expression<T> node )
    {
        Results.Push( this.Interpreter( node, node.Type ) );
        return node;

        //if ( Scope.Depth == 0 )
        //{
        //    ResultStack.Push( node );
        //    return node;
        //}

        //var freeVariables = FreeVariableVisitor.GetFreeVariables( node );

        //if ( freeVariables.Count == 0 )
        //{
        //    ResultStack.Push( node );
        //    return node;
        //}

        //var capturedScope = new Dictionary<ParameterExpression, object>();

        //foreach ( var variable in freeVariables )
        //{
        //    if ( !Scope.Values.TryGetValue( variable, out var value ) )
        //        throw new InterpreterException( $"Captured variable '{variable.Name}' is not defined.", node );

        //    capturedScope[variable] = value;
        //}

        //ResultStack.Push( new Closure( node, capturedScope ) );

        //return node;
    }

    protected override Expression VisitInvocation( InvocationExpression node )
    {
        Visit( node.Expression );
        var targetValue = Results.Pop();

        //LambdaExpression lambda = null;
        Delegate lambdaDelegate;
       // Dictionary<ParameterExpression, object> capturedScope = null;

        switch ( targetValue )
        {
            //case Closure closure:
            //    //lambda = closure.LambdaExpr;
            //    //lambda = closure.Lambda;
            //    lambda = closure.Lambda as LambdaExpression;
            //    capturedScope = closure.CapturedScope;
            //    break;

            //case LambdaExpression directLambda:
            //    lambda = directLambda;
            //    break;

            case Delegate @delegate:
                lambdaDelegate = @delegate;
                break;

            default:
                throw new InterpreterException( "Invocation target is not a valid lambda or closure.", node );
        }

        Scope.EnterScope();

        try
        {
            //if ( capturedScope is not null )
            //{
            //    foreach ( var (param, value) in capturedScope )
            //        Scope.Values[param] = value;
            //}

            //if ( lambda != null )
            //{
            //    for ( var i = 0; i < node.Arguments.Count; i++ )
            //    {
            //        Visit( node.Arguments[i] );
            //        Scope.Values[lambda.Parameters[i]] = ResultStack.Pop();
            //    }

            //    Visit( lambda.Body );
            //} 

            var arguments = new object[node.Arguments.Count];

            for ( var i = 0; i < node.Arguments.Count; i++ )
            {
                Visit( node.Arguments[i] );
                arguments[i] = Results.Pop();
            }

            var result = lambdaDelegate.DynamicInvoke( arguments );

            Results.Push( result );

            return node;
        }
        catch ( Exception ex )
        {
            throw;
        }
        finally
        {
            Scope.ExitScope();
        }
    }

    protected override Expression VisitMethodCall( MethodCallExpression node )
    {
        var isStatic = node.Method.IsStatic;
        object instance = null;

        if ( !isStatic )
        {
            Visit( node.Object );
            instance = Results.Pop();
        }

        var arguments = new object[node.Arguments.Count];
        var capturedValues = new Dictionary<int, Dictionary<ParameterExpression, object>>();
        var hasClosure = false;

        for ( var i = 0; i < node.Arguments.Count; i++ )
        {
            Visit( node.Arguments[i] );
            var argValue = Results.Pop();

            switch ( argValue )
            {
                case Closure closure:
                    hasClosure = true;
                    //arguments[i] = this.Interpreter( closure.LambdaExpr );
                    arguments[i] = closure.Lambda;
                    capturedValues[i] = closure.CapturedScope;
                    break;

                case LambdaExpression lambdaExpr:
                    //arguments[i] = this.Interpreter( lambdaExpr );
                    arguments[i] = this.Interpreter( lambdaExpr );
                    break;

                default:
                    arguments[i] = argValue;
                    break;
            }
        }

        if ( !hasClosure )
        {
            try
            {
                var result = node.Method.Invoke( instance, arguments );
                Results.Push( result );
                return node;
            }
            catch ( TargetInvocationException invocationException )
            {
                throw invocationException.InnerException ?? invocationException;
            }
            catch ( Exception ex )
            {
                throw;
            }
        }

        try
        {
            Scope.EnterScope();

            foreach ( var capturedScope in capturedValues.Values )
            {
                foreach ( var (param, value) in capturedScope )
                    Scope.Values[param] = value;
            }

            var result = node.Method.Invoke( instance, arguments );
            Results.Push( result );
            return node;
        }
        finally
        {
            Scope.ExitScope();
        }
    }

    protected override Expression VisitBinary( BinaryExpression node )
    {
        if ( node.NodeType is ExpressionType.Assign
            or ExpressionType.AddAssign
            or ExpressionType.SubtractAssign
            or ExpressionType.MultiplyAssign
            or ExpressionType.DivideAssign
            or ExpressionType.ModuloAssign
            or ExpressionType.LeftShiftAssign
            or ExpressionType.RightShiftAssign )
        {
            switch ( node.Left )
            {
                case MemberExpression memberExpr:
                    Visit( memberExpr.Expression ); // Visit and push instance
                    break;

                case IndexExpression indexExpr:
                    Visit( indexExpr.Object ); // Visit and push instance

                    foreach ( var arg in indexExpr.Arguments )
                    {
                        Visit( arg ); // Visit and push index arguments
                    }

                    break;
            }
        }
        else
        {
            Visit( node.Left ); // Visit and push leftValue
        }

        Visit( node.Right ); // Visit and push rightValue

        var result = _evaluator.Binary( node );
        Results.Push( result );

        return node;
    }

    protected override Expression VisitTypeBinary( TypeBinaryExpression node )
    {
        Visit( node.Expression );
        var operand = Results.Pop();

        var result = operand is not null && node.TypeOperand.IsAssignableFrom( operand.GetType() );

        Results.Push( result );
        return node;
    }

    protected override Expression VisitUnary( UnaryExpression node )
    {
        Visit( node.Operand ); // Visit and push operand

        if ( node.NodeType == ExpressionType.Throw )
        {
            var exception = Results.Pop() as Exception;

            if ( Navigation != null && exception == null )
            {
                exception = Navigation.Exception;
            }

            //Mode = InterpreterMode.Navigating;
            Navigation = new Navigation( exception: exception );

            return node;
        }

        var result = _evaluator.Unary( node );
        Results.Push( result );

        return node;
    }

    protected override Expression VisitConstant( ConstantExpression node )
    {
        Results.Push( node.Value );
        return node;
    }

    protected override Expression VisitDefault( DefaultExpression node )
    {
        var defaultValue = node.Type.IsValueType && node.Type != typeof( void )
            ? RuntimeHelpers.GetUninitializedObject( node.Type )
            : null;

        Results.Push( defaultValue );
        return node;
    }

    //protected override Expression VisitExtension( Expression node )
    //{
    //    if ( _extensions.TryGetValue( node, out var reduced ) )
    //    {
    //        return Visit( reduced );
    //    }

    //    reduced = node.ReduceAndCheck();
    //    _extensions[node] = reduced;

    //    return Visit( reduced );
    //}

    protected override Expression VisitIndex( IndexExpression node )
    {
        var arguments = new object[node.Arguments.Count];
        for ( var i = 0; i < node.Arguments.Count; i++ )
        {
            Visit( node.Arguments[i] );
            arguments[i] = Results.Pop();
        }

        Visit( node.Object );
        var instance = Results.Pop();

        var result = node.Indexer!.GetValue( instance, arguments );
        Results.Push( result );

        return node;
    }

    protected override Expression VisitListInit( ListInitExpression node )
    {
        Visit( node.NewExpression );
        var instance = Results.Pop();

        foreach ( var initializer in node.Initializers )
        {
            var arguments = new object[initializer.Arguments.Count];

            for ( var index = 0; index < initializer.Arguments.Count; index++ )
            {
                Visit( initializer.Arguments[index] );
                arguments[index] = Results.Pop();
            }

            initializer.AddMethod.Invoke( instance, arguments );
        }

        Results.Push( instance );
        return node;
    }

    protected override Expression VisitMember( MemberExpression node )
    {
        Visit( node.Expression );
        var instance = Results.Pop();

        object result;
        switch ( node.Member )
        {
            case PropertyInfo prop:
                result = prop.GetValue( instance );
                break;
            case FieldInfo field:
                //if ( field.FieldType.IsValueType )
                //{
                //    var typeReference = __makeref(instance);
                //    result = field.GetValueDirect( typeReference );
                //    break;
                //}
                result = field.GetValue( instance );
                break;
            default:
                throw new InterpreterException( $"Unsupported member access: {node.Member.Name}", node );
        }

        Results.Push( result );
        return node;
    }

    protected override Expression VisitNew( NewExpression node )
    {
        var arguments = new object[node.Arguments.Count];
        var capturedValues = new Dictionary<int, Dictionary<ParameterExpression, object>>();
        var hasClosure = false;

        for ( var index = 0; index < node.Arguments.Count; index++ )
        {
            Visit( node.Arguments[index] );
            var argValue = Results.Pop();

            switch ( argValue )
            {
                case Closure closure:
                    hasClosure = true;
                    //arguments[index] = this.Interpreter( closure.LambdaExpr );
                    arguments[index] = closure.Lambda;
                    capturedValues[index] = closure.CapturedScope;
                    break;

                case LambdaExpression lambdaExpr:
                    arguments[index] = this.Interpreter( lambdaExpr );
                    break;

                default:
                    arguments[index] = argValue;
                    break;
            }
        }

        var constructor = node.Constructor;

        if ( constructor is null )
        {
            throw new InterpreterException( $"No valid constructor found for type {node.Type}.", node );
        }

        if ( !hasClosure )
        {
            var instance = constructor.Invoke( arguments );
            Results.Push( instance );
            return node;
        }

        Scope.EnterScope();

        try
        {
            foreach ( var (_, capturedScope) in capturedValues )
            {
                foreach ( var (param, value) in capturedScope )
                    Scope.Values[param] = value;
            }

            var instance = constructor.Invoke( arguments );
            Results.Push( instance );
            return node;
        }
        finally
        {
            Scope.ExitScope();
        }
    }

    protected override Expression VisitNewArray( NewArrayExpression node )
    {
        var elementType = node.Type.GetElementType();

        switch ( node.NodeType )
        {
            case ExpressionType.NewArrayInit:
                {
                    // Handle NewArrayInit: Array initialized with values
                    var values = new object[node.Expressions.Count];

                    for ( var i = 0; i < node.Expressions.Count; i++ )
                    {
                        Visit( node.Expressions[i] );
                        values[i] = Results.Pop();
                    }

                    var array = Array.CreateInstance( elementType!, values.Length );

                    for ( var i = 0; i < values.Length; i++ )
                        array.SetValue( values[i], i );

                    Results.Push( array );
                    break;
                }
            case ExpressionType.NewArrayBounds:
                {
                    // Handle NewArrayBounds: Array created with specified dimensions
                    var lengths = new int[node.Expressions.Count];

                    for ( var i = 0; i < node.Expressions.Count; i++ )
                    {
                        Visit( node.Expressions[i] );
                        lengths[i] = (int) Results.Pop();
                    }

                    var array = Array.CreateInstance( elementType!, lengths );
                    Results.Push( array );
                    break;
                }
            default:
                throw new InterpreterException( $"Unsupported array creation type: {node.NodeType}", node );
        }

        return node;
    }

    protected override Expression VisitParameter( ParameterExpression node )
    {
        if ( !Scope.Values.TryGetValue( node, out var value ) )
            throw new InterpreterException( $"Parameter '{node.Name}' not found.", node );

        Results.Push( value );
        return node;
    }

    //public record Closure( LambdaExpression LambdaExpr, Dictionary<ParameterExpression, object> CapturedScope );
    public record Closure( object Lambda, Dictionary<ParameterExpression, object> CapturedScope );

    private sealed class FreeVariableVisitor : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _declaredVariables = [];
        private readonly HashSet<ParameterExpression> _freeVariables = [];

        public static HashSet<ParameterExpression> GetFreeVariables( Expression expression )
        {
            var visitor = new FreeVariableVisitor();
            visitor.Visit( expression );
            return visitor._freeVariables;
        }

        protected override Expression VisitBlock( BlockExpression node )
        {
            _declaredVariables.UnionWith( node.Variables );

            return base.VisitBlock(node);
        }

        protected override CatchBlock VisitCatchBlock( CatchBlock node )
        {
            _declaredVariables.Add( node.Variable );

            return base.VisitCatchBlock(node);
        }

        protected override Expression VisitLambda<T>( Expression<T> node )
        {
            _declaredVariables.UnionWith( node.Parameters );

            return base.VisitLambda( node );
        }

        protected override Expression VisitParameter( ParameterExpression node )
        {
            if ( !_declaredVariables.Contains( node ) )
            {
                _freeVariables.Add( node );
            }

            return base.VisitParameter( node );
        }
    }
}
