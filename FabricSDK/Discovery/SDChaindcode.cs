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
// ReSharper disable PossibleMultipleEnumeration

namespace Hyperledger.Fabric.SDK.Discovery
{
    public class SDChaindcode
    {
        public SDChaindcode(SDChaindcode sdChaindcode)
        {
            Name = sdChaindcode.Name;
            Layouts = sdChaindcode.Layouts.Select(a => new SDLayout(a)).ToList();
        }

        public SDChaindcode(string name, List<SDLayout> layouts)
        {
            Name = name;
            Layouts = layouts;
        }

        public string Name { get; }
        public List<SDLayout> Layouts { get; set; }

        // returns number of layouts left.
        public int IgnoreList(List<string> names)
        {
            if (names != null && names.Count != 0)
            {
                foreach (SDLayout sdLayout in Layouts.ToList())
                {
                    if (!sdLayout.IgnoreList(names))
                        Layouts.Remove(sdLayout);
                }
            }
            return Layouts.Count;
        }

        public int IgnoreListSDEndorser(IEnumerable<SDEndorser> sdEndorsers)
        {
            if (sdEndorsers != null)
            {
                foreach (SDLayout sdLayout in Layouts.ToList())
                {
                    if (!sdLayout.IgnoreListSDEndorser(sdEndorsers))
                        Layouts.Remove(sdLayout);
                }
            }
            return Layouts.Count;
        }

        public bool EndorsedList(IEnumerable<SDEndorser> sdEndorsers)
        {
            bool ret = false;
            foreach (SDLayout sdLayout in Layouts)
            {
                if (sdLayout.EndorsedList(sdEndorsers))
                    ret = true;
            }
            return ret;
        }

        // return the set needed or null if the policy was not meet.
        public List<SDEndorser> MeetsEndorsmentPolicy(IEnumerable<SDEndorser> endpoints)
        {
            List<SDEndorser> ret = null; // not meet.
            foreach (SDLayout sdLayout in Layouts)
            {
                List<SDEndorser> needed = sdLayout.MeetsEndorsmentPolicy(endpoints);
                if (needed != null && (ret == null || ret.Count > needed.Count))
                    ret = needed; // needed is less so lets go with that.
            }
            return ret;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(1000);
            sb.Append("SDChaindcode(name: ").Append(Name);
            if (null != Layouts && Layouts.Count > 0)
            {
                sb.Append(", layouts: [");
                string sep = "";
                foreach (SDLayout sdLayout in Layouts)
                {
                    sb.Append(sep).Append(sdLayout + "");
                    sep = " ,";
                }
                sb.Append("]");
            }
            sb.Append(")");
            return sb.ToString();
        }
    }
}