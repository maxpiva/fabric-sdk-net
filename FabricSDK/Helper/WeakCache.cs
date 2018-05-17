using System;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class WeakCache<T,S> where T:class
    {
        private Dictionary<S, WeakReference<T>> _dict=new Dictionary<S, WeakReference<T>>();
        private Func<S, T> createF;

        public void SetCreationFunction(Func<S, T> createFunc)
        {
            createF = createFunc;

        }
        public T Get(S key)
        {
            if (createF == null)
                return null;
            lock (_dict)
            {
                T value;
                if (_dict.ContainsKey(key))
                {
                    if (!_dict[key].TryGetTarget(out value))
                    {
                        value = createF(key);
                        _dict[key]=new WeakReference<T>(value);
                    }
                    return value;
                }
                value = createF(key);
                _dict.Add(key,new WeakReference<T>(value));
                return value;
            }
        }
    }
}
