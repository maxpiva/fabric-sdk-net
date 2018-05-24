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

using System.Collections.Generic;
using System.Text;
using Google.Protobuf;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.NetExtensions;

namespace Hyperledger.Fabric.SDK.Transaction
{
    public class GetConfigBlockBuilder : CSCCProposalBuilder
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(GetConfigBlockBuilder));
        private GetConfigBlockBuilder()
        {
            AddArg("GetConfigBlock");
        }

        public GetConfigBlockBuilder ChannelId(string channelId)
        {
            if (channelId == null)
            {
                ProposalException exp = new ProposalException("Parameter channelId needs to be non-empty string .");
                logger.ErrorException(exp.Message, exp);
                throw exp;
            }

            AddArg(channelId);
            return this;
        }

        public new GetConfigBlockBuilder Context(TransactionContext context)
        {
            base.Context(context);
            return this;
        }

        public new static GetConfigBlockBuilder Create()
        {
            return new GetConfigBlockBuilder();
        }
    }
}