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
using System.Diagnostics;
using System.Linq;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Discovery
{
    public class SDNetwork
    {
        private readonly Dictionary<string, List<byte[]>> tlsCerts = new Dictionary<string, List<byte[]>>();
        private readonly Dictionary<string, List<byte[]>> tlsIntermCerts = new Dictionary<string, List<byte[]>>();
        private HashSet<string> chaincodeNames;
        private Dictionary<string, SDEndorser> endorsers = new Dictionary<string, SDEndorser>();
        private Dictionary<string, SDOrderer> ordererEndpoints = new Dictionary<string, SDOrderer>();
        private readonly int SERVICE_DISCOVER_FREQ_SECONDS;


        private Stopwatch sp;

        public SDNetwork(int service_discover_in_seconds)
        {
            SERVICE_DISCOVER_FREQ_SECONDS = service_discover_in_seconds;
        }

        public IReadOnlyCollection<SDEndorser> Endorsers => endorsers.Values.ToList();
        public IReadOnlyCollection<string> OrdererEndpoints => ordererEndpoints.Keys.ToList();

        public bool TimeElapsed => sp == null || sp.Elapsed.TotalSeconds >= SERVICE_DISCOVER_FREQ_SECONDS;

        public List<SDOrderer> SDOrderers => ordererEndpoints.Values.ToList();

        public IReadOnlyCollection<string> PeerEndpoints => endorsers.Keys.ToList();

        public HashSet<string> ChaincodesNames
        {
            get
            {
                if (null == chaincodeNames)
                {
                    if (null == endorsers)
                    {
                        return new HashSet<string>();
                    }

                    HashSet<string> ret = new HashSet<string>();
                    foreach (SDEndorser sdEndorser in endorsers.Values)
                    {
                        if (sdEndorser.Chaincodes != null)
                        {
                            foreach (var v in sdEndorser.Chaincodes)
                            {
                                if (!ret.Contains(v.Name))
                                    ret.Add(v.Name);
                            }
                        }
                    }

                    chaincodeNames = ret;
                }

                return chaincodeNames;
            }
        }

        public void End()
        {
            sp = new Stopwatch();
            sp.Start();
        }

        public void SetOrdererEndpoints(Dictionary<string, SDOrderer> endpoints)
        {
            ordererEndpoints = endpoints;
        }

        public void SetEndorsers(Dictionary<string, SDEndorser> endrsers)
        {
            endorsers = endrsers;
        }

        public void AddTlsCert(string mspid, byte[] cert)
        {
            if (!tlsCerts.ContainsKey(mspid))
                tlsCerts[mspid] = new List<byte[]>();
            tlsCerts[mspid].Add(cert);
        }

        public void AddTlsIntermCert(string mspid, byte[] cert)
        {
            if (!tlsIntermCerts.ContainsKey(mspid))
                tlsIntermCerts[mspid] = new List<byte[]>();
            tlsIntermCerts[mspid].Add(cert);
        }

        public SDEndorser GetEndorserByEndpoint(string endpoint) => endorsers.GetOrNull(endpoint);

        public IReadOnlyList<byte[]> GetTlsCerts(string mspid)
        {
            List<byte[]> bytes = tlsCerts.GetOrNull(mspid);
            if (null == bytes)
                return new List<byte[]>();
            return bytes.ToList();
        }

        public IReadOnlyList<byte[]> GetTlsIntermediateCerts(string mspid)
        {
            List<byte[]> bytes = tlsIntermCerts.GetOrNull(mspid);
            if (null == bytes)
                return new List<byte[]>();
            return bytes.ToList();
        }
    }
}