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
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.SDK.Channels;
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
        private string channelName = string.Empty;

        private byte[] clientTLSCertificateDigest;
        private string endPoint;


        private OrdererClient ordererClient;

        public Orderer(string name, string url, Properties properties) : base(name, url, properties)
        {
            logger.Trace($"Created {this}");
        }

        public byte[] ClientTLSCertificateDigest => clientTLSCertificateDigest ?? (clientTLSCertificateDigest = SDK.Endpoint.Create(Url, Properties).GetClientTLSCertificateDigest());

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
                    throw new ArgumentException("Channel can not be null");
                if (null != base.Channel && base.Channel != value)
                    throw new ArgumentException($"Can not add orderer {Name} to channel {value.Name} because it already belongs to channel {Channel.Name}.");
                logger.Debug($"{ToString()} setting channel {value}");
                base.Channel = value;
                channelName = value.Name;
            }
        }


        public string Endpoint
        {
            get
            {
                if (null == endPoint)
                {
                    (string _, string host, int port) = Utils.ParseGrpcUrl(Url);
                    endPoint = host + ":" + port.ToString().ToLowerInvariant().Trim();
                }

                return endPoint;
            }
        }

        public static Orderer Create(string name, string url, Properties properties)
        {
            return new Orderer(name, url, properties);
        }


        public void UnsetChannel()
        {
            logger.Debug($"{ToString()} unsetting channel");
            base.Channel = null;
            channelName = string.Empty;
        }


        /**
         * Send transaction to Order
         *
         * @param transaction transaction to be sent
         */

        public BroadcastResponse SendTransaction(Envelope transaction)
        {
            return SendTransactionAsync(transaction).RunAndUnwrap();
        }

        public async Task<BroadcastResponse> SendTransactionAsync(Envelope transaction, CancellationToken token = default(CancellationToken))
        {
            if (shutdown)
                throw new TransactionException($"Orderer {Name} was shutdown.");
            if (transaction == null)
                throw new ArgumentException("Unable to send a null transaction");
            logger.Debug($"{ToString()} Order.sendTransaction");
            OrdererClient localOrdererClient = GetOrdererClient();

            try
            {
                return await localOrdererClient.SendTransactionAsync(transaction, token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                RemoveOrdererClient(true);
                ordererClient = null;
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private OrdererClient GetOrdererClient()
        {
            OrdererClient localOrdererClient = ordererClient;
            if (localOrdererClient == null || !localOrdererClient.IsChannelActive)
            {
                logger.Trace($"Channel {channelName} creating new orderer client {ToString()}");
                localOrdererClient = new OrdererClient(this, SDK.Endpoint.Create(Url, Properties), Properties);
                ordererClient = localOrdererClient;
            }

            return localOrdererClient;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void RemoveOrdererClient(bool force)
        {
            OrdererClient localOrderClient = ordererClient;
            ordererClient = null;
            if (null != localOrderClient)
            {
                logger.Debug($"Channel {channelName} removing orderer client {ToString()}, isActive: {localOrderClient.IsChannelActive}");
                try
                {
                    localOrderClient.Shutdown(force);
                }
                catch (Exception e)
                {
                    logger.Error($"{ToString()} error message: {e.Message}");
                    logger.Trace(e.Message, e);
                }
            }
        }

        public List<DeliverResponse> SendDeliver(Envelope transaction)
        {
            return SendDeliverAsync(transaction).RunAndUnwrap();
        }

        public async Task<List<DeliverResponse>> SendDeliverAsync(Envelope transaction, CancellationToken token = default(CancellationToken))
        {
            if (shutdown)
                throw new TransactionException($"Orderer {Name} was shutdown.");
            OrdererClient localOrdererClient = GetOrdererClient();
            logger.Debug($"{ToString()} Orderer.sendDeliver");


            if (localOrdererClient == null || !localOrdererClient.IsChannelActive)
            {
                localOrdererClient = new OrdererClient(this, SDK.Endpoint.Create(Url, Properties), Properties);
                ordererClient = localOrdererClient;
            }

            try
            {
                return await localOrdererClient.SendDeliverAsync(transaction, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                logger.Error($"{ToString()} removing {localOrdererClient} due to {e.Message}", e);
                RemoveOrdererClient(true);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (shutdown)
                return;
            shutdown = true;
            logger.Debug($"Shutting down {ToString()}");
            RemoveOrdererClient(true);
        }

        ~Orderer()
        {
            Shutdown(true);
        }

        public override string ToString() => $"Orderer{{id: {id}, channelName: {channelName}, name:{Name}, url: {Url}}}";
    } // end Orderer
}