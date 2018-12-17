using System.Collections.Generic;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Idemix;
using Hyperledger.Fabric.SDK.Identity;
using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.SDK.User
{
    public class IdemixUser : IUser
    {
        public IdemixUser(string name, string mspId, IdemixEnrollment enrollment)
        {
            Name = name;
            MspId = mspId;
            Enrollment = enrollment;
        }

        public IdemixIssuerPublicKey Ipk => ((IdemixEnrollment) Enrollment).Ipk;

        public IdemixCredential IdemixCredential => ((IdemixEnrollment) Enrollment).Cred;

        public CredentialRevocationInformation Cri => ((IdemixEnrollment) Enrollment).Cri;

        public BIG Sk => ((IdemixEnrollment) Enrollment).Sk;

        public KeyPair RevocationPk => ((IdemixEnrollment) Enrollment).RevocationPk;

        public string Ou => ((IdemixEnrollment) Enrollment).Ou;

        public IdemixRoles RoleMask => ((IdemixEnrollment) Enrollment).RoleMask;

        public string Name { get; }
        public HashSet<string> Roles { get; } = null;
        public string Account { get; } = null;
        public string Affiliation { get; } = null;
        public IEnrollment Enrollment { get; }
        public string MspId { get; }
    }
}