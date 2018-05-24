/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using Hyperledger.Fabric.SDK.Exceptions;

namespace Hyperledger.Fabric.SDK
{

    public class QueryByChaincodeRequest : TransactionRequest
    {
        private QueryByChaincodeRequest(IUser userContext) : base(userContext)
        {
        }

        public static QueryByChaincodeRequest Create(IUser userContext)
        {
            return new QueryByChaincodeRequest(userContext);
        }

        /**
         * Transient data added to the proposal that is not added to the ledger.
         *
         * @param transientMap Map of strings to bytes that's added to the proposal
         * @throws InvalidArgumentException if the argument is null.
         */
        public void SetTransientMap(Dictionary<string, byte[]> transientMap)
        {
            this.transientMap = transientMap ?? throw new InvalidArgumentException("Transient map may not be set to null");
        }
    }
}
