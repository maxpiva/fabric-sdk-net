/*
 *
 *  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using System.Collections.Generic;
using Hyperledger.Fabric.Protos.Ledger.Rwset;
using Hyperledger.Fabric.Protos.Ledger.Rwset.Kvrwset;
using Hyperledger.Fabric.SDK.Helper;


namespace Hyperledger.Fabric.SDK
{
    public class TxReadWriteSetInfo
    {
        private readonly TxReadWriteSet txReadWriteSet;

        public TxReadWriteSetInfo(TxReadWriteSet txReadWriteSet) {
            this.txReadWriteSet = txReadWriteSet;
        }

        public int NsRwsetCount => txReadWriteSet.NsRwset.Count;
        
        public NsRwsetInfo GetNsRwsetInfo(int index) => new NsRwsetInfo(txReadWriteSet.NsRwset[index]);

        public IEnumerable<NsRwsetInfo> NsRwsetInfos => new BaseCollection<NsRwsetInfo>(()=>NsRwsetCount,GetNsRwsetInfo);

        public class NsRwsetInfo
        {
            private readonly NsReadWriteSet nsReadWriteSet;

            public NsRwsetInfo(NsReadWriteSet nsReadWriteSet) {

                this.nsReadWriteSet = nsReadWriteSet;
            }

            public KVRWSet Rwset => KVRWSet.Parser.ParseFrom(nsReadWriteSet.Rwset);
            public string Namespace => nsReadWriteSet.Namespace;

        }
    }
}
