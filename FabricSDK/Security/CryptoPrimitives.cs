/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement.JPake;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Security.Certificates;
using Org.BouncyCastle.Utilities.IO.Pem;
using Org.BouncyCastle.X509;
using CryptoException = Hyperledger.Fabric.SDK.Exceptions.CryptoException;
using ECCurve = System.Security.Cryptography.ECCurve;
using ECPoint = System.Security.Cryptography.ECPoint;
using HashAlgorithm = System.Security.Cryptography.HashAlgorithm;
using PemReader = Org.BouncyCastle.OpenSsl.PemReader;

namespace Hyperledger.Fabric.SDK.Security
{

    public class CryptoPrimitives : ICryptoSuite
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(CryptoPrimitives));
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL ? Config.Instance.GetDiagnosticFileDumper() : null;

        internal string curveName;
        internal string hashAlgorithm = Config.Instance.GetHashAlgorithm();
        internal int securityLevel = Config.Instance.GetSecurityLevel();
        private string CERTIFICATE_FORMAT = Config.Instance.GetCertificateFormat();
        private string DEFAULT_SIGNATURE_ALGORITHM = Config.Instance.GetSignatureAlgorithm();

        private Dictionary<int, string> securityCurveMapping = Config.Instance.GetSecurityCurveMapping();

        // Following configuration settings are hardcoded as they don't deal with any interactions with Fabric MSP and BCCSP components
        // If you wish to make these customizable, follow the logic from setProperties();
        //TODO May need this for TCERTS ?
//    private String ASYMMETRIC_KEY_TYPE = "EC";
//    private String KEY_AGREEMENT_ALGORITHM = "ECDH";
//    private String SYMMETRIC_KEY_TYPE = "AES";
//    private int SYMMETRIC_KEY_BYTE_COUNT = 32;
//    private String SYMMETRIC_ALGORITHM = "AES/CFB/NoPadding";
//    private int MAC_KEY_BYTE_COUNT = 32;

        public CryptoPrimitives()
        {
            //String securityProviderClassName = config.getSecurityProviderClassName();

            //SECURITY_PROVIDER = setUpExplicitProvider(securityProviderClassName);

            //Decided TO NOT do this as it can have affects over the whole JVM and could have
            // unexpected results.  The embedding application can easily do this!
            // Leaving this here as a warning.
            // Security.insertProviderAt(SECURITY_PROVIDER, 1); // 1 is top not 0 :)
        }
        /*
        public Provider setUpExplicitProvider(String securityProviderClassName) {
            if (null == securityProviderClassName)
            {
                throw new InstantiationException(format("Security provider class name property (%s) set to null  ", Config.SECURITY_PROVIDER_CLASS_NAME));
            }

            if (CryptoSuiteFactory.DEFAULT_JDK_PROVIDER.equals(securityProviderClassName))
            {
                return null;
            }

            Class < ?> aClass = Class.forName(securityProviderClassName);
            if (null == aClass)
            {
                throw new InstantiationException(format("Getting class for security provider %s returned null  ", securityProviderClassName));
            }

            if (!Provider.class.isAssignableFrom(aClass)) {
                throw new InstantiationException(format("Class for security provider %s is not a Java security provider", aClass.getName()));
            }
            Provider securityProvider = (Provider) aClass.newInstance();
            if (securityProvider == null)
            {
                throw new InstantiationException(format("Creating instance of security %s returned null  ", aClass.getName()));
            }

            return securityProvider;
        }
        */

        //    /**
        //     * sets the signature algorithm used for signing/verifying.
        //     *
        //     * @param sigAlg the name of the signature algorithm. See the list of valid names in the JCA Standard Algorithm Name documentation
        //     */
        //    public void setSignatureAlgorithm(String sigAlg) {
        //        this.DEFAULT_SIGNATURE_ALGORITHM = sigAlg;
        //    }

        //    /**
        //     * returns the signature algorithm used by this instance of CryptoPrimitives.
        //     * Note that fabric and fabric-ca have not yet standardized on which algorithms are supported.
        //     * While that plays out, CryptoPrimitives will try the algorithm specified in the certificate and
        //     * the default SHA256withECDSA that's currently hardcoded for fabric and fabric-ca
        //     *
        //     * @return the name of the signature algorithm
        //     */
        //    public String getSignatureAlgorithm() {
        //        return this.DEFAULT_SIGNATURE_ALGORITHM;
        //    }

        public List<X509Certificate2> BytesToCertificates(byte[] certBytes)
        {
            if (certBytes == null || certBytes.Length == 0)
            {
                throw new CryptoException("BytesToCertificates: input null or zero length");
            }

            return GetX509Certificates(certBytes);
        }


        public X509Certificate2 BytesToCertificate(byte[] certBytes)
        {
            if (certBytes == null || certBytes.Length == 0)
            {
                throw new CryptoException("bytesToCertificate: input null or zero length");
            }

            return GetX509Certificate(certBytes);


//        X509Certificate certificate;
//        try {
//            BufferedInputStream pem = new BufferedInputStream(new ByteArrayInputStream(certBytes));
//            CertificateFactory certFactory = CertificateFactory.getInstance(CERTIFICATE_FORMAT);
//            certificate = (X509Certificate) certFactory.generateCertificate(pem);
//        } catch (CertificateException e) {
//            String emsg = "Unable to converts byte array to certificate. error : " + e.getMessage();
//            logger.error(emsg);
//            logger.debug("input bytes array :" + new String(certBytes));
//            throw new CryptoException(emsg, e);
//        }
//
//        return certificate;
        }

        /**
         * Return X509Certificate  from pem bytes.
         * So you may ask why this ?  Well some providers (BC) seems to have problems with creating the
         * X509 cert from bytes so here we go through all available providers till one can convert. :)
         *
         * @param pemCertificate
         * @return
         */

        private X509Certificate2 GetX509Certificate(byte[] pemCertificate)
        {
            X509Certificate2 ret = null;
            Pkcs12Store store = new Pkcs12StoreBuilder().Build();
            Org.BouncyCastle.X509.X509Certificate cert=null;
            AsymmetricCipherKeyPair privKey = null;
            using (MemoryStream ms = new MemoryStream(pemCertificate))
            {
                PemReader pemReader = new PemReader(new StreamReader(ms));
                object o;
                while ((o = pemReader.ReadObject()) != null)
                {
                    if (o is Org.BouncyCastle.X509.X509Certificate)
                    {
                        cert = (Org.BouncyCastle.X509.X509Certificate) o;
                    }
                    else if (o is AsymmetricCipherKeyPair)
                    {
                        privKey = (AsymmetricCipherKeyPair) o;
                    }
                }
            }

            if (cert == null && privKey == null)
            {
                throw new CryptoException("BytesToCertificate: Invalid Certificate");
            }
            if (cert!=null && privKey==null)
                return new X509Certificate2(DotNetUtilities.ToX509Certificate(cert));
            store.SetKeyEntry("Hyperledger.Fabric", new AsymmetricKeyEntry(privKey.Private), new[] {new X509CertificateEntry(cert)});
            using (MemoryStream ms = new MemoryStream())
            {
                store.Save(ms, "test".ToCharArray(), new SecureRandom());
                ms.Flush();
                ms.Position = 0;
                return new X509Certificate2(ms.ToArray(), "test",X509KeyStorageFlags.Exportable|X509KeyStorageFlags.DefaultKeySet);
            }

        }

        internal List<X509Certificate2> GetX509Certificates(byte[] pemCertificates)
        {
            List<X509Certificate2> certs = new List<X509Certificate2>();
            List<(Org.BouncyCastle.X509.X509Certificate, AsymmetricCipherKeyPair)> ls = new List<(Org.BouncyCastle.X509.X509Certificate, AsymmetricCipherKeyPair)>();
            AsymmetricCipherKeyPair privKey = null;
            Org.BouncyCastle.X509.X509Certificate entry = null;
            using (MemoryStream ms = new MemoryStream(pemCertificates))
            {
                PemReader pemReader = new PemReader(new StreamReader(ms));
                object o;
                while ((o = pemReader.ReadObject()) != null)
                {
                    if (o is Org.BouncyCastle.X509.X509Certificate)
                    {
                        if (entry!=null)
                            ls.Add((entry, privKey));
                        entry = (Org.BouncyCastle.X509.X509Certificate) o;
                        privKey = null;
                    }
                    else if (o is AsymmetricCipherKeyPair)
                    {
                        privKey = (AsymmetricCipherKeyPair) o;
                        ls.Add((entry, privKey));
                        entry = null;
                        privKey = null;
                    }
                }
                if(entry!=null)
                    ls.Add((entry, privKey));
            }

            foreach ((Org.BouncyCastle.X509.X509Certificate c, AsymmetricCipherKeyPair p) in ls)
            {
                if (c== null && p == null)
                    continue;
                if (c != null && p == null)
                {
                    certs.Add(new X509Certificate2(DotNetUtilities.ToX509Certificate(c)));
                }
                else
                {
                    Pkcs12Store store = new Pkcs12StoreBuilder().Build();
                    store.SetKeyEntry("Hyperledger.Fabric", new AsymmetricKeyEntry(p.Private), new[] { new X509CertificateEntry(c) });
                    using (MemoryStream ms = new MemoryStream())
                    {
                        store.Save(ms, "test".ToCharArray(), new SecureRandom());
                        ms.Flush();
                        ms.Position = 0;
                        certs.Add(new X509Certificate2(ms.ToArray(), "test", X509KeyStorageFlags.Exportable | X509KeyStorageFlags.DefaultKeySet));
                    }
                }
            }

            return certs;
        }



        public AsymmetricAlgorithm GetAsymmetricAlgorithm(AsymmetricKeyParameter privKey)
        {

            if (privKey == null)
                throw new CryptoException("Invalid Private Key");
            if (privKey is RsaKeyParameters)
            {
                RSACryptoServiceProvider sv = new RSACryptoServiceProvider();
                sv.ImportParameters(DotNetUtilities.ToRSAParameters((RsaKeyParameters)privKey));
                return sv;
            }

            if (privKey is DsaPrivateKeyParameters)
            {
                DSACryptoServiceProvider sv = new DSACryptoServiceProvider();
                DsaPrivateKeyParameters kp = (DsaPrivateKeyParameters)privKey;
                DSAParameters p = new DSAParameters();
                p.G = kp.Parameters.G.ToByteArrayUnsigned();
                p.P = kp.Parameters.P.ToByteArrayUnsigned();
                p.Q = kp.Parameters.Q.ToByteArrayUnsigned();
                p.X = kp.X.ToByteArrayUnsigned();
                p.Counter = kp.Parameters.ValidationParameters.Counter;
                p.Seed = kp.Parameters.ValidationParameters.GetSeed();
                sv.ImportParameters(p);
                return sv;
            }

            if (privKey is ECPrivateKeyParameters)
            {
                ECPrivateKeyParameters or = (ECPrivateKeyParameters)privKey;
                //TODO HACK - Mapping maybe?
                string bouncyclass = or.Parameters.Curve.GetType().Name;
                int idx = bouncyclass.LastIndexOf(".");
                string name = bouncyclass.Substring(idx + 1).Replace("Curve", "").ToLowerInvariant();
                //
                ECParameters q = new ECParameters();
                q.Curve = ECCurve.CreateFromFriendlyName(name);
                q.Q.X = or.Parameters.G.X.GetEncoded();
                q.Q.Y = or.Parameters.G.Y.GetEncoded();
                q.D = or.D.ToByteArrayUnsigned();
                q.Validate();
                return ECDsa.Create(q);

            }
            throw new CryptoException("Unsupported private key");
        }
        /**
         * Return PrivateKey  from pem bytes.
         *
         * @param pemKey pem-encoded private key
         * @return
         */
        public AsymmetricAlgorithm BytesToPrivateKey(byte[] pemKey)
        {
            if (pemKey == null || pemKey.Length == 0)
                throw new CryptoException("private key cannot be null");
            AsymmetricKeyParameter privKey = null;
            using (MemoryStream ms = new MemoryStream(pemKey))
            {
                PemReader pemReader = new PemReader(new StreamReader(ms));
                object o;
                while ((o = pemReader.ReadObject()) != null)
                {
                    if (o is AsymmetricKeyParameter)
                    {
                        privKey = (AsymmetricKeyParameter) o;
                        break;
                    }
                }
            }

            return GetAsymmetricAlgorithm(privKey);
            
        }


        public bool Verify(byte[] pemCertificate, string signatureAlgorithm, byte[] signature, byte[] plainText)
        {
            bool isVerified = false;

            if (plainText == null || signature == null || pemCertificate == null)
            {
                return false;
            }

            if (Config.Instance.ExtraLogLevel(10))
            {
                if (null != diagnosticFileDumper)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("plaintext in hex: " + BitConverter.ToString(plainText).Replace("-", string.Empty));
                    sb.AppendLine("signature in hex: " + BitConverter.ToString(signature).Replace("-", string.Empty));
                    sb.Append("PEM cert in hex: " + BitConverter.ToString(pemCertificate).Replace("-", string.Empty));
                    logger.Trace("verify :  " + diagnosticFileDumper.CreateDiagnosticFile(sb.ToString()));
                }
            }

            try
            {

                X509Certificate2 certificate = GetX509Certificate(pemCertificate);

                if (certificate != null)
                {

                    isVerified = ValidateCertificate(certificate);
                    if (isVerified)
                    {
                        // only proceed if cert is trusted
                        if (certificate.PublicKey.Key is RSACryptoServiceProvider)
                        {
                            RSACryptoServiceProvider prov = (RSACryptoServiceProvider) certificate.PublicKey.Key;
                            HashAlgorithm hs = HashAlgorithm.Create(signatureAlgorithm);
                            if (hs == null)
                            {
                                CryptoException ex = new CryptoException($"Cannot verify. Signature algorithm {signatureAlgorithm} is invalid.");
                                logger.ErrorException(ex.Message, ex);
                                throw ex;
                            }

                            isVerified = prov.VerifyData(plainText, hs, signature);
                        }
                        else if (certificate.PublicKey.Key is DSACryptoServiceProvider)
                        {
                            DSACryptoServiceProvider prov = (DSACryptoServiceProvider) certificate.PublicKey.Key;
                            isVerified = prov.VerifyData(plainText, signature);
                        }
                        else if (certificate.PublicKey.Key is ECDsa)
                        {
                            ECDsa prov = (ECDsa) certificate.PublicKey.Key;
                            isVerified = prov.VerifyData(plainText, signature, (HashAlgorithmName) Enum.Parse(typeof(HashAlgorithmName), signatureAlgorithm, true));
                        }
                    }
                }
            }
            catch (CryptoException ee)
            {
                throw ee;
            }
            catch (Exception e)
            {
                CryptoException ex = new CryptoException("Cannot verify signature. " + e.Message + "\r\nCertificate: " + BitConverter.ToString(pemCertificate).Replace("-", string.Empty), e);
                logger.ErrorException(ex.Message, ex);
                throw ex;
            }

            return isVerified;
        } // verify

        internal X509Store trustStore = null;

        private void CreateTrustStore()
        {
            try
            {
                X509Store store = new X509Store("Hyperledger.Fabric.Sdk", StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                SetTrustStore(store);

            }
            catch (Exception e)
            {
                throw new CryptoException("Cannot create trust store. Error: " + e.Message, e);
            }
        }

        /**
         * setTrustStore uses the given KeyStore object as the container for trusted
         * certificates
         *
         * @param keyStore the KeyStore which will be used to hold trusted certificates
         * @throws InvalidArgumentException
         */
        internal void SetTrustStore(X509Store keyStore)
        {
            trustStore = keyStore ?? throw new InvalidArgumentException("Need to specify a java.security.KeyStore input parameter");
        }

        /**
         * getTrustStore returns the KeyStore object where we keep trusted certificates.
         * If no trust store has been set, this method will create one.
         *
         * @return the trust store as a java.security.KeyStore object
         * @throws CryptoException
         * @see KeyStore
         */
        public X509Store GetTrustStore()
        {
            if (trustStore == null)
            {
                CreateTrustStore();
            }

            return trustStore;
        }

        /**
         * addCACertificateToTrustStore adds a CA cert to the set of certificates used for signature validation
         *
         * @param caCertPem an X.509 certificate in PEM format
         * @param alias     an alias associated with the certificate. Used as shorthand for the certificate during crypto operations
         * @throws CryptoException
         * @throws InvalidArgumentException
         */


        public void AddCACertificateToTrustStoreFromFile(string caCertPemFile, string alias)
        {

            if (string.IsNullOrEmpty(caCertPemFile))
                throw new InvalidArgumentException("The certificate cannot be null");
            if (string.IsNullOrEmpty(alias))
                throw new InvalidArgumentException("You must assign an alias to a certificate when adding to the trust store");
            try
            {
                byte[] data = File.ReadAllBytes(caCertPemFile);
                X509Certificate2 caCert = BytesToCertificate(data);
                AddCACertificateToTrustStore(caCert, alias);

            }
            catch (Exception e)
            {
                throw new CryptoException("Unable to add CA certificate to trust store. Error: " + e.Message, e);
            }

        }

        /**
         * addCACertificatesToTrustStore adds a CA certs in a stream to the trust store  used for signature validation
         *
         * @param bis an X.509 certificate stream in PEM format in bytes
         * @throws CryptoException
         * @throws InvalidArgumentException
         */
        public void AddCACertificatesToTrustStoreFromFile(string caCertPemFile)
        {

            if (string.IsNullOrEmpty(caCertPemFile))
                throw new InvalidArgumentException("The certificate stream bis cannot be null");
            try
            {
                byte[] data = File.ReadAllBytes(caCertPemFile);
                if (data.Length == 0)
                    throw new CryptoException("AddCACertificatesToTrustStore: input zero length");
                List<X509Certificate2> caCerts = GetX509Certificates(data);
                foreach (X509Certificate2 caCert in caCerts)
                    AddCACertificateToTrustStore(caCert);
            }
            catch (CertificateException e)
            {
                throw new CryptoException("Unable to add CA certificate to trust store. Error: " + e.Message, e);
            }
        }

        public void AddCACertificateToTrustStore(X509Certificate2 certificate)
        {

            string alias = certificate.SerialNumber ?? certificate.GetHashCode().ToString();
            AddCACertificateToTrustStore(certificate, alias);
        }

        /**
         * addCACertificateToTrustStore adds a CA cert to the set of certificates used for signature validation
         *
         * @param caCert an X.509 certificate
         * @param alias  an alias associated with the certificate. Used as shorthand for the certificate during crypto operations
         * @throws CryptoException
         * @throws InvalidArgumentException
         */
        public void AddCACertificateToTrustStore(X509Certificate2 caCert, string alias)
        {

            if (string.IsNullOrEmpty(alias))
            {
                throw new InvalidArgumentException("You must assign an alias to a certificate when adding to the trust store.");
            }

            if (caCert == null)
            {
                throw new InvalidArgumentException("Certificate cannot be null.");
            }

            try
            {
                if (Config.Instance.ExtraLogLevel(10))
                {
                    if (null != diagnosticFileDumper)
                    {
                        logger.Trace($"Adding cert to trust store. alias: {alias}" + diagnosticFileDumper.CreateDiagnosticFile(alias + "cert: " + caCert.ToString()));
                    }
                }

                GetTrustStore().Add(caCert);
            }
            catch (Exception e)
            {
                string emsg = "Unable to add CA certificate to trust store. Error: " + e.Message;
                logger.Error(emsg, e);
                throw new CryptoException(emsg, e);
            }
        }


        public void LoadCACertificates(List<X509Certificate2> certificates)
        {
            if (certificates == null || certificates.Count == 0)
            {
                throw new CryptoException("Unable to load CA certificates. List is empty");
            }

            try
            {
                foreach (X509Certificate2 x509Certificate2 in certificates)
                {
                    AddCACertificateToTrustStore(x509Certificate2);
                }
            }
            catch (Exception e)
            {
                // Note: This can currently never happen (as cert<>null and alias<>null)
                throw new CryptoException("Unable to add certificate to trust store. Error: " + e.Message, e);
            }
        }

        /* (non-Javadoc)
         * @see org.hyperledger.fabric.sdk.security.CryptoSuite#loadCACertificatesAsBytes(java.util.Collection)
         */

        public void LoadCACertificatesAsBytes(List<byte[]> certificatesBytes)
        {
            if (certificatesBytes == null || certificatesBytes.Count == 0)
            {
                throw new CryptoException("List of CA certificates is empty. Nothing to load.");
            }

            StringBuilder sb = new StringBuilder();
            List<X509Certificate2> certList = new List<X509Certificate2>();
            foreach (byte[] certBytes in certificatesBytes)
            {
                if (null != diagnosticFileDumper)
                {
                    sb.AppendLine("certificate to load:" + BitConverter.ToString(certBytes).Replace("-", string.Empty));
                }

                certList.Add(BytesToCertificate(certBytes));
            }

            LoadCACertificates(certList);
            if (diagnosticFileDumper != null && sb.Length > 1)
            {
                logger.Trace("loaded certificates: " + diagnosticFileDumper.CreateDiagnosticFile(sb.ToString()));

            }
        }

        /**
         * validateCertificate checks whether the given certificate is trusted. It
         * checks if the certificate is signed by one of the trusted certs in the
         * trust store.
         *
         * @param certPEM the certificate in PEM format
         * @return true if the certificate is trusted
         */
        public bool ValidateCertificate(byte[] certPEM)
        {

            if (certPEM == null)
            {
                return false;
            }

            try
            {

                X509Certificate2 certificate = GetX509Certificate(certPEM);
                if (null == certificate)
                {
                    throw new Exception("Certificate transformation returned null");
                }

                return ValidateCertificate(certificate);
            }
            catch (Exception e)
            {
                logger.Error("Cannot validate certificate. Error is: " + e.Message + "\r\nCertificate (PEM, hex): " + BitConverter.ToString(certPEM).Replace("-", string.Empty));
                return false;
            }
        }

        public bool ValidateCertificate(X509Certificate2 cert)
        {
            if (cert == null)
                return false;
            try
            {
                return cert.Verify();
            }
            catch (Exception e)
            {
                logger.Error("Cannot validate certificate. Error is: " + e.Message + "\r\nCertificate" + cert.ToString());
            }

            return false;
        } // validateCertificate

        /**
         * Security Level determines the elliptic curve used in key generation
         *
         * @param securityLevel currently 256 or 384
         * @throws InvalidArgumentException
         */
        public void SetSecurityLevel(int securityLevel)
        {
            logger.Trace($"setSecurityLevel to {securityLevel}", securityLevel);

            if (securityCurveMapping.Count == 0)
            {
                throw new InvalidArgumentException("Security curve mapping has no entries.");
            }

            if (!securityCurveMapping.ContainsKey(securityLevel))
            {
                StringBuilder sb = new StringBuilder();
                string sp = "";
                foreach (int x in securityCurveMapping.Keys)
                {
                    sb.Append(sp).Append(x);
                    sp = ", ";

                }

                throw new InvalidArgumentException($"Illegal security level: {securityLevel}. Valid values are: {sb.ToString()}");
            }

            string lcurveName = securityCurveMapping[securityLevel];

            logger.Debug($"Mapped curve strength {securityLevel} to {lcurveName}");
            X9ECParameters pars = ECNamedCurveTable.GetByName(lcurveName);
            //Check if can match curve name to requested strength.
            if (pars == null)
            {

                InvalidArgumentException invalidArgumentException = new InvalidArgumentException($"Curve {curveName} defined for security strength {securityLevel} was not found.");

                logger.ErrorException(invalidArgumentException.Message, invalidArgumentException);
                throw invalidArgumentException;

            }

            this.curveName = lcurveName;
            this.securityLevel = securityLevel;
        }

        public void SetHashAlgorithm(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm) || !("SHA2".Equals(algorithm, StringComparison.InvariantCultureIgnoreCase) || "SHA3".Equals(algorithm, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new InvalidArgumentException("Illegal Hash function family: " + algorithm + " - must be either SHA2 or SHA3");
            }

            this.hashAlgorithm = algorithm;
        }

        public AsymmetricAlgorithm KeyGen()
        {
            return EcdsaKeyGen();
        }

        private AsymmetricAlgorithm EcdsaKeyGen()
        {
            return GenerateKey("EC", curveName);
        }

        private AsymmetricAlgorithm GenerateKey(string encryptionName, string curveName)
        {
            try
            {
                ECCurve curve = ECCurve.CreateFromFriendlyName(curveName);
                ECDsa ec = ECDsa.Create(curve);
                return ec;
            }
            catch (Exception exp)
            {
                throw new CryptoException("Unable to generate key pair", exp);
            }
        }

        /**
         * Decodes an ECDSA signature and returns a two element BigInteger array.
         *
         * @param signature ECDSA signature bytes.
         * @return BigInteger array for the signature's r and s values
         * @throws Exception
         */
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
                        {
                            sigs[count] = integer;
                        }

                        count++;
                    }
                }
            }

            if (count != 2)
            {
                throw new CryptoException($"Invalid ECDSA signature. Expected count of 2 but got: {count}. Signature is: {BitConverter.ToString(signature).Replace("-", string.Empty)}");
            }

            return sigs;
        }


        /**
         * Sign data with the specified elliptic curve private key.
         *
         * @param privateKey elliptic curve private key.
         * @param data       data to sign
         * @return the signed data.
         * @throws CryptoException
         */
        private byte[] EcdsaSignToBytes(AsymmetricAlgorithm privateKey, byte[] data)
        {
            try
            {
      
                ECDsa ecdsa = (ECDsa)privateKey;
                ECParameters q = ecdsa.ExportParameters(true);
                X9ECParameters par = SecNamedCurves.GetByName(q.Curve.Oid.FriendlyName);
                BigInteger X = new BigInteger(q.Q.X);
                BigInteger Y = new BigInteger(q.Q.Y);
                Org.BouncyCastle.Math.EC.ECPoint g = par.Curve.CreatePoint(X, Y);
                ECDomainParameters domain = new ECDomainParameters(par.Curve, g, par.N);
                ECDsaSigner signer = new ECDsaSigner(new HMacDsaKCalculator(DigestUtilities.GetDigest(DEFAULT_SIGNATURE_ALGORITHM.Replace("withECDSA", ""))));
                ECPrivateKeyParameters privkey = new ECPrivateKeyParameters(new BigInteger(1, q.D), domain);
                signer.Init(true, privkey);
                BigInteger[] sigs = signer.GenerateSignature(data);
                sigs = PreventMalleability(sigs, par.N);
                using (MemoryStream ms = new MemoryStream())
                {
                    DerSequenceGenerator seq = new DerSequenceGenerator(ms);
                    seq.AddObject(new DerInteger(sigs[0]));
                    seq.AddObject(new DerInteger(sigs[1]));
                    seq.Close();
                    ms.Flush();
                    return ms.ToArray();
                }
            }
            catch (Exception e)
            {
                throw new CryptoException("Could not sign the message using private key", e);
            }

        }

        /**
         * @{@link ECPrivateKey}.
         */

        public byte[] Sign(AsymmetricAlgorithm privateKey, byte[] data)
        {
            return EcdsaSignToBytes(privateKey, data);
        }

        private BigInteger[] PreventMalleability(BigInteger[] sigs, BigInteger curveN)
        {
            BigInteger cmpVal = curveN.Divide(BigInteger.Two);
            BigInteger sval = sigs[1];

            if (sval.CompareTo(cmpVal) == 1)
            {

                sigs[1] = curveN.Subtract(sval);
            }

            return sigs;
        }

        /**
         * generateCertificationRequest
         *
         * @param subject The subject to be added to the certificate
         * @param pair    Public private key pair
         * @return PKCS10CertificationRequest Certificate Signing Request.
         * @throws OperatorCreationException
         */

        public string GenerateCertificationRequest(string subject, AsymmetricAlgorithm publickey, AsymmetricAlgorithm privatekey)
        {
            try
            {
                IDictionary attrs = new Hashtable();
                attrs.Add(X509Name.CN, "Requested Test Certificate");
                AsymmetricCipherKeyPair priv = DotNetUtilities.GetKeyPair(publickey);
                AsymmetricCipherKeyPair pub = DotNetUtilities.GetKeyPair(privatekey);
                ISignatureFactory sf = new Asn1SignatureFactory("SHA256withECDSA", priv.Private);
                Pkcs10CertificationRequest csr = new Pkcs10CertificationRequest(sf, new X509Name(new ArrayList(attrs.Keys), attrs), pub.Public, null, priv.Private);
                return CertificationRequestToPEM(csr);
            }
            catch (Exception e)
            {

                logger.ErrorException(e.Message,e);
                throw new InvalidArgumentException(e);

            }

        }
        public string GenerateCertificationRequest(string subject, AsymmetricAlgorithm keypair)
        {
            try
            {
                IDictionary attrs = new Hashtable();
                attrs.Add(X509Name.CN, "Requested Test Certificate");
                AsymmetricCipherKeyPair keyp = DotNetUtilities.GetKeyPair(keypair);
                ISignatureFactory sf = new Asn1SignatureFactory("SHA256withECDSA", keyp.Private);
                Pkcs10CertificationRequest csr = new Pkcs10CertificationRequest(sf, new X509Name(new ArrayList(attrs.Keys), attrs), keyp.Public, null, keyp.Private);
                return CertificationRequestToPEM(csr);
            }
            catch (Exception e)
            {

                logger.ErrorException(e.Message, e);
                throw new InvalidArgumentException(e);

            }

        }

        /**
         * certificationRequestToPEM - Convert a PKCS10CertificationRequest to PEM
         * format.
         *
         * @param csr The Certificate to convert
         * @return An equivalent PEM format certificate.
         * @throws IOException
         */

        private string CertificationRequestToPEM(Pkcs10CertificationRequest csr)
        {
            PemObject pemCSR = new PemObject("CERTIFICATE REQUEST", csr.GetEncoded());
            StringWriter str = new StringWriter();
            PemWriter pemWriter = new PemWriter(str);
            pemWriter.WriteObject(pemCSR);
            str.Flush();
            str.Close();
            return str.ToString();
        }



        public byte[] Hash(byte[] input)
        {
            IDigest digest = GetHashDigest();
            byte[] retValue = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(input,0,input.Length);
            digest.DoFinal(retValue, 0);
            return retValue;
        }


        public ICryptoSuiteFactory GetCryptoSuiteFactory()
        {
            return HLSDKJCryptoSuiteFactory.Instance; //Factory for this crypto suite.
        }

        private bool inited = false;


        public void Init()
        {
            if (inited)
            {
                throw new InvalidArgumentException("Crypto suite already initialized");
            }

            ResetConfiguration();

        }

        private IDigest GetHashDigest()
        {
            if ("SHA3".Equals(hashAlgorithm, StringComparison.CurrentCultureIgnoreCase))
            {
                return new Sha3Digest();
            }
            else
            {
                // Default to SHA2
                return new Sha256Digest();
            }
        }

        //    /**
        //     * Shake256 hash the supplied byte data.
        //     *
        //     * @param in        byte array to be hashed.
        //     * @param bitLength of the result.
        //     * @return the hashed byte data.
        //     */
        //    public byte[] shake256(byte[] in, int bitLength) {
        //
        //        if (bitLength % 8 != 0) {
        //            throw new IllegalArgumentException("bit length not modulo 8");
        //
        //        }
        //
        //        final int byteLen = bitLength / 8;
        //
        //        SHAKEDigest sd = new SHAKEDigest(256);
        //
        //        sd.update(in, 0, in.length);
        //
        //        byte[] out = new byte[byteLen];
        //
        //        sd.doFinal(out, 0, byteLen);
        //
        //        return out;
        //
        //    }

        /**
         * Resets curve name, hash algorithm and cert factory. Call this method when a config value changes
         *
         * @throws CryptoException
         * @throws InvalidArgumentException
         */
        private void ResetConfiguration()
        {

            SetSecurityLevel(securityLevel);
            SetHashAlgorithm(hashAlgorithm);
        }

//    /* (non-Javadoc)
    //     * @see org.hyperledger.fabric.sdk.security.CryptoSuite#setProperties(java.util.Properties)
    //     */
    //    @Override
        public void SetProperties(Properties properties)
        {
            if (properties == null || properties.Count==0) {
                throw new InvalidArgumentException("properties must not be null");
            }
            //        if (properties != null) {
            hashAlgorithm = properties.Contains(Config.HASH_ALGORITHM) ? properties[Config.HASH_ALGORITHM] : hashAlgorithm;
            string secLevel = properties.Contains(Config.SECURITY_LEVEL) ? properties[Config.SECURITY_LEVEL] : securityLevel.ToString();
            securityLevel = int.Parse(secLevel);
            if (properties.Contains(Config.SECURITY_CURVE_MAPPING)) {
                securityCurveMapping = Config.ParseSecurityCurveMappings(properties[Config.SECURITY_CURVE_MAPPING]);
            } else {
                securityCurveMapping = Config.Instance.GetSecurityCurveMapping();
            }
            CERTIFICATE_FORMAT = properties.Contains(Config.CERTIFICATE_FORMAT) ? properties[Config.CERTIFICATE_FORMAT] : CERTIFICATE_FORMAT;
            DEFAULT_SIGNATURE_ALGORITHM = properties.Contains(Config.SIGNATURE_ALGORITHM) ? properties[Config.SIGNATURE_ALGORITHM] : DEFAULT_SIGNATURE_ALGORITHM;
            ResetConfiguration();

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

        public byte[] CertificateToDER(string certificatePEM)
        {

            byte[] content = null;

            try
            {
                PemReader pemReader = new PemReader(new StringReader(certificatePEM));
                PemObject pemObject = pemReader.ReadPemObject();
                content = pemObject.Content;
            }
            catch (Exception e)
            {
                // best attempt
            }
            return content;
        }

    }
}

