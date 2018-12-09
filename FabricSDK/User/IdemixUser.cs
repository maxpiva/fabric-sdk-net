using System;
using System.Collections.Generic;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.Protos.Ledger.Rwset;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Idemix;
using Hyperledger.Fabric.SDK.Identity;
using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.SDK.User
{

	public class IdemixUser : IUser
    {

        public string Name { get; }
        public HashSet<string> Roles { get; }
        public string Account { get; }
        public string Affiliation { get; }
        public IEnrollment Enrollment { get; }
        public string MspId { get; }

		public IdemixUser(String name, String mspId, IdemixEnrollment enrollment) {
			this.Name = name;
			this.MspId = mspId;
			this.Enrollment = enrollment;
		}

        public IdemixIssuerPublicKey Ipk => ((IdemixEnrollment) Enrollment).Ipk;

        public IdemixCredential IdemixCredential => ((IdemixEnrollment) Enrollment).Cred;

		public CredentialRevocationInformation Cri => ((IdemixEnrollment) Enrollment).Cri;

		public BIG Sk => ((IdemixEnrollment)Enrollment).Sk;

		public KeyPair RevocationPk => ((IdemixEnrollment)Enrollment).RevocationPk;

		public string Ou => ((IdemixEnrollment)Enrollment).Ou;

		public IdemixRoles RoleMask => ((IdemixEnrollment)Enrollment).RoleMask;

    }
}
