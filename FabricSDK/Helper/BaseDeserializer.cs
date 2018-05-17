using System;
using System.Collections.Generic;
using System.Text;
using Hyperledger.Fabric.SDK.NetExtensions;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class BaseDeserializer<T> where T: class
    {
        protected internal byte[] byteString;
        protected WeakReference<T> reference;
        public BaseDeserializer(byte[] byteString)
        {
            this.byteString = byteString;
        }
        internal T Reference => byteString.GetOrDeserializeProtoBufWR(ref reference);
    }
}
