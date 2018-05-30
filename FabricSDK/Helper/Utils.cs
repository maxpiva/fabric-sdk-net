/*
 *  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *        http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.Utilities.Encoders;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Hyperledger.Fabric.SDK.Helper
{
    public static class Utils
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Utils));
        private static readonly bool TRACE_ENABED = logger.IsTraceEnabled();

        private static int MAX_LOG_STRING_LENGTH = Config.Instance.MaxLogStringLength();

        public static void WriteAllBytes(this Stream stream, byte[] data)
        {
            stream.Write(data, 0, data.Length);
        }
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
            uint value = BitConverter.ToUInt32(buf, 0);
            double maxint = 0x100000000D;
            return (int)(max * (value / maxint));
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> shuffle)
        {
            return shuffle.OrderBy<T, int>((a) => RANDOM.Next(int.MaxValue));
        }
        public static Dictionary<string, string> Clone(this Dictionary<string, string> dic)
        {
            return dic.ToDictionary(a => (string)a.Key.Clone(), a => (string)a.Value.Clone());

        }

        public static long GetLongProperty(this Properties dic, string key, long def = 0)
        {
            long ret = def;
            if (dic.Contains(key))
            {
                if (long.TryParse(dic[key], out ret))
                    return ret;
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
            return BitConverter.ToString(data).Replace("-", string.Empty).ToLowerInvariant();
        }
        public static string ToHexString(this ByteString data)
        {
            return BitConverter.ToString(data.ToByteArray()).Replace("-", string.Empty).ToLowerInvariant();
        }
        public static string ToUTF8String(this byte[] data)
        {
            return Encoding.UTF8.GetString(data);
        }

        public static byte[] FromHexString(this string data)
        {
            return Regex.Split(data, "(?<=\\G..)(?!$)").Select(x => Convert.ToByte(x, 16)).ToArray();
        }



        public static byte[] ToBytes(this string data)
        {
            return Encoding.UTF8.GetBytes(data);
        }

        public static byte[] ToByteArray(this Stream stream)
        {
            if (stream is MemoryStream)
                return ((MemoryStream)stream).ToArray();
            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
        public static void UserContextCheck(this IUser userContext)
        {
            if (userContext == null)
                throw new InvalidArgumentException("UserContext is null");
            if (string.IsNullOrEmpty(userContext.Name))
                throw new InvalidArgumentException("UserContext user's name missing.");
            if (userContext.Enrollment == null)
                throw new InvalidArgumentException($"UserContext for user {userContext.Name} has no enrollment set.");
            if (string.IsNullOrEmpty(userContext.MspId))
                throw new InvalidArgumentException($"UserContext for user {userContext.Name} has user's MSPID missing.");
            if (string.IsNullOrEmpty(userContext.Enrollment.Cert))
                throw new InvalidArgumentException($"UserContext for user {userContext.Name} enrollment missing user certificate.");
            if (userContext.Enrollment.Key == null)
                throw new InvalidArgumentException($"UserContext for user {userContext.Name} has Enrollment missing signing key");
        }

        /**
         * Generate hash of the given input using the given Digest.
         *
         * @param input  input data.
         * @param digest the digest to use for hashing
         * @return hashed data.
         */
        public static byte[] Hash(byte[] input, IDigest digest)
        {
            byte[] retValue = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(input, 0, input.Length);
            digest.DoFinal(retValue, 0);
            return retValue;
        }


        private static readonly int NONONCE_LENGTH = 24;

        private static RNGCryptoServiceProvider RANDOM = new RNGCryptoServiceProvider();

        public static byte[] GenerateNonce()
        {

            byte[] values = new byte[NONONCE_LENGTH];
            RANDOM.GetBytes(values);
            return values;
        }

        /**
         * Generate parameter hash for the given chaincode path,func and args
         *
         * @param path Chaincode path
         * @param func Chaincode function name
         * @param args List of arguments
         * @return hash of path, func and args
         */
        public static string GenerateParameterHash(string path, string func, List<string> args)
        {
            logger.Debug($"GenerateParameterHash : path={path}, func={func}, args={string.Join(",",args)}");
                
            // Append the arguments
            StringBuilder param = new StringBuilder(path.Replace("\\","/"));
            param.Append(func);
            args.ForEach(a=>param.Append(a));

            // Compute the hash
            return Hash(Encoding.UTF8.GetBytes(param.ToString()), new Sha3Digest()).ToHexString();
        }

        /**
         * Generate hash of a chaincode directory
         *
         * @param rootDir      Root directory
         * @param chaincodeDir Channel code directory
         * @param hash         Previous hash (if any)
         * @return hash of the directory
         * @throws IOException
         */
        public static string GenerateDirectoryHash(string rootDir, string chaincodeDir, string hash)
        {
            // Generate the project directory
            string projectPath = null;
            if (rootDir == null)
            {
                projectPath = Path.Combine(chaincodeDir);
            }
            else
            {
                projectPath = Path.Combine(rootDir, chaincodeDir);
            }
            DirectoryInfo dir=new DirectoryInfo(projectPath);
            if (!dir.Exists)
                throw new IOException($"The chaincode path \"{projectPath}\" is invalid");
            FileInfo[] files = dir.GetFiles().Where(a => (a.Attributes & (FileAttributes.Normal | FileAttributes.ReadOnly)) != 0).OrderBy(a => a.Name).ToArray();
            string finalhash = hash;
            foreach (FileInfo f in files)
            {
                try
                {
                    byte[] buf = File.ReadAllBytes(f.FullName);
                    byte[] toHash = buf.Union(Encoding.UTF8.GetBytes(finalhash)).ToArray();
                    finalhash=Hash(toHash, new Sha3Digest()).ToHexString();
                }
                catch (IOException ex)
                {
                    throw new Exception($"Error while reading file {f.FullName}", ex);
                }
            }


        // If original hash and final hash are the same, it indicates that no new contents were found
        if (finalhash.Equals(hash)) {
            throw new IOException($"The chaincode directory \"{projectPath}\" has no files");
        }

            return finalhash;
    }

    /**
     * Compress the contents of given directory using Tar and Gzip to an in-memory byte array.
     *
     * @param sourceDirectory  the source directory.
     * @param pathPrefix       a path to be prepended to every file name in the .tar.gz output, or {@code null} if no prefix is required.
     * @param chaincodeMetaInf
     * @return the compressed directory contents.
     * @throws IOException
     */
        public static byte[] GenerateTarGz(string sourceDirectory, string pathPrefix, string chaincodeMetaInf)
        {
            logger.Trace($"generateTarGz: sourceDirectory: {sourceDirectory}, pathPrefix: {pathPrefix}, chaincodeMetaInf: {chaincodeMetaInf}");
            string sourcePath = sourceDirectory;
            string[] sfiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            string[] mfiles = chaincodeMetaInf != null ? Directory.GetFiles(chaincodeMetaInf, "*", SearchOption.AllDirectories) : new string[0];
            using (MemoryStream bos = new MemoryStream())
            {
                using (var writer = WriterFactory.Open(bos, ArchiveType.Tar, CompressionType.GZip))
                {
                    foreach(string s in sfiles)
                        writer.Write(Path.Combine(pathPrefix??"",s.Substring(sourcePath.Length+1)).Replace("\\","/"),s);
                    foreach (string s in mfiles)
                        writer.Write(Path.Combine("META-INF", s.Substring(chaincodeMetaInf.Length + 1)).Replace("\\","/"), s);
                    bos.Flush();
                }
                return bos.ToArray();
            }
        }



        /**
         * Generate a v4 UUID
         *
         * @return String representation of {@link UUID}
         */
        public static string GenerateUUID()
    {
        return Guid.NewGuid().ToString().Replace("-", string.Empty);
    }

    /**
     * Create a new {@link Timestamp} instance based on the current time
     *
     * @return timestamp
     */
    public static DateTime GenerateTimestamp()
    {
        return DateTime.UtcNow;
    }

    /**
     * Delete a file or directory
     *
     * @param file {@link File} representing file or directory
     * @throws IOException
     */
        public static void DeleteFileOrDirectory(string name)
        {
            try
            {
                if (Directory.Exists(name))
                {
                    Directory.Delete(name, true);
                }
                else if (File.Exists(name))
                    File.Delete(name);
            }
            catch (Exception e)
            {
                throw new Exception("File or directory does not exist",e);
            }

        }
        public static (string Protocol, string Host, int Port) ParseGrpcUrl(string url)
        {
            (string Protocol, string Host, int Port) ret;
            if (string.IsNullOrEmpty(url))
                throw  new IllegalArgumentException("URL cannot be null or empty");
            Dictionary<string, string> props = new Dictionary<string, string>();
            Regex p = new Regex("([^:]+)[:]//([^:]+)[:]([0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            Match m = p.Match(url);
            if (m.Success)
            {
                ret.Protocol = m.Groups[1].Value;
                ret.Host = m.Groups[2].Value;
                string ports = m.Groups[3].Value;
                int port;
                if (!int.TryParse(ports,out port))
                    throw  new IllegalArgumentException("Invalid port");
                ret.Port = port;
                if (!"grpc".Equals(ret.Protocol,StringComparison.InvariantCultureIgnoreCase) && !"grpcs".Equals(ret.Protocol, StringComparison.InvariantCultureIgnoreCase))
                    throw  new IllegalArgumentException($"Invalid protocol expected grpc or grpcs and found {ret.Protocol}.");
                return ret;
            }
            throw  new IllegalArgumentException("URL must be of the format protocol://host:port");

            // TODO: allow all possible formats of the URL
        }
        /**
 * Check if the strings Grpc url is valid
 *
 * @param url
 * @return Return the exception that indicates the error or null if ok.
 */
        public static Exception CheckGrpcUrl(String url)
        {
            try
            {

                ParseGrpcUrl(url);
                return null;

            }
            catch (Exception e)
            {
                return e;
            }
        }


        /**
         * Read a file from classpath
         *
         * @param fileName
         * @return byte[] data
         * @throws IOException
         */
        /*
        public static byte[] readFileFromClasspath(String fileName) throws IOException
{
    InputStream is = Utils.class.getClassLoader().getResourceAsStream(fileName);
byte[] data = ByteStreams.toByteArray(is);
        try {
            is.close();
        } catch (IOException ex) {
        }
        return data;
    }
    */
   






        /**
         * Private constructor to prevent instantiation.
         */


    }

}

