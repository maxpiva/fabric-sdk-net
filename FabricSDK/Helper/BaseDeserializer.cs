using System;
using Google.Protobuf;
using Hyperledger.Fabric.SDK.Exceptions;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class BaseDeserializer<T> where T : class, IMessage<T>, new()
    {
        protected internal ByteString bs;
        protected WeakReference<T> reference = null;

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
                T ret;
                byte[] data = bs.ToByteArray();
                if (reference == null)
                    ret = Parse(data);
                else
                {
                    reference.TryGetTarget(out ret);
                    if (ret == null)
                        ret = Parse(data);
                }

                return ret;
            }
            set => reference = new WeakReference<T>(value);
        }

        private T Parse(byte[] data)
        {
            try
            {
                MessageParser<T> parser = new MessageParser<T>(() => new T());
                T ret = parser.ParseFrom(data);
                reference = new WeakReference<T>(ret);
                return ret;
            }
            catch (Exception e)
            {
                throw new InvalidProtocolBufferRuntimeException(e);
            }
        }
    }
}