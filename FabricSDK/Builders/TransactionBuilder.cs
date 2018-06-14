/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology - All Rights Reserved.
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

using System.Collections.Generic;
using System.Text;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Config = Hyperledger.Fabric.SDK.Helper.Config;

namespace Hyperledger.Fabric.SDK.Builders
{
    public class TransactionBuilder
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(TransactionBuilder));
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL ? Config.Instance.GetDiagnosticFileDumper() : null;

        private Proposal chaincodeProposal;
        private List<Endorsement> endorsements;
        private ByteString proposalResponsePayload;

        public static TransactionBuilder Create()
        {
            return new TransactionBuilder();
        }

        public TransactionBuilder ChaincodeProposal(Proposal chaincodeProposal)
        {
            this.chaincodeProposal = chaincodeProposal;
            return this;
        }

        public TransactionBuilder Endorsements(List<Endorsement> endorsements)
        {
            this.endorsements = endorsements;
            return this;
        }

        public TransactionBuilder ProposalResponsePayload(ByteString proposalResponsePayload)
        {
            this.proposalResponsePayload = proposalResponsePayload;
            return this;
        }

        public Payload Build()
        {
            return CreateTransactionCommonPayload(chaincodeProposal, proposalResponsePayload, endorsements);
        }

        private Payload CreateTransactionCommonPayload(Proposal chaincodeProposal, ByteString proposalResponsePayload, List<Endorsement> endorsements)
        {
            ChaincodeEndorsedAction chaincodeEndorsedAction = new ChaincodeEndorsedAction {ProposalResponsePayload = proposalResponsePayload};
            chaincodeEndorsedAction.Endorsements.AddRange(endorsements);
            //ChaincodeActionPayload
            ChaincodeActionPayload chaincodeActionPayload = new ChaincodeActionPayload {Action = chaincodeEndorsedAction};

            //We need to remove any transient fields - they are not part of what the peer uses to calculate hash.
            ChaincodeProposalPayload p = ChaincodeProposalPayload.Parser.ParseFrom(chaincodeProposal.Payload);
            ChaincodeProposalPayload chaincodeProposalPayloadNoTrans = new ChaincodeProposalPayload {Input = p.Input};
            chaincodeActionPayload.ChaincodeProposalPayload = chaincodeProposalPayloadNoTrans.ToByteString();


            TransactionAction transactionAction = new TransactionAction();

            Header header = Header.Parser.ParseFrom(chaincodeProposal.Header);

            if (Config.Instance.ExtraLogLevel(10))
            {
                if (null != diagnosticFileDumper)
                {
                    StringBuilder sb = new StringBuilder(10000);
                    sb.Append("transaction header bytes:" + header.ToByteString().ToHexString());
                    sb.Append("\n");
                    sb.Append("transaction header sig bytes:" + header.SignatureHeader.ToHexString());
                    logger.Trace("transaction header:  " + diagnosticFileDumper.CreateDiagnosticFile(sb.ToString()));
                }
            }

            transactionAction.Header = header.SignatureHeader;

            if (Config.Instance.ExtraLogLevel(10))
            {
                if (null != diagnosticFileDumper)
                {
                    logger.Trace("transactionActionBuilder.setPayload: " + diagnosticFileDumper.CreateDiagnosticFile(chaincodeActionPayload.ToByteString().ToHexString()));
                }
            }

            transactionAction.Payload = chaincodeActionPayload.ToByteString();

            //Transaction
            Protos.Peer.FabricTransaction.Transaction transaction = new Protos.Peer.FabricTransaction.Transaction();
            transaction.Actions.Add(transactionAction);

            return new Payload {Header = header, Data = transaction.ToByteString()};
        }
        
    }
}