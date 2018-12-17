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
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.SDK.Channels;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Discovery
{
    public class SDOrderer : ISDAdditionInfo<Orderer>
    {
        public SDOrderer(Channel channel, string mspid, string endPoint, IEnumerable<byte[]> tlsCerts, IEnumerable<byte[]> tlsIntermediateCerts)
        {
            Channel = channel;
            MspId = mspid;
            Endpoint = endPoint;
            TLSCerts = tlsCerts.ToList();
            TLSIntermediateCerts = tlsIntermediateCerts.ToList();
        }

        public Channel Channel { get; }
        public HFClient Client => Channel.Client;
        public IReadOnlyDictionary<string, Orderer> EndpointMap => Channel.OrdererEndpointMap;
        public Task<Orderer> AddAsync(Properties config, CancellationToken token=default(CancellationToken))
        {
            Properties properties = new Properties();
            string protocol = this.FindClientProp(config, "protocol", "grpcs:");
            string clientCertFile = this.FindClientProp(config, "clientCertFile", null);
            if (null != clientCertFile)
                properties.Set("clientCertFile", clientCertFile);
            string clientKeyFile = this.FindClientProp(config, "clientKeyFile", null);
            if (null != clientKeyFile)
                properties.Set("clientKeyFile", clientKeyFile);            
            string clientCertBytes = this.FindClientProp(config, "clientCertBytes", null);
            if (null != clientCertBytes)
                properties.Set("clientCertBytes", clientCertBytes);
            string clientKeyBytes = this.FindClientProp(config, "clientKeyBytes", null);
            if (null != clientKeyBytes)
                properties.Set("clientKeyBytes", clientKeyBytes);
            string hostnameOverride = this.FindClientProp(config, "hostnameOverride", null);
            if (null != hostnameOverride)
                properties.Set("hostnameOverride", hostnameOverride);
            byte[] pemBytes = this.GetAllTLSCerts();
            if (pemBytes?.Length > 0)
                properties.Set("pemBytes", pemBytes);
            Orderer orderer = Client.NewOrderer(Endpoint,protocol + "//" + Endpoint, properties);
            Channel.AddOrderer(orderer);
            return Task.FromResult(orderer);
        }

        public List<byte[]> TLSIntermediateCerts { get; }
        public string Endpoint { get; }
        public string MspId { get; }
        public List<byte[]> TLSCerts { get; }

    }
}