/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Newtonsoft.Json;

namespace Hyperledger.Fabric.SDK
{
    /**
     * The Orderer class represents a orderer to which SDK sends deploy, invoke, or query requests.
     */
    
    public class Orderer : BaseClient
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Orderer));

        private byte[] clientTLSCertificateDigest;

        private OrdererClient ordererClient;


        public Orderer(string name, string url, Properties properties) : base(name, url, properties)
        {
        }

        public byte[] ClientTLSCertificateDigest => clientTLSCertificateDigest ?? (clientTLSCertificateDigest = new Endpoint(Url, Properties).GetClientTLSCertificateDigest());

        /**
         * Get the channel of which this orderer is a member.
         *
         * @return {Channel} The channel of which this orderer is a member.
         */
        [JsonIgnore]
        public override Channel Channel
        {
            get => base.Channel;
            set
            {
                if (value == null)
                    throw new InvalidArgumentException("Channel can not be null");
                if (null != base.Channel && base.Channel != value)
                    throw new InvalidArgumentException($"Can not add orderer {Name} to channel {value.Name} because it already belongs to channel {Channel.Name}.");
                base.Channel = value;
            }
        }

        public static Orderer Create(string name, string url, Properties properties)
        {
            return new Orderer(name, url, properties);
        }


        public void UnsetChannel()
        {
            base.Channel = null;
        }


        /**
         * Send transaction to Order
         *
         * @param transaction transaction to be sent
         */

        public BroadcastResponse SendTransaction(Envelope transaction)
        {
            return SendTransactionAsync(transaction).RunAndUnwarp();
        }
        public async Task<BroadcastResponse> SendTransactionAsync(Envelope transaction, CancellationToken token=default(CancellationToken))
        {
            if (shutdown)
                throw new TransactionException($"Orderer {Name} was shutdown.");
            if (transaction==null)
                throw new IllegalArgumentException("Unable to send a null transaction");
            logger.Debug($"Order.sendTransaction name: {Name}, url: {Url}");
            OrdererClient localOrdererClient = ordererClient;
            if (localOrdererClient == null || !localOrdererClient.IsChannelActive())
            {
                ordererClient = new OrdererClient(this, new Endpoint(Url, Properties), Properties);
                localOrdererClient = ordererClient;
            }
            try
            {
                return await localOrdererClient.SendTransactionAsync(transaction,token);
            }
            catch (Exception)
            {
                ordererClient = null;
                throw;
            }
        }
        public List<DeliverResponse> SendDeliver(Envelope transaction)
        {
            return SendDeliverAsync(transaction).RunAndUnwarp();
        }
        public async Task<List<DeliverResponse>> SendDeliverAsync(Envelope transaction, CancellationToken token=default(CancellationToken))
        {
            if (shutdown)
                throw new TransactionException($"Orderer {Name} was shutdown.");
            OrdererClient localOrdererClient = ordererClient;
            logger.Debug($"Order.sendDeliver name: {Name}, url: {Url}");
            if (localOrdererClient == null || !localOrdererClient.IsChannelActive())
            {
                localOrdererClient = new OrdererClient(this, new Endpoint(Url, Properties), Properties);
                ordererClient = localOrdererClient;
            }
            try
            {
                return await localOrdererClient.SendDeliverAsync(transaction,token);
            }
            catch (Exception)
            {
                ordererClient = null;
                throw;
            }
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (shutdown)
                return;
            shutdown = true;
            base.Channel = null;
            if (ordererClient != null)
            {
                OrdererClient torderClientDeliver = ordererClient;
                ordererClient = null;
                torderClientDeliver.Shutdown(force);
            }
        }

        ~Orderer()
        {
            Shutdown(true);
        }
        public override string ToString() => "Orderer: " + Name + "(" + Url + ")";
    } // end Orderer
}