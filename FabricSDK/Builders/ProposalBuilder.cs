/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using System.Text;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Requests;

namespace Hyperledger.Fabric.SDK.Builders
{
    public class ProposalBuilder
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(ProposalBuilder));
        private static readonly bool IS_DEBUG_LEVEL = logger.IsDebugEnabled();
        protected List<ByteString> argList;
        protected ChaincodeSpec.Types.Type ccType = ChaincodeSpec.Types.Type.Golang;

        private Protos.Peer.ChaincodeID chaincodeID;

        // The channel that is being targeted . note blank string means no specific channel
        private string channelID;
        protected TransactionContext context;
        protected TransactionRequest request;
        protected Dictionary<string, byte[]> transientMap;

        protected ProposalBuilder()
        {
        }

        public static ProposalBuilder Create()
        {
            return new ProposalBuilder();
        }

        public ProposalBuilder ChaincodeID(Protos.Peer.ChaincodeID ccodeID)
        {
            chaincodeID = ccodeID;
            return this;
        }

        public ProposalBuilder Args(List<ByteString> argLst)
        {
            argList = argLst;
            return this;
        }

        public ProposalBuilder AddArg(string arg)
        {
            if (argList == null)
                argList = new List<ByteString>();
            argList.Add(ByteString.CopyFromUtf8(arg));
            return this;
        }

        public ProposalBuilder AddArg(ByteString arg)
        {
            if (argList == null)
                argList = new List<ByteString>();
            argList.Add(arg);
            return this;
        }

        public void ClearArgs()
        {
            argList = null;
        }


        public ProposalBuilder Context(TransactionContext ctx)
        {
            context = ctx;
            if (null == channelID)
                channelID = ctx.Channel.Name; //Default to context channel.
            return this;
        }

        public ProposalBuilder Request(TransactionRequest req)
        {
            request = req;

            chaincodeID = req.ChaincodeID.FabricChaincodeID;

            switch (req.ChaincodeLanguage)
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
                    throw new ArgumentException("Requested chaincode type is not supported: " + req.ChaincodeLanguage);
            }

            transientMap = req.TransientMap;
            return this;
        }

        public virtual Proposal Build()
        {
            if (request != null && request.NoChannelID)
                channelID = "";
            return CreateFabricProposal(channelID, chaincodeID);
        }

        private Proposal CreateFabricProposal(string chID, Protos.Peer.ChaincodeID ccodeID)
        {
            if (null == transientMap)
                transientMap = new Dictionary<string, byte[]>();

            if (IS_DEBUG_LEVEL)
            {
                foreach (KeyValuePair<string, byte[]> tme in transientMap)
                    logger.Debug($"transientMap('{tme.Key.LogString()}', '{Encoding.UTF8.GetString(tme.Value).LogString()}'))");
            }

            ChaincodeHeaderExtension chaincodeHeaderExtension = new ChaincodeHeaderExtension {ChaincodeId = ccodeID};

            ChannelHeader chainHeader = ProtoUtils.CreateChannelHeader(HeaderType.EndorserTransaction, context.TxID, chID, context.Epoch, context.FabricTimestamp, chaincodeHeaderExtension, null);

            ChaincodeInvocationSpec chaincodeInvocationSpec = CreateChaincodeInvocationSpec(ccodeID, ccType);

            ChaincodeProposalPayload payload = new ChaincodeProposalPayload {Input = chaincodeInvocationSpec.ToByteString()};
            foreach (KeyValuePair<string, byte[]> pair in transientMap)
                payload.TransientMap.Add(pair.Key, ByteString.CopyFrom(pair.Value));
            Header header = new Header {SignatureHeader = ProtoUtils.GetSignatureHeaderAsByteString(context), ChannelHeader = chainHeader.ToByteString()};

            return new Proposal {Header = header.ToByteString(), Payload = payload.ToByteString()};
        }

        private ChaincodeInvocationSpec CreateChaincodeInvocationSpec(Protos.Peer.ChaincodeID ccodeID, ChaincodeSpec.Types.Type langType)
        {
            List<ByteString> allArgs = new List<ByteString>();

            if (argList != null && argList.Count > 0)
            {
                // If we already have an argList then the Builder subclasses have already set the arguments
                // for chaincodeInput. Accept the list and pass it on to the chaincodeInput builder
                // TODO need to clean this logic up so that common protobuf struct builds are in one place
                allArgs = argList;
            }
            else if (request != null)
            {
                // if argList is empty and we have a Request, build the chaincodeInput args array from the Request args and argbytes lists
                allArgs.Add(ByteString.CopyFromUtf8(request.Fcn));
                List<string> args = request.Args;
                if (args != null && args.Count > 0)
                {
                    foreach (string arg in args)
                        allArgs.Add(ByteString.CopyFromUtf8(arg));
                }

                // TODO currently assume that chaincodeInput args are strings followed by byte[].
                // Either agree with Fabric folks that this will always be the case or modify all Builders to expect
                // a List of Objects and determine if each list item is a string or a byte array
                List<byte[]> argBytes = request.ArgsBytes;
                if (argBytes != null && argBytes.Count > 0)
                {
                    foreach (byte[] arg in argBytes)
                        allArgs.Add(ByteString.CopyFrom(arg));
                }
            }

            if (IS_DEBUG_LEVEL)
            {
                StringBuilder logout = new StringBuilder(1000);

                logout.Append($"ChaincodeInvocationSpec type: {langType.ToString()}, chaincode name: {ccodeID.Name}, chaincode path: {ccodeID.Path}, chaincode version: {ccodeID.Version}");

                string sep = "";
                logout.Append(" args(");

                foreach (ByteString x in allArgs)
                {
                    logout.Append(sep).Append("\"").Append(x.ToStringUtf8().LogString()).Append("\"");
                    sep = ", ";
                }

                logout.Append(")");

                logger.Debug(logout.ToString);
            }

            ChaincodeInput chaincodeInput = new ChaincodeInput();
            chaincodeInput.Args.AddRange(allArgs);
            ChaincodeSpec chaincodeSpec = new ChaincodeSpec {Type = langType, ChaincodeId = ccodeID, Input = chaincodeInput};

            return new ChaincodeInvocationSpec {ChaincodeSpec = chaincodeSpec};
        }

        public ProposalBuilder CcType(ChaincodeSpec.Types.Type ctype)
        {
            ccType = ctype;
            return this;
        }
    }
}