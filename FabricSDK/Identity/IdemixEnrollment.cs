using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Idemix;
using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.SDK.Identity
{
    public class IdemixEnrollment : IEnrollment
    {
        private static readonly string algo = "idemix";

        public IdemixEnrollment(IdemixIssuerPublicKey ipk, KeyPair revocationPk, string mspId, BIG sk, IdemixCredential cred, CredentialRevocationInformation cri, string ou, IdemixRoles roleMask)
        {
            Ipk = ipk;
            RevocationPk = revocationPk;
            MspId = mspId;
            Sk = sk;
            Cred = cred;
            Cri = cri;
            Ou = ou;
            RoleMask = roleMask;
        }


        public IdemixIssuerPublicKey Ipk { get; }
        public KeyPair RevocationPk { get; }
        public string MspId { get; }
        public BIG Sk { get; }
        public IdemixCredential Cred { get; }
        public CredentialRevocationInformation Cri { get; }
        public string Ou { get; }
        public IdemixRoles RoleMask { get; }
        public string Key => null;
        public string Cert => null;
    }
}