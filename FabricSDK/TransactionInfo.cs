/*
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
 */

using System;
using System.Runtime.CompilerServices;
using Hyperledger.Fabric.SDK.Protos;
using Hyperledger.Fabric.SDK.Protos.Common;
using Hyperledger.Fabric.SDK.Protos.Protos;

namespace Hyperledger.Fabric.SDK
{

    /**
     * TransactionInfo contains the data from a {@link ProcessedTransaction} message
     */
    public class TransactionInfo
    {
        private string txID;
        private ProcessedTransaction processedTransaction;

        public TransactionInfo(string txID, ProcessedTransaction processedTransaction)
        {
            this.txID = txID;
            this.processedTransaction = processedTransaction;
        }

        /**
         * @return the transaction ID of this {@link ProcessedTransaction}
         */
        public string TransactionID => txID;

        /**
         * @return the {@link Envelope} of this {@link ProcessedTransaction}
         */
        public Envelope Envelope => processedTransaction.transactionEnvelope;

        /**
         * @return the raw {@link ProcessedTransaction}
         */
        public ProcessedTransaction ProcessedTransaction => processedTransaction;

        /**
         * @return the {@link TxValidationCode} of this {@link ProcessedTransaction}
         */
        public TxValidationCode ValidationCode => (TxValidationCode) processedTransaction.validationCode;

    }
}
