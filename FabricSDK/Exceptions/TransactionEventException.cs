/*
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using System;

namespace Hyperledger.Fabric.SDK.Exceptions
{
    public class TransactionEventException : TransactionException
    {
        public TransactionEvent TransactionEvent { get; }
        /**
           * save the TransactionEvent in the exception so that caller can use for debugging
           *
           * @param message
           * @param transactionEvent
           */
        public TransactionEventException(string message, TransactionEvent transactionEvent) : base(message)
        {
            TransactionEvent = transactionEvent;
        }
        /**
         * save the TransactionEvent in the exception so that caller can use for debugging
         *
         * @param message
         * @param transactionEvent
         * @param throwable
         */
        public TransactionEventException(string message, TransactionEvent transactionEvent, Exception parent) : base(message,parent)
        {
            TransactionEvent = transactionEvent;
        }


    }
}
