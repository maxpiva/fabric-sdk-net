/*
*  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

using Hyperledger.Fabric.SDK.Idemix;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Fabric_CA.SDK.Requests
{
    /**
     * An enrollment request is information required to enroll the user with member service.
     */
    public class IdemixEnrollmentRequest
    {
        public IdemixEnrollmentRequest()
        {
        }

        public IdemixEnrollmentRequest(IdemixCredRequest credRequest)
        {
            IdemixCredReq = credRequest;
        }

        public IdemixCredRequest IdemixCredReq { get; set; }

        public string CaName { get; set; }


        // Convert the enrollment request to a JSON string
        public string ToJson()
        {
            return ToJsonObject().ToString();
        }

        // Convert the enrollment request to a JSON object
        public JObject ToJsonObject()
        {
            JObject ret = new JObject();
            ret.Add("request", IdemixCredReq == null ? (JToken) JValue.CreateNull() : (JToken) IdemixCredReq.ToJsonObject());
            if (CaName != null)
                ret.Add(HFCAClient.FABRIC_CA_REQPROP, CaName);
            return ret;
        }
    }
}