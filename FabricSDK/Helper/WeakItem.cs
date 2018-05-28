using System;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class WeakItem<T,S> where T : class
    {
        private WeakReference<T> reference;
        private readonly Func<S, T> createF;
        private readonly Func<S> keyF;
        public WeakItem(Func<S,T> create_func, Func<S> key_func)
        {
            createF = create_func;
            keyF = key_func;
        }

        public T Reference
        {
            get
            {
                T ret;
                S obj = keyF();
                if (reference == null)
                {
                    ret = createF(obj);
                    reference = new WeakReference<T>(ret);
                }
                else
                {
                    reference.TryGetTarget(out ret);
                    if (ret == null)
                    {
                        ret = createF(obj);
                        reference = new WeakReference<T>(ret);
                    }
                }
                return ret;
            }
        }
    }
}