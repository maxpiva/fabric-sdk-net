using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Google.Protobuf;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;
using Org.BouncyCastle.Bcpg.OpenPgp;


namespace Hyperledger.Fabric.SDK.NetExtensions
{
    public static class ProtoBuf
    {
        /*
        public static T DeserializeProtoBuf<T>(this byte[] data)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                    return Serializer.Deserialize<T>(ms);
            }
            catch (Exception e)
            {
                throw new InvalidProtocolBufferRuntimeException(e);
            }
        }

        public static byte[] SerializeProtoBuf<T>(this T o)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    Serializer.Serialize<T>(ms,o);
                    ms.Flush();
                    return ms.ToArray();
                }
            }
            catch (Exception e)
            {
                throw new InvalidProtocolBufferRuntimeException(e);
            }
        }
        */
        public static T GetOrDeserializeProtoBufWR<T>(this byte[] data, ref WeakReference<T> reference) where T:class, IMessage<T>, new()
        {
            reference.TryGetTarget(out T ret);
            if (ret == null)
            {
                try
                {
                    MessageParser<T> parser=new MessageParser<T>(() => new T());
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
        public static T GetOrCreateWR<T,S>(this S obj, ref WeakReference<T> reference,Func<S, T> create_func) where T : class
        {
            reference.TryGetTarget(out T ret);
            if (ret == null)
            {
                ret = create_func(obj);
                reference = new WeakReference<T>(ret);
            }
            return ret;
        }

        public static void WriteAllBytes(this Stream stream, byte[] data)
        {
            stream.Write(data,0,data.Length);
        }
    }
    public class CountDownLatch
    {
        private int remain;
        private EventWaitHandle e;

        public CountDownLatch(int count)
        {
            remain = count;
            e = new ManualResetEvent(false);
        }

        public void Signal()
        {
            if (Interlocked.Decrement(ref remain) == 0)
                e.Set();
        }

        public bool Wait()
        {
            return e.WaitOne();
        }
        public bool Wait(int mill)
        {
            return e.WaitOne(mill);
        }
    }
    public static class Utils
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Utils));

        private static bool TRACE_ENABED => logger.IsTraceEnabled();
        private static Helper.Config config => Hyperledger.Fabric.SDK.Helper.Config.GetConfig();
            
        private static int MAX_LOG_STRING_LENGTH => config.MaxLogStringLength();


        public static TValue GetOrNull<TKey, TValue>(this Dictionary<TKey, TValue> tr, TKey key)
        {
            if (tr.ContainsKey(key))
                return tr[key];
            return default(TValue);
        }

        public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> data)
        {
            foreach (T n in data)
            {
                if (!set.Contains(n))
                    set.Add(n);
            }
        }

        public static int Next(this RNGCryptoServiceProvider provider, int max)
        {
            byte[] buf = new byte[4];
            provider.GetBytes(buf);
            uint value=BitConverter.ToUInt32(buf,0);
            double maxint = 0x100000000D;
            return (int) (max * (value / maxint));
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> shuffle, RNGCryptoServiceProvider provider)
        {
            return shuffle.OrderBy<T, int>((a) => provider.Next(int.MaxValue));
        }
        public static Dictionary<string, string> Clone(this Dictionary<string, string> dic) 
        {
            return  dic.ToDictionary(a => (string)a.Key.Clone(), a => (string)a.Value.Clone());

        }

        public static long GetLongProperty(this Dictionary<string, object> dic, string key, long def = 0)
        {
            long ret = def;
            if (dic.ContainsKey(key))
            {
                object o = dic[key];
                if (o is long)
                    ret = (long) o;
                if (o is int)
                    ret = (int) o;
                if (o is short)
                    ret = (short) o;
                if (o is byte)
                    ret = (byte) o;
            }

            return ret;
        }
        public static string LogString(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            string ret = Regex.Replace(str, "[^\\p{Print}]", "?");
            ret = ret.Substring(0, Math.Min(ret.Length, MAX_LOG_STRING_LENGTH)) + (ret.Length > MAX_LOG_STRING_LENGTH ? "..." : "");
            return ret;

        }

        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", string.Empty);
        }
        public static string ToHexString(this ByteString data)
        {
            return BitConverter.ToString(data.ToByteArray()).Replace("-", string.Empty);
        }
        public static string ToUTF8String(this byte[] data)
        {
            return Encoding.UTF8.GetString(data);
        }

        public static byte[] ToBytes(this string data)
        {
            return Encoding.UTF8.GetBytes(data);
        }

        public static byte[] ToByteArray(this Stream stream)
        {
            if (stream is MemoryStream)
                return ((MemoryStream) stream).ToArray();
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

    }

    public static class User
    {
        public static void UserContextCheck(this IUser userContext)
        {
            if (userContext == null)
                throw new InvalidArgumentException("UserContext is null");
            if (string.IsNullOrEmpty(userContext.Name))
                throw new InvalidArgumentException("UserContext user's name missing.");
            if (userContext.Enrollment==null)
               throw new InvalidArgumentException($"UserContext for user {userContext.Name} has no enrollment set.");
            if (string.IsNullOrEmpty(userContext.MspId))
               throw new InvalidArgumentException($"UserContext for user {userContext.Name} has user's MSPID missing.");
            if (string.IsNullOrEmpty(userContext.Enrollment.Cert))
                throw new InvalidArgumentException($"UserContext for user {userContext.Name} enrollment missing user certificate.");
            if (userContext.Enrollment.Key == null)
                throw new InvalidArgumentException($"UserContext for user {userContext.Name} has Enrollment missing signing key");
        }
    }
}
