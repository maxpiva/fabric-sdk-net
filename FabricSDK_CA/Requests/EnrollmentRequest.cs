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

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Fabric_CA.SDK.Requests
{
    /**
     * An enrollment request is information required to enroll the user with member service.
     */
    public class EnrollmentRequest
    {
        // The Certificate Authority's name


        // A PEM-encoded string containing the CSR (Certificate Signing Request) based on PKCS #10

        // Comma-separated list of host names to associate with the certificate
        private readonly List<string> hosts = new List<string>();

        // Attribute requests. added v1.1
        private Dictionary<string, AttrReq> attrreqs; //new HashMap<>();

        // Key pair for generating certification request

        // Label used in HSM operations

        // Name of the signing profile to use when issuing the certificate

        // Constructor
        public EnrollmentRequest()
        {
        }

        /**
         * EnrollmentRequest All the fields are optional
         *
         * @param profile
         * @param label
         * @param keypair Keypair used to sign or create the certificate if needed.
         */
        public EnrollmentRequest(string profile, string label, KeyPair keypair)
        {
            Profile = profile;
            Label = label;
            KeyPair = keypair;
        }

        /**
 * The certificate signing request if it's not supplied it will be generated.
 *
 * @param csr
 */

        public string CSR { get; set; }

        /**
 * The Key pair to create the signing certificate if not supplied it will be generated.
 *
 * @param keypair
 */
        public KeyPair KeyPair { get; set; }

        public string CAName { get; set; }

        public string Profile { get; set; }

        public string Label { get; set; }

        public List<string> Hosts => hosts.ToList();

        public void AddHost(string host)
        {
            hosts.Add(host);
        }

        // Convert the enrollment request to a JSON string
        public string ToJson()
        {
            return ToJsonObject().ToString(Formatting.None);
        }

        // Convert the enrollment request to a JSON object
        public JObject ToJsonObject()
        {
            JObject f = new JObject();
            if (Profile != null)
                f.Add(new JProperty("profile", Profile));
            if (hosts.Count > 0)
                f.Add(new JProperty("hosts", new JArray(hosts.Select(a => new JValue(a)))));
            if (Label != null)
                f.Add("label", Label);
            if (CAName != null)
                f.Add(HFCAClient.FABRIC_CA_REQPROP, CAName);
            f.Add("certificate_request", CSR);
            if (attrreqs != null)
            {
                JArray ja = new JArray();
                foreach (AttrReq atr in attrreqs.Values)
                {
                    JObject obj = new JObject();
                    obj.Add(new JProperty("name", atr.Name));
                    if (atr.Optional.HasValue)
                        obj.Add(new JProperty("optional", atr.Optional.Value));
                    ja.Add(obj);
                }

                f.Add("attr_reqs", ja);
            }

            return f;
        }

        /**
         * Add attribute request to ensure no attributes are in the certificate - not even default ones.
         *
         * @throws InvalidArgumentException
         */
        public void AddAttrReq()
        {
            if (attrreqs != null && attrreqs.Count > 0)
                throw new InvalidArgumentException("Attributes have already been defined.");
            attrreqs = new Dictionary<string, AttrReq>();
        }

        /**
         * Add attribute to certificate.
         *
         * @param name Name of attribute.
         * @return Attribute added.
         * @throws InvalidArgumentException
         */
        public AttrReq AddAttrReq(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new InvalidArgumentException("name may not be null or empty.");
            return new AttrReq(this, name);
        }

        public class AttrReq
        {
            public AttrReq(EnrollmentRequest parent, string name)
            {
                Name = name;
                if (parent.attrreqs == null)
                    parent.attrreqs = new Dictionary<string, AttrReq>();
                parent.attrreqs.Add(name, this);
            }

            public string Name { get; }
            public bool? Optional { get; set; }

            public AttrReq SetOptional(bool optional)
            {
                Optional = optional;
                return this;
            }
        }
    }
}