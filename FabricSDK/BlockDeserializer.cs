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

using System;
using System.Collections.Concurrent;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.NetExtensions;
using Hyperledger.Fabric.SDK.Protos.Common;

namespace Hyperledger.Fabric.SDK
{
    public class BlockDeserializer
    {
        private readonly Block block;
        private readonly ConcurrentDictionary<int, WeakReference<EnvelopeDeserializer>> envelopes = new ConcurrentDictionary<int, WeakReference<EnvelopeDeserializer>>();
        public Block Block => block;

        public BlockDeserializer(Block block)
        {
            this.block = block;
        }

        public byte[] PreviousHash
        {
            get
            {
                byte[] data = block.Header.DataHash;
                return block?.Header?.PreviousHash;
            }

        }

        public byte[] DataHash => block?.Header?.DataHash;
        public long Number => (long) (block?.Header?.Number ?? 0);


        public BlockData Data => block?.Data;

        public EnvelopeDeserializer GetData(int index)
        {
            EnvelopeDeserializer ret;
            if (index >= Data.Datas.Count)
            {
                return null;
            }

            if (envelopes.TryGetValue(index, out WeakReference<EnvelopeDeserializer> envelopeWeakReference))
            {
                envelopeWeakReference.TryGetTarget(out ret);
                if (ret != null)
                    return ret;
                envelopes.TryRemove(index, out envelopeWeakReference);
            }

            ret = EnvelopeDeserializer.Create(Data.Datas[index], TransActionsMetaData[index]);
            envelopes.TryAdd(index, new WeakReference<EnvelopeDeserializer>(ret));
            return ret;
        }


        public byte[] TransActionsMetaData => block.Metadata.Metadatas[(int)BlockMetadataIndex.TransactionsFilter];
    }
}

