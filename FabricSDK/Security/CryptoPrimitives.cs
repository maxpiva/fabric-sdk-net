using System;
using System.Collections.Generic;
using System.Text;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using CryptoException = Hyperledger.Fabric.SDK.Exceptions.CryptoException;

namespace Hyperledger.Fabric.SDK.Security
{
    public class CryptoPrimitives : ICryptoSuite
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(CryptoPrimitives));
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL ? Config.Instance.GetDiagnosticFileDumper() : null;


        private readonly bool inited = false;

        private KeyStore _store = new KeyStore();
        private string CERTIFICATE_FORMAT = Config.Instance.GetCertificateFormat();

        internal string curveName;
        private string DEFAULT_SIGNATURE_ALGORITHM = Config.Instance.GetSignatureAlgorithm();
        internal string hashAlgorithm = Config.Instance.GetHashAlgorithm();

        private Dictionary<int, string> securityCurveMapping = Config.Instance.GetSecurityCurveMapping();
        internal int securityLevel = Config.Instance.GetSecurityLevel();

        public KeyStore Store
        {
            get => _store;
            set => _store = value ?? throw new ArgumentException("Cannot set empty store");
        }

        public bool Verify(byte[] certificate, string signatureAlgorithm, byte[] signature, byte[] plainText)
        {
            if (plainText == null || signature == null || certificate == null)
                return false;
            if (Config.Instance.ExtraLogLevel(10))
            {
                if (null != diagnosticFileDumper)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("plaintext in hex: " + BitConverter.ToString(plainText).Replace("-", string.Empty));
                    sb.AppendLine("signature in hex: " + BitConverter.ToString(signature).Replace("-", string.Empty));
                    sb.Append("PEM cert in hex: " + BitConverter.ToString(certificate).Replace("-", string.Empty));
                    logger.Trace("verify :  " + diagnosticFileDumper.CreateDiagnosticFile(sb.ToString()));
                }
            }

            try
            {
                string plaincert = certificate.ToUTF8String();
                X509Certificate cert = Certificate.PEMToX509Certificate(plaincert);
                string key = cert.SubjectDN + "_" + cert.SerialNumber;
                KeyPair pair = Store.GetOrAddKey(key, () => KeyPair.Create(plaincert));
                return pair.Verify(plainText, signature, signatureAlgorithm);
            }
            catch (CryptoException)
            {
                throw;
            }
            catch (Exception e)
            {
                CryptoException ex = new CryptoException("Cannot verify signature. " + e.Message + "\r\nCertificate: " + BitConverter.ToString(certificate).Replace("-", string.Empty), e);
                logger.ErrorException(ex.Message, ex);
                throw ex;
            }
        }

        public byte[] Hash(byte[] input)
        {
            string hashalgo = hashAlgorithm;
            if (hashalgo.ToUpperInvariant() == "SHA2")
                hashalgo = "SHA256";
            else if (hashalgo.ToUpperInvariant() == "SHA3")
                hashalgo = "SHA3-256";
            IDigest digest = DigestUtilities.GetDigest(hashalgo);
            byte[] retValue = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(input, 0, input.Length);
            digest.DoFinal(retValue, 0);
            return retValue;
        }
        /* (non-Javadoc)
         * @see org.hyperledger.fabric.sdk.security.CryptoSuite#getProperties()
         */

        public Properties GetProperties()
        {
            Properties properties = new Properties();
            properties.Set(Config.HASH_ALGORITHM, hashAlgorithm);
            properties.Set(Config.SECURITY_LEVEL, securityLevel.ToString());
            properties.Set(Config.CERTIFICATE_FORMAT, CERTIFICATE_FORMAT);
            properties.Set(Config.SIGNATURE_ALGORITHM, DEFAULT_SIGNATURE_ALGORITHM);
            return properties;
        }

        public ICryptoSuiteFactory GetCryptoSuiteFactory()
        {
            return Factory.Instance; //Factory for this crypto suite.
        }

        public KeyPair KeyGen()
        {
            return KeyPair.GenerateECDSA(curveName);
        }

        public byte[] Sign(KeyPair key, byte[] plainText)
        {
            if (key == null)
                throw new ArgumentException("Null Keypair");
            if (plainText == null)
                throw new ArgumentException("Data that to be signed is null.");
            if (plainText.Length == 0)
                throw new ArgumentException("Data to be signed was empty.");
            return key.Sign(plainText, DEFAULT_SIGNATURE_ALGORITHM);
        }

        public string GenerateCertificationRequest(string user, KeyPair keypair)
        {
            return keypair.GenerateCertificationRequest(user, DEFAULT_SIGNATURE_ALGORITHM);
        }

        public void SetProperties(Properties properties)
        {
            if (properties == null || properties.Count == 0)
                throw new ArgumentException("properties must not be null");
            hashAlgorithm = properties.Contains(Config.HASH_ALGORITHM) ? properties[Config.HASH_ALGORITHM] : hashAlgorithm;
            string secLevel = properties.Contains(Config.SECURITY_LEVEL) ? properties[Config.SECURITY_LEVEL] : securityLevel.ToString();
            securityLevel = int.Parse(secLevel);
            if (properties.Contains(Config.SECURITY_CURVE_MAPPING))
                securityCurveMapping = Config.ParseSecurityCurveMappings(properties[Config.SECURITY_CURVE_MAPPING]);
            else
                securityCurveMapping = Config.Instance.GetSecurityCurveMapping();
            CERTIFICATE_FORMAT = properties.Contains(Config.CERTIFICATE_FORMAT) ? properties[Config.CERTIFICATE_FORMAT] : CERTIFICATE_FORMAT;
            DEFAULT_SIGNATURE_ALGORITHM = properties.Contains(Config.SIGNATURE_ALGORITHM) ? properties[Config.SIGNATURE_ALGORITHM] : DEFAULT_SIGNATURE_ALGORITHM;
            ResetConfiguration();
        }

        private void ResetConfiguration()
        {
            SetSecurityLevel(securityLevel);
            SetHashAlgorithm(hashAlgorithm);
        }

        /**
        * Security Level determines the elliptic curve used in key generation
        *
        * @param securityLevel currently 256 or 384
        * @throws InvalidArgumentException
        */
        public void SetSecurityLevel(int secLevel)
        {
            logger.Trace($"setSecurityLevel to {secLevel}", secLevel);

            if (securityCurveMapping.Count == 0)
            {
                throw new ArgumentException("Security curve mapping has no entries.");
            }

            if (!securityCurveMapping.ContainsKey(secLevel))
            {
                StringBuilder sb = new StringBuilder();
                string sp = "";
                foreach (int x in securityCurveMapping.Keys)
                {
                    sb.Append(sp).Append(x);
                    sp = ", ";
                }

                throw new ArgumentException($"Illegal security level: {secLevel}. Valid values are: {sb}");
            }

            string lcurveName = securityCurveMapping[secLevel];

            logger.Debug($"Mapped curve strength {secLevel} to {lcurveName}");
            X9ECParameters pars = ECNamedCurveTable.GetByName(lcurveName);
            //Check if can match curve name to requested strength.
            if (pars == null)
            {
                ArgumentException argumentException = new ArgumentException($"Curve {curveName} defined for security strength {secLevel} was not found.");

                logger.ErrorException(argumentException.Message, argumentException);
                throw argumentException;
            }

            curveName = lcurveName;
            securityLevel = secLevel;
        }

        public void SetHashAlgorithm(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm) || !("SHA2".Equals(algorithm, StringComparison.InvariantCultureIgnoreCase) || "SHA3".Equals(algorithm, StringComparison.InvariantCultureIgnoreCase)))
                throw new ArgumentException("Illegal Hash function family: " + algorithm + " - must be either SHA2 or SHA3");
            hashAlgorithm = algorithm;
        }

        public void Init()
        {
            if (inited)
                throw new ArgumentException("Crypto suite already initialized");
            ResetConfiguration();
        }
    }
}