using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509.Extension;
using CryptoException = Hyperledger.Fabric.SDK.Exceptions.CryptoException;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using X509Extension = Org.BouncyCastle.Asn1.X509.X509Extension;

namespace Hyperledger.Fabric.SDK.Security
{
    public class KeyPair
    {
        private bool keypairgenerated;

        private string pem;
        private AsymmetricKeyParameter privateKey;
        private AsymmetricKeyParameter publicKey;

        private KeyPair()
        {
        }
        public static byte[] PemToDer(string pem)
        {
            PemReader pemReader = new PemReader(new StringReader(pem));
            return pemReader.ReadPemObject().Content;
        }

        public AsymmetricKeyParameter PublicKey
        {
            get
            {
                if (!keypairgenerated && !string.IsNullOrEmpty(pem))
                    PopulateFromPEM();
                return publicKey;
            }
            set
            {
                publicKey = value;
                pem = null;
            }
        }

        public AsymmetricKeyParameter PrivateKey
        {
            get
            {
                if (!keypairgenerated && !string.IsNullOrEmpty(pem))
                    PopulateFromPEM();
                return privateKey;
            }
            set
            {
                privateKey = value;
                pem = null;
            }
        }

        public string Pem
        {
            get
            {
                if (string.IsNullOrEmpty(pem) && (publicKey != null || privateKey != null))
                    pem = AsymmetricCipherKeyPairToPEM(publicKey, privateKey);
                return pem;
            }
            set
            {
                pem = value;
                publicKey = privateKey = null;
                keypairgenerated = false;
            }
        }

        private void PopulateFromPEM()
        {
            (AsymmetricKeyParameter pubKey, AsymmetricKeyParameter privKey, X509Certificate _) = PEMToAsymmetricCipherKeyPairAndCert(pem);
            publicKey = pubKey;
            privateKey = privKey;
            keypairgenerated = true;
        }

        public static KeyPair GenerateECDSA(string curveName)
        {
            return GenerateECDSAKey("EC", curveName);
        }

        public string GenerateCertificationRequest(string subject, string signaturealgorithm)
        {
            try
            {
                if (PrivateKey == null)
                    throw new CryptoException("Unable to generate csr, private key not found");
                if (PublicKey == null)
                    throw new CryptoException("Unable to generate csr, public key not found");
                var extensions = new Dictionary<DerObjectIdentifier, X509Extension>();
                extensions.Add(X509Extensions.SubjectKeyIdentifier, new X509Extension(false, new DerOctetString(new SubjectKeyIdentifierStructure(PublicKey))));
                DerSet exts = new DerSet(new AttributePkcs(PkcsObjectIdentifiers.Pkcs9AtExtensionRequest, new DerSet(new X509Extensions(extensions))));
                var attributes = new Dictionary<DerObjectIdentifier, string> {{X509Name.CN, subject}};
                ISignatureFactory sf = new Asn1SignatureFactory(signaturealgorithm, PrivateKey, new SecureRandom());
                Pkcs10CertificationRequest csr = new Pkcs10CertificationRequest(sf, new X509Name(attributes.Keys.ToList(), attributes), publicKey, exts);
                using (StringWriter str = new StringWriter())
                {
                    PemWriter pemWriter = new PemWriter(str);
                    pemWriter.WriteObject(csr);
                    str.Flush();
                    return str.ToString();
                }
            }
            catch (Exception e)
            {
                throw new CryptoException($"Unable to generate csr. {e.Message}", e);
            }
        }

        private static KeyPair GenerateECDSAKey(string encryptionName, string curveName)
        {
            try
            {
                DerObjectIdentifier doi = SecNamedCurves.GetOid(curveName);
                ECKeyPairGenerator g = new ECKeyPairGenerator(encryptionName);
                g.Init(new ECKeyGenerationParameters(doi, new SecureRandom()));
                return Create(g.GenerateKeyPair());
            }
            catch (Exception exp)
            {
                throw new CryptoException("Unable to generate key pair", exp);
            }
        }

        public bool Verify(byte[] data, byte[] signature, string signatureAlgorithm)
        {
            if (PublicKey == null)
                throw new ArgumentException("Unable to verify signature, public key not found");
            if (data == null || data.Length == 0)
                throw new ArgumentException("Unable to verify empty data");
            if (signature == null || signature.Length == 0)
                throw new ArgumentException("Unable to verify with an empty signature");
            ISigner signer = SignerUtilities.GetSigner(signatureAlgorithm);
            signer.Init(false, PublicKey);
            signer.BlockUpdate(data, 0, data.Length);
            return signer.VerifySignature(signature);
        }

        public byte[] Sign(byte[] data, string signatureAlgorithm)
        {
            if (PrivateKey == null)
                throw new ArgumentException("Unable to sign data, private key not found");
            if (data == null || data.Length == 0)
                throw new ArgumentException("Unable to sign empty data");
            ISigner signer = SignerUtilities.GetSigner(signatureAlgorithm);
            signer.Init(true, PrivateKey);
            signer.BlockUpdate(data, 0, data.Length);
            byte[] signature = signer.GenerateSignature();
            if (PrivateKey is ECPrivateKeyParameters)
            {
                ECPrivateKeyParameters privkey = (ECPrivateKeyParameters) PrivateKey;
                BigInteger N = privkey.Parameters.N;
                BigInteger[] sigs = DecodeECDSASignature(signature);
                sigs = PreventMalleability(sigs, N);
                using (MemoryStream ms = new MemoryStream())
                {
                    DerSequenceGenerator seq = new DerSequenceGenerator(ms);
                    seq.AddObject(new DerInteger(sigs[0]));
                    seq.AddObject(new DerInteger(sigs[1]));
                    seq.Close();
                    ms.Flush();
                    signature = ms.ToArray();
                }
            }

            return signature;
        }

        private BigInteger[] PreventMalleability(BigInteger[] sigs, BigInteger curveN)
        {
            BigInteger cmpVal = curveN.Divide(BigInteger.Two);
            BigInteger sval = sigs[1];
            if (sval.CompareTo(cmpVal) == 1)
                sigs[1] = curveN.Subtract(sval);
            return sigs;
        }

        private static BigInteger[] DecodeECDSASignature(byte[] signature)
        {
            Asn1InputStream asnInputStream = new Asn1InputStream(signature);
            Asn1Object asn1 = asnInputStream.ReadObject();
            BigInteger[] sigs = new BigInteger[2];
            int count = 0;
            if (asn1 is Asn1Sequence)
            {
                Asn1Sequence asn1Sequence = (Asn1Sequence) asn1;
                foreach (Asn1Encodable asn1Encodable in asn1Sequence)
                {
                    Asn1Object asn1Primitive = asn1Encodable.ToAsn1Object();
                    if (asn1Primitive is DerInteger)
                    {
                        DerInteger asn1Integer = (DerInteger) asn1Primitive;
                        BigInteger integer = asn1Integer.Value;
                        if (count < 2)
                            sigs[count] = integer;
                        count++;
                    }
                }
            }

            if (count != 2)
                throw new CryptoException($"Invalid ECDSA signature. Expected count of 2 but got: {count}. Signature is: {BitConverter.ToString(signature).Replace("-", string.Empty)}");
            return sigs;
        }


        public static KeyPair Create(AsymmetricKeyParameter pubKey, AsymmetricKeyParameter privKey)
        {
            KeyPair kp = new KeyPair();
            kp.privateKey = privKey;
            kp.publicKey = pubKey;
            kp.keypairgenerated = true;
            kp.pem = AsymmetricCipherKeyPairToPEM(pubKey, privKey);
            return kp;
        }

        public static KeyPair Create(AsymmetricCipherKeyPair keyPair)
        {
            KeyPair kp = new KeyPair();
            kp.privateKey = keyPair.Private;
            kp.publicKey = keyPair.Public;
            kp.keypairgenerated = true;
            kp.pem = AsymmetricCipherKeyPairToPEM(keyPair.Public, keyPair.Private);
            return kp;
        }

        public static KeyPair Create(string pem)
        {
            if (string.IsNullOrEmpty(pem))
                throw new ArgumentException("Empty PEM Key provided");
            KeyPair kp = new KeyPair();
            kp.pem = pem;
            (AsymmetricKeyParameter pubKey, AsymmetricKeyParameter privKey, X509Certificate _) = PEMToAsymmetricCipherKeyPairAndCert(pem);
            kp.privateKey = privKey;
            kp.publicKey = pubKey;
            kp.keypairgenerated = true;
            return kp;
        }

        public static KeyPair Create(X509Certificate2 cert)
        {
            if (cert == null)
                throw new ArgumentException("Empty Cerificatte provided");

            KeyPair kp = new KeyPair();
            (AsymmetricKeyParameter pubKey, AsymmetricKeyParameter privKey) = X509Certificate2ToAsymmetricCipherKeyPair(cert);
            kp.privateKey = privKey;
            kp.publicKey = pubKey;
            kp.keypairgenerated = true;
            return kp;
        }

        public static KeyPair Create(byte[] pkcs12, string password)
        {
            if (pkcs12 == null || pkcs12.Length == 0)
                throw new ArgumentException("Empty PKCS12 Cerificate provided");

            KeyPair kp = new KeyPair();
            (AsymmetricKeyParameter pubKey, AsymmetricKeyParameter privKey) = Pkcs12ArrayToAsymmetricCipherKeyPair(pkcs12, password);
            kp.privateKey = privKey;
            kp.publicKey = pubKey;
            kp.keypairgenerated = true;
            return kp;
        }

        public static KeyPair Create(string pkcs12filename, string password)
        {
            KeyPair kp = new KeyPair();
            (AsymmetricKeyParameter pubKey, AsymmetricKeyParameter privKey) = Pkcs12FileToAsymmetricCipherKeyPair(pkcs12filename, password);
            kp.privateKey = privKey;
            kp.publicKey = pubKey;
            kp.keypairgenerated = true;
            return kp;
        }

        public static (AsymmetricKeyParameter PubKey, AsymmetricKeyParameter PrivKey) PKCS12ToAsymmetricCipherKeyPair(Pkcs12Store pkstore)
        {
            AsymmetricKeyParameter pub = null;
            AsymmetricKeyParameter priv = null;
            foreach (string s in pkstore.Aliases.Cast<string>())
            {
                X509CertificateEntry entry = pkstore.GetCertificate(s);
                if (entry != null)
                    pub = entry.Certificate.GetPublicKey();
                AsymmetricKeyEntry kentry = pkstore.GetKey(s);
                if (kentry != null)
                    priv = kentry.Key;
            }

            if (pub == null)
                throw new CryptoException("Certificate not found");
            return (pub, priv);
        }

        public static (AsymmetricKeyParameter PubKey, AsymmetricKeyParameter PrivKey) X509Certificate2ToAsymmetricCipherKeyPair(X509Certificate2 cert)
        {
            byte[] certidata = cert.Export(X509ContentType.Pkcs12);
            return Pkcs12ArrayToAsymmetricCipherKeyPair(certidata, null);
        }

        public static (AsymmetricKeyParameter PubKey, AsymmetricKeyParameter PrivKey) Pkcs12FileToAsymmetricCipherKeyPair(string filename, string password)
        {
            if (string.IsNullOrEmpty(filename))
                throw new CryptoException("Pkcs12 filename is empty");
            if (!File.Exists(filename))
                throw new CryptoException($"Unable to open pkcs12 file {filename}");
            try
            {
                byte[] array = File.ReadAllBytes(filename);
                return Pkcs12ArrayToAsymmetricCipherKeyPair(array, password);
            }
            catch (Exception e)
            {
                throw new CryptoException($"Unable to open pkcs12 file {filename}, IO ERROR. {e.Message}");
            }
        }

