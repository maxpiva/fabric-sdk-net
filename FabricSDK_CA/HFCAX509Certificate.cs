using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;

namespace Hyperledger.Fabric_CA.SDK
{
    /**
     * An x509 credential
     */
    public class HFCAX509Certificate : HFCACredential
    {

        public HFCAX509Certificate(string pem)
        {
            PEM = pem;
            X509 = GetX509Certificate();
            Serial = GetSerial();
            AKI = GetAKI();
        }

        public string PEM { get; }

        public X509Certificate X509 { get; }

        // serial and aki together form a unique identifier for a certificate
        public BigInteger Serial { get; }

        public AuthorityKeyIdentifier AKI { get; }

        private X509Certificate GetX509Certificate()
        {
            if (PEM == null)
            {
                throw new HFCACertificateException("Certificate PEM is null");
            }

            (AsymmetricKeyParameter _, AsymmetricKeyParameter _, X509Certificate certificate) = KeyPair.PEMToAsymmetricCipherKeyPairAndCert(PEM);
            return certificate;
        }

        private BigInteger GetSerial()
        {
            if (X509 == null)
                throw new HFCACertificateException("Certificate is null");
            return X509.SerialNumber;
        }

        private AuthorityKeyIdentifier GetAKI()
        {
            if (X509 == null)
            {
                throw new HFCACertificateException("Certificate is null");
            }
            Asn1OctetString akiOc = X509.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier);
            return AuthorityKeyIdentifier.GetInstance(Asn1Sequence.GetInstance(akiOc.GetOctets()));
        }
    }
}