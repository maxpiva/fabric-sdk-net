using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Idemix;
using Hyperledger.Fabric.SDK.Security;
using Newtonsoft.Json;

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

        [JsonIgnore]
        public IdemixIssuerPublicKey Ipk { get; }
        [JsonIgnore]
        public KeyPair RevocationPk { get; }
        public string MspId { get; }
        [JsonIgnore]
        public BIG Sk { get; }
        [JsonIgnore]
        public IdemixCredential Cred { get; }
        [JsonIgnore]
        public CredentialRevocationInformation Cri { get; }
        public string Ou { get; }
        public IdemixRoles RoleMask { get; }
        [JsonIgnore]
        public string Key => null;
        [JsonIgnore]
        public string Cert => null;
    }
}