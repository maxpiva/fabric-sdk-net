using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.SDK.Exceptions;


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
            get
            {
                byte[] data = bs.ToByteArray();
                reference.TryGetTarget(out T ret);
                if (ret == null)
                {
                    try
                    {
                        MessageParser<T> parser = new MessageParser<T>(() => new T());
                        ret = parser.ParseFrom(data);
                    }
                    catch (Exception e)
                    {
                        throw new InvalidProtocolBufferRuntimeException(e);
                    }

                    reference = new WeakReference<T>(ret);
                }
                return ret;
            }
            set => reference = new WeakReference<T>(value);
        }
    }
}
