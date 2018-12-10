using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO.Pem;
using Org.BouncyCastle.X509;
using CryptoException = Hyperledger.Fabric.SDK.Exceptions.CryptoException;
using PemReader = Org.BouncyCastle.OpenSsl.PemReader;
using PemWriter = Org.BouncyCastle.OpenSsl.PemWriter;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Hyperledger.Fabric.SDK.Security
{
    public class TLSCertificateKeyPair
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(TLSCertificateKeyPair));

        public TLSCertificateKeyPair(byte[] certPemBytes, byte[] certDerBytes, byte[] keyPemBytes)
        {
            CertPEMBytes = certPemBytes;
            CertDERBytes = certDerBytes;
            KeyPEMBytes = keyPemBytes;
        }

        public TLSCertificateKeyPair(Properties properties)
        {
            // check for mutual TLS - both clientKey and clientCert must be present
            if (properties.Contains("clientKeyFile") && properties.Contains("clientKeyBytes"))
                throw new ArgumentException("Properties \"clientKeyFile\" and \"clientKeyBytes\" must cannot both be set");
            if (properties.Contains("clientCertFile") && properties.Contains("clientCertBytes"))
                throw new ArgumentException("Properties \"clientCertFile\" and \"clientCertBytes\" must cannot both be set");
            if (properties.Contains("clientKeyFile") || properties.Contains("clientCertFile"))
            {
                if (properties.Contains("clientKeyFile") && properties.Contains("clientCertFile"))
                {
                    try
                    {
                        logger.Trace($"Reading clientKeyFile: {properties["clientKeyFile"]}");
                        KeyPEMBytes = File.ReadAllBytes(Path.GetFullPath(properties["clientKeyFile"]));
                        logger.Trace($"Reading clientCertFile: {properties["clientCertFile"]}");
                        CertPEMBytes = File.ReadAllBytes(Path.GetFullPath(properties["clientCertFile"]));
                        //Check if both right
                        GetCertificate();
                        GetPrivateKey();
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException("Failed to parse TLS client key and/or cert", e);
                    }
                }
                else
                {
                    throw new ArgumentException("Properties \"clientKeyFile\" and \"clientCertFile\" must both be set or both be null");
                }
            }
            else if (properties.Contains("clientKeyThumbprint"))
            {
                X509Certificate2 certi = SearchCertificateByFingerprint(properties["clientKeyThumbprint"]);
                if (certi == null)
                    throw new ArgumentException($"Thumbprint {properties["clientKeyThumbprint"]} not found in KeyStore");
                CertPEMBytes = ExportToPEMCert(certi);
                KeyPEMBytes = ExportToPEMKey(certi);
                //Check if both right
                GetCertificate();
                GetPrivateKey();
            }
            else if (properties.Contains("clientKeySubject"))
            {
                X509Certificate2 certi = SearchCertificateBySubject(properties["clientKeySubject"]);
                if (certi == null)
                    throw new ArgumentException($"Subject {properties["clientKeySubject"]} not found in KeyStore");
                CertPEMBytes = ExportToPEMCert(certi);
                KeyPEMBytes = ExportToPEMKey(certi);
                //Check if both right
                GetCertificate();
                GetPrivateKey();
            }
            else if (properties.Contains("clientKeyBytes") || properties.Contains("clientCertBytes"))
            {
                KeyPEMBytes = properties["clientKeyBytes"]?.ToBytes();
                CertPEMBytes = properties["clientCertBytes"]?.ToBytes();
                if (KeyPEMBytes == null || CertPEMBytes == null)
                {
                    throw new ArgumentException("Properties \"clientKeyBytes\" and \"clientCertBytes\" must both be set or both be null");
                }
                //Check if both right
                GetCertificate();
                GetPrivateKey();
            }

            if (CertPEMBytes != null)
            {
                using (MemoryStream isr = new MemoryStream(CertPEMBytes))
                {
                    StreamReader reader = new StreamReader(isr);
                    PemReader pr = new PemReader(reader);
                    PemObject po = pr.ReadPemObject();
                    CertDERBytes = po.Content;
                }
            }
        }

        /***
         * Creates a TLSCertificateKeyPair out of the given {@link X509Certificate} and {@link KeyPair}
         * encoded in PEM and also in DER for the certificate
         * @param x509Cert the certificate to process
         * @param keyPair  the key pair to process
         * @return a TLSCertificateKeyPair
         * @throws IOException upon failure
         */
        public TLSCertificateKeyPair(Certificate x509Cert, KeyPair keyPair)
        {
            using (MemoryStream baos = new MemoryStream())
            {
                StreamWriter writer = new StreamWriter(baos);
                PemWriter w = new PemWriter(writer);
                w.WriteObject(x509Cert.X509Certificate);
                writer.Flush();
                writer.Close();
                CertPEMBytes = baos.ToArray();
            }
            using (MemoryStream isr = new MemoryStream(CertPEMBytes))
            {
                StreamReader reader = new StreamReader(isr);
                PemReader pr = new PemReader(reader);
                PemObject po = pr.ReadPemObject();
                CertDERBytes = po.Content;
            }
            using (MemoryStream baos = new MemoryStream())
            {
                StreamWriter writer = new StreamWriter(baos);
                PemWriter w = new PemWriter(writer);
                w.WriteObject(keyPair.PrivateKey);
                writer.Flush();
                writer.Close();
                KeyPEMBytes = baos.ToArray();
            }
        }

        /***
         * @return the certificate, in PEM encoding
         */
        public byte[] CertPEMBytes { get; }

        /***
         * @return the certificate, in DER encoding
         */
        public byte[] CertDERBytes { get; }

        /***
         * @return the key, in PEM encoding
         */
        public byte[] KeyPEMBytes { get; }


        public byte[] GetDigest()
        {
            //The digest must be SHA256 over the DER encoded certificate. The PEM has the exact DER sequence in hex encoding around the begin and end markers

            IDigest digest = new Sha256Digest();
            byte[] clientTLSCertificateDigest = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(CertDERBytes, 0, CertDERBytes.Length);
            digest.DoFinal(clientTLSCertificateDigest, 0);
            return clientTLSCertificateDigest;
        }

        private static X509Certificate2 SearchCertificate(string value, Func<string, X509Certificate2, bool> check_func)
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

        private static X509Certificate2 SearchCertificateBySubject(string subject)
        {
            return SearchCertificate(subject, (certname, m) => m.Subject.IndexOf("CN=" + certname, 0, StringComparison.InvariantCultureIgnoreCase) >= 0 || m.Issuer.IndexOf("CN=" + certname, 0, StringComparison.InvariantCultureIgnoreCase) >= 0);
        }

        private static X509Certificate2 SearchCertificateByFingerprint(string finger)
        {
            return SearchCertificate(finger, (certname, m) => m.Thumbprint?.Equals(certname, StringComparison.InvariantCultureIgnoreCase) ?? false);
        }


        private static byte[] ExportToPEMCert(X509Certificate2 cert)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");
            return builder.ToString().ToBytes();
        }

        private static byte[] ExportToPEMKey(X509Certificate2 cert)
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

        public AsymmetricKeyParameter GetPrivateKey()
        {
            try
            {
                logger.Trace($"client TLS private key bytes size: {KeyPEMBytes.Length}");
                logger.Trace($"client TLS key bytes: {KeyPEMBytes.ToHexString()}");
                (AsymmetricKeyParameter _, AsymmetricKeyParameter privateKey, X509Certificate _) = KeyPair.PEMToAsymmetricCipherKeyPairAndCert(KeyPEMBytes.ToUTF8String());
                logger.Trace("converted TLS key.");
                return privateKey;
            }
            catch (CryptoException e)
            {
                logger.Error($"Failed to parse private key TLS client {KeyPEMBytes.ToUTF8String()}");
                throw new ArgumentException($"Failed to parse TLS client private key", e);
            }
        }

        public X509Certificate GetCertificate()
        {
            try
            {
                logger.Trace("client TLS certificate bytes:" + CertPEMBytes.ToHexString());
                X509Certificate clientCert = Certificate.PEMToX509Certificate(CertPEMBytes.ToUTF8String());
                logger.Trace("converted client TLS certificate.");
                return clientCert;
            }
            catch (CryptoException e)
            {
                logger.Error($"Failed to parse certificate TLS client {CertPEMBytes.ToUTF8String()}");
                throw new ArgumentException($"Failed to parse TLS client certificate", e);
            }
        }


        /***
         * Creates a TLS client certificate key pair
         * @return a TLSCertificateKeyPair
         */
        public static TLSCertificateKeyPair CreateClientCert(string commonName = null, string signatureAlgorithm = "SHA256withECDSA", string keyType = "EC")
        {
            return CreateCert(CertType.CLIENT, null, commonName, signatureAlgorithm, keyType);
        }

        /***
         * Creates a TLS server certificate key pair with the given DNS subject alternative name
         * @param subjectAlternativeName the DNS SAN to be encoded in the certificate
         * @return a TLSCertificateKeyPair
         */
        public static TLSCertificateKeyPair CreateServerCert(string subjectAlternativeName, string commonName = null, string signatureAlgorithm = "SHA256withECDSA", string keyType = "EC")
        {
            return CreateCert(CertType.SERVER, subjectAlternativeName, commonName, signatureAlgorithm, keyType);
        }

        private static TLSCertificateKeyPair CreateCert(CertType certType, string subjectAlternativeName, string commonName, string signatureAlgorithm, string keyType)
        {
            if (commonName == null)
                commonName = Guid.NewGuid().ToString();

            KeyPair keyPair = CreateKeyPair(keyType);
            Certificate cert = CreateSelfSignedCertificate(certType, keyPair, commonName, signatureAlgorithm, subjectAlternativeName);
            return new TLSCertificateKeyPair(cert, keyPair);
        }

        // ReSharper disable once UnusedParameter.Local
        private static KeyPair CreateKeyPair(string _)
        {
            //TODO
            //Currently only EC supported
            var gen = new ECKeyPairGenerator();
            var keyGenParam = new KeyGenerationParameters(new SecureRandom(), 256);
            gen.Init(keyGenParam);
            return KeyPair.Create(gen.GenerateKeyPair());
        }

        private static void AddSAN(X509V3CertificateGenerator certBuilder, string san)
        {
            Asn1Encodable[] subjectAlternativeNames = new Asn1Encodable[] {new GeneralName(GeneralName.DnsName, san)};
            certBuilder.AddExtension(X509Extensions.SubjectAlternativeName, false, new DerSequence(subjectAlternativeNames));
        }

        private static X509V3CertificateGenerator CreateCertBuilder(KeyPair keyPair, string commonName)
        {
            X509V3CertificateGenerator gen = new X509V3CertificateGenerator();
            gen.SetSubjectDN(new X509Name("CN="+commonName));
            gen.SetIssuerDN(new X509Name("CN=" + commonName));
            DateTime now = DateTime.UtcNow;
            gen.SetNotBefore(now.AddDays(-1));
            gen.SetNotAfter(now.AddYears(10));
            BigInteger bg = new BigInteger(160, new SecureRandom());
            gen.SetSerialNumber(bg);
            gen.SetPublicKey(keyPair.PublicKey);

            /*
            var authorityKeyIdentifier =  new AuthorityKeyIdentifier(
                SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.PublicKey),
                    new GeneralNames(new GeneralName(new X509Name("CN=" + commonName))),
                    bg);
            gen.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, authorityKeyIdentifier);

            var subjectKeyIdentifier =
                new SubjectKeyIdentifier(
                    SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keyPair.PublicKey));
            gen.AddExtension(
                X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifier);*/
            return gen;

        }

        private static ExtendedKeyUsage KeyUsage(CertType type) => new ExtendedKeyUsage(type == CertType.SERVER ? KeyPurposeID.IdKPServerAuth : KeyPurposeID.IdKPClientAuth);

        private static Certificate CreateSelfSignedCertificate(CertType certType, KeyPair keyPair, string commonName, string signatureAlgorithm, string san)
        {
            X509V3CertificateGenerator certBuilder = CreateCertBuilder(keyPair, commonName);
            // Basic constraints
            BasicConstraints constraints = new BasicConstraints(true);
            certBuilder.AddExtension(X509Extensions.BasicConstraints, true, constraints);
            // Key usage
            KeyUsage usage = new KeyUsage(Org.BouncyCastle.Asn1.X509.KeyUsage.KeyEncipherment | Org.BouncyCastle.Asn1.X509.KeyUsage.DigitalSignature | Org.BouncyCastle.Asn1.X509.KeyUsage.KeyCertSign);
            certBuilder.AddExtension(X509Extensions.KeyUsage, false, usage);
            // Extended key usage
            var usages = new[] { KeyPurposeID.IdKPServerAuth, KeyPurposeID.IdKPClientAuth };
            certBuilder.AddExtension(X509Extensions.ExtendedKeyUsage, false, KeyUsage(certType));
            if (san != null)
                AddSAN(certBuilder, san);
            ISignatureFactory signatureFactory = new Asn1SignatureFactory(signatureAlgorithm, keyPair.PrivateKey, new SecureRandom());
            return Certificate.Create(certBuilder.Generate(signatureFactory), keyPair.PrivateKey);
        }

        /*
        private class SelfSignedKeyIdentifier
        {

            private readonly byte[] bytes;
            public SelfSignedKeyIdentifier()
            {
                bytes = new byte[20];
                new SecureRandom().NextBytes(bytes);
            }

            public byte[] AuthorityKeyIdentifier => bytes;
            public byte[] SubjectKeyIdentifier => bytes;
        }
        */
        private enum CertType
        {
            CLIENT,
            SERVER
        }
    }
}