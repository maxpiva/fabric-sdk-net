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

using Hyperledger.Fabric.SDK.Protos.Peer.FabricProposal;

namespace Hyperledger.Fabric.SDK.Transaction
{

    public class LSCCProposalBuilder : ProposalBuilder
    {
        private static readonly string LSCC_CHAIN_NAME = "lscc";
        private static readonly Protos.Peer.ChaincodeID CHAINCODE_ID_LSCC = new Protos.Peer.ChaincodeID {Name = LSCC_CHAIN_NAME};

        public new LSCCProposalBuilder Context(TransactionContext context)
        {
            base.Context(context);
            return this;
        }

        public override Proposal Build()
        {

            ChaincodeID(CHAINCODE_ID_LSCC);
            return base.Build();
        }
    }
}
