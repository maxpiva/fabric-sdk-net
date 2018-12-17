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

using System;
using System.Collections.Generic;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;

// ReSharper disable NotResolvedInText


namespace Hyperledger.Fabric.SDK.Blocks
{
    /**
     * BlockInfo contains the data from a {@link Block}
     */
    public class BlockInfo
    {


        private readonly BlockDeserializer block; //can be only one or the other.
        private readonly FilteredBlock filteredBlock;

        private int transactionCount = -1;

        public BlockInfo(Block block)
        {
            filteredBlock = null;
            this.block = new BlockDeserializer(block);
        }

        public BlockInfo(DeliverResponse resp)
        {
            if (resp.TypeCase == DeliverResponse.TypeOneofCase.Block)
            {
                Block respBlock = resp.Block;
                filteredBlock = null;
                if (respBlock == null)
                    throw new ArgumentNullException("DeliverResponse type block but block is null");
                block = new BlockDeserializer(respBlock);
            }
            else if (resp.TypeCase == DeliverResponse.TypeOneofCase.FilteredBlock)
            {
                filteredBlock = resp.FilteredBlock;
                block = null;
                if (filteredBlock == null)
                    throw new ArgumentNullException("DeliverResponse type filter block but filter block is null");
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
        public byte[] PreviousHash => IsFiltered ? null : block.PreviousHash.ToByteArray();

        /**
         * @return the {@link Block} data hash value and null if filtered block.
         */
        public byte[] DataHash => IsFiltered ? null : block.DataHash.ToByteArray();


        /**
         * @return the {@link Block} transaction metadata value return null if filtered block.
         */
        public byte[] TransActionsMetaData => IsFiltered ? null : block.TransActionsMetaData;


        /**
         * @return the {@link Block} index number
         */
        public long BlockNumber => IsFiltered ? (long) filteredBlock.Number : block.Number;


        /**
         * getEnvelopeCount
         *
         * @return the number of transactions in this block.
         */

        public int EnvelopeCount => IsFiltered ? filteredBlock.FilteredTransactions.Count : block.Data.Data.Count;

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

        public IEnumerable<EnvelopeInfo> EnvelopeInfos => new EnumerableBuilder<EnvelopeInfo>(() => IsFiltered ? filteredBlock.FilteredTransactions.Count : block.Data.Data.Count, GetEnvelopeInfo);


        /**
         * Return a specific envelope in the block by it's index.
         *
         * @param envelopeIndex
         * @return EnvelopeInfo that contains information on the envelope.
         * @throws InvalidProtocolBufferException
         */

        public EnvelopeInfo GetEnvelopeInfo(int envelopeIndex)
        {
            EnvelopeInfo ret;

            if (IsFiltered)
            {
                switch (filteredBlock.FilteredTransactions[envelopeIndex].Type)
                {
                    case HeaderType.EndorserTransaction:
                        ret = new TransactionEnvelopeInfo(this, filteredBlock.FilteredTransactions[envelopeIndex]);
                        break;
                    default: //just assume base properties.
                        ret = new EnvelopeInfo(this, filteredBlock.FilteredTransactions[envelopeIndex]);
                        break;
                }
            }
            else
            {
                EnvelopeDeserializer ed = EnvelopeDeserializer.Create(block.Block.Data.Data[envelopeIndex], block.TransActionsMetaData[envelopeIndex]);

                switch (ed.Type)
                {
                    case (int)HeaderType.EndorserTransaction:
                        ret = new TransactionEnvelopeInfo(this, (EndorserTransactionEnvDeserializer)ed);
                        break;
                    default: //just assume base properties.
                        ret = new EnvelopeInfo(this, ed);
                        break;
                }
            }

            return ret;
        }



        



    }
}