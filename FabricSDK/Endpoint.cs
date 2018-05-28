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
/*
package org.hyperledger.fabric.sdk;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.security.PrivateKey;
import java.security.cert.X509Certificate;
import java.util.Base64;
import java.util.Collections;
import java.util.HashMap;
import java.util.Map;
import java.util.Properties;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import javax.net.ssl.SSLException;

import com.google.common.collect.ImmutableMap;
import io.grpc.ManagedChannelBuilder;
import io.grpc.netty.GrpcSslContexts;
import io.grpc.netty.NegotiationType;
import io.grpc.netty.NettyChannelBuilder;
import io.netty.handler.ssl.SslContext;
import io.netty.handler.ssl.SslContextBuilder;
import io.netty.handler.ssl.SslProvider;
import org.apache.commons.codec.binary.Hex;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.bouncycastle.asn1.x500.RDN;
import org.bouncycastle.asn1.x500.X500Name;
import org.bouncycastle.asn1.x500.style.BCStyle;
import org.bouncycastle.asn1.x500.style.IETFUtils;
import org.bouncycastle.cert.jcajce.JcaX509CertificateHolder;
import org.bouncycastle.crypto.Digest;
import org.bouncycastle.crypto.digests.SHA256Digest;
import org.hyperledger.fabric.sdk.exception.CryptoException;
import org.hyperledger.fabric.sdk.helper.Config;
import org.hyperledger.fabric.sdk.security.CryptoPrimitives;

import static java.lang.String.format;
import static java.nio.charset.StandardCharsets.UTF_8;
import static org.hyperledger.fabric.sdk.helper.Utils.parseGrpcUrl;
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Grpc.Core;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;

using Hyperledger.Fabric.SDK.Security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using CryptoException = Hyperledger.Fabric.SDK.Exceptions.CryptoException;
using Utils = Hyperledger.Fabric.SDK.Helper.Utils;

namespace Hyperledger.Fabric.SDK
{
    public class Endpoint
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Endpoint));

        private readonly string SSLNEGOTIATION = Config.Instance.GetDefaultSSLNegotiationType();

        private static readonly ConcurrentDictionary<string, string> CN_CACHE = new ConcurrentDictionary<string, string>();
        private readonly byte[] tlsClientCertificatePEMBytes;
        internal SslCredentials creds;


        private byte[] clientTLSCertificateDigest;

        public Endpoint(string url, Properties properties)
        {
            NetworkConfig.ReplaceNettyOptions(properties);

            logger.Trace($"Creating endpoint for url {url}");
            Url = url;
            string cn = null;
            string nt = null;
            byte[] pemBytes = null;
            var purl = Utils.ParseGrpcUrl(url);

            Protocol = purl.Protocol;
            Host = purl.Host;
            Port = purl.Port;
            byte[] ckb = null, ccb = null;

            if (properties != null)
            {
                if ("grpcs".Equals(Protocol, StringComparison.InvariantCultureIgnoreCase))
                {
                    CryptoPrimitives cp;
                    try
                    {
                        cp = new CryptoPrimitives();
                    }
                    catch (Exception e)
                    {
                        throw;
                    }

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
                                        catch (IOException e)
                                        {
                                            throw new IllegalArgumentException($"Failed to read certificate file {Path.GetFullPath(pem)}");
                                        }
                                    }
                                }
                            }

                            bis.Flush();
                            pemBytes = bis.ToArray();

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
                            cn = properties.Contains("hostnameOverride") ? (string) properties["hostnameOverride"] : null;
                            bool trustsv = properties.Contains("trustServerCertificate") && ((string) properties["trustServerCertificate"]).Equals("true", StringComparison.InvariantCultureIgnoreCase);
                            if (cn == null && trustsv)
                            {
                                string cnKey = pemBytes.ToUTF8String();
                                CN_CACHE.TryGetValue(cnKey, out cn);
                                if (cn == null)
                                {
                                    X509Certificate2 cert = cp.BytesToCertificate(pemBytes);
                                    cn = cert.GetNameInfo(X509NameType.DnsName, false);
                                    CN_CACHE.TryAdd(cnKey, cn);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            /// Mostly a development env. just log it.
                            logger.Error("Error getting Subject CN from certificate. Try setting it specifically with hostnameOverride property. " + e);
                        }
                    }

                    // check for mutual TLS - both clientKey and clientCert must be present
                    if (properties.Contains("clientKeyFile") && properties.Contains("clientKeyBytes"))
                    {
                        throw new IllegalArgumentException("Properties \"clientKeyFile\" and \"clientKeyBytes\" must cannot both be set");
                    }
                    else if (properties.Contains("clientCertFile") && properties.Contains("clientCertBytes"))
                    {
                        throw new IllegalArgumentException("Properties \"clientCertFile\" and \"clientCertBytes\" must cannot both be set");
                    }
                    else if (properties.Contains("clientKeyFile") || properties.Contains("clientCertFile"))
                    {
                        if (properties.Contains("clientKeyFile") && properties.Contains("clientCertFile"))
                        {
                            try
                            {
                                ckb = File.ReadAllBytes(Path.GetFullPath((string) properties["clientKeyFile"]));
                                ccb = File.ReadAllBytes(Path.GetFullPath((string) properties["clientCertFile"]));
                            }
                            catch (Exception e)
                            {
                                throw new IllegalArgumentException("Failed to parse TLS client key and/or cert", e);
                            }
                        }
                        else
                        {
                            throw new IllegalArgumentException("Properties \"clientKeyFile\" and \"clientCertFile\" must both be set or both be null");
                        }
                    }
                    else if (properties.Contains("clientKeyThumbprint"))
                    {
                        X509Certificate2 certi = SearchCertificateByFingerprint((string) properties["clientKeyThumbprint"]);
                        if (certi == null)
                            throw new IllegalArgumentException($"Thumbprint {(string) properties["clientKeyThumbprint"]} not found in KeyStore");
                        ccb = ExportToPEMCert(certi);
                        ckb = ExportToPEMKey(certi);
                    }
                    else if (properties.Contains("clientKeySubject"))
                    {
                        X509Certificate2 certi = SearchCertificateBySubject((string) properties["clientKeySubject"]);
                        if (certi == null)
                            throw new IllegalArgumentException($"Subject {(string) properties["clientKeySubject"]} not found in KeyStore");
                        ccb = ExportToPEMCert(certi);
                        ckb = ExportToPEMKey(certi);
                    }
                    else if (properties.Contains("clientKeyBytes") || properties.Contains("clientCertBytes"))
                    {
                        ckb = properties["clientKeyBytes"].ToBytes();
                        ccb = properties["clientCertBytes"].ToBytes();
                        if (ckb == null || ccb == null)
                        {
                            throw new IllegalArgumentException("Properties \"clientKeyBytes\" and \"clientCertBytes\" must both be set or both be null");
                        }
                    }

                    if (ckb != null && ccb != null)
                    {
                        string what = "private key";
                        try
                        {
                            logger.Trace("client TLS private key bytes size:" + ckb.Length);
                            //clientKey = cp.BytesToPrivateKey(ckb);
                            logger.Trace("converted TLS key.");
                            what = "certificate";
                            logger.Trace("client TLS certificate bytes:" + ccb.ToHexString());
                            //clientCert = new X509Certificate2[] {(X509Certificate2) cp.BytesToCertificate(ccb)};
                            logger.Trace("converted client TLS certificate.");
                            tlsClientCertificatePEMBytes = ccb; // Save this away it's the exact pem we used.
                        }
                        catch (CryptoException e)
                        {
                            throw new IllegalArgumentException("Failed to parse TLS client " + what, e);
                        }
                    }

                    nt = null;
                    if (properties.Contains("negotiationType"))
                        nt = (string) properties["negotiationType"];
                    if (null == nt)
                    {
                        nt = SSLNEGOTIATION;
                        logger.Trace($"Endpoint {url} specific Negotiation type not found use global value: {SSLNEGOTIATION}");
                    }

                    if (!"TLS".Equals(nt, StringComparison.InvariantCultureIgnoreCase) && !"plainText".Equals(nt, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new IllegalArgumentException($"Endpoint {url} property of negotiationType has to be either TLS or plainText. value:'{nt}'");
                    }
                }
            }

            try
            {
                List<ChannelOption> options = new List<ChannelOption>();
                foreach (string str in properties)
                {
                    if (str.StartsWith("grpc."))
                    {
                        if (int.TryParse(properties[str], out int value))
                            options.Add(new ChannelOption(str,value));
                        else
                            options.Add(new ChannelOption(str,properties[str]));
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
                        try
                        {
                            if (ckb != null && ccb != null)
                                creds = new SslCredentials(pemBytes.ToUTF8String(), new KeyCertificatePair(ccb.ToUTF8String(), ckb.ToUTF8String()));
                            else
                                creds = new SslCredentials(pemBytes.ToUTF8String());
                            if (cn != null)
                                options.Add(new ChannelOption("grpc.default_authority", cn));
                            Credentials = creds;
                            ChannelOptions = options;
                        }
                        catch (Exception sslex)
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    throw new IllegalArgumentException("invalid protocol: " + Protocol);
                }
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw e;
            }
        }

        /*
        private static final Pattern METHOD_PATTERN = Pattern.compile("grpc\\.NettyChannelBuilderOption\\.([^.]*)$");
        private static final Map<Class<?>, Class<?>> WRAPPERS_TO_PRIM = new ImmutableMap.Builder<Class<?>, Class<?>>()
        .put(Boolean.class, boolean.class).put(Byte.class, byte.class).put(Character.class, char.class).put(Double.class, double.class).put(Float.class, float.class).put(Integer.class, int.class).put(Long.class, long.class).put(Short.class, short.class).put(Void.class, void.class).build();

        private void addNettyBuilderProps(NettyChannelBuilder channelBuilder, Properties props)
        {

            if (props == null)
            {
                return;
            }

            for (Map.Entry < ?, ?> es :
            props.entrySet()) {
                Object methodprop = es.getKey();
                if (methodprop == null)
                {
                    continue;
                }

                String methodprops = System.String.valueOf(methodprop);

                Matcher match = METHOD_PATTERN.matcher(methodprops);

                String methodName = null;

                if (match.matches() && match.groupCount() == 1)
                {
                    methodName = match.group(1).trim();

                }

                if (null == methodName || "forAddress".equals(methodName) || "build".equals(methodName))
                {

                    continue;
                }

                Object parmsArrayO = es.getValue();
                Object[] parmsArray;
                if (!(parmsArrayO instanceof Object[])) {
                    parmsArray = new Object[] {parmsArrayO};

                } else {
                    parmsArray = (Object[]) parmsArrayO;
                }

                Class < ?>[] classParms = new Class[parmsArray.length];
                int i = -1;
                for (Object oparm :
                parmsArray) {
                    ++i;

                    if (null == oparm)
                    {
                        classParms[i] = Object.class;
                        continue;
                    }

                    Class < ?> unwrapped = WRAPPERS_TO_PRIM.get(oparm.getClass());
                    if (null != unwrapped)
                    {
                        classParms[i] = unwrapped;
                    }
                    else
                    {

                        Class < ?> clz = oparm.getClass();

                        Class < ?> ecz = clz.getEnclosingClass();
                        if (null != ecz && ecz.isEnum())
                        {
                            clz = ecz;
                        }

                        classParms[i] = clz;
                    }
                }

                final Method method = channelBuilder.getClass().getMethod(methodName, classParms);

                method.invoke(channelBuilder, parmsArray);

                if (logger.isTraceEnabled())
                {
                    StringBuilder sb = new StringBuilder(200);
                    String sep = "";
                    for (Object p :
                    parmsArray) {
                        sb.append(sep).append(p + "");
                        sep = ", ";

                    }
                    logger.trace(format("Endpoint with url: %s set managed channel builder method %s (%s) ", url, method, sb.toString()));

                }

            }

        }
                */
        public Grpc.Core.Channel BuildChannel() => new Grpc.Core.Channel(Host,Port,Credentials,ChannelOptions);

        public string Host { get; }

        public string Protocol { get; }

        public string Url { get; }

        public int Port { get; }

        public ChannelCredentials Credentials { get; }

        public List<ChannelOption> ChannelOptions { get; }
        public byte[] GetClientTLSCertificateDigest()
        {
            //The digest must be SHA256 over the DER encoded certificate. The PEM has the exact DER sequence in hex encoding around the begin and end markers

            if (tlsClientCertificatePEMBytes != null && clientTLSCertificateDigest == null)
            {
                Regex regex = new Regex("-+[ \t]*(BEGIN|END)[ \t]+CERTIFICATE[ \t]*-+");
                string pemCert = tlsClientCertificatePEMBytes.ToUTF8String();
                byte[] derBytes = Convert.FromBase64String(regex.Replace(pemCert, string.Empty).Replace(" ", string.Empty));
                IDigest digest = new Sha256Digest();
                clientTLSCertificateDigest = new byte[digest.GetDigestSize()];
                digest.BlockUpdate(derBytes, 0, derBytes.Length);
                digest.DoFinal(clientTLSCertificateDigest, 0);
            }

            return clientTLSCertificateDigest;
        }

        private X509Certificate2 SearchCertificate(string value, Func<string, X509Certificate2, bool> check_func)
        {
            StoreName[] stores = {StoreName.My, StoreName.TrustedPublisher, StoreName.TrustedPeople, StoreName.Root, StoreName.CertificateAuthority, StoreName.AuthRoot, StoreName.AddressBook};
            StoreLocation[] locations = {StoreLocation.CurrentUser, StoreLocation.LocalMachine};
            foreach (StoreLocation location in locations)
            {
                foreach (StoreName s in stores)
                {
                    X509Store store = new X509Store(s, location);
                    store.Open(OpenFlags.ReadOnly);
                    foreach (X509Certificate2 m in store.Certificates)
                    {
                        if (check_func(value, m))
                        {
                            store.Close();
                            return m;
                        }
                    }

                    store.Close();
                }
            }

            return null;
        }

        private X509Certificate2 SearchCertificateBySubject(string subject)
        {
            return SearchCertificate(subject, (certname, m) => m.Subject.IndexOf("CN=" + certname, 0, StringComparison.InvariantCultureIgnoreCase) >= 0 || m.Issuer.IndexOf("CN=" + certname, 0, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }

        private X509Certificate2 SearchCertificateByFingerprint(string finger)
        {
            return SearchCertificate(finger, (certname, m) => m.Thumbprint?.Equals(certname, StringComparison.InvariantCultureIgnoreCase) ?? false);
        }


        public static byte[] ExportToPEMCert(X509Certificate2 cert)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");

            return builder.ToString().ToBytes();
        }

        public static byte[] ExportToPEMKey(X509Certificate2 cert)
        {
            AsymmetricCipherKeyPair keyPair = DotNetUtilities.GetRsaKeyPair(cert.GetRSAPrivateKey());
            using (StringWriter str = new StringWriter())
            {
                PemWriter pw = new PemWriter(str);
                pw.WriteObject(keyPair.Private);
                str.Flush();
                return str.ToString().ToBytes();
            }
        }
    }
}