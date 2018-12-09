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
/**
 * A RevocationRequest defines the attributes required to revoke credentials with member service.
 */

using System;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Fabric_CA.SDK.Requests
{
    public class RevocationRequest
    {
        // Constructor
        public RevocationRequest(string caName, string id, string serial, string aki, string reason)
        {
            if (string.IsNullOrEmpty(id))
            {
                if (string.IsNullOrEmpty(serial) || string.IsNullOrEmpty(aki))
                    throw new ArgumentException("Enrollment ID is empty, thus both aki and serial must have non-empty values");
            }
            User = id;
            Serial = serial;
            Aki = aki;
            Reason = reason;
            CAName = caName;
        }

        // Constructor with genCRL parameter
        public RevocationRequest(string caName, string id, string serial, string aki, string reason, bool genCRL) : this(caName, id, serial, aki, reason)
        {
            GenCRL = genCRL;
        }

        public string User { get; set; }

        public string Serial { get; set; }

        public string Aki { get; set; }

        public string Reason { get; set; }

        public bool? GenCRL { get; set; }

        public string CAName { get; }

        // Convert the revocation request to a JSON string
        public string ToJson()
        {
            return ToJsonObject().ToString();
        }

        // Convert the revocation request to a JSON object
        private JObject ToJsonObject()
        {
            JObject f = new JObject();
            if (User != null)
            {
                // revoke all enrollments of this user, serial and aki are ignored in this case
                f.Add(new JProperty("id", User));
            }
            else
            {
                // revoke one particular enrollment
                f.Add(new JProperty("serial", Serial));
                f.Add(new JProperty("aki", Aki));
            }

            if (null != Reason)
                f.Add(new JProperty("reason", Reason));
            if (CAName != null)
                f.Add(new JProperty(HFCAClient.FABRIC_CA_REQPROP, CAName));
            if (GenCRL != null)
                f.Add(new JProperty("gencrl", GenCRL.Value));
            return f;
        }
    }
}