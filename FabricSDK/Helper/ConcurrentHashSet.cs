using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class ConcurrentHashSet<T> : ICollection<T>, IReadOnlyCollection<T>
    {
        private readonly ConcurrentDictionary<T, byte> set;

        public ConcurrentHashSet()
        {
            set = new ConcurrentDictionary<T, byte>();
        }

        public ConcurrentHashSet(IEnumerable<T> collection)
        {
            set = new ConcurrentDictionary<T, byte>(collection.Select(a => new KeyValuePair<T, byte>(a, 0)));
        }

        public ConcurrentHashSet(IEqualityComparer<T> comparer)
        {
            set = new ConcurrentDictionary<T, byte>(comparer);
        }

        public ConcurrentHashSet(IEnumerable<T> collection, IEqualityComparer<T> comparer)
        {
            set = new ConcurrentDictionary<T, byte>(collection.Select(a => new KeyValuePair<T, byte>(a, 0)), comparer);
        }

        public ConcurrentHashSet(int concurrencyLevel, int capacity)
        {
            set = new ConcurrentDictionary<T, byte>(concurrencyLevel, capacity);
        }

        public ConcurrentHashSet(int concurrencyLevel, IEnumerable<T> collection, IEqualityComparer<T> comparer)
        {
            set = new ConcurrentDictionary<T, byte>(concurrencyLevel, collection.Select(a => new KeyValuePair<T, byte>(a, 0)), comparer);
        }

        public ConcurrentHashSet(int concurrencyLevel, int capacity, IEqualityComparer<T> comparer)
        {
            set = new ConcurrentDictionary<T, byte>(concurrencyLevel, capacity, comparer);
        }

        public bool IsEmpty => set.IsEmpty;

        public int Count => set.Count;

        public void Clear() => set.Clear();

        public bool Contains(T item)
        {
            if (item == null)
                return false;
            return set.ContainsKey(item);
        }

        void ICollection<T>.Add(T item) => ((ICollection<KeyValuePair<T, byte>>) set).Add(new KeyValuePair<T, byte>(item, 0));

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            foreach (KeyValuePair<T, byte> pair in set)
                array[arrayIndex++] = pair.Key;
        }

        bool ICollection<T>.Remove(T item) => TryRemove(item);

        public bool TryAdd(T item) => set.TryAdd(item, 0);

        public bool TryRemove(T item) => set.TryRemove(item, out _);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => set.Keys.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => set.Keys.GetEnumerator();
        bool ICollection<T>.IsReadOnly => false;
    }
}