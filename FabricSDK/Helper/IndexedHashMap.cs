using System.Collections.Generic;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class IndexedHashMap<TKey, TValue> : LinkedHashMap<TKey, TValue>
    {
        Dictionary<TKey, int> kmap = new Dictionary<TKey, int> ();

        public override void Add(TKey key, TValue value)
        {
            kmap.Add(key, Count);
            base.Add(key, value);
        }

        public TValue GetOrNull(TKey key)
        {
            if (ContainsKey(key))
                return this[key];
            return default(TValue);
        }
        public int Index(TKey key)
        {
            if (kmap.ContainsKey(key))
                return kmap[key];
            return -1;
        }
    }
}