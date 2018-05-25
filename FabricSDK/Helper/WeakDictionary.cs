using System;
using System.Collections.Generic;
using System.Text;


namespace Hyperledger.Fabric.SDK.Helper
{
    public class WeakDictionary<TKey,TValue> where TValue:class
    {
        private readonly Dictionary<TKey, WeakReference<TValue>> _dict=new Dictionary<TKey, WeakReference<TValue>>();
        private readonly Func<TKey, TValue> createF;

        public WeakDictionary(Func<TKey, TValue> createFunc)
        {
            createF = createFunc;
        }



        public TValue Get(TKey key)
        {
            if (createF == null)
                return null;
            lock (_dict)
            {
                TValue value;
                if (_dict.ContainsKey(key))
                {
                    if (!_dict[key].TryGetTarget(out value))
                    {
                        value = createF(key);
                        _dict[key]=new WeakReference<TValue>(value);
                    }
                    return value;
                }
                value = createF(key);
                _dict.Add(key,new WeakReference<TValue>(value));
                return value;
            }
        }
    }
}
