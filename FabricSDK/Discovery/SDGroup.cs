/*
 *
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
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hyperledger.Fabric.SDK.Discovery
{
    public class SDGroup
    {
        private readonly List<SDEndorser> endorsers = new List<SDEndorser>();
        private int endorsed; // number that have been now endorsed.

        public SDGroup(string name, int required, IEnumerable<SDEndorser> endorsers)
        {
            Name = name;
            Required = required;
            this.endorsers.AddRange(endorsers);
        }

        public SDGroup(SDGroup group)
        {
            //copy constructor
            Name = group.Name;
            Required = group.Required;
            endorsers.AddRange(group.endorsers);
            endorsed = 0; // on copy reset to no endorsements
        }

        public int StillRequired => Required - endorsed;

        public string Name { get; }

        public int Required { get; }

        public List<SDEndorser> Endorsers => endorsers.ToList();

        //returns true if there are still sufficent endorsers for this group.
        public bool IgnoreList(IEnumerable<string> names)
        {
            HashSet<string> bnames = new HashSet<string>(names);
            foreach (SDEndorser sde in endorsers.ToList())
            {
                if (bnames.Contains(sde.Endpoint))
                    endorsers.Remove(sde);
            }

            return endorsers.Count >= Required;
        }

        //returns true if there are still sufficent endorsers for this group.
        public bool IgnoreListSDEndorser(IEnumerable<SDEndorser> sdEndorsers)
        {
            HashSet<SDEndorser> bnames = new HashSet<SDEndorser>(sdEndorsers);
            foreach (SDEndorser sde in endorsers.ToList())
            {
                if (bnames.Contains(sde))
                    endorsers.Remove(sde);
            }

            return endorsers.Count >= Required;
        }

        // retrun true if th endorsements have been meet.
        public bool EndorsedList(IEnumerable<SDEndorser> sdEndorsers)
        {
            //This is going to look odd so here goes: Service discovery can't guarantee the endpoint certs are valid
            // and so there may be multiple endpoints with different MSP ids. However if we have gotten an
            // endorsement from an endpoint that means it's been satisfied and can be removed.
            List<SDEndorser> sdend = sdEndorsers.ToList();
            if (endorsed >= Required)
            {
                return true;
            }

            if (sdend.Count>0)
            {
                HashSet<string> enames = new HashSet<string>(sdend.Select(a => a.Endpoint).Distinct());
                foreach (SDEndorser sde in endorsers.ToList())
                {
                    if (enames.Contains(sde.Endpoint))
                    {
                        endorsed = Math.Min(Required, endorsed++);
                        return true;
                    }

                    return false;
                }
            }

            return endorsed >= Required;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(512);
            sb.Append("SDGroup: { name: ").Append(Name).Append(", required: ").Append(Required);

            if (endorsers.Count > 0)
            {
                sb.Append(", endorsers: [");
                string sep2 = "";
                foreach (SDEndorser sdEndorser in endorsers)
                {
                    sb.Append(sep2).Append(sdEndorser);
                    sep2 = ", ";
                }

                sb.Append("]");
            }
            else
            {
                sb.Append(", endorsers: []");
            }

            sb.Append("}");
            return sb.ToString();
        }

        // Returns
        public List<SDEndorser> MeetsEndorsmentPolicy(IEnumerable<SDEndorser> allEndorsed, List<SDEndorser> requiredYet)
        {
            HashSet<SDEndorser> ret = new HashSet<SDEndorser>();
            List<SDEndorser> allend = allEndorsed.ToList();
            foreach (SDEndorser hasBeenEndorsed in allend)
            {
                foreach (SDEndorser sdEndorser in endorsers)
                {
                    if (hasBeenEndorsed.Equals(sdEndorser))
                    {
                        ret.Add(sdEndorser);
                        if (ret.Count >= Required)
                        {
                            return ret.ToList(); // got what we needed.
                        }
                    }
                }
            }

            if (null != requiredYet)
            {
                foreach (SDEndorser sdEndorser in endorsers)
                {
                    if (!allend.Contains(sdEndorser))
                    {
                        requiredYet.Add(sdEndorser);
                    }
                }
            }

            return null; // group has not meet endorsement.
        }
    }
}