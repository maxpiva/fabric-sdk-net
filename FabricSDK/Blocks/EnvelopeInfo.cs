using System;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.Deserializers;

namespace Hyperledger.Fabric.SDK.Blocks
{

    /**
     * Wrappers Envelope
     */

    public class EnvelopeInfo
    {
        private readonly EnvelopeDeserializer envelopeDeserializer;
        protected readonly FilteredTransaction filteredTx;
        private readonly HeaderDeserializer headerDeserializer;
        protected readonly BlockInfo parent;

        //private final EnvelopeDeserializer envelopeDeserializer;

        public EnvelopeInfo(BlockInfo parent, EnvelopeDeserializer envelopeDeserializer)
        {
            this.envelopeDeserializer = envelopeDeserializer;
            headerDeserializer = envelopeDeserializer.Payload.Header;
            this.parent = parent;
            filteredTx = null;
        }

        public EnvelopeInfo(BlockInfo parent, FilteredTransaction filteredTx)
        {
            this.filteredTx = filteredTx;
            envelopeDeserializer = null;
            headerDeserializer = null;
            this.parent = parent;
        }

        /**
         * This block is filtered
         *
         * @return true if it's filtered.
         */
        public bool IsFiltered => filteredTx != null;

        public FilteredTransaction FilteredTX => filteredTx;

        /**
         * Get channel id
         *
         * @return The channel id also referred to as channel name.
         */
        public string ChannelId => parent.IsFiltered ? parent.FilteredBlock.ChannelId : headerDeserializer.ChannelHeader.ChannelId;

        /**
         * This is the creator or submitter of the transaction.
         * Returns null for a filtered block.
         *
         * @return {@link IdentitiesInfo}
         */
        public IdentitiesInfo Creator => IsFiltered ? null : new IdentitiesInfo(headerDeserializer.Creator);


        /**
         * The nonce of the transaction.
         *
         * @return return null for filtered block.
         */
        public byte[] Nonce => IsFiltered ? null : headerDeserializer.Nonce;

        /**
         * The transaction ID
         *
         * @return the transaction id.
         */
        public string TransactionID => parent.IsFiltered ? filteredTx.Txid : headerDeserializer.ChannelHeader.TxId;

        /**
         * @return epoch and -1 if filtered block.
         * @deprecated
         */

        public long Epoch => parent.IsFiltered ? -1 : (long) headerDeserializer.ChannelHeader.Epoch;

        /**
         * Timestamp
         *
         * @return timestamp and null if filtered block.
         */

        public DateTime? Timestamp
        {
            get
            {
                if (parent.IsFiltered)
                    return null;
                return headerDeserializer.ChannelHeader.Timestamp.ToDateTime();
            }
        }

        /**
         * @return whether this Transaction is marked as TxValidationCode.VALID
         */
        public bool IsValid => parent.IsFiltered ? filteredTx.TxValidationCode == TxValidationCode.Valid : envelopeDeserializer.IsValid;

        /**
         * @return the validation code of this Transaction (enumeration TxValidationCode in Transaction.proto)
         */
        public byte ValidationCode => parent.IsFiltered ? (byte) filteredTx.TxValidationCode : envelopeDeserializer.ValidationCode;

        public EnvelopeType EnvelopeType
        {
            get
            {
                int type;

                if (parent.IsFiltered)
                {
                    type = (int) filteredTx.Type;
                }
                else
                {
                    type = headerDeserializer.ChannelHeader.Type;
                }

                switch (type)
                {
                    case (int) HeaderType.EndorserTransaction:
                        return EnvelopeType.TRANSACTION_ENVELOPE;

                    default:
                        return EnvelopeType.ENVELOPE;
                }
            }
        }



    }
}