using System.Collections;

namespace Hyperbee.Expressions.CompilerServices.Collections
{
    public class TreeDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _values = new();
        private readonly TreeDictionary<TKey, TValue> _parent;
        // Track children?

        public TreeDictionary(TreeDictionary<TKey, TValue> parent = null)
        {
            _parent = parent;
        }

        public TreeDictionary<TKey, TValue> Branch() => new(this);

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            _values.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _values.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            EnumerateTree().ToList().CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return _values.Remove(item.Key);
        }

        public int Count => EnumerateTree().Count();

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            _values.Add(key, value);
        }

        public bool ContainsKey(TKey key)
        {
            return TryGetValue(key, out _);
        }

        public bool Remove(TKey key)
        {
            return _values.Remove(key);
        }

        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
                throw new KeyNotFoundException();
            }
            set
            {
                _values[key] = value;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            TreeDictionary<TKey, TValue> current = this;
            while (current != null)
            {
                if (current._values.TryGetValue(key, out value))
                {
                    return true;
                }
                current = current._parent;
            }
            value = default;
            return false;
        }

        public ICollection<TKey> Keys => EnumerateKeys();

        public ICollection<TValue> Values => EnumerateValues();

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach ( var kvp in EnumerateTree() )
            {
                yield return kvp;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private IEnumerable<KeyValuePair<TKey, TValue>> EnumerateTree()
        {
            var current = this;
            var seenKeys = new HashSet<TKey>();

            while (current != null)
            {
                foreach (var kvp in current._values)
                {
                    if (seenKeys.Add(kvp.Key))
                    {
                        yield return kvp;
                    }
                }
                current = current._parent;
            }
        }

        private ICollection<TKey> EnumerateKeys()
        {
            return EnumerateTree().Select(kvp => kvp.Key).ToList();
        }

        private ICollection<TValue> EnumerateValues()
        {
            return EnumerateTree().Select( kvp => kvp.Value ).ToList();
        }
    }
}
