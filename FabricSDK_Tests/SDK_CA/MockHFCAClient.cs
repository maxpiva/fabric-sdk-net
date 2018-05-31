/*
 *  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *      http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */


using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric_CA.SDK;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Fabric.Tests.SDK_CA
{
    /**
     * A Mock class for testing HFCAClient.java
     *
     */

    public class MockHFCAClient : HFCAClient
    {
        private string httpPostResponse = null;

        private MockHFCAClient(string name, string url, Properties properties) : base(name, url, properties)
        {
        }

        public override async Task<string> HttpPostAsync(string url, string body, NetworkCredential credentials, CancellationToken token=default(CancellationToken))
        {
            return httpPostResponse ?? await base.HttpPostAsync(url, body, credentials, token);
        }

        public override async Task<JObject> HttpPostAsync(string url, string body, IUser admin, CancellationToken token = default(CancellationToken))
        {
            JObject response;

            if (httpPostResponse == null)
            {
                response = await base.HttpPostAsync(url, body, admin, token);
            }
            else
            {
                response = JObject.Parse(httpPostResponse);

                // TODO: HFCAClient could do with some minor refactoring to avoid duplicating this code here!!
                JObject result = response["result"] as JObject;
                if (result == null)
                {
                    EnrollmentException e = new EnrollmentException($"POST request to {url} failed request body {body} Body of response did not contain result");
                    throw e;
                }
            }

            return response;
        }

        public new static MockHFCAClient Create(string url, Properties properties)
        {
            return new MockHFCAClient(null, url, properties);
        }

        public new static MockHFCAClient Create(string name, string url, Properties properties)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidArgumentException("name must not be null or an empty string.");
            }

            return new MockHFCAClient(name, url, properties);
        }

        // Sets the test string to be returned from httpPost
        // If null, it returns the actual response
        public void SetHttpPostResponse(string httpPostResponse)
        {
            this.httpPostResponse = httpPostResponse;
        }
    }
}