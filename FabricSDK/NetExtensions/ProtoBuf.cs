using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Protos.Common;
using ProtoBuf;

namespace Hyperledger.Fabric.SDK.NetExtensions
{
    public static class ProtoBuf
    {
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

        public static T GetOrDeserializeProtoBufWR<T>(this byte[] data, ref WeakReference<T> reference) where T:class
        {
            reference.TryGetTarget(out T ret);
            if (ret == null)
            {
                try
                {
                    ret = data.DeserializeProtoBuf<T>();
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
    }

    public static class Utils
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Utils));

        private static bool TRACE_ENABED => logger.IsTraceEnabled();
        private static Helper.Config config => Hyperledger.Fabric.SDK.Helper.Config.GetConfig();
            
        private static int MAX_LOG_STRING_LENGTH => config.MaxLogStringLength();


        public static string GetOrNull(this Dictionary<string,string> tr, string key)
        {
            if (tr.ContainsKey(key))
                return tr[key];
            return null;
        }



        public static string LogString(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return str;
            string ret = Regex.Replace(str, "[^\\p{Print}]", "?");
            ret = ret.Substring(0, Math.Min(ret.Length, MAX_LOG_STRING_LENGTH)) + (ret.Length > MAX_LOG_STRING_LENGTH ? "..." : "");
            return ret;

        }
        public static byte[] CloneBytes(this byte[] origin)
        {
            byte[] result=new byte[origin.Length];
            Array.Copy(origin,result,origin.Length);
            return result;
        }

        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", string.Empty);
        }

        public static string ToUTF8String(this byte[] data)
        {
            return Encoding.UTF8.GetString(data);
        }

        public static byte[] ToBytes(this string data)
        {
            return Encoding.UTF8.GetBytes(data)
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
