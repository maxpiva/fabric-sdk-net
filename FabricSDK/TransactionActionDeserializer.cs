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
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK
{
    public class TransactionActionDeserializer : BaseDeserializer<TransactionAction>
    {
        private readonly WeakItem<ChaincodeActionPayloadDeserializer, TransactionAction> chaincodeActionPayloadDeserializer;

        public TransactionActionDeserializer(ByteString byteString) : base(byteString)
        {
            chaincodeActionPayloadDeserializer = new WeakItem<ChaincodeActionPayloadDeserializer, TransactionAction>((tac) => new ChaincodeActionPayloadDeserializer(tac.Payload), () => Reference);
        }

        public TransactionActionDeserializer(TransactionAction transactionAction) : base(transactionAction)
        {
            chaincodeActionPayloadDeserializer = new WeakItem<ChaincodeActionPayloadDeserializer, TransactionAction>((tac) => new ChaincodeActionPayloadDeserializer(tac.Payload), () => Reference);
        }

        public TransactionAction TransactionAction => Reference;

        public ChaincodeActionPayloadDeserializer Payload => chaincodeActionPayloadDeserializer.Reference;
    }
}