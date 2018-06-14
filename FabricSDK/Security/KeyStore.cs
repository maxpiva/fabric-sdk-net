using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Hyperledger.Fabric.SDK.Exceptions;
using Org.BouncyCastle.Pkix;
using Org.BouncyCastle.Utilities.Collections;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Store;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Hyperledger.Fabric.SDK.Security
{
    public class KeyStore
    {
        private readonly List<Certificate> certs;

        //TODO Add weakreference
        private readonly Dictionary<string, KeyPair> keypairs;

        public KeyStore()
        {
            certs = new List<Certificate>();
            keypairs = new Dictionary<string, KeyPair>();
        }

        public List<Certificate> Certificates => certs.ToList();
        public Dictionary<string, KeyPair> KeyPairs => keypairs.ToDictionary(a => a.Key, a => a.Value);

        public void AddCertificate(string pems)
        {
            if (string.IsNullOrEmpty(pems))
                throw new InvalidArgumentException("Empty Certificate/s provided");
            Certificate.PEMCertificateListToPEMs(pems).ForEach(a => certs.Add(Certificate.Create(a)));
        }

        public void AddCertificate(X509Certificate2 cert)
        {
            if (cert == null)
                throw new InvalidArgumentException("Empty Certificate provided");
            certs.Add(Certificate.Create(cert));
        }

        public void AddCertificateFromFile(string file)
        {
            if (string.IsNullOrEmpty(file))
                throw new InvalidArgumentException($"Empty filename provided");
            if (!File.Exists(file))
                throw new InvalidArgumentException($"{file} not found");
            try
            {
                string data = File.ReadAllText(file);
                Certificate.PEMCertificateListToPEMs(data).ForEach(a => certs.Add(Certificate.Create(a)));
            }
            catch (Exception e)
            {
                throw new CryptoException($"Error loading {file}. {e.Message}", e);
            }
        }

        public KeyPair GetOrAddKey(string alias, Func<KeyPair> createF)
        {
            lock (keypairs)
            {
                KeyPair fnd;
                if (keypairs.ContainsKey(alias))
                    fnd = keypairs[alias];
                else
                {
                    fnd = createF();
                    keypairs.Add(alias, fnd);
                }

                return fnd;
            }
        }

        public bool Validate(string cert)
        {
            if (string.IsNullOrEmpty(cert))
                return false;
            try
            {
                return Validate(Certificate.Create(cert));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Validate(X509Certificate2 cert)
        {
            if (cert == null)
                return false;
            try
            {
                return Validate(Certificate.Create(cert));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Validate(Certificate cert)
        {
            X509CertificateParser parser = new X509CertificateParser();
            PkixCertPathBuilder builder = new PkixCertPathBuilder();
            try
            {
                // Separate root from itermediate
                List<X509Certificate> intermediateCerts = new List<X509Certificate>();
                HashSet rootCerts = new HashSet();

                foreach (X509Certificate x509Cert in certs.Select(a => a.X509Certificate))
                {
                    // Separate root and subordinate certificates
                    if (x509Cert.IssuerDN.Equivalent(x509Cert.SubjectDN))
                        rootCerts.Add(new TrustAnchor(x509Cert, null));
                    else
                        intermediateCerts.Add(x509Cert);
                }

                // Create chain for this certificate
                X509CertStoreSelector holder = new X509CertStoreSelector();
                holder.Certificate = cert.X509Certificate;

                // WITHOUT THIS LINE BUILDER CANNOT BEGIN BUILDING THE CHAIN
                intermediateCerts.Add(holder.Certificate);

                PkixBuilderParameters builderParams = new PkixBuilderParameters(rootCerts, holder);
                builderParams.IsRevocationEnabled = false;

                X509CollectionStoreParameters intermediateStoreParameters = new X509CollectionStoreParameters(intermediateCerts);

                builderParams.AddStore(X509StoreFactory.Create("Certificate/Collection", intermediateStoreParameters));
                try
                {
                    PkixCertPathBuilderResult result = builder.Build(builderParams);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                throw new CryptoException($"Error validating certificate. {e.Message}");
            }
        }

        /*    public bool ValidateCertificate(X509Certificate2 cert)
        {
            if (cert == null)
                return false;
            try
            {
                bool ret = cert.Verify();
                if (ret)
                    return true;
                if (GetTrustStore().Certificates.Count == 0)
                    return false;
                var chain = new X509Chain();
                foreach (var extra in GetTrustStore().Certificates)
                    chain.ChainPolicy.ExtraStore.Add(extra);
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                bool n = chain.Build(cert);
                return (chain.ChainElements.Count > 1) && n;
            }
            catch (Exception e)
            {
                logger.Error("Cannot validate certificate. Error is: " + e.Message + "\r\nCertificate" + cert.ToString());
            }

            return false;
        } // validateCertificate
*/
    }
}