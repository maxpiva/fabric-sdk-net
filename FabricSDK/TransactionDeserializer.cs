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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Google.Protobuf;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.NetExtensions;

namespace Hyperledger.Fabric.SDK
{
    public class TransactionDeserializer : BaseDeserializer<Protos.Peer.FabricTransaction.Transaction>
    {
        private readonly WeakDictionary<int, TransactionActionDeserializer> transactionActions;

        public TransactionDeserializer(ByteString byteString) : base(byteString)
        {
            transactionActions = new WeakDictionary<int, TransactionActionDeserializer>((index) => new TransactionActionDeserializer(Transaction.Actions[index]));
        }

        public Protos.Peer.FabricTransaction.Transaction Transaction => Reference;

        public int ActionsCount => Transaction?.Actions?.Count ?? 0;


        public TransactionActionDeserializer GetTransactionAction(int index)
        {
            return transactionActions.Get(index);
        }

        private IEnumerable<TransactionActionDeserializer> TransactionActions => new BaseCollection<TransactionActionDeserializer>(() => ActionsCount, GetTransactionAction);

    }
}
