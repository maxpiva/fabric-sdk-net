using System;
using Hyperledger.Fabric.SDK.Security;
using Org.BouncyCastle.Crypto;

namespace Hyperledger.Fabric.SDK.Identity
{
    public class X509SigningIdentity : X509Identity, ISigningIdentity
    {
        private readonly ICryptoSuite cryptoSuite;

        public X509SigningIdentity(ICryptoSuite cryptoSuite, IUser user) : base(user)
        {
            this.cryptoSuite = cryptoSuite ?? throw new ArgumentException("CryptoSuite is null");
        }

        public byte[] Sign(byte[] msg)
        {
            return cryptoSuite.Sign(user.Enrollment.GetKeyPair(), msg);
        }

        public bool VerifySignature(byte[] msg, byte[] sig)
        {
            throw new CryptoException("Not Implemented yet!!!");
        }
    }
}