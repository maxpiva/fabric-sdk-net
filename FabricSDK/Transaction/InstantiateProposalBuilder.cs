/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *        http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;

namespace Hyperledger.Fabric.SDK.Transaction
{
    public class InstantiateProposalBuilder : LSCCProposalBuilder
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(InstantiateProposalBuilder));
        protected string action = "deploy";

        private string chaincodeName;
        private string chaincodePath;

        private byte[] chaincodePolicy;
        private TransactionRequest.Type chaincodeType = TransactionRequest.Type.GO_LANG;
        private string chaincodeVersion;
        private List<string> iargList = new List<string>();

        protected InstantiateProposalBuilder()
        {
        }

        public void SetTransientMap(Dictionary<string, byte[]> transientMap)
        {
            this.transientMap = transientMap ?? throw new InvalidArgumentException("Transient map may not be null");
        }

        public new static InstantiateProposalBuilder Create()
        {
            return new InstantiateProposalBuilder();
        }

        public InstantiateProposalBuilder ChaincodePath(string chaincodePath)
        {
            this.chaincodePath = chaincodePath;
            return this;
        }

        public InstantiateProposalBuilder ChaincodeName(string chaincodeName)
        {
            this.chaincodeName = chaincodeName;
            return this;
        }

        public InstantiateProposalBuilder ChaincodeType(TransactionRequest.Type chaincodeType)
        {
            this.chaincodeType = chaincodeType;
            return this;
        }

        public void ChaincodeEndorsementPolicy(ChaincodeEndorsementPolicy policy)
        {
            if (policy != null)
            {
                chaincodePolicy = policy.ChaincodeEndorsementPolicyAsBytes;
            }
        }

        public InstantiateProposalBuilder Argss(List<string> argList)
        {
            iargList = argList;
            return this;
        }


        public override Proposal Build()
        {
            ConstructInstantiateProposal();
            return base.Build();
        }

        private void ConstructInstantiateProposal()
        {
            try
            {
                CreateNetModeTransaction();
            }
            catch (InvalidArgumentException exp)
            {
                logger.ErrorException(exp.Message, exp);
                throw;
            }
            catch (Exception exp)
            {
                logger.ErrorException(exp.Message, exp);
                throw new ProposalException("IO Error while creating install transaction", exp);
            }
        }

        private void CreateNetModeTransaction()
        {
            logger.Debug("NetModeTransaction");
            /*
            if (chaincodeType == null)
            {
                throw new InvalidArgumentException("Chaincode type is required");
            }
            */
            LinkedList<string> modlist = new LinkedList<string>();
            modlist.AddFirst("init");
            iargList.ForEach(a => modlist.AddAfter(modlist.Last, a));

            switch (chaincodeType)
            {
                case TransactionRequest.Type.JAVA:
                    CcType(ChaincodeSpec.Types.Type.Java);
                    break;
                case TransactionRequest.Type.NODE:
                    CcType(ChaincodeSpec.Types.Type.Node);
                    break;
                case TransactionRequest.Type.GO_LANG:
                    CcType(ChaincodeSpec.Types.Type.Golang);
                    break;
                default:
                    throw new InvalidArgumentException("Requested chaincode type is not supported: " + chaincodeType);
            }

            ChaincodeDeploymentSpec depspec = ProtoUtils.CreateDeploymentSpec(ccType, chaincodeName, chaincodePath, chaincodeVersion, modlist.ToList(), null);

            List<ByteString> argsList = new List<ByteString>();
            argsList.Add(ByteString.CopyFromUtf8(action));
            argsList.Add(ByteString.CopyFromUtf8(context.ChannelID));
            argsList.Add(depspec.ToByteString());
            if (chaincodePolicy != null)
            {
                argsList.Add(ByteString.CopyFrom(chaincodePolicy));
            }

            Args(argsList);
        }

        public void SetChaincodeVersion(string chaincodeVersion)
        {
            this.chaincodeVersion = chaincodeVersion;
        }
    }
}