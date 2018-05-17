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
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.NetExtensions;
using Hyperledger.Fabric.SDK.Protos.Peer;
using Hyperledger.Fabric.SDK.Protos.Peer.FabricTransaction;

namespace Hyperledger.Fabric.SDK
{
    public class ChaincodeEndorsedActionDeserializer : BaseDeserializer<ChaincodeEndorsedAction>
    {
        private WeakReference<ProposalResponsePayloadDeserializer> proposalResponsePayload;

        public ChaincodeEndorsedActionDeserializer(ChaincodeEndorsedAction action) : base(action.SerializeProtoBuf())
        {
            reference = new WeakReference<ChaincodeEndorsedAction>(action);
        }

        public ChaincodeEndorsedAction ChaincodeEndorsedAction => byteString.GetOrDeserializeProtoBufWR(ref chaincodeEndorsedAction);

        public int EndorsementsCount => ChaincodeEndorsedAction?.Endorsements?.Count ?? 0;


        public List<Endorsement> Endorsements => ChaincodeEndorsedAction?.Endorsements;

        public byte[] GetEndorsementSignature(int index)
        {

            return ChaincodeEndorsedAction?.Endorsements[index]?.Signature;
        }

        public ProposalResponsePayloadDeserializer ProposalResponsePayload => ChaincodeEndorsedAction.ProposalResponsePayload.GetOrCreateWR(ref proposalResponsePayload, (payload) => new ProposalResponsePayloadDeserializer(payload));
    }
}
