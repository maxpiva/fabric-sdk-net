using System;
using System.IO;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.Protos.Msp.MspConfig;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Idemix;
using Hyperledger.Fabric.SDK.Identity;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using CryptoException = Hyperledger.Fabric.SDK.Exceptions.CryptoException;

namespace Hyperledger.Fabric.SDK.User
{
    public class IdemixUserStore
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(IdemixUserStore));


        private static readonly string USER_PATH = "user";
        private static readonly string VERIFIER_PATH = "msp";
        private static readonly string IPK_CONFIG = "IssuerPublicKey";
        private static readonly string REVOCATION_PUBLIC_KEY = "RevocationPublicKey";
        protected readonly IdemixIssuerPublicKey ipk;
        protected readonly string mspId;


        protected readonly string storePath;

        public IdemixUserStore(string storePath, string mspId)
        {
            this.storePath = storePath;
            this.mspId = mspId;
            IssuerPublicKey ipkProto = ReadIdemixIssuerPublicKey(Path.Combine(mspId, VERIFIER_PATH + IPK_CONFIG));
            ipk = new IdemixIssuerPublicKey(ipkProto);
            if (!ipk.Check())
            {
                throw new CryptoException("Failed verifying issuer public key.");
            }
        }

        public IUser GetUser(string id)
        {
            IdemixMSPSignerConfig signerConfig = ReadIdemixMSPConfig(Path.Combine(mspId, USER_PATH + id));
            KeyPair revocationPk = ReadIdemixRevocationPublicKey(mspId);
            BIG sk = BIG.FromBytes(signerConfig.Sk.ToByteArray());
            IdemixCredential cred = new IdemixCredential(Credential.Parser.ParseFrom(signerConfig.Cred));
            CredentialRevocationInformation cri = CredentialRevocationInformation.Parser.ParseFrom(signerConfig.CredentialRevocationInformation);

            IdemixEnrollment enrollment = new IdemixEnrollment(ipk, revocationPk, mspId, sk, cred, cri, signerConfig.OrganizationalUnitIdentifier, (IdemixRoles) signerConfig.Role);
            return new IdemixUser(id, mspId, enrollment);
        }

        /**
         * Helper function: parse Idemix MSP Signer config (is part of the MSPConfig proto) from path
         *
         * @param id
         * @return IdemixMSPSignerConfig proto
         */
        protected IdemixMSPSignerConfig ReadIdemixMSPConfig(string id)
        {
            string path = storePath + id;
            return IdemixMSPSignerConfig.Parser.ParseFrom(File.ReadAllBytes(path));
        }

        /**
         * Parse Idemix issuer public key from the config file
         *
         * @param id
         * @return Idemix IssuerPublicKey proto
         */
        protected IssuerPublicKey ReadIdemixIssuerPublicKey(string id)
        {
            string path = storePath + id;
            byte[] data = null;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (IOException e)
            {
                logger.ErrorException(e.Message, e);
            }

            try
            {
                return IssuerPublicKey.Parser.ParseFrom(data);
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                return null;
            }
        }

        /**
         * Parse Idemix long-term revocation public key
         *
         * @param id
         * @return idemix long-term revocation public key
         */
        protected KeyPair ReadIdemixRevocationPublicKey(string id)
        {
            string path = Path.Combine(mspId, VERIFIER_PATH, id);
            AsymmetricKeyParameter pub = PublicKeyFactory.CreateKey(KeyPair.PemToDer(File.ReadAllText(path)));
            return KeyPair.Create(pub, null);
        }
    }
}