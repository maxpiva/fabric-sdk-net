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

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hyperledger.Fabric.SDK.Discovery
{
    public class SDLayout
    {
        private readonly List<SDGroup> groups = new List<SDGroup>();

        public SDLayout()
        {
        }

        //Copy constructor
        public SDLayout(SDLayout sdLayout)
        {
            foreach (SDGroup group in sdLayout.Groups)
            {
                groups.Add(new SDGroup(group));
            }
        }

        public List<SDGroup> Groups => groups.ToList();

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(1000);
            sb.Append("SDLayout: {");
            if (groups.Count > 0)
            {
                sb.Append("groups: [");
                string sep2 = "";
                foreach (SDGroup group in groups)
                {
                    sb.Append(sep2).Append(group);
                    sep2 = ", ";
                }

                sb.Append("]");
            }
            else
            {
                sb.Append(", groups: []");
            }

            sb.Append("}");
            return sb.ToString();
        }

        //return true if the groups still exist to get endorsement.
        public bool IgnoreList(IEnumerable<string> names)
        {
            bool ret = true;
            HashSet<string> bnames = new HashSet<string>(names);

            foreach (SDGroup group in groups)
            {
                if (!group.IgnoreList(bnames))
                {
                    ret = false; // group can no longer be satisfied.
                }
            }

            return ret;
        }

        public bool IgnoreListSDEndorser(IEnumerable<SDEndorser> names)
        {
            bool ret = true;
            HashSet<SDEndorser> bnames = new HashSet<SDEndorser>(names);

            foreach (SDGroup group in groups)
            {
                if (!group.IgnoreListSDEndorser(bnames))
                {
                    ret = false; // group can no longer be satisfied.
                }
            }

            return ret;
        }

        // endorsement has been meet.
        public bool EndorsedList(IEnumerable<SDEndorser> sdEndorsers)
        {
            int endorsementMeet = 0;
            foreach (SDGroup group in groups)
            {
                if (group.EndorsedList(sdEndorsers))
                {
                    ++endorsementMeet;
                }
            }

            return endorsementMeet >= groups.Count;
        }

        //       Returns null when not meet and endorsers needed if it is.
        public List<SDEndorser> MeetsEndorsmentPolicy(IEnumerable<SDEndorser> endpoints)
        {
            HashSet<SDEndorser> ret = new HashSet<SDEndorser>();

            foreach (SDGroup group in groups)
            {
                List<SDEndorser> sdEndorsers = group.MeetsEndorsmentPolicy(endpoints, null);
                if (null == sdEndorsers)
                {
                    return null; // group was not satisfied
                }

                foreach (SDEndorser r in sdEndorsers)
                {
                    if (!ret.Contains(r))
                        ret.Add(r);
                }
            }

            return ret.ToList();
        }

        public void AddGroup(string key, int required, List<SDEndorser> endorsers)
        {
            SDGroup grp = new SDGroup(key, required, endorsers);
            groups.Add(grp);
        }
    }
}