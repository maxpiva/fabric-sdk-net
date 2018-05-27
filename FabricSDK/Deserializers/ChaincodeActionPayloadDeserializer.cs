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
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Deserializers
{
    public class ChaincodeActionPayloadDeserializer : BaseDeserializer<ChaincodeActionPayload>
    {
        private readonly WeakItem<ChaincodeEndorsedActionDeserializer, ChaincodeActionPayload> chaincodeEndorsedActionDeserializer;
        private readonly WeakItem<ChaincodeProposalPayloadDeserializer, ChaincodeActionPayload> chaincodeProposalPayloadDeserializer;

        public ChaincodeActionPayloadDeserializer(ByteString byteString) : base(byteString)
        {
            chaincodeEndorsedActionDeserializer = new WeakItem<ChaincodeEndorsedActionDeserializer, ChaincodeActionPayload>((action) => new ChaincodeEndorsedActionDeserializer(action.Action), () => Reference);
            chaincodeProposalPayloadDeserializer = new WeakItem<ChaincodeProposalPayloadDeserializer, ChaincodeActionPayload>((payload) => new ChaincodeProposalPayloadDeserializer(payload.ChaincodeProposalPayload), () => Reference);
        }

        public ChaincodeActionPayload ChaincodeActionPayload => Reference;
        public ChaincodeEndorsedActionDeserializer Action => chaincodeEndorsedActionDeserializer.Reference;
        public ChaincodeProposalPayloadDeserializer ChaincodeProposalPayload => chaincodeProposalPayloadDeserializer.Reference;
    }
}