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
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Deserializers
{
    public class ChaincodeEndorsedActionDeserializer : BaseDeserializer<ChaincodeEndorsedAction>
    {
        private readonly WeakItem<ProposalResponsePayloadDeserializer, ChaincodeEndorsedAction> proposalResponsePayload;

        public ChaincodeEndorsedActionDeserializer(ChaincodeEndorsedAction action) : base(action.ToByteString())
        {
            reference = new WeakReference<ChaincodeEndorsedAction>(action);
            proposalResponsePayload = new WeakItem<ProposalResponsePayloadDeserializer, ChaincodeEndorsedAction>((payload) => new ProposalResponsePayloadDeserializer(payload.ProposalResponsePayload), () => Reference);
        }

        public ChaincodeEndorsedAction ChaincodeEndorsedAction => Reference;

        public int EndorsementsCount => ChaincodeEndorsedAction?.Endorsements?.Count ?? 0;


        public List<Endorsement> Endorsements => ChaincodeEndorsedAction?.Endorsements.ToList();

        public ProposalResponsePayloadDeserializer ProposalResponsePayload => proposalResponsePayload.Reference;

        public byte[] GetEndorsementSignature(int index)
        {
            return ChaincodeEndorsedAction?.Endorsements[index]?.Signature.ToByteArray();
        }
    }
}