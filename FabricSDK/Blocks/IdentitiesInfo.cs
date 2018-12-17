using Hyperledger.Fabric.Protos.Msp;

namespace Hyperledger.Fabric.SDK.Blocks
{
    public class IdentitiesInfo
    {
        public IdentitiesInfo(SerializedIdentity identity)
        {
            Mspid = identity.Mspid;
            Id = identity.IdBytes.ToStringUtf8();
        }

        /**
         * The MSPId of the user.
         *
         * @return The MSPid of the user.
         */
        public string Mspid { get; }

        /**
         * The identification of the identity usually the certificate.
         *
         * @return The certificate of the user in PEM format.
         */
        public string Id { get; }
    }
}