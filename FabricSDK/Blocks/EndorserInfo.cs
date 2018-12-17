using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;

namespace Hyperledger.Fabric.SDK.Blocks
{
    public class EndorserInfo
    {
        private readonly Endorsement endorsement;

        public EndorserInfo(Endorsement endorsement)
        {
            this.endorsement = endorsement;
        }

        public byte[] Signature => endorsement.Signature.ToByteArray();

        /**
         * @return
         * @deprecated use getId and getMspid
         */
        public byte[] Endorser => endorsement.Endorser.ToByteArray();

        public string Id => SerializedIdentity.Parser.ParseFrom(endorsement.Endorser).IdBytes.ToStringUtf8();

        public string Mspid => SerializedIdentity.Parser.ParseFrom(endorsement.Endorser).Mspid;
    }
}