using Hyperledger.Fabric.Protos.Peer.PeerEvents;

namespace Hyperledger.Fabric.SDK.Blocks
{
    public class TransactionEvent : TransactionEnvelopeInfo
    {
        public TransactionEvent(BlockEvent evnt, TransactionEnvelopeInfo transactionEnvelopeInfo) : base(evnt, transactionEnvelopeInfo.TransactionDeserializer)
        {
        }

        public TransactionEvent(BlockEvent evnt, FilteredTransaction filteredTransaction) : base(evnt, filteredTransaction)
        {
        }

        /**
         * The BlockEvent for this TransactionEvent.
         *
         * @return BlockEvent for this transaction.
         */

        public BlockEvent BlockEvent => (BlockEvent)parent;

        /**
         * The event hub that received this event.
         *
         * @return May return null if peer eventing service detected the event.
         * @deprecated use new peer eventing services {@link #getPeer()}
         */

        public EventHub EventHub => BlockEvent.EventHub;

        /**
         * The peer that received this event.
         *
         * @return May return null if deprecated eventhubs are still being used, otherwise return the peer.
         */

        public Peer Peer => BlockEvent.Peer;
    }
}