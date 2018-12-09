/*
 *  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

namespace Hyperledger.Fabric.SDK.Builders
{

    public class QueryInstalledChaincodesBuilder : LSCCProposalBuilder
    {
//    private static final Log logger = LogFactory.getLog(QueryInstalledChaincodesBuilder.class);

        private QueryInstalledChaincodesBuilder()
        {
            AddArg("getinstalledchaincodes");
        }

        public new QueryInstalledChaincodesBuilder Context(TransactionContext ctx)
        {
            base.Context(ctx);
            return this;
        }

        public new static QueryInstalledChaincodesBuilder Create()
        {
            return new QueryInstalledChaincodesBuilder();
        }
    }
}
