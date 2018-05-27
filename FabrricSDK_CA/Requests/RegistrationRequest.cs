/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology - All Rights Reserved.
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
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Fabric_CA.SDK.Requests
{
    /**
     * A registration request is information required to register a user, peer, or other
     * type of member.
     */
    public class RegistrationRequest
    {
        // Affiliation for a user
        // Array of attribute names and values


        // Constructor

        /**
         * Register user with certificate authority
         *
         * @param id The id of the user to register.
         * @throws Exception
         */
        public RegistrationRequest(string id)
        {
            EnrollmentID = id ?? throw new Exception("id may not be null");
        }

        /**
         * Register user with certificate authority
         *
         * @param id          The id of the user to register.
         * @param affiliation The user's affiliation.
         * @throws Exception
         */
        public RegistrationRequest(string id, string affiliation)
        {
            EnrollmentID = id ?? throw new Exception("id may not be null");
            Affiliation = affiliation ?? throw new Exception("affiliation may not be null");
        }

        public string EnrollmentID { get; set; }

        public string Secret { get; set; }

        public int? MaxEnrollments { get; set; } = null;

        public string Type { get; set; } = HFCAClient.HFCA_TYPE_CLIENT;

        public string Affiliation { get; set; }

        public List<Attribute> Attributes { get; } = new List<Attribute>();

        public string CAName { get; set; }

        public void AddAttribute(Attribute attr)
        {
            Attributes.Add(attr);
        }

        // Convert the registration request to a JSON string
        public string ToJson()
        {
            return ToJsonObject().ToString();
        }

        // Convert the registration request to a JSON object
        public JObject ToJsonObject()
        {
            JObject r = new JObject();
            r.Add(new JProperty("id", EnrollmentID));
            r.Add(new JProperty("type", Type));
            if (Secret != null)
                r.Add(new JProperty("secret", Secret));
            if (null != MaxEnrollments)
                r.Add("max_enrollments", MaxEnrollments.Value);
            if (Affiliation != null)
                r.Add("affiliation", Affiliation);
            JArray ab = new JArray();
            Attributes.ForEach(a => ab.Add(a.ToJsonObject()));
            if (CAName != null)
                r.Add(HFCAClient.FABRIC_CA_REQPROP, CAName);
            r.Add("attrs", ab);
            return r;
        }
    }
}