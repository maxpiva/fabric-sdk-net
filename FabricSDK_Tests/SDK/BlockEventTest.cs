/*
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.Protos.Peer.PeerEvents;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK
{

    [TestClass]
    [TestCategory("SDK")]
    public class BlockEventTest
    {
        private static Block block, badBlock;
        private static BlockHeader blockHeader;
        private static BlockData blockData;
        private static BlockMetadata blockMetadata;
        private static EventHub eventHub;
        private static Event goodEventBlock;
        private static Event badEventBlock;


        [ClassInitialize]
        public static void SetUpBeforeClass(TestContext context)
        {

            eventHub = new EventHub("test", "grpc://lh:99", null);

            // build a block with 3 transactions, set transaction 1,3 as valid, transaction 2 as invalid

            BlockData blockDataBuilder = new BlockData();
            Payload payloadBuilder = new Payload();
            ChannelHeader channelHeaderBuilder = new ChannelHeader();
            channelHeaderBuilder.Type = (int) HeaderType.EndorserTransaction;
            Header headerBuilder = new Header();
            Envelope envelopeBuilder = new Envelope();

            channelHeaderBuilder.ChannelId = "TESTCHANNEL";

            // transaction 1
            channelHeaderBuilder.TxId = "TRANSACTION1";
            headerBuilder.ChannelHeader = channelHeaderBuilder.ToByteString();
            payloadBuilder.Header = new Header(headerBuilder);
            payloadBuilder.Data = ByteString.CopyFrom("test data".ToBytes());
            envelopeBuilder.Payload = payloadBuilder.ToByteString();
            blockDataBuilder.Data.Add(envelopeBuilder.ToByteString());

            // transaction 2
            channelHeaderBuilder.TxId = "TRANSACTION2";
            headerBuilder.ChannelHeader = channelHeaderBuilder.ToByteString();
            payloadBuilder.Header = new Header(headerBuilder);
            payloadBuilder.Data = ByteString.CopyFrom("test data".ToBytes());
            envelopeBuilder.Payload = payloadBuilder.ToByteString();
            blockDataBuilder.Data.Add(envelopeBuilder.ToByteString());

            // transaction 3
            channelHeaderBuilder.TxId = "TRANSACTION3";
            headerBuilder.ChannelHeader = channelHeaderBuilder.ToByteString();
            payloadBuilder.Header = new Header(headerBuilder);
            payloadBuilder.Data = ByteString.CopyFrom("test data".ToBytes());
            envelopeBuilder.Payload = payloadBuilder.ToByteString();

            blockDataBuilder.Data.Add(envelopeBuilder.ToByteString());
            // blockData with 3 envelopes
            blockData = new BlockData(blockDataBuilder);

            // block header
            BlockHeader blockHeaderBuilder = new BlockHeader();
            blockHeaderBuilder.Number = 1;
            blockHeaderBuilder.PreviousHash = ByteString.CopyFrom("previous_hash".ToBytes());
            blockHeaderBuilder.DataHash = ByteString.CopyFrom("data_hash".ToBytes());
            blockHeader = new BlockHeader(blockHeaderBuilder);

            // block metadata
            BlockMetadata blockMetadataBuilder = new BlockMetadata();
            blockMetadataBuilder.Metadata.Add(ByteString.CopyFrom("signatures".ToBytes())); //BlockMetadataIndex.SIGNATURES_VALUE
            blockMetadataBuilder.Metadata.Add(ByteString.CopyFrom("last_config".ToBytes())); //BlockMetadataIndex.LAST_CONFIG_VALUE,
            // mark 2nd transaction in block as invalid
            byte[] txResultsMap = new byte[] {(byte) TxValidationCode.Valid, (byte) TxValidationCode.InvalidOtherReason, (byte) TxValidationCode.Valid};
            blockMetadataBuilder.Metadata.Add(ByteString.CopyFrom(txResultsMap)); //BlockMetadataIndex.TRANSACTIONS_FILTER_VALUE
            blockMetadataBuilder.Metadata.Add(ByteString.CopyFrom("orderer".ToBytes())); //BlockMetadataIndex.ORDERER_VALUE
            blockMetadata = new BlockMetadata(blockMetadataBuilder);

            Block blockBuilder = new Block();
            blockBuilder.Data = new BlockData(blockData);
            blockBuilder.Header = new BlockHeader(blockHeader);
            blockBuilder.Metadata = new BlockMetadata(blockMetadata);
            block = new Block(blockBuilder);

            goodEventBlock = new Event {Block = new Block(blockBuilder)};

            // block with bad header
            headerBuilder.ChannelHeader = ByteString.CopyFrom("bad channel header".ToBytes());
            payloadBuilder.Header = new Header(headerBuilder);
            payloadBuilder.Data = ByteString.CopyFrom("test data".ToBytes());
            envelopeBuilder.Payload = payloadBuilder.ToByteString();
            blockDataBuilder.Data.Clear();
            blockDataBuilder.Data.Add(envelopeBuilder.ToByteString());
            blockBuilder.Data = new BlockData(blockDataBuilder);
            badBlock = new Block(blockBuilder);
            badEventBlock = new Event {Block = new Block(badBlock)};
        }

        [TestMethod]
        public void TestBlockEvent()
        {
            try
            {
                BlockEvent be = new BlockEvent(eventHub, goodEventBlock);
                Assert.AreEqual(be.ChannelId, "TESTCHANNEL");
                CollectionAssert.AreEqual(be.Block.ToByteArray(), block.ToByteArray());
                List<BlockEvent.TransactionEvent> txList = be.TransactionEvents.ToList();
                Assert.AreEqual(txList.Count, 3);
                BlockEvent.TransactionEvent te = txList[1];
                Assert.IsFalse(te.IsValid);
                Assert.AreEqual(te.ValidationCode, (byte) TxValidationCode.InvalidOtherReason);
                te = txList[2];
                Assert.IsTrue(te.IsValid);
            }
            catch (InvalidProtocolBufferException e)
            {
                Assert.Fail($"did not parse Block correctly.Error: {e.Message}");
            }
        }

        // Bad block input causes constructor to throw exception
        [TestMethod]
        [ExpectedException(typeof(InvalidProtocolBufferException))]
        public void TestBlockEventBadBlock()
        {
            BlockEvent be = new BlockEvent(eventHub, badEventBlock);
            string _ = be.ChannelId;
        }

    }

}
