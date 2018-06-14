using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO.Pem;
using CryptoException = Hyperledger.Fabric.SDK.Exceptions.CryptoException;
using PemReader = Org.BouncyCastle.OpenSsl.PemReader;
using PemWriter = Org.BouncyCastle.OpenSsl.PemWriter;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Hyperledger.Fabric.SDK.Security
{
    public class Certificate
    {
        private X509Certificate2 msone;
        private string pem;
        private X509Certificate entry;

        public X509Certificate X509Certificate
        {
            get
            {
                if (entry == null)
                {
                    if (!string.IsNullOrEmpty(pem))
                        entry = PEMToX509Certificate(pem);
                    else if (msone != null)
                    {
                        pem = X509Certificate2ToPEM(msone);
                        entry = PEMToX509Certificate(pem);
                    }
                }
                return entry;
            }
        }

        public X509Certificate2 X509Certificate2
        {
            get
            {
                if (msone == null && !string.IsNullOrEmpty(pem))
                    msone = PEMToX509Certificate2(pem);
                return msone;
            }
            set
            {
                msone = value;
                pem = null;
                entry = null;
            }
        }

        public string Pem
        {
            get
            {
                if (string.IsNullOrEmpty(pem) && msone != null)
                    pem = X509Certificate2ToPEM(msone);
                return pem;
            }
            set
            {
                pem = value;
                msone = null;
                entry = null;
            }
        }

        public static Certificate Create(X509Certificate2 cert)
        {
            if (cert==null)
                throw new IllegalArgumentException("Empty cert provided");
            Certificate kp = new Certificate();
            kp.msone = cert;
            kp.Pem = X509Certificate2ToPEM(cert);
            return kp;
        }

        public static Certificate Create(string pem)
        {
            if (string.IsNullOrEmpty(pem))
                throw new IllegalArgumentException("Empty PEM provided");
            Certificate kp = new Certificate();
            kp.pem = pem;
            kp.msone = PEMToX509Certificate2(pem);
            return kp;
        }


        public static X509Certificate2 PEMToX509Certificate2(string pemCertificate)
        {
            (AsymmetricKeyParameter pubKey, AsymmetricKeyParameter privKey, X509Certificate certificate) = KeyPair.PEMToAsymmetricCipherKeyPairAndCert(pemCertificate);
            if (certificate == null)
                throw new CryptoException("Invalid Certificate");
            if (privKey == null)
                return new X509Certificate2(DotNetUtilities.ToX509Certificate(certificate));
            Pkcs12Store store = new Pkcs12StoreBuilder().Build();
            store.SetKeyEntry("Hyperledger.Fabric", new AsymmetricKeyEntry(privKey), new[] {new X509CertificateEntry(certificate)});
            using (MemoryStream ms = new MemoryStream())
            {
                store.Save(ms, null, new SecureRandom());
                ms.Flush();
                ms.Position = 0;
                return new X509Certificate2(ms.ToArray(), (string) null, X509KeyStorageFlags.Exportable);
            }
        }

        private static string DumpOnePEM(X509Certificate cert, AsymmetricKeyParameter privkey)
        {
            using (StringWriter sw = new StringWriter())
            {
                PemWriter pemWriter = new PemWriter(sw);
                pemWriter.WriteObject(cert);
                if (privkey != null)
                    pemWriter.WriteObject(privkey);
                sw.Flush();
                return sw.ToString();
            }
        }

        public byte[] ExtractDER()
        {
            return ExtractDER(Pem);
        }
        public static byte[] ExtractDER(string pemcert)
        {
            byte[] content = null;

            try
            {
                using (StringReader sr = new StringReader(pemcert))
                {
                    PemReader pemReader = new PemReader(sr);
                    PemObject pemObject = pemReader.ReadPemObject();
                    content = pemObject.Content;

                }
            }
            catch (Exception)
            {
                // best attempt
            }
            return content;
        }
        public static List<string> PEMCertificateListToPEMs(string pemList)
        {
            List<string> pems=new List<string>();
            if (string.IsNullOrEmpty(pemList))
                throw new CryptoException("private key cannot be null");
            AsymmetricKeyParameter privkey = null;
            X509Certificate cert = null;
            using (StringReader ms = new StringReader(pemList))
            {
                PemReader pemReader = new PemReader(ms);
                object o;
                while ((o = pemReader.ReadObject()) != null)
                {
                    if (o is AsymmetricKeyParameter)
                    {
                        AsymmetricKeyParameter par = (AsymmetricKeyParameter)o;
                        if (par.IsPrivate)
                            privkey = par;
                    }

                    if (o is AsymmetricCipherKeyPair)
                    {
                        privkey = ((AsymmetricCipherKeyPair)o).Private;
                    }

                    if (o is X509Certificate)
                    {
                        if (cert != null)
                        {
                            pems.Add(DumpOnePEM(cert, privkey));
                            privkey = null;
                        }
                        X509Certificate cc = (X509Certificate)o;
                        cert = cc;
                    }
                }
                if (cert!=null)
                    pems.Add(DumpOnePEM(cert, privkey));
            }
            return pems;
        }
        public static string X509Certificate2ToPEM(X509Certificate2 cert)
        {
            try
            {
                if (cert.HasPrivateKey)
                {
                  
                    byte[] pkcsarray = cert.Export(X509ContentType.Pkcs12);
                    if (pkcsarray == null || pkcsarray.Length == 0)
                        throw new CryptoException("Empty PKCS12 Array");
                    X509Certificate certout = null;
                    AsymmetricKeyParameter priv = null;
                    using (MemoryStream ms = new MemoryStream(pkcsarray))
                    {
                        Pkcs12Store pkstore = new Pkcs12Store();
                        pkstore.Load(ms, new char[]{});
                        foreach (string s in pkstore.Aliases.Cast<string>())
                        {
                            X509CertificateEntry entry = pkstore.GetCertificate(s);
                            if (entry != null)
                                certout = entry.Certificate;
                            AsymmetricKeyEntry kentry = pkstore.GetKey(s);
                            if (kentry != null)
                                priv = kentry.Key;
                        }

                        if (certout == null)
                            throw new CryptoException("Certificate not found");
                    }

                    using (StringWriter sw = new StringWriter())
                    {
                        PemWriter pemWriter = new PemWriter(sw);
                        pemWriter.WriteObject(certout);
                        if (priv != null)
                            pemWriter.WriteObject(priv);
                        sw.Flush();
                        return sw.ToString();
                    }
                }

                X509Certificate c = DotNetUtilities.FromX509Certificate(cert);
                return DumpOnePEM(c, null);
                {
                    
                }
               // return cert.Export(X509ContentType.SerializedCert).ToUTF8String();               
            }
            catch (Exception e)
            {
                throw new CryptoException($"Unable to open pkcs12, wrong password?. {e.Message}", e);
            }
        }


        public static X509Certificate PEMToX509Certificate(string pemCertificate)
        {
            (AsymmetricKeyParameter _, AsymmetricKeyParameter _, X509Certificate certificate) = KeyPair.PEMToAsymmetricCipherKeyPairAndCert(pemCertificate);
            if (certificate == null)
                throw new CryptoException("Invalid Certificate");
            return certificate;
        }
        /*
        public static X509Certificate2 X509CertificateToX509Certificate2(X509Certificate certificate)
        {
            if (certificate == null)
                throw new CryptoException("Invalid Certificate");
            Pkcs12Store store = new Pkcs12StoreBuilder().Build();
            using (MemoryStream ms = new MemoryStream())
            {
                store.Save(ms, null, new SecureRandom());
                ms.Flush();
                ms.Position = 0;
                return new X509Certificate2(ms.ToArray(), (string)null, X509KeyStorageFlags.Exportable);
            }
        }

        public static X509Certificate X509Certificate2ToX509Certificate(X509Certificate2 certificate2)
        {
            return DotNetUtilities.FromX509Certificate(certificate2);
        }
        */
    }
}