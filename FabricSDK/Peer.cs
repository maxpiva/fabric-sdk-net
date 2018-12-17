/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at`
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */
/**
 * The Peer class represents a peer to which SDK sends deploy, or query proposals requests.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Discovery;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Security;
using Newtonsoft.Json;
using Response = Hyperledger.Fabric.Protos.Discovery.Response;

namespace Hyperledger.Fabric.SDK
{
    /**
* Endorsing peer installs and runs chaincode.
*/


    public class Peer : BaseClient, IEquatable<Peer>
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Peer));
        private static readonly bool IS_DEBUG_LEVEL = logger.IsDebugEnabled();
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private static IPeerEventingServiceDisconnected _disconnectedHandler;
        private string channelName; // used for logging.
        private byte[] clientTLSCertificateDigest;
        internal EndorserClient endorserClent;
        private string endPoint;
        private bool foundClientTLSCertificateDigest;
        private long lastBlockNumber = -1;
        private PeerEventServiceClient peerEventingClient;
        private string protocol;
        [JsonIgnore] private long reconnectCount;
        private TransactionContext transactionContext;


        public Peer(string name, string url, Properties properties) : base(name, url, properties)
        {
            _disconnectedHandler = DefaultDisconnectHandler;
            reconnectCount = 0L;
            logger.Debug("Created " + ToString());
        }

        [JsonIgnore]
        public bool HasConnected { get; set; } // has this peer connected.


        [JsonIgnore]
        public BlockEvent LastBlockEvent { get; private set; }

        /**
         * Set the channel the peer is on.
         *
         * @param channel
         */
        [JsonIgnore]
        public override Channel Channel
        {
            get => base.Channel;
            set
            {
                if (null != base.Channel)
                    throw new ArgumentException($"Can not add peer {Name} to channel {value.Name} because it already belongs to channel {base.Channel.Name}.");
                logger.Debug($"{ToString()} setting channel to {value}, from {base.Channel}");
                base.Channel = value;
                channelName = Channel.Name;
            }
        }

        [JsonIgnore]
        internal Channel intChannel
        {
            set => base.Channel = value;
        }

        [JsonIgnore]
        public long LastConnectTime { get; set; }

        [JsonIgnore]
        public long ReconnectCount => reconnectCount;


        public static IPeerEventingServiceDisconnected DefaultDisconnectHandler => _disconnectedHandler ?? (_disconnectedHandler = new PeerEventingServiceDisconnect());

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

        public string Protocol
        {
            get
            {
                if (null == protocol)
                {
                    (string protc, string _, int _) = Utils.ParseGrpcUrl(Url);
                    protocol = protc;
                }

                return protocol;
            }
        }
        [JsonIgnore]
        public string EventingStatus
        {
            get
            {
                PeerEventServiceClient lpeerEventingClient = peerEventingClient;
                if (null == lpeerEventingClient)
                {
                    return " eventing client service not active.";
                }

                return lpeerEventingClient.Status;
            }
        }


        public bool Equals(Peer other)
        {
            if (this == other)
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return Name.Equals(other.Name) && Url.Equals(other.Url);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetTLSCertificateKeyPair(TLSCertificateKeyPair tlsCertificateKeyPair)
        {
            if (Properties == null)
                Properties = new Properties();
            Properties.Set("clientKeyBytes", tlsCertificateKeyPair.KeyPEMBytes.ToUTF8String());
            Properties.Set("clientCertBytes", tlsCertificateKeyPair.CertPEMBytes.ToUTF8String());
            Endpoint endpoint = SDK.Endpoint.Create(Url, Properties);
            foundClientTLSCertificateDigest = true;
            clientTLSCertificateDigest = endpoint.GetClientTLSCertificateDigest();
            RemoveEndorserClient(true);
            endorserClent = new EndorserClient(channelName, Name, endpoint);
        }

        public static Peer Create(string name, string grpcURL, Properties properties)
        {
            return new Peer(name, grpcURL, properties);
        }


        public async Task InitiateEventingAsync(TransactionContext transContext, Channel.PeerOptions peersOptions, CancellationToken token = default(CancellationToken))
        {
            transactionContext = transContext.RetryTransactionSameContext();
            if (peerEventingClient == null)
            {
                //PeerEventServiceClient(Peer peer, ManagedChannelBuilder<?> channelBuilder, Properties properties)
                //   peerEventingClient = new PeerEventServiceClient(this, new HashSet<Channel>(Arrays.asList(new Channel[] {channel})));

                peerEventingClient = new PeerEventServiceClient(this, SDK.Endpoint.Create(Url, Properties), Properties, peersOptions);

                await peerEventingClient.ConnectAsync(transContext, token).ConfigureAwait(false);
            }
        }

        public void UnsetChannel()
        {
            logger.Debug($"{ToString()} unset {Channel}");
            base.Channel = null;
        }


        public async Task<ProposalResponse> SendProposalAsync(SignedProposal proposal, CancellationToken token = default(CancellationToken))
        {
            CheckSendProposal(proposal);


            if (IS_DEBUG_LEVEL)
                logger.Debug($"peer.sendProposalAsync {ToString()}");

            EndorserClient localEndorserClient = GetEndorserClient();

            try
            {
                return await localEndorserClient.SendProposalAsync(proposal, token).ConfigureAwait(false);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception)
            {
                RemoveEndorserClient(true);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private EndorserClient GetEndorserClient()
        {
            EndorserClient localEndorserClient = endorserClent; //work off thread local copy.

            if (null == localEndorserClient || !localEndorserClient.IsChannelActive)
            {
                if (IS_TRACE_LEVEL)
                    logger.Trace($"Channel {channelName} creating new endorser client {ToString()}");

                Endpoint endpoint = SDK.Endpoint.Create(Url, Properties);
                foundClientTLSCertificateDigest = true;
                clientTLSCertificateDigest = endpoint.GetClientTLSCertificateDigest();
                localEndorserClient = new EndorserClient(channelName, Name, endpoint);
                if (IS_DEBUG_LEVEL)
                    logger.Debug($"{ToString()} created new  {localEndorserClient.ToString()}");
                endorserClent = localEndorserClient;
            }

            return localEndorserClient;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void RemoveEndorserClient(bool force)
        {
            EndorserClient localEndorserClient = endorserClent;
            endorserClent = null;

            if (null != localEndorserClient)
            {
                if (IS_DEBUG_LEVEL)
                    logger.Debug($"Peer {ToString()} removing endorser client {localEndorserClient.ToString()}, isActive: {localEndorserClient.IsChannelActive}");
                try
                {
                    localEndorserClient.Shutdown(force);
                }
                catch (Exception e)
                {
                    logger.Warn($"{ToString()} error message: {e.Message}");
                }
            }
        }

        public async Task<Response> SendDiscoveryRequestAsync(SignedRequest discoveryRequest, int? milliseconds = null, CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"peer.sendDiscoveryRequstAsync name: {Name}, url: {Url}");
            EndorserClient localEndorserClient = GetEndorserClient();
            try
            {
                return await localEndorserClient.SendDiscoveryRequestAsync(discoveryRequest, milliseconds, token).ConfigureAwait(false);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception)
            {
                RemoveEndorserClient(true);
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] GetClientTLSCertificateDigest()
        {
            byte[] lclientTLSCertificateDigest = clientTLSCertificateDigest;
            if (lclientTLSCertificateDigest == null)
            {
                if (!foundClientTLSCertificateDigest)
                {
                    foundClientTLSCertificateDigest = true;
                    Endpoint endpoint = SDK.Endpoint.Create(Url, Properties);
                    lclientTLSCertificateDigest = endpoint.GetClientTLSCertificateDigest();
                }
            }

            return lclientTLSCertificateDigest;
        }

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        private void CheckSendProposal(SignedProposal proposal)
        {
            if (proposal == null)
                throw new PeerException($"{ToString()} Proposal is null");
            if (shutdown)
                throw new PeerException($"{ToString()} was shutdown.");
            Exception e = Utils.CheckGrpcUrl(Url);
            if (e != null)
                throw new ArgumentException("Bad peer url.", e);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (shutdown)
                return;
            string me = ToString();
            logger.Debug($"{me} is shutting down.");

            shutdown = true;
            base.Channel = null;
            LastBlockEvent = null;
            lastBlockNumber = -1;
            RemoveEndorserClient(force);


            PeerEventServiceClient lpeerEventingClient = peerEventingClient;
            peerEventingClient = null;
            if (null != lpeerEventingClient)
            {
                // PeerEventServiceClient peerEventingClient1 = peerEventingClient;
                logger.Debug($"{me} is shutting down {lpeerEventingClient}");
                lpeerEventingClient.Shutdown(force);
            }

            logger.Debug($"{me} is shut down.");

            lpeerEventingClient?.Shutdown(force);
        }

        ~Peer()
        {
            if (!shutdown)
                logger.Debug($"{ToString()} finalized without previous shutdown.");
            Shutdown(true);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ReconnectPeerEventServiceClient(PeerEventServiceClient failedPeerEventServiceClient, Exception throwable, CancellationToken token = default(CancellationToken))
        {
            if (shutdown)
            {
                logger.Debug($"{ToString()}not reconnecting PeerEventServiceClient shutdown ");
                return;
            }

            IPeerEventingServiceDisconnected ldisconnectedHandler = _disconnectedHandler;
            if (null == ldisconnectedHandler)
            {
                return; // just wont reconnect.
            }

            TransactionContext ltransactionContext = transactionContext;
            if (ltransactionContext == null)
            {
                logger.Warn($"{ToString()} not reconnecting PeerEventServiceClient no transaction available ");

                return;
            }

            TransactionContext fltransactionContext = ltransactionContext.RetryTransactionSameContext();

            Channel.PeerOptions peerOptions = null != failedPeerEventServiceClient.GetPeerOptions() ? failedPeerEventServiceClient.GetPeerOptions() : Channel.PeerOptions.CreatePeerOptions();
            Task.Run(async () => { await ldisconnectedHandler.DisconnectedAsync(new PeerEventingServiceDisconnectEvent(this, throwable, peerOptions, fltransactionContext), token).ConfigureAwait(false); }, token);
        }

        public void ResetReconnectCount()
        {
            reconnectCount = 0L;
        }

        public void IncrementReconnectCount()
        {
            Interlocked.Increment(ref reconnectCount);
        }


        /**
         * Set class to handle Event hub disconnects
         *
         * @param newPeerEventingServiceDisconnectedHandler New handler to replace.  If set to null no retry will take place.
         * @return the old handler.
         */

        public IPeerEventingServiceDisconnected SetPeerEventingServiceDisconnected(IPeerEventingServiceDisconnected newPeerEventingServiceDisconnectedHandler)
        {
            IPeerEventingServiceDisconnected ret = _disconnectedHandler;
            _disconnectedHandler = newPeerEventingServiceDisconnectedHandler;
            return ret;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetLastBlockSeen(BlockEvent lastBlockSeen)
        {
            HasConnected = true;
            long newLastBlockNumber = lastBlockSeen.BlockNumber;
            // overkill but make sure.
            if (lastBlockNumber < newLastBlockNumber)
            {
                lastBlockNumber = newLastBlockNumber;
                LastBlockEvent = lastBlockSeen;
                if (IS_TRACE_LEVEL)
                    logger.Trace($"{ToString()} last block seen: {lastBlockNumber}");
            }
        }


        public override string ToString()
        {
            return $"Peer{{ id: {id}, name: {Name}, channelName: {channelName}, url: {Url}}}";
        }

        public interface IPeerEventingServiceDisconnected
        {
            /**
             * Called when a disconnect is detected in peer eventing service.
             *
             * @param event
             */
            Task DisconnectedAsync(IPeerEventingServiceDisconnectEvent evnt, CancellationToken token);
        }

        public interface IPeerEventingServiceDisconnectEvent
        {
            /**
             * The latest BlockEvent received by peer eventing service.
             *
             * @return The latest BlockEvent.
             */

            BlockEvent LatestBlockReceived { get; }

            /**
             * Last connect time
             *
             * @return Last connect time as reported by System.currentTimeMillis()
             */
            long LastConnectTime { get; }

            /**
             * Number reconnection attempts since last disconnection.
             *
             * @return reconnect attempts.
             */

            long ReconnectCount { get; }

            /**
             * Last exception throw for failing connection
             *
             * @return
             */

            Exception ExceptionThrown { get; }

            Task ReconnectAsync(long? startEvent, CancellationToken token);
        }


        public class PeerEventingServiceDisconnectEvent : IPeerEventingServiceDisconnectEvent
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private static readonly ILog logger = LogProvider.GetLogger(typeof(PeerEventingServiceDisconnectEvent));
            private readonly TransactionContext filteredTransactionContext;
            private readonly Peer peer;
            private readonly Channel.PeerOptions peerOptions;

            public PeerEventingServiceDisconnectEvent(Peer peer, Exception throwable, Channel.PeerOptions options, TransactionContext context)
            {
                this.peer = peer;
                ExceptionThrown = throwable;
                peerOptions = options;
                filteredTransactionContext = context;
            }


            public BlockEvent LatestBlockReceived => peer.LastBlockEvent;
            public long LastConnectTime => peer.LastConnectTime;
            public long ReconnectCount => peer.ReconnectCount;
            public Exception ExceptionThrown { get; }

            public async Task ReconnectAsync(long? startBlockNumber, CancellationToken token = default(CancellationToken))
            {
                logger.Trace("reconnecting startBLockNumber" + startBlockNumber);
                peer.IncrementReconnectCount();
                if (startBlockNumber == null)
                {
                    peerOptions.StartEventsNewest();
                }
                else
                {
                    peerOptions.StartEvents(startBlockNumber.Value);
                }

                PeerEventServiceClient lpeerEventingClient = new PeerEventServiceClient(peer, new Endpoint(peer.Url, peer.Properties), peer.Properties, peerOptions);
                await lpeerEventingClient.ConnectAsync(filteredTransactionContext, token).ConfigureAwait(false);
                peer.peerEventingClient = lpeerEventingClient;
            }
        }


        public class PeerEventingServiceDisconnect : IPeerEventingServiceDisconnected
        {
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private static readonly ILog logger = LogProvider.GetLogger(typeof(PeerEventingServiceDisconnect));
            private readonly long PEER_EVENT_RETRY_WAIT_TIME = Config.Instance.GetPeerRetryWaitTime();

            public async Task DisconnectedAsync(IPeerEventingServiceDisconnectEvent evnt, CancellationToken token = default(CancellationToken))
            {
                BlockEvent lastBlockEvent = evnt.LatestBlockReceived;
                Exception thrown = evnt.ExceptionThrown;
                long sleepTime = PEER_EVENT_RETRY_WAIT_TIME;

                if (thrown is PeerEventingServiceException)
                {
                    // means we connected and got an error or connected but timout waiting on the response
                    // not going away.. sleep longer.
                    sleepTime = Math.Min(5000L, PEER_EVENT_RETRY_WAIT_TIME + evnt.ReconnectCount * 100L); //wait longer if we connected.
                    //don't flood server.
                }

                long? startBlockNumber = null;

                if (null != lastBlockEvent)
                {
                    startBlockNumber = lastBlockEvent.BlockNumber;
                }

                if (0 != evnt.ReconnectCount)
                {
                    try
                    {
                        await Task.Delay((int) sleepTime, token).ConfigureAwait(false);
                        ;
                    }
                    catch (Exception e)
                    {
                        logger.Warn($"{ToString()} {e.Message}");
                    }
                }

                try
                {
                    await evnt.ReconnectAsync(startBlockNumber, token).ConfigureAwait(false);
                }
                catch (TransactionException e)
                {
                    logger.Warn($"{ToString()} {e.Message}");
                }
            }
        }
    } // end Peer

    /**
* Possible roles a peer can perform.
*/
    public enum PeerRole
    {
        ENDORSING_PEER,
        CHAINCODE_QUERY,
        LEDGER_QUERY,
        EVENT_SOURCE,
        SERVICE_DISCOVERY
    }

    public static class PeerRoleExtensions
    {
        public static string ToValue(this PeerRole role)
        {
            switch (role)
            {
                case PeerRole.ENDORSING_PEER:
                    return "endorsingPeer";
                case PeerRole.CHAINCODE_QUERY:
                    return "chaincodeQuery";
                case PeerRole.LEDGER_QUERY:
                    return "ledgerQuery";
                case PeerRole.EVENT_SOURCE:
                    return "eventSource";
                case PeerRole.SERVICE_DISCOVERY:
                    return "serviceDiscovery";
            }

            return string.Empty;
        }

        public static List<PeerRole> All() => Enum.GetValues(typeof(PeerRole)).Cast<PeerRole>().ToList();
        public static List<PeerRole> NoEventSource() => Enum.GetValues(typeof(PeerRole)).Cast<PeerRole>().Except(new[] {PeerRole.EVENT_SOURCE}).ToList();
        public static List<PeerRole> NoDiscovery() => Enum.GetValues(typeof(PeerRole)).Cast<PeerRole>().Except(new[] { PeerRole.SERVICE_DISCOVERY }).ToList();

        public static PeerRole PeerRoleFromValue(this string value)
        {
            switch (value)
            {
                case "endorsingPeer":
                    return PeerRole.ENDORSING_PEER;
                case "chaincodeQuery":
                    return PeerRole.CHAINCODE_QUERY;
                case "ledgerQuery":
                    return PeerRole.LEDGER_QUERY;
                case "eventSource":
                    return PeerRole.EVENT_SOURCE;
                case "serviceDiscovery":
                    return PeerRole.SERVICE_DISCOVERY;
            }

            return PeerRole.CHAINCODE_QUERY;
        }
    }
}