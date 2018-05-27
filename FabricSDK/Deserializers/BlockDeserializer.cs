/*
 *
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
 *
 */

using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Deserializers
{
    public class BlockDeserializer
    {
        private readonly WeakDictionary<int, EnvelopeDeserializer> envelopes;

        public BlockDeserializer(Block block)
        {
            Block = block;
            envelopes = new WeakDictionary<int, EnvelopeDeserializer>((index) => EnvelopeDeserializer.Create(Data?.Data[index], TransActionsMetaData[index]));
        }

        public Block Block { get; }

        public ByteString PreviousHash
        {
            get
            {
                ByteString _ = Block.Header.DataHash;
                return Block?.Header?.PreviousHash;
            }
        }

        public ByteString DataHash => Block?.Header?.DataHash;
        public long Number => (long) (Block?.Header?.Number ?? 0);
        public BlockData Data => Block?.Data;
        public byte[] TransActionsMetaData => Block?.Metadata?.Metadata[(int) BlockMetadataIndex.TransactionsFilter].ToByteArray();
        public EnvelopeDeserializer GetData(int index) => envelopes.Get(index);
    }
}