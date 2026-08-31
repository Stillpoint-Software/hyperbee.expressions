using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.Linq.Expressions.Expression;
using static Hyperbee.Expressions.ExpressionExtensions;

namespace Hyperbee.Expressions.Compiler.IssueTests;

/// <summary>
/// The reproductions from reported issues, kept as they were reported.
/// </summary>
/// <remarks>
/// These are transcribed from the issue text rather than reduced to a minimal case, so that what
/// passes here is what the reporter ran. Each is checked under the System compiler, which is what
/// the reports used, and under HEC.
/// </remarks>
[TestClass]
public class ReportedIssues
{
    private static async Task<int> AddOneAsync( int value )
    {
        await Task.Yield();
        return value + 1;
    }

    private static async Task<int> ThrowIntAsync()
    {
        await Task.Yield();
        throw new Exception( "Boom!" );
    }

    private static async Task<object> ThrowAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException( "Boom" );
    }

    /// <summary>
    /// Issue 159 -- async lowering conflates distinct ParameterExpression instances with the
    /// same name. Reported as returning 6 instead of 16, or throwing "An item with the same key
    /// has already been added" for nested shadowing.
    /// </summary>
    [TestMethod]
    public async Task Issue159_DistinctParametersSharingAName()
    {
        var input = Parameter( typeof( int ), "value" );

        // Different ParameterExpression instance, intentionally same name.
        var local = Variable( typeof( int ), "value" );

        var result = Variable( typeof( int ), "result" );

        var nestedBlock =
            Block(
                new[] { local },

                Assign(
                    local,
                    Add( input, Constant( 5 ) )
                ),

                Assign(
                    result,
                    Await(
                        Call(
                            typeof( ReportedIssues ),
                            nameof( AddOneAsync ),
                            Type.EmptyTypes,
                            local
                        )
                    )
                )
            );

        var asyncBlock =
            BlockAsync(
                new[] { result },
                nestedBlock,
                result
            );

        var lambda =
            Lambda<Func<int, Task<int>>>(
                asyncBlock,
                input
            );

        Assert.AreEqual( 16, await lambda.Compile()( 10 ) );
        Assert.AreEqual( 16, await HyperbeeCompiler.Compile( lambda )( 10 ) );
    }

    /// <summary>
    /// Issue 158 -- the catch variable becomes unbound when a TryCatch containing an Await is
    /// lowered by BlockAsync. Reported as InvalidOperationException at compile time: "variable
    /// 'ex' of type 'System.Exception' referenced from scope '', but it is not defined".
    /// </summary>
    [TestMethod]
    public async Task Issue158_CatchVariableStaysBound()
    {
        var exception = Parameter( typeof( Exception ), "ex" );

        var throwAsyncMethod = typeof( ReportedIssues )
            .GetMethod(
                nameof( ThrowAsync ),
                BindingFlags.NonPublic |
                BindingFlags.Static )!;

        var exceptionCtor = typeof( Exception )
            .GetConstructor( [typeof( string ), typeof( Exception )] )!;

        var expression = Lambda<Func<Task<object>>>(
            BlockAsync( TryCatch(
                Await(
                    Call( throwAsyncMethod ) ),
                Catch(
                    exception,
                    Throw(
                        New(
                            exceptionCtor,
                            Property(
                                exception,
                                nameof( Exception.Message ) ),
                            exception ),
                        typeof( object ) ) ) ) ) );

        // Reported failure was at compile time.
        var system = expression.Compile();
        var hyperbee = HyperbeeCompiler.Compile( expression );

        // The handler rethrows, wrapping the original. Both the message and the inner
        // exception come from the catch variable, so this fails if it lost its binding.
        foreach ( var compiled in new[] { system, hyperbee } )
        {
            Exception? thrown = null;

            try
            {
                await compiled();
            }
            catch ( Exception ex )
            {
                thrown = ex;
            }

            Assert.IsNotNull( thrown, "the handler should have rethrown" );
            Assert.AreEqual( "Boom", thrown.Message );
            Assert.IsInstanceOfType( thrown.InnerException, typeof( InvalidOperationException ) );
        }
    }

    /// <summary>
    /// Issue 159 (follow-up) -- a bare rethrow in a catch block that BlockAsync lowers. Reported
    /// as InvalidOperationException at compile time: "Rethrow statement is valid only inside a
    /// Catch block", because lowering moves the handler body out of the try.
    /// </summary>
    [TestMethod]
    public async Task Issue159_BareRethrowInsideLoweredCatch()
    {
        var exception = Parameter( typeof( Exception ) );

        var throwAsyncMethod = typeof( ReportedIssues )
            .GetMethod(
                nameof( ThrowIntAsync ),
                BindingFlags.NonPublic |
                BindingFlags.Static )!;

        var lambda = Lambda<Func<Task<int>>>(
            BlockAsync(
                TryCatch(
                    Await(
                        Call( throwAsyncMethod ) ),
                    Catch(
                        exception,
                        Rethrow( typeof( int ) ) ) ) ) );

        // Reported failure was at compile time.
        var system = lambda.Compile();
        var hyperbee = HyperbeeCompiler.Compile( lambda );

        // The rethrow must surface the original exception, with its stack trace intact.
        foreach ( var compiled in new[] { system, hyperbee } )
        {
            Exception? thrown = null;

            try
            {
                await compiled();
            }
            catch ( Exception ex )
            {
                thrown = ex;
            }

            Assert.IsNotNull( thrown, "the handler should have rethrown" );
            Assert.AreEqual( "Boom!", thrown.Message );
            Assert.IsNull( thrown.InnerException );
            Assert.Contains( nameof( ThrowIntAsync ), thrown.StackTrace ?? string.Empty );
        }
    }
}
