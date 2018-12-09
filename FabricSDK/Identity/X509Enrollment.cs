
using System;
using System.Runtime.Serialization;
using Hyperledger.Fabric.SDK.Security;
using Org.BouncyCastle.Crypto;

namespace Hyperledger.Fabric.SDK.Identity
{
    [Serializable]
    public class X509Enrollment : IEnrollment
    {
        public X509Enrollment(string key, string cert)
        {
            Key = key;
            Cert = cert;
        }

        public X509Enrollment(KeyPair signingKeyPair, string signedPem)
        {
            this.Key = KeyPair.AsymmetricCipherKeyPairToPEM(null, signingKeyPair.PrivateKey);
            this.Cert = signedPem;
        }

        public X509Enrollment(AsymmetricKeyParameter key, string signedPem)
        {
            this.Key = KeyPair.AsymmetricCipherKeyPairToPEM(null, key);
            this.Cert = signedPem;
        }
        [DataMember(Name = "key")]
        public string Key { get; }
        [DataMember(Name = "cert")]
        public string Cert { get; }

    }
}
