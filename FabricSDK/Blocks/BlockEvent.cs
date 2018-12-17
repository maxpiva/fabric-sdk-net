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


using System.Collections.Generic;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Blocks
{
    /**
     * A wrapper for the Block returned in an Event
     *
     * @see Block
     */
    public class BlockEvent : BlockInfo
    {
        //    private static final Log logger = LogFactory.getLog(BlockEvent.class);

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
            EventHub = eventHub;
            Peer = null;
            this.evnt = evnt;
        }

        public BlockEvent(Peer peer, DeliverResponse resp) : base(resp)
        {
            EventHub = null;
            Peer = peer;
            evnt = null;
        }

        /**
         * Get the Event Hub that received the event.
         *
         * @return an Event Hub. Maybe null if new peer eventing services is being used.
         * @deprecated Use new peer eventing services
         */
        public EventHub EventHub { get; }

        /**
         * The Peer that received this event.
         *
         * @return Peer that received this event. Maybe null if source is legacy event hub.
         */
        public Peer Peer { get; }

        //    /**
        //     * Raw proto buff event.
        //     *
        //     * @return Return raw protobuf event.
        //     */
        //
        //    public Event getEvent() {
        //        return event;
        //    }

        public bool IsBlockEvent
        {
            get
            {
                if (Peer != null)
                {
                    return true; //peer always returns Block type events;
                }

                return evnt != null && evnt.EventCase == Event.EventOneofCase.Block;
            }
        }

        public IEnumerable<TransactionEvent> TransactionEvents => new EnumerableBuilder<TransactionEvent>(() => TransactionCount, GetTransactionEvent);

        public TransactionEvent GetTransactionEvent(int index)
        {
            TransactionEvent ret = null;

            EnvelopeInfo envelopeInfo = GetEnvelopeInfo(index);
            if (envelopeInfo.EnvelopeType == EnvelopeType.TRANSACTION_ENVELOPE)
            {
                ret = IsFiltered ? new TransactionEvent(this, envelopeInfo.FilteredTX) : new TransactionEvent(this, (TransactionEnvelopeInfo) envelopeInfo);
            }

            return ret;
        }

      
    } // BlockEvent
}