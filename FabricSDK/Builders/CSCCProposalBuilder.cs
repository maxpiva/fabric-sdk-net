/*
 *  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;

namespace Hyperledger.Fabric.SDK.Builders
{
    public class CSCCProposalBuilder : ProposalBuilder
    {
        private static readonly string CSCC_CHAIN_NAME = "cscc";
        private static readonly Protos.Peer.ChaincodeID CHAINCODE_ID_CSCC = new Protos.Peer.ChaincodeID {Name = CSCC_CHAIN_NAME};

        public new CSCCProposalBuilder Context(TransactionContext context)
        {
            base.Context(context);
            return this;
        }


        public override Proposal Build()
        {
            CcType(ChaincodeSpec.Types.Type.Golang);
            ChaincodeID(CHAINCODE_ID_CSCC);
            return base.Build();
        }
    }
}