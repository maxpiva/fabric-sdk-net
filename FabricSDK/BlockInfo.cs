/*
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */
/*
package org.hyperledger.fabric.sdk;

import java.util.ArrayList;
import java.util.Date;
import java.util.Iterator;
import java.util.List;

import com.google.protobuf.ByteString;
import com.google.protobuf.InvalidProtocolBufferException;
import org.hyperledger.fabric.protos.common.Common;
import org.hyperledger.fabric.protos.common.Common.Block;
import org.hyperledger.fabric.protos.ledger.rwset.Rwset.TxReadWriteSet;
import org.hyperledger.fabric.protos.msp.Identities;
import org.hyperledger.fabric.protos.peer.Chaincode.ChaincodeInput;
import org.hyperledger.fabric.protos.peer.FabricTransaction;
import org.hyperledger.fabric.protos.peer.PeerEvents;
import org.hyperledger.fabric.protos.peer.PeerEvents.FilteredTransaction;
import org.hyperledger.fabric.sdk.exception.InvalidProtocolBufferRuntimeException;
import org.hyperledger.fabric.sdk.transaction.ProtoUtils;

import static java.lang.String.format;
import static org.hyperledger.fabric.protos.peer.FabricProposalResponse.Endorsement;
import static org.hyperledger.fabric.sdk.BlockInfo.EnvelopeType.TRANSACTION_ENVELOPE;
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.NetExtensions;
using Hyperledger.Fabric.SDK.Protos.Common;
using Hyperledger.Fabric.SDK.Protos.Ledger.Rwset;
using Hyperledger.Fabric.SDK.Protos.Msp;
using Hyperledger.Fabric.SDK.Protos.Peer;
using Hyperledger.Fabric.SDK.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.SDK.Transaction;


namespace Hyperledger.Fabric.SDK
{
    public enum EnvelopeType
    {
        TRANSACTION_ENVELOPE,
        ENVELOPE
    }

    /**
     * BlockInfo contains the data from a {@link Block}
     */
    public class BlockInfo
    {
        private readonly BlockDeserializer block; //can be only one or the other.
        private readonly Protos.Peer.FilteredBlock filteredBlock;

        public BlockInfo(Block block)
        {

            filteredBlock = null;
            this.block = new BlockDeserializer(block);
        }

        public BlockInfo(DeliverResponse resp)
        {


            if (resp.ShouldSerializeBlock())
            {
                Block respBlock = resp.Block;
                filteredBlock = null;
                if (respBlock == null)
                {
                    throw new ArgumentNullException("DeliverResponse type block but block is null");
                }

                this.block = new BlockDeserializer(respBlock);
            }
            else if (resp.ShouldSerializeFilteredBlock())
            {
                filteredBlock = resp.FilteredBlock;
                block = null;
                if (filteredBlock == null)
                {
                    throw new ArgumentNullException("DeliverResponse type filter block but filter block is null");
                }

            }
            else
            {
                throw new ArgumentException($"DeliverResponse type has unexpected type");
            }

        }

        public bool IsFiltered
        {
            get
            {
                if (filteredBlock == null && block == null)
                {
                    throw new ArgumentException("Both block and filter is null.");
                }

                if (filteredBlock != null && block != null)
                {
                    throw new ArgumentException("Both block and filter are set.");
                }

                return filteredBlock != null;
            }
        }

        public string ChannelId => IsFiltered ? filteredBlock.ChannelId : GetEnvelopeInfo(0).ChannelId;


        /**
         * @return the raw {@link Block}
         */
        public Block Block => IsFiltered ? null : block.Block;

        /**
         * @return the raw {@link org.hyperledger.fabric.protos.peer.PeerEvents.FilteredBlock}
         */
        public FilteredBlock FilteredBlock => !IsFiltered ? null : filteredBlock;

        /**
         * @return the {@link Block} previousHash value and null if filtered block.
         */
        public byte[] PreviousHash => IsFiltered ? null : block.PreviousHash;

        /**
         * @return the {@link Block} data hash value and null if filtered block.
         */
        public byte[] DataHash => IsFiltered ? null : block.DataHash;


        /**
         * @return the {@link Block} transaction metadata value return null if filtered block.
         */
        public byte[] TransActionsMetaData => IsFiltered ? null : block.TransActionsMetaData;


        /**
         * @return the {@link Block} index number
         */
        public long BlockNumber => IsFiltered ? (long) filteredBlock.Number : (long) block.Number;


        /**
         * getEnvelopeCount
         *
         * @return the number of transactions in this block.
         */

        public int EnvelopeCount => IsFiltered ? filteredBlock.FilteredTransactions.Count : block.Data.Datas.Count;

        private int transactionCount = -1;

        /**
         * Number of endorser transaction found in the block.
         *
         * @return Number of endorser transaction found in the block.
         */

        public int TransactionCount
        {
            get
            {
                if (IsFiltered)
                {

                    int ltransactionCount = transactionCount;
                    if (ltransactionCount < 0)
                    {
                        ltransactionCount = 0;

                        for (int i = filteredBlock.FilteredTransactions.Count - 1; i >= 0; --i)
                        {
                            if (filteredBlock.FilteredTransactions[i].Type == HeaderType.EndorserTransaction)
                            {
                                ++ltransactionCount;
                            }
                        }

                        transactionCount = ltransactionCount;
                    }

                    return transactionCount;
                }
                else
                {
                    int ltransactionCount = transactionCount;
                    if (ltransactionCount < 0)
                    {

                        ltransactionCount = 0;
                        for (int i = EnvelopeCount - 1; i >= 0; --i)
                        {
                            try
                            {
                                EnvelopeInfo envelopeInfo = GetEnvelopeInfo(i);
                                if (envelopeInfo.EnvelopeType == EnvelopeType.TRANSACTION_ENVELOPE)
                                {
                                    ++ltransactionCount;
                                }
                            }
                            catch (Exception e)
                            {
                                throw new InvalidProtocolBufferRuntimeException(e);
                            }
                        }

                        transactionCount = ltransactionCount;
                    }

                    return transactionCount;
                }

            }
        }



        /**
         * Wrappers Envelope
         */

        public class EnvelopeInfo
        {
            private readonly EnvelopeDeserializer envelopeDeserializer;
            private readonly HeaderDeserializer headerDeserializer;
            protected readonly FilteredTransaction filteredTx;
            protected readonly BlockInfo parent;

            /**
             * This block is filtered
             *
             * @return true if it's filtered.
             */
            public bool IsFiltered => filteredTx != null;

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
             * Get channel id
             *
             * @return The channel id also referred to as channel name.
             */
            public string ChannelId => parent.IsFiltered ? parent.FilteredBlock.ChannelId : headerDeserializer.ChannelHeader.ChannelId;


            public class IdentitiesInfo
            {
                /**
                 * The MSPId of the user.
                 *
                 * @return The MSPid of the user.
                 */
                public string Mspid { get; private set; }

                /**
                 * The identification of the identity usually the certificate.
                 *
                 * @return The certificate of the user in PEM format.
                 */
                public string Id { get; private set; }



                public IdentitiesInfo(SerializedIdentity identity)
                {
                    Mspid = identity.Mspid;
                    Id = Encoding.UTF8.GetString(identity.IdBytes);
                }

            }

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

            public DateTime? Timestamp => parent.IsFiltered ? null : headerDeserializer.ChannelHeader.Timestamp;

            /**
             * @return whether this Transaction is marked as TxValidationCode.VALID
             */
            public bool IsValid => parent.IsFiltered ? (filteredTx.TxValidationCode == TxValidationCode.Valid) : envelopeDeserializer.IsValid;

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



        /**
         * Return a specific envelope in the block by it's index.
         *
         * @param envelopeIndex
         * @return EnvelopeInfo that contains information on the envelope.
         * @throws InvalidProtocolBufferException
         */

        public EnvelopeInfo GetEnvelopeInfo(int envelopeIndex)
        {

            try
            {

                EnvelopeInfo ret;

                if (IsFiltered)
                {

                    switch (filteredBlock.FilteredTransactions[envelopeIndex].Type)
                    {
                        case HeaderType.EndorserTransaction:
                            ret = new TransactionEnvelopeInfo(this, this.filteredBlock.FilteredTransactions[envelopeIndex]);
                            break;
                        default: //just assume base properties.
                            ret = new EnvelopeInfo(this, this.filteredBlock.FilteredTransactions[envelopeIndex]);
                            break;
                    }

                }
                else
                {

                    EnvelopeDeserializer ed = EnvelopeDeserializer.Create(block.Block.Data.Datas[envelopeIndex], block.TransActionsMetaData[envelopeIndex]);

                    switch (ed.Type)
                    {
                        case (int) HeaderType.EndorserTransaction:
                            ret = new TransactionEnvelopeInfo(this, (EndorserTransactionEnvDeserializer) ed);
                            break;
                        default: //just assume base properties.
                            ret = new EnvelopeInfo(this, ed);
                            break;

                    }

                }

                return ret;

            }
            catch (Exception e)
            {
                throw e;
            }

        }

        /**
         * Return and iterable EnvelopeInfo over each Envelope contained in the Block
         *
         * @return
         */



        public class TransactionEnvelopeInfo : EnvelopeInfo
        {
            protected readonly EndorserTransactionEnvDeserializer transactionDeserializer;

            public TransactionEnvelopeInfo(BlockInfo parent, FilteredTransaction filteredTx) : base(parent, filteredTx)
            {
                this.transactionDeserializer = null;
            }

            /**
             * Signature for the transaction.
             *
             * @return byte array that as the signature.
             */
            public byte[] Signature => transactionDeserializer.Signature;

            public TransactionEnvelopeInfo(BlockInfo parent, EndorserTransactionEnvDeserializer transactionDeserializer) : base(parent, transactionDeserializer)
            {

                this.transactionDeserializer = transactionDeserializer;
            }

            public EndorserTransactionEnvDeserializer TransactionDeserializer => transactionDeserializer;

            public int TransactionActionInfoCount => parent.IsFiltered ? filteredTx.TransactionActions.ChaincodeActions.Count : transactionDeserializer.Payload.Transaction.ActionsCount;



            public class TransactionActionInfo
            {
                private BlockInfo parent;
                private readonly TransactionActionDeserializer transactionAction;
                private readonly FilteredChaincodeAction filteredAction;
                private WeakCache<EndorserInfo, int> infos = new WeakCache<EndorserInfo, int>();

                public bool IsFiltered => filteredAction != null;

                public TransactionActionInfo(BlockInfo parent, TransactionActionDeserializer transactionAction)
                {
                    this.parent = parent;
                    this.transactionAction = transactionAction;
                    filteredAction = null;
                    infos.SetCreationFunction((index) => new EndorserInfo(transactionAction.Payload.Action.ChaincodeEndorsedAction.Endorsements[index]));
                }

                public TransactionActionInfo(BlockInfo parent, FilteredChaincodeAction filteredAction)
                {
                    this.parent = parent;
                    this.filteredAction = filteredAction;
                    transactionAction = null;
                    infos.SetCreationFunction((index) => new EndorserInfo(transactionAction.Payload.Action.ChaincodeEndorsedAction.Endorsements[index]));
                }

                public byte[] ResponseMessageBytes => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseMessage.ToBytes();

                public string ResponseMessage => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseMessage;

                public int ResponseStatus => IsFiltered ? -1 : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseStatus;
                public int ChaincodeInputArgsCount => IsFiltered ? 0 : transactionAction.Payload.ChaincodeProposalPayload.ChaincodeInvocationSpec.ChaincodeInput.ChaincodeInput.Args.Count;
                public byte[] GetChaincodeInputArgs(int index) => IsFiltered ? null : transactionAction.Payload.ChaincodeProposalPayload.ChaincodeInvocationSpec.ChaincodeInput.ChaincodeInput.Args[index];
                public int EndorsementsCount => IsFiltered ? 0 : transactionAction.Payload.Action.EndorsementsCount;

                public EndorserInfo GetEndorsementInfo(int index) => IsFiltered ? null : infos.Get(index);

                public byte[] ProposalResponseMessageBytes => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseMessage.ToBytes();

                public byte[] ProposalResponsePayload => IsFiltered ? null : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponsePayload;

                public int ProposalResponseStatus => IsFiltered ? -1 : transactionAction.Payload.Action.ProposalResponsePayload.Extension.ResponseStatus;

                /**
                 * Get read write set for this transaction. Will return null on for Eventhub events.
                 * For eventhub events find the block by block number to get read write set if needed.
                 *
                 * @return Read write set.
                 */

                public TxReadWriteSetInfo TxReadWriteSet
                {
                    get
                    {
                        if (parent.IsFiltered)
                        {
                            return null;

                        }

                        TxReadWriteSet txReadWriteSet = transactionAction.Payload.Action.ProposalResponsePayload.Extension.Results;
                        if (txReadWriteSet == null)
                        {
                            return null;
                        }

                        return new TxReadWriteSetInfo(txReadWriteSet);

                    }
                }

                /**
                 * Get chaincode events for this transaction.
                 *
                 * @return A chaincode event if the chaincode set an event otherwise null.
                 */

                public ChaincodeEvent Event
                {
                    get
                    {
                        if (IsFiltered)
                            return new ChaincodeEvent(filteredAction.ChaincodeEvent.SerializeProtoBuf());
                        return transactionAction.Payload.Action.ProposalResponsePayload.Extension.Event;
                    }

                }
            }
        }

        public IEnumerable<EnvelopeInfo> EnvelopeInfos => new BaseCollection<EnvelopeInfo>(() => IsFiltered ? filteredBlock.FilteredTransactions.Count : block.Data.Datas.Count, GetEnvelopeInfo);


        public class EndorserInfo
        {
            private readonly Endorsement endorsement;

            public EndorserInfo(Endorsement endorsement)
            {

                this.endorsement = endorsement;
            }

            public byte[] Signature => endorsement.Signature;

            /**
             * @return
             * @deprecated use getId and getMspid
             */
            public byte[] Endorser => endorsement.Endorser;

            public string Id => endorsement.Endorser.DeserializeProtoBuf<SerializedIdentity>().IdBytes.ToUTF8String();

            public string Mspid => endorsement.Endorser.DeserializeProtoBuf<SerializedIdentity>().Mspid;
        }
    }
}
