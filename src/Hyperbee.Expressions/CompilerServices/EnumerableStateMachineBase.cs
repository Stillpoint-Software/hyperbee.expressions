using System.Collections;
using System.ComponentModel;

namespace Hyperbee.Expressions.CompilerServices;

/// <summary>
/// Base for generated enumerable state machines. Carries the plumbing that is the same for
/// every one of them, so a generated type defines only its fields and <see cref="MoveNext"/>.
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
    /// The lowered state index: -1 before enumeration begins, -2 once it has finished.
    /// </summary>
    public int __state = -1;

    /// <summary>
    /// The value most recently yielded.
    /// </summary>
    public TResult __current;

    /// <summary>
    /// Advances the machine. Generated types override this with the lowered body.
    /// </summary>
    public abstract bool MoveNext();

    public TResult Current => __current;

    object IEnumerator.Current => __current;

    public IEnumerator<TResult> GetEnumerator()
    {
        // TODO: this needs more logic for handling threads and multiple enumerators
        __state = 0;
        return this;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Reset() => throw new NotSupportedException();

    public void Dispose()
    {
        // TODO: Dispose all disposable fields
        // TODO: NOTE: this could include nested IEnumerable<> state machines
        __state = -2;
    }
}
