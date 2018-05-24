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
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.NetExtensions;


namespace Hyperledger.Fabric.SDK
{
    public class ChaincodeProposalPayloadDeserializer : BaseDeserializer<ChaincodeProposalPayload>
    {
        private WeakItem<ChaincodeInvocationSpecDeserializer, ChaincodeProposalPayload> invocationSpecDeserializer;

        public ChaincodeProposalPayloadDeserializer(ByteString byteString) : base(byteString)
        {
            invocationSpecDeserializer = new WeakItem<ChaincodeInvocationSpecDeserializer, ChaincodeProposalPayload>((input) => new ChaincodeInvocationSpecDeserializer(input.Input), () => Reference);
        }

        public ChaincodeProposalPayload ChaincodeProposalPayload => Reference;

        public ChaincodeInvocationSpecDeserializer ChaincodeInvocationSpec => invocationSpecDeserializer.Reference;
    }
}
