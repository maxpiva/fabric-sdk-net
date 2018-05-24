using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.SDK.NetExtensions;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class BaseDeserializer<T> where T: class, IMessage<T>, new()
    {
        protected internal ByteString bs;
        protected WeakReference<T> reference;
        public BaseDeserializer(ByteString byteString)
        {
            bs = byteString;
        }
        public BaseDeserializer(T reference)
        {
            bs = reference.ToByteString();
            Reference = reference;
        }
        internal T Reference
        {
            get => bs.ToByteArray().GetOrDeserializeProtoBufWR(ref reference);
            set => reference = new WeakReference<T>(value);
        }
    }
}
