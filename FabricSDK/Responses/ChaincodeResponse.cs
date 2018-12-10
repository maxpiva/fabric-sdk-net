/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology - All Rights Reserved.
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

// ReSharper disable UnusedParameter.Local
namespace Hyperledger.Fabric.SDK.Responses
{
    public class ChaincodeResponse
    {
        internal int statusReturnCode = -1;

        public enum ChaincodeResponseStatus
        {
            UNDEFINED = 0,
            SUCCESS = 200,
            FAILURE = 500
        }

        public ChaincodeResponse(string transactionID, string chaincodeID, int istatus, string message)
        {
            Status = istatus < 400 ? ChaincodeResponseStatus.SUCCESS : ChaincodeResponseStatus.FAILURE;
            statusReturnCode = istatus;
            Message = message;
            TransactionID = transactionID;
        }

        public bool IsInvalid => Status != ChaincodeResponseStatus.SUCCESS;

        /**
     * @return the status
     */
        public ChaincodeResponseStatus Status { get; }

        /**
     * @return the message
     */
        public string Message { get; }

        /**
     * @return the transactionID
     */
        public string TransactionID { get; }

//    /**
//     * @return the chaincodeID
//     */
//    public ChaincodeID getChaincodeID() {
//        return new ChaincodeID()
//    }
    }
}