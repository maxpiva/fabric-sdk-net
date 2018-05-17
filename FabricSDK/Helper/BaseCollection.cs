using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class BaseCollection<T> : IEnumerable<T>
    {
        private readonly Func<int> cntF;
        private readonly Func<int, T> yieldF;
        public BaseCollection(Func<int> countFunction, Func<int, T> yieldFunction)
        {
            cntF = countFunction;
            yieldF = yieldFunction;
        }
        public IEnumerator<T> GetEnumerator()
        {
            for (int x = 0; x < cntF(); x++)
            {
                yield return yieldF(x);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
