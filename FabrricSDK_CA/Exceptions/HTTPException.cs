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

using System;

namespace Hyperledger.Fabric_CA.SDK.Exceptions
{
    public class HTTPException : Exception
    {
        /**
         * @param message error message
         * @param statusCode HTTP status code
         * @param parent
         */
        public HTTPException(string message, int statusCode, Exception parent) : base(message, parent)
        {
            StatusCode = statusCode;
        }

        /**
         * @param message error message
         * @param statusCode HTTP status code
         */
        public HTTPException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }

        /**
         * @param message error message
         * @param parent
         */
        public HTTPException(string message, Exception parent) : base(message, parent)
        {
        }

        /**
         * @param message error message
         */
        public HTTPException(string message) : base(message)
        {
        }

        /**
         * @return HTTP status code
         */
        public int StatusCode { get; } = -1;
    }
}