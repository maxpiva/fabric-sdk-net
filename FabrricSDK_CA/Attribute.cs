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
using Newtonsoft.Json.Linq;

namespace Hyperledger.Fabric_CA.SDK
{
    // An attribute name and value which is used when registering a new user
    public class Attribute
    {
        public Attribute(string name, string value) : this(name, value, null)
        {
        }

        /**
         * @param name             Attribute name.
         * @param value            Attribute value.
         * @param defaultAttribute Attribute should be included in certificate even if not specified during enrollment.
         */
        public Attribute(string name, string value, bool? defaultAttribute)
        {
            Name = name;
            Value = value;
            ECert = defaultAttribute;
        }

        private bool? ECert { get; }

        public string Name { get; }

        public string Value { get; }

        public JObject ToJsonObject()
        {
            List<JProperty> props = new List<JProperty>();
            props.Add(new JProperty("name", Name));
            props.Add(new JProperty("value", Value));
            if (ECert != null)
                props.Add(new JProperty("ecert", ECert.Value));
            JObject ret = new JObject();
            props.ForEach(a => ret.Add(a));
            return ret;
        }
    }
}