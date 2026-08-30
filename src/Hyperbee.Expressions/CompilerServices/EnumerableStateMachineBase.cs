using System.Collections;
using System.ComponentModel;

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// Base for generated enumerable state machines. Carries the plumbing that is the same for
/// every one of them, so a generated type defines only its fields, <see cref="MoveNext"/>
/// and <see cref="Clone"/>.
/// </summary>
/// <remarks>
/// <para>
/// This was emitted per state machine: two GetEnumerator overloads, two Current accessors,
/// Reset and Dispose -- six methods of hand-written IL for a type that differs from the next
/// one only in its fields. Defining the type was about half the cost of compiling a
/// <c>BlockEnumerable</c>. Writing it once here is cheaper to build and less IL to get wrong.
/// </para>
/// <para>
/// Public because generated types derive from it and emitted code reaches these members
/// directly. Not intended for use from source.
/// </para>
/// </remarks>
[EditorBrowsable( EditorBrowsableState.Never )]
public abstract class EnumerableStateMachineBase<TResult> : IEnumerable<TResult>, IEnumerator<TResult>
{
    /// <summary>
    /// The lowered state index. Negative values are not states: -1 before an enumerator has
    /// been taken, <see cref="ReadyState"/> once one has but before the body has run, and -2
    /// once enumeration has finished.
    /// </summary>
    public int __state = -1;

    /// <summary>
    /// Taken an enumerator, not yet advanced. No state id is negative, so the jump table
    /// matches nothing and the body starts from the top.
    /// </summary>
    /// <remarks>
    /// Distinct from state 0 because disposing here must not run a finally: nothing has been
    /// entered yet. It was 0, which is a real state, and disposing ran the finally of a try
    /// the body had not reached.
    /// </remarks>
    private const int ReadyState = -3;

    /// <summary>
    /// The value most recently yielded.
    /// </summary>
    public TResult __current;

    /// <summary>
    /// Set while <see cref="Dispose"/> is driving the machine to its pending finally blocks.
    /// </summary>
    public bool __disposing;

    private readonly int _initialThreadId = Environment.CurrentManagedThreadId;

    /// <summary>
    /// Advances the machine. Generated types override this with the lowered body.
    /// </summary>
    public abstract bool MoveNext();

    /// <summary>
    /// A new machine carrying the same enclosing values, with its own state and its own
    /// locals. Generated types override this.
    /// </summary>
    protected abstract EnumerableStateMachineBase<TResult> Clone();

    public TResult Current => __current;

    object IEnumerator.Current => __current;

    /// <summary>
    /// The enumerator for this sequence.
    /// </summary>
    /// <remarks>
    /// The machine is its own enumerator, which it can be exactly once: the state index and
    /// the hoisted locals are fields, so a second enumeration sharing them would resume
    /// where the first stopped rather than start over, and two running at once would step on
    /// each other. The first caller on the thread that built the machine gets it, and every
    /// caller after that gets a copy. This is what a C# iterator does.
    /// </remarks>
    public IEnumerator<TResult> GetEnumerator()
    {
        if ( __state == -1 && _initialThreadId == Environment.CurrentManagedThreadId )
        {
            __state = ReadyState;
            return this;
        }

        var clone = Clone();

        clone.__state = ReadyState;

        return clone;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Reset() => throw new NotSupportedException();

    /// <summary>
    /// Ends enumeration, running whatever finally blocks are pending.
    /// </summary>
    /// <remarks>
    /// Abandoning a sequence -- breaking out of a foreach, or taking the first few elements
    /// -- disposes the enumerator, and the finally blocks the body is suspended inside have
    /// to run. Re-entering MoveNext with <see cref="__disposing"/> set routes the machine to
    /// those finally blocks instead of resuming the body, and out.
    ///
    /// Only a machine suspended mid-enumeration has any pending: one that never started
    /// never entered a try, and one that ran to completion left through them already.
    /// </remarks>
    public void Dispose()
    {
        if ( __state < 0 )
        {
            __state = -2;
            return;
        }

        __disposing = true;

        try
        {
            MoveNext();
        }
        finally
        {
            __disposing = false;
            __state = -2;
        }
    }
}