        public static (AsymmetricKeyParameter PubKey, AsymmetricKeyParameter PrivKey) Pkcs12ArrayToAsymmetricCipherKeyPair(byte[] pkcsarray, string password)
        {
            if (pkcsarray == null || pkcsarray.Length == 0)
                throw new CryptoException("Empty PKCS12 Array");
            try
            {
                using (MemoryStream ms = new MemoryStream(pkcsarray))
                {
                    Pkcs12Store pkstore = new Pkcs12Store(ms, password.ToCharArray());
                    return PKCS12ToAsymmetricCipherKeyPair(pkstore);
                }
            }
            catch (Exception e)
            {
                throw new CryptoException($"Unable to open pkcs12, wrong password?. {e.Message}", e);
            }
        }

        public static AsymmetricKeyParameter DerivePublicKey(AsymmetricKeyParameter privkey)
        {
            switch (privkey)
            {
                case ECPrivateKeyParameters ec:
                    return new ECPublicKeyParameters(ec.Parameters.G.Multiply(ec.D), ec.Parameters);
                case RsaPrivateCrtKeyParameters rsa:
                    return new RsaKeyParameters(false, rsa.Modulus, rsa.PublicExponent);
                case DsaPrivateKeyParameters dsa:
                    return new DsaPublicKeyParameters(dsa.Parameters.G.ModPow(dsa.X, dsa.Parameters.P), dsa.Parameters);
                case DHPrivateKeyParameters dh:
                    return new DHPublicKeyParameters(dh.Parameters.G.ModPow(dh.X, dh.Parameters.P), dh.Parameters);
            }

            return null;
        }

        public static (AsymmetricKeyParameter PubKey, AsymmetricKeyParameter PrivKey, X509Certificate Certificate) PEMToAsymmetricCipherKeyPairAndCert(string pemKey)
        {
            if (string.IsNullOrEmpty(pemKey))
                throw new CryptoException("private key cannot be null");
            AsymmetricKeyParameter privkey = null;
            AsymmetricKeyParameter pubkey = null;
            X509Certificate cert = null;
            using (StringReader ms = new StringReader(pemKey))
            {
                PemReader pemReader = new PemReader(ms);
                object o;
                while ((o = pemReader.ReadObject()) != null)
                {
                    if (o is AsymmetricKeyParameter)
                    {
                        AsymmetricKeyParameter par = (AsymmetricKeyParameter) o;
                        if (par.IsPrivate)
                            privkey = par;
                        else
                            pubkey = par;
                    }

                    if (o is AsymmetricCipherKeyPair)
                    {
                        privkey = ((AsymmetricCipherKeyPair) o).Private;
                        pubkey = ((AsymmetricCipherKeyPair) o).Public;
                    }

                    if (o is X509Certificate)
                    {
                        X509Certificate cc = (X509Certificate) o;
                        pubkey = cc.GetPublicKey();
                        cert = cc;
                    }
                }
            }

            if (pubkey != null || privkey != null)
            {
                if (privkey != null && pubkey == null)
                    pubkey = DerivePublicKey(privkey);
            }

            if (pubkey == null && privkey == null && cert == null)
                throw new CryptoException("Invalid Certificate");
            return (pubkey, privkey, cert);
        }

        public static string AsymmetricCipherKeyPairToPEM(AsymmetricKeyParameter pubKey, AsymmetricKeyParameter privKey)
        {
            using (StringWriter sw = new StringWriter())
            {
                PemWriter pemWriter = new PemWriter(sw);
                if (pubKey != null)
                    pemWriter.WriteObject(pubKey);
                if (privKey != null)
                    pemWriter.WriteObject(privKey);
                sw.Flush();
                return sw.ToString();
            }
        }
    }
}