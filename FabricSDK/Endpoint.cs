/*
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Grpc.Core;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.SDK
{
    public class Endpoint
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Endpoint));

        private static readonly ConcurrentDictionary<string, string> CN_CACHE = new ConcurrentDictionary<string, string>();
        private readonly TLSCertificateKeyPair ckp;

        private readonly string SSLNEGOTIATION = Config.Instance.GetDefaultSSLNegotiationType();


        private byte[] clientTLSCertificateDigest;
        internal SslCredentials creds;

        public Endpoint(string url, Properties properties)
        {
            NetworkConfig.ReplaceNettyOptions(properties);

            logger.Trace($"Creating endpoint for url {url}");
            Url = url;
            string cn = null;
            string nt ;
            byte[] pemBytes = null;
            var purl = Utils.ParseGrpcUrl(url);

            Protocol = purl.Protocol;
            Host = purl.Host;
            Port = purl.Port;
            if (properties != null)
            {
                ckp = new TLSCertificateKeyPair(properties);
                if ("grpcs".Equals(Protocol, StringComparison.InvariantCultureIgnoreCase))
                {
                    using (MemoryStream bis = new MemoryStream())
                    {
                        try
                        {
                            if (properties.Contains("pemBytes"))
                                bis.WriteAllBytes(properties["pemBytes"].ToBytes());
                            if (properties.Contains("pemFile"))
                            {
                                string pemFile = properties["pemFile"];
                                Regex r = new Regex("[ \t]*,[ \t]*");
                                string[] pems = r.Split(pemFile);

                                foreach (string pem in pems)
                                {
                                    if (!string.IsNullOrEmpty(pem))
                                    {
                                        try
                                        {
                                            bis.WriteAllBytes(File.ReadAllBytes(Path.GetFullPath(pem)));
                                        }
                                        catch (IOException)
                                        {
                                            throw new ArgumentException($"Failed to read certificate file {Path.GetFullPath(pem)}");
                                        }
                                    }
                                }
                            }

                            bis.Flush();
                            pemBytes = bis.ToArray();
                            logger.Trace($"Endpoint {url} pemBytes: {pemBytes.ToHexString()}");
                            if (pemBytes.Length == 0)
                                pemBytes = null;
                        }
                        catch (Exception e)
                        {
                            throw new Exception($"Failed to read CA certificates file {e}");
                        }
                    }

                    if (pemBytes == null)
                    {
                        logger.Warn($"Endpoint {url} is grpcs with no CA certificates");
                    }

                    if (null != pemBytes)
                    {
                        try
                        {
                            cn = properties.Contains("hostnameOverride") ?  properties["hostnameOverride"] : null;
                            bool trustsv = properties.Contains("trustServerCertificate") && ( properties["trustServerCertificate"]).Equals("true", StringComparison.InvariantCultureIgnoreCase);
                            if (cn == null && trustsv)
                            {
                                string cnKey = pemBytes.ToUTF8String();
                                CN_CACHE.TryGetValue(cnKey, out cn);
                                if (cn == null)
                                {
                                    X509Certificate2 cert = Certificate.PEMToX509Certificate2(cnKey);
                                    cn = cert.GetNameInfo(X509NameType.DnsName, false);
                                    CN_CACHE.TryAdd(cnKey, cn);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // Mostly a development env. just log it.
                            logger.Error("Error getting Subject CN from certificate. Try setting it specifically with hostnameOverride property. " + e);
                        }
                    }

                    nt = null;
                    if (properties.Contains("negotiationType"))
                        nt = properties["negotiationType"];
                    if (null == nt)
                    {
                        nt = SSLNEGOTIATION;
                        logger.Trace($"Endpoint {url} specific Negotiation type not found use global value: {SSLNEGOTIATION}");
                    }

                    if (!"TLS".Equals(nt, StringComparison.InvariantCultureIgnoreCase) && !"plainText".Equals(nt, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new ArgumentException($"Endpoint {url} property of negotiationType has to be either TLS or plainText. value:'{nt}'");
                    }
                }
            }

            try
            {
                List<ChannelOption> options = new List<ChannelOption>();
                if (properties != null)
                {
                    foreach (string str in properties.Keys)
                    {
                        if (str.StartsWith("grpc."))
                        {
                            if (int.TryParse(properties[str], out int value))
                                options.Add(new ChannelOption(str, value));
                            else
                                options.Add(new ChannelOption(str, properties[str]));
                        }
                    }
                }

                if (Protocol.Equals("grpc", StringComparison.InvariantCultureIgnoreCase))
                {
                    ChannelOptions = options;
                    Credentials = null;
                }
                else if (Protocol.Equals("grpcs", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (pemBytes == null)
                    {
                        // use root certificate
                        ChannelOptions = options;
                        Credentials = null;
                    }
                    else
                    {
                        if (ckp != null && ckp.CertPEMBytes != null && ckp.KeyPEMBytes != null)
                            creds = new SslCredentials(pemBytes.ToUTF8String(), new KeyCertificatePair(ckp.CertPEMBytes.ToUTF8String(), ckp.KeyPEMBytes.ToUTF8String()));
                        else
                            creds = new SslCredentials(pemBytes.ToUTF8String());
                        if (cn != null)
                            options.Add(new ChannelOption("grpc.ssl_target_name_override", cn));
                        Credentials = creds;
                        ChannelOptions = options;
                        logger.Trace($"Endpoint {url} final server pemBytes: {pemBytes.ToHexString()}");
                    }
                }
                else
                {
                    throw new ArgumentException("invalid protocol: " + Protocol);
                }
            }
            catch (Exception e)
            {
                logger.ErrorException($"Endpoint {url}, exception '{e.Message}'", e);
                throw;
            }
        }

        public string Host { get; }

        public string Protocol { get; }

        public string Url { get; }

        public int Port { get; }

        public ChannelCredentials Credentials { get; }

        public List<ChannelOption> ChannelOptions { get; }

        public Grpc.Core.Channel BuildChannel()
        {
            return new Grpc.Core.Channel(Host, Port, Credentials ?? ChannelCredentials.Insecure, ChannelOptions);
        }

        public byte[] GetClientTLSCertificateDigest()
        {
            //The digest must be SHA256 over the DER encoded certificate. The PEM has the exact DER sequence in hex encoding around the begin and end markers
            if (ckp != null && clientTLSCertificateDigest == null)
                clientTLSCertificateDigest = ckp.GetDigest();
            return clientTLSCertificateDigest;
        }


        public static Endpoint Create(string url, Properties properties)
        {
            return new Endpoint(url, properties);
        }
    }
}