/*
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *   http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using Hyperledger.Fabric.SDK.Protos.Peer;

namespace Hyperledger.Fabric.SDK
{
    /**
     * A wrapper for the Block returned in an Event
     *
     * @see Block
     */
    public class BlockEvent : BlockInfo
    {
//    private static final Log logger = LogFactory.getLog(BlockEvent.class);

    private readonly EventHub eventHub;
    private readonly Peer peer;
    private readonly Event evnt;

    /**
     * creates a BlockEvent object by parsing the input Block and retrieving its constituent Transactions
     *
     * @param eventHub a Hyperledger Fabric Block message
     * @throws InvalidProtocolBufferException
     * @see Block
     */
    public BlockEvent(EventHub eventHub, Event evnt) : base(evnt.Block)
    {
        this.eventHub = eventHub;
        this.peer = null;
        this.evnt = evnt;
    }

    public BlockEvent(Peer peer, DeliverResponse resp) : base(resp) {
        eventHub = null;
        this.peer = peer;
        this.evnt = null;

    }

    /**
     * Get the Event Hub that received the event.
     *
     * @return an Event Hub. Maybe null if new peer eventing services is being used.
     * @deprecated Use new peer eventing services
     */
    public EventHub EventHub =>eventHub;

    /**
     * The Peer that received this event.
     *
     * @return Peer that received this event. Maybe null if source is legacy event hub.
     */
    public Peer Peer => peer;

//    /**
//     * Raw proto buff event.
//     *
//     * @return Return raw protobuf event.
//     */
//
//    public Event getEvent() {
//        return event;
//    }

        private bool IsBlockEvent
        {
            get
            {
                if (peer != null)
                {
                    return true; //peer always returns Block type events;
                }
                return evnt != null && evnt.ShouldSerializeBlock();
            }
        }
    TransactionEvent TransactionEvent(int index)
    {
        TransactionEvent ret = null;

        EnvelopeInfo envelopeInfo = getEnvelopeInfo(index);
        if (envelopeInfo.getType() == EnvelopeType.TRANSACTION_ENVELOPE) {
            if (isFiltered()) {
                ret = new TransactionEvent(getEnvelopeInfo(index).filteredTx);
            } else {
                ret = new TransactionEvent((TransactionEnvelopeInfo) getEnvelopeInfo(index));
            }
        }

        return ret;
    }

    public class TransactionEvent extends TransactionEnvelopeInfo {
        TransactionEvent(TransactionEnvelopeInfo transactionEnvelopeInfo) {
            super(transactionEnvelopeInfo.getTransactionDeserializer());
        }

        TransactionEvent(PeerEvents.FilteredTransaction filteredTransaction) {
            super(filteredTransaction);
        }

        /**
         * The BlockEvent for this TransactionEvent.
         *
         * @return BlockEvent for this transaction.
         */

        public BlockEvent getBlockEvent() {

            return BlockEvent.this;

        }

        /**
         * The event hub that received this event.
         *
         * @return May return null if peer eventing service detected the event.
         * @deprecated use new peer eventing services {@link #getPeer()}
         */

        public EventHub getEventHub() {

            return BlockEvent.this.getEventHub();
        }

        /**
         * The peer that received this event.
         *
         * @return May return null if deprecated eventhubs are still being used, otherwise return the peer.
         */

        public Peer getPeer() {

            return BlockEvent.this.getPeer();
        }
    }

    List<TransactionEvent> getTransactionEventsList() {

        ArrayList<TransactionEvent> ret = new ArrayList<TransactionEvent>(getTransactionCount());
        for (TransactionEvent transactionEvent : getTransactionEvents()) {
            ret.add(transactionEvent);
        }

        return ret;

    }

    public Iterable<TransactionEvent> getTransactionEvents() {

        return new TransactionEventIterable();

    }

    class TransactionEventIterator implements Iterator<TransactionEvent> {
        final int max;
        int ci = 0;
        int returned = 0;

        TransactionEventIterator() {
            max = getTransactionCount();
        }

        @Override
        public boolean hasNext() {
            return returned < max;

        }

        @Override
        public TransactionEvent next() {

            TransactionEvent ret = null;
            // Filter for only transactions but today it's not really needed.
            //  Blocks with transactions only has transactions or a single pdate.
            try {
                do {

                    ret = getTransactionEvent(ci++);

                } while (ret == null);

            } catch (InvalidProtocolBufferException e) {
                throw new InvalidProtocolBufferRuntimeException(e);
            }
            ++returned;
            return ret;
        }
    }

    class TransactionEventIterable implements Iterable<TransactionEvent> {

        @Override
        public Iterator<TransactionEvent> iterator() {
            return new TransactionEventIterator();
        }
    }

} // BlockEvent
}
