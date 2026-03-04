using System.Runtime.CompilerServices;

namespace Hyperbee.Expressions.Compiler;

/// <summary>
/// Provides an <see cref="IRuntimeVariables"/> implementation that wraps
/// an array of <see cref="IStrongBox"/> instances, giving live read/write
/// access to variables stored in StrongBox closures.
/// </summary>
internal static class RuntimeVariablesHelper
{
    /// <summary>
    /// Creates an <see cref="IRuntimeVariables"/> from an array of strong boxes.
    /// Called at runtime by compiled RuntimeVariables expressions.
    /// </summary>
    public static IRuntimeVariables Create( IStrongBox[] boxes )
    {
        return new RuntimeVariablesList( boxes );
    }

    private sealed class RuntimeVariablesList : IRuntimeVariables
    {
        private readonly IStrongBox[] _boxes;

        public RuntimeVariablesList( IStrongBox[] boxes )
        {
            _boxes = boxes;
        }

        public int Count => _boxes.Length;

        public object? this[int index]
        {
            get => _boxes[index].Value;
            set => _boxes[index].Value = value;
        }
    }
}
