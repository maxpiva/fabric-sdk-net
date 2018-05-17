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
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.NetExtensions;
using Hyperledger.Fabric.SDK.Protos.Peer.FabricTransaction;

namespace Hyperledger.Fabric.SDK
{
    public class ChaincodeActionPayloadDeserializer : BaseDeserializer<ChaincodeActionPayload>
    {
        private WeakReference<ChaincodeEndorsedActionDeserializer> chaincodeEndorsedActionDeserializer;
        private WeakReference<ChaincodeProposalPayloadDeserializer> chaincodeProposalPayloadDeserializer;

        public ChaincodeActionPayloadDeserializer(byte[] byteString) : base(byteString)
        {
        }

        public ChaincodeActionPayload ChaincodeActionPayload => Reference;
        public ChaincodeEndorsedActionDeserializer Action => ChaincodeActionPayload.Action.GetOrCreateWR(ref chaincodeEndorsedActionDeserializer, (action) => new ChaincodeEndorsedActionDeserializer(action));
        public ChaincodeProposalPayloadDeserializer ChaincodeProposalPayload => ChaincodeActionPayload.ChaincodeProposalPayload.GetOrCreateWR(ref chaincodeProposalPayloadDeserializer, (payload) => new ChaincodeProposalPayloadDeserializer(payload));
    }
}
