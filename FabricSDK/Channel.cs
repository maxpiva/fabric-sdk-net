/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *   http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Responses;

using Newtonsoft.Json;
using Config = Hyperledger.Fabric.SDK.Helper.Config;
using Metadata = Hyperledger.Fabric.Protos.Common.Metadata;
using ProposalResponse = Hyperledger.Fabric.SDK.Responses.ProposalResponse;
using Status = Hyperledger.Fabric.Protos.Common.Status;

namespace Hyperledger.Fabric.SDK
{
/**
 * The class representing a channel with which the client SDK interacts.
 * <p>
 */
    [Serializable]
    
    public class Channel
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Channel));
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private static readonly string SYSTEM_CHANNEL_NAME = "";

        private static readonly RNGCryptoServiceProvider RANDOM = new RNGCryptoServiceProvider();

        /**
         * load the peer organizations CA certificates into the channel's trust store so that we
         * can verify signatures from peer messages
         *
         * @throws InvalidArgumentException
         * @throws CryptoException
         */

        private static readonly AsyncLock _certificatelock = new AsyncLock();

        private readonly LinkedHashMap<string, BL> blockListeners = new LinkedHashMap<string, BL>();

        // final Set<Peer> eventingPeers = Collections.synchronizedSet(new HashSet<>());

        private readonly LinkedHashMap<string, ChaincodeEventListenerEntry> chainCodeListeners = new LinkedHashMap<string, ChaincodeEventListenerEntry>();

        private readonly long CHANNEL_CONFIG_WAIT_TIME = Config.Instance.GetChannelConfigWaitTime();
        /**
         * A queue each eventing hub will write events to.
         */

        private readonly long DELTA_SWEEP = Config.Instance.GetTransactionListenerCleanUpTimeout();


        private readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL ? Config.Instance.GetDiagnosticFileDumper() : null;

        private readonly long ORDERER_RETRY_WAIT_TIME = Config.Instance.GetOrdererRetryWaitTime();
        private readonly LinkedHashMap<string, LinkedList<TL>> txListeners = new LinkedHashMap<string, LinkedList<TL>>();
        private string blh = null;

        internal HFClient client;

        private LinkedList<EventHub> eventHubs = new LinkedList<EventHub>();
        /**
         * Runs processing events from event hubs.
         */

        private CancellationTokenSource eventQueueTokenSource = null;
        private Block genesisBlock;
        internal volatile bool initialized = false;
        private IReadOnlyDictionary<string, MSP> msps = new Dictionary<string, MSP>();
        internal LinkedList<Orderer> orderers = new LinkedList<Orderer>();

        // Name of the channel is only meaningful to the client
        private ConcurrentDictionary<Peer, PeerOptions> peerOptionsMap = new ConcurrentDictionary<Peer, PeerOptions>();

        private ConcurrentDictionary<PeerRole, List<Peer>> peerRoleSetMap = new ConcurrentDictionary<PeerRole, List<Peer>>();

        // The peers on this channel to which the client can connect
        internal List<Peer> peers = new List<Peer>();

        //Cleans up any transaction listeners that will probably never complete.
        private Timer sweeper = null;

        public Channel(string name, HFClient client) : this(name, client, false)
        {
        }


        /**
         * @param name
         * @param client
         * @throws InvalidArgumentException
         */

        private Channel(string name, HFClient client, bool systemChannel)
        {
            ChannelEventQueue = new ChannelEventQue(this);
            FillRoles();
            IsSystemChannel = systemChannel;
            if (systemChannel)
            {
                name = SYSTEM_CHANNEL_NAME; //It's special !
                initialized = true;
            }
            else
            {
                if (string.IsNullOrEmpty(name))
                    throw new InvalidArgumentException("Channel name is invalid can not be null or empty.");
            }
            Name = name;
            this.client = client ?? throw new InvalidArgumentException("Channel client is invalid can not be null.");
            logger.Debug($"Creating channel: {(IsSystemChannel ? "SYSTEM_CHANNEL" : name)}, client context {client.UserContext.Name}");
        }

        public Channel()
        {
            initialized = false;
            IsShutdown = false;
            msps = new Dictionary<string, MSP>();
            txListeners = new LinkedHashMap<string, LinkedList<TL>>();
            ChannelEventQueue = new ChannelEventQue(this);
            blockListeners = new LinkedHashMap<string, BL>();
        }

        /**
         * Get all Event Hubs on this channel.
         *
         * @return Event Hubs
         */
        
        public IReadOnlyList<EventHub> EventHubs
        {
            get { return eventHubs.ToList(); }
            private set { eventHubs = new LinkedList<EventHub>(value); }
        }

        
        public IReadOnlyList<Orderer> Orderers
        {
            get { return orderers.ToList(); }
            private set { orderers = new LinkedList<Orderer>(value); }
        }

        /**
         * Get the channel name
         *
         * @return The name of the channel
         */
        
        public string Name { get; internal set; }

        
        public Dictionary<Peer, PeerOptions> PeerOptionsMap
        {
            get { return peerOptionsMap.ToDictionary(a => a.Key, a => a.Value); }
            private set { peerOptionsMap = new ConcurrentDictionary<Peer, PeerOptions>(value); }
        }

        
        public Dictionary<PeerRole, List<Peer>> PeerRoleMap
        {
            get { return peerRoleSetMap.ToDictionary(a => a.Key, a => a.Value.ToList()); }
            private set { peerRoleSetMap = new ConcurrentDictionary<PeerRole, List<Peer>>(value); }
        }


        
        public bool IsSystemChannel { get; private set; }

        /**
         * Get the peers for this channel.
         *
         * @return the peers.
         */
        
        public IReadOnlyList<Peer> Peers
        {
            get { return peers; }
            private set { peers = value.ToList(); }
        }


        /**
         * Is the channel shutdown.
         *
         * @return return true if the channel is shutdown.
         */
        [JsonIgnore]
        public bool IsShutdown { get; internal set; } = false;

        [JsonIgnore]
        public ChannelEventQue ChannelEventQueue { get; }

        /**
         * Is channel initialized.
         *
         * @return true if the channel has been initialized.
         */
        [JsonIgnore]
        public bool IsInitialized
        {
            get { return initialized; }
        }


        private async Task InitChannelAsync(Orderer orderer, ChannelConfiguration channelConfiguration, CancellationToken token, params byte[][] signers)
        {
            logger.Debug($"Creating new channel {Name} on the Fabric");
            Channel ordererChannel = orderer.Channel;
            try
            {
                AddOrderer(orderer);
                //-----------------------------------------
                Envelope ccEnvelope = Envelope.Parser.ParseFrom(channelConfiguration.ChannelConfigurationBytes);
                Payload ccPayload = Payload.Parser.ParseFrom(ccEnvelope.Payload);
                ChannelHeader ccChannelHeader = ChannelHeader.Parser.ParseFrom(ccPayload.Header.ChannelHeader);
                if (ccChannelHeader.Type != (int) HeaderType.ConfigUpdate)
                    throw new InvalidArgumentException($"Creating channel; {Name} expected config block type {HeaderType.ConfigUpdate}, but got: {ccChannelHeader.Type}");
                if (!Name.Equals(ccChannelHeader.ChannelId))
                    throw new InvalidArgumentException($"Expected config block for channel: {Name}, but got: {ccChannelHeader.ChannelId}");
                ConfigUpdateEnvelope configUpdateEnv = ConfigUpdateEnvelope.Parser.ParseFrom(ccPayload.Data);
                ByteString configUpdate = configUpdateEnv.ConfigUpdate;
                await SendUpdateChannelAsync(configUpdate.ToByteArray(), signers, orderer, token);
                //         final ConfigUpdateEnvelope.Builder configUpdateEnvBuilder = configUpdateEnv.toBuilder();`
                //---------------------------------------
                //          sendUpdateChannel(channelConfiguration, signers, orderer);
                await GetGenesisBlockAsync(orderer, token); // get Genesis block to make sure channel was created.
                if (genesisBlock == null)
                    throw new TransactionException($"New channel {Name} error. Genesis bock returned null");
                logger.Debug($"Created new channel {Name} on the Fabric done.");
            }
            catch (TransactionException e)
            {
                orderer.UnsetChannel();
                if (null != ordererChannel)
                    orderer.Channel = ordererChannel;
                logger.ErrorException($"Channel {Name} error: {e.Message}", e);
                throw e;
            }
            catch (Exception e)
            {
                orderer.UnsetChannel();
                if (null != ordererChannel)
                    orderer.Channel = ordererChannel;
                string msg = $"Channel {Name} error: {e.Message}";
                logger.ErrorException(msg, e);
                throw new TransactionException(msg, e);
            }
        }

        public static Channel Deserialize(string json)
        {
            Channel ch = JsonConvert.DeserializeObject<Channel>(json);
            foreach (EventHub eventHub in ch.eventHubs.ToList())
                eventHub.SetEventQue(ch.ChannelEventQueue);
            return ch;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }


        public void FillRoles()
        {
            foreach (PeerRole r in Enum.GetValues(typeof(PeerRole)))
                peerRoleSetMap.TryAdd(r, new List<Peer>());
        }

        /**
         * For requests that are not targeted for a specific channel.
         * User's can not directly create this channel.
         *
         * @param client
         * @return a new system channel.
         * @throws InvalidArgumentException
         */

        public static Channel CreateSystemChannel(HFClient client)
        {
            return new Channel(SYSTEM_CHANNEL_NAME, client, true);
        }

        /**
         * createNewInstance
         *
         * @param name
         * @return A new channel
         */
        public static Channel Create(string name, HFClient clientContext)
        {
            return new Channel(name, clientContext);
        }

        public static async Task<Channel> CreateAsync(string name, HFClient hfClient, Orderer orderer, ChannelConfiguration channelConfiguration, CancellationToken token = default(CancellationToken), params byte[][] signers)
        {
            Channel ch = new Channel(name, hfClient);
            await ch.InitChannelAsync(orderer, channelConfiguration, token, signers);
            return ch;
        }

        private static void CheckHandle(string tag, string handle)
        {
            if (string.IsNullOrEmpty(handle))
                throw new InvalidArgumentException("Handle is invalid.");
            if (!handle.StartsWith(tag) || !handle.EndsWith(tag))
                throw new InvalidArgumentException("Handle is wrong type.");
        }

        /**
         * Update channel with specified channel configuration
         *
         * @param updateChannelConfiguration Updated Channel configuration
         * @param signers                    signers
         * @throws TransactionException
         * @throws InvalidArgumentException
         */

        public void UpdateChannelConfiguration(UpdateChannelConfiguration updateChannelConfiguration, params byte[] signers)
        {
            UpdateChannelConfiguration(updateChannelConfiguration, GetRandomOrderer(), signers);
        }

        public Task UpdateChannelConfigurationAsync(UpdateChannelConfiguration updateChannelConfiguration, CancellationToken token = default(CancellationToken), params byte[][] signers)
        {
            return UpdateChannelConfigurationAsync(updateChannelConfiguration, GetRandomOrderer(), token, signers);
        }
        /**
         * Update channel with specified channel configuration
         *
         * @param updateChannelConfiguration Channel configuration
         * @param signers                    signers
         * @param orderer                    The specific orderer to use.
         * @throws TransactionException
         * @throws InvalidArgumentException
         */



        public void UpdateChannelConfiguration(UpdateChannelConfiguration updateChannelConfiguration, Orderer orderer, params byte[][] signers)
        {
            UpdateChannelConfigurationAsync(updateChannelConfiguration, orderer, new CancellationToken(), signers).RunAndUnwarp();
        }

        public async Task UpdateChannelConfigurationAsync(UpdateChannelConfiguration updateChannelConfiguration, Orderer orderer, CancellationToken token = default(CancellationToken), params byte[][] signers)
        {
            CheckChannelState();
            CheckOrderer(orderer);
            try
            {
                long startLastConfigIndex = await GetLastConfigIndexAsync(orderer, token);
                logger.Trace($"startLastConfigIndex: {startLastConfigIndex}. Channel config wait time is: {CHANNEL_CONFIG_WAIT_TIME}");
                await SendUpdateChannelAsync(updateChannelConfiguration.UpdateChannelConfigurationBytes, signers, orderer, token);
                long currentLastConfigIndex = -1;
                Stopwatch timer = new Stopwatch();
                timer.Start();
                //Try to wait to see the channel got updated but don't fail if we don't see it.
                do
                {
                    currentLastConfigIndex = await GetLastConfigIndexAsync(orderer, token);
                    if (currentLastConfigIndex == startLastConfigIndex)
                    {
                        timer.Stop();
                        long duration = timer.ElapsedMilliseconds;
                        if (duration > CHANNEL_CONFIG_WAIT_TIME)
                        {
                            logger.Warn($"Channel {Name} did not get updated last config after {duration} ms, Config wait time: {CHANNEL_CONFIG_WAIT_TIME} ms. startLastConfigIndex: {startLastConfigIndex}, currentLastConfigIndex: {currentLastConfigIndex}");
                            //waited long enough ..
                            currentLastConfigIndex = startLastConfigIndex - 1L; // just bail don't throw exception.
                        }
                        else
                        {
                            try
                            {
                                Thread.Sleep((int) ORDERER_RETRY_WAIT_TIME); //try again sleep
                            }
                            catch (Exception e)
                            {
                                TransactionException te = new TransactionException("update channel thread Sleep", e);
                                logger.WarnException(te.Message, te);
                            }
                        }
                    }
                    logger.Trace($"currentLastConfigIndex: {currentLastConfigIndex}");
                } while (currentLastConfigIndex == startLastConfigIndex);
            }
            catch (TransactionException e)
            {
                logger.ErrorException($"Channel {Name} error: {e.Message}", e);
                throw e;
            }
            catch (Exception e)
            {
                string msg = $"Channel {Name} error: {e.Message}";
                logger.ErrorException(msg, e);
                throw new TransactionException(msg, e);
            }
        }

        private async Task SendUpdateChannelAsync(byte[] configupdate, byte[][] signers, Orderer orderer, CancellationToken token)
        {
            logger.Debug($"Channel {Name} sendUpdateChannel");
            CheckOrderer(orderer);
            try
            {
                Stopwatch watch = new Stopwatch();
                watch.Start();
                Status statusCode = Status.Unknown;
                do
                {
                    //Make sure we have fresh transaction context for each try just to be safe.
                    TransactionContext transactionContext = GetTransactionContext();
                    ConfigUpdateEnvelope configUpdateEnv = new ConfigUpdateEnvelope {ConfigUpdate = ByteString.CopyFrom(configupdate)};
                    foreach (byte[] signer in signers)
                        configUpdateEnv.Signatures.Add(ConfigSignature.Parser.ParseFrom(signer));
                    //--------------
                    // Construct Payload Envelope.
                    ByteString sigHeaderByteString = ProtoUtils.GetSignatureHeaderAsByteString(transactionContext);
                    ChannelHeader payloadChannelHeader = ProtoUtils.CreateChannelHeader(HeaderType.ConfigUpdate, transactionContext.TxID, Name, transactionContext.Epoch, transactionContext.FabricTimestamp, null, null);
                    Header payloadHeader = new Header {ChannelHeader = payloadChannelHeader.ToByteString(), SignatureHeader = sigHeaderByteString};
                    ByteString payloadByteString = new Payload {Header = payloadHeader, Data = configUpdateEnv.ToByteString()}.ToByteString();
                    ByteString payloadSignature = transactionContext.SignByteStrings(payloadByteString);
                    Envelope payloadEnv = new Envelope {Signature = payloadSignature, Payload = payloadByteString};
                    BroadcastResponse trxResult = await orderer.SendTransactionAsync(payloadEnv, token);
                    statusCode = trxResult.Status;
                    logger.Debug($"Channel {Name} sendUpdateChannel {statusCode}");
                    if (statusCode == Status.NotFound || statusCode == Status.ServiceUnavailable)
                    {
                        // these we can retry..
                        long duration = watch.ElapsedMilliseconds;
                        if (duration > CHANNEL_CONFIG_WAIT_TIME)
                        {
                            //waited long enough .. throw an exception
                            string info = trxResult.Info ?? "";
                            throw new TransactionException($"Channel {Name} update error timed out after {duration} ms. Status value {statusCode}. Status {info}");
                        }
                        try
                        {
                            Thread.Sleep((int) ORDERER_RETRY_WAIT_TIME); //try again sleep
                        }
                        catch (Exception e)
                        {
                            TransactionException te = new TransactionException("update thread Sleep", e);
                            logger.WarnException(te.Message, te);
                        }
                    }
                    else if (Status.Success != statusCode)
                    {
                        // Can't retry.
                        string info = trxResult.Info ?? "";
                        throw new TransactionException($"New channel {Name} error. StatusValue {statusCode}. Status {info}");
                    }
                } while (Status.Success != statusCode); // try again
            }
            catch (TransactionException e)
            {
                logger.ErrorException($"Channel {Name} error: {e.Message}", e);
                throw e;
            }
            catch (Exception e)
            {
                string msg = $"Channel {Name} error: {e.Message}";

                logger.ErrorException(msg, e);
                throw new TransactionException(msg, e);
            }
        }
        [JsonIgnore]
        public IEnrollment Enrollment => client.UserContext.Enrollment;

        /**
         * Add a peer to the channel
         *
         * @param peer The Peer to add.
         * @return Channel The current channel added.
         * @throws InvalidArgumentException
         */
        public Channel AddPeer(Peer peer)
        {
            return AddPeer(peer, PeerOptions.CreatePeerOptions());
        }

        /**
         * Add a peer to the channel
         *
         * @param peer        The Peer to add.
         * @param peerOptions see {@link PeerRole}
         * @return Channel The current channel added.
         * @throws InvalidArgumentException
         */
        public Channel AddPeer(Peer peer, PeerOptions peerOptions)
        {
            if (IsShutdown)
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            if (initialized)
                throw new InvalidArgumentException($"Channel {Name} has been initialized.");
            if (null == peer)
                throw new InvalidArgumentException("Peer is invalid can not be null.");
            if (peer.Channel != null && peer.Channel != this)
                throw new InvalidArgumentException($"Peer already connected to channel {peer.Channel.Name}");
            if (null == peerOptions)
                throw new InvalidArgumentException("Peer is invalid can not be null.");
            peer.Channel = this;
            peers.Add(peer);
            peerOptionsMap[peer] = peerOptions.Clone();
            foreach (PeerRole peerRole in peerRoleSetMap.Keys)
            {
                if (peerOptions.PeerRoles.Contains(peerRole))
                    peerRoleSetMap[peerRole].Add(peer);
            }
            return this;
        }


        private IReadOnlyList<Peer> GetEventingPeers()
        {
            return peerRoleSetMap[PeerRole.EVENT_SOURCE]?.ToList() ?? new List<Peer>();
        }

        private IReadOnlyList<Peer> GetEndorsingPeers()
        {
            return peerRoleSetMap[PeerRole.ENDORSING_PEER]?.ToList() ?? new List<Peer>();
        }

        private IReadOnlyList<Peer> GetChaincodePeers()
        {
            return GetPeers(new[] {PeerRole.CHAINCODE_QUERY, PeerRole.ENDORSING_PEER});
        }

        private IReadOnlyList<Peer> GetChaincodeQueryPeers()
        {
            return peerRoleSetMap[PeerRole.CHAINCODE_QUERY]?.ToList() ?? new List<Peer>();
        }

        private IReadOnlyList<Peer> GetLedgerQueryPeers()
        {
            return peerRoleSetMap[PeerRole.LEDGER_QUERY]?.ToList() ?? new List<Peer>();
        }

        /**
         * Join the peer to the channel. The peer is added with all roles see {@link PeerOptions}
         *
         * @param peer the peer to join the channel.
         * @return
         * @throws ProposalException
         */
        public Channel JoinPeer(Peer peer)
        {
            return JoinPeer(peer, PeerOptions.CreatePeerOptions());
        }

        public Task<Channel> JoinPeerAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            return JoinPeerAsync(peer, PeerOptions.CreatePeerOptions(), token);
        }

        /**
         * @param peer        the peer to join the channel.
         * @param peerOptions see {@link PeerOptions}
         * @return
         * @throws ProposalException
         */
        public Channel JoinPeer(Peer peer, PeerOptions peerOptions)
        {
            return JoinPeerAsync(peer, peerOptions).RunAndUnwarp();
        }

        public async Task<Channel> JoinPeerAsync(Peer peer, PeerOptions peerOptions, CancellationToken token = default(CancellationToken))
        {
            try
            {
                return await JoinPeerAsync(GetRandomOrderer(), peer, peerOptions, token);
            }
            catch (ProposalException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new ProposalException(e);
            }
        }

        /**
         * Join peer to channel
         *
         * @param orderer     The orderer to get the genesis block.
         * @param peer        the peer to join the channel.
         * @param peerOptions see {@link PeerOptions}
         * @return
         * @throws ProposalException
         */
        public Channel JoinPeer(Orderer orderer, Peer peer, PeerOptions peerOptions)
        {
            return JoinPeerAsync(orderer, peer, peerOptions).RunAndUnwarp();
        }

        public async Task<Channel> JoinPeerAsync(Orderer orderer, Peer peer, PeerOptions peerOptions, CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"Channel {Name} joining peer {peer.Name}, url: {peer.Url}");
            if (IsShutdown)
                throw new ProposalException($"Channel {Name} has been shutdown.");
            Channel peerChannel = peer.Channel;
            if (null != peerChannel && peerChannel != this)
                throw new ProposalException($"Can not add peer {peer.Name} to channel {Name} because it already belongs to channel {peerChannel.Name}.");
            if (genesisBlock == null && orderers.Count == 0)
            {
                ProposalException e = new ProposalException("Channel missing genesis block and no orderers configured");
                logger.ErrorException(e.Message, e);
            }
            try
            {
                genesisBlock = await GetGenesisBlockAsync(orderer, token);
                logger.Debug($"Channel {Name} got genesis block");
                Channel systemChannel = CreateSystemChannel(client); //channel is not really created and this is targeted to system channel
                TransactionContext transactionContext = systemChannel.GetTransactionContext();
                Proposal joinProposal = JoinPeerProposalBuilder.Create().Context(transactionContext).GenesisBlock(genesisBlock).Build();
                logger.Debug("Getting signed proposal.");
                SignedProposal signedProposal = GetSignedProposal(transactionContext, joinProposal);
                logger.Debug("Got signed proposal.");
                AddPeer(peer, peerOptions); //need to add peer.
                List<ProposalResponse> resp = await SendProposalToPeersAsync(new[] {peer}, signedProposal, transactionContext, token);
                ProposalResponse pro = resp.First();
                if (pro.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    logger.Info($"Peer {peer.Name} joined into channel {Name}");
                else
                {
                    RemovePeerInternal(peer);
                    throw new ProposalException($"Join peer to channel {Name} failed.  Status {pro.Status}, details: {pro.Message}");
                }
            }
            catch (ProposalException e)
            {
                RemovePeerInternal(peer);
                logger.ErrorException(e.Message, e);
                throw e;
            }
            catch (Exception e)
            {
                peers.Remove(peer);
                logger.ErrorException(e.Message, e);
                throw new ProposalException(e.Message, e);
            }
            return this;
        }

        private async Task<Block> GetConfigBlockAsync(List<Peer> peers, CancellationToken token)
        {
            //   logger.debug(format("getConfigBlock for channel %s with peer %s, url: %s", name, peer.getName(), peer.getUrl()));

            if (IsShutdown)
                throw new ProposalException($"Channel {Name} has been shutdown.");
            if (peers.Count == 0)
                throw new ProposalException("No peers go get config block");
            TransactionContext transactionContext = null;
            SignedProposal signedProposal = null;
            try
            {
                transactionContext = GetTransactionContext();
                transactionContext.Verify = false; // can't verify till we get the config block.
                Proposal proposal = GetConfigBlockBuilder.Create().Context(transactionContext).ChannelId(Name).Build();
                logger.Debug("Getting signed proposal.");
                signedProposal = GetSignedProposal(transactionContext, proposal);
                logger.Debug("Got signed proposal.");
            }
            catch (Exception e)
            {
                throw new ProposalException(e);
            }
            ProposalException lastException = new ProposalException($"GetConfigBlock for channel {Name} failed.");
            foreach (Peer peer in peers)
            {
                try
                {
                    List<ProposalResponse> resp = await SendProposalToPeersAsync(new[] {peer}, signedProposal, transactionContext, token);
                    if (resp.Count > 0)
                    {
                        ProposalResponse pro = resp.First();
                        if (pro.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                        {
                            logger.Trace($"getConfigBlock from peer {peer.Name} on channel {Name} success");
                            return Block.Parser.ParseFrom(pro.ProtoProposalResponse.Response.Payload.ToByteArray());
                        }
                        lastException = new ProposalException($"GetConfigBlock for channel {Name} failed with peer {peer.Name}.  Status {pro.Status}, details: {pro.Message}");
                        logger.Warn(lastException.Message);
                    }
                    else
                        logger.Warn($"Got empty proposals from {peer.Name}");
                }
                catch (Exception e)
                {
                    lastException = new ProposalException($"GetConfigBlock for channel {Name} failed with peer {peer.Name}.", e);
                    logger.Warn(lastException.Message);
                }
            }
            throw lastException;
        }

        /**
         * Removes the peer connection from the channel.
         * This does NOT unjoin the peer from from the channel.
         * Fabric does not support that at this time -- maybe some day, but not today
         *
         * @param peer
         */
        public void RemovePeer(Peer peer)
        {
            if (initialized)
                throw new InvalidArgumentException($"Can not remove peer from channel {Name} already initialized.");
            if (IsShutdown)
                throw new InvalidArgumentException($"Can not remove peer from channel {Name} already shutdown.");
            CheckPeer(peer);
            RemovePeerInternal(peer);
        }

        private void RemovePeerInternal(Peer peer)
        {
            peers.Remove(peer);
            peerOptionsMap.TryRemove(peer, out _);
            foreach (List<Peer> peerRoleSet in peerRoleSetMap.Values)
                peerRoleSet.Remove(peer);
            peer.UnsetChannel();
        }

        /**
         * Add an Orderer to this channel.
         *
         * @param orderer the orderer to add.
         * @return this channel.
         * @throws InvalidArgumentException
         */

        public Channel AddOrderer(Orderer orderer)
        {
            if (IsShutdown)
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            if (null == orderer)
            {
                throw new InvalidArgumentException("Orderer is invalid can not be null.");
            }
            logger.Debug($"Channel {Name} adding orderer {orderer.Name}, url: {orderer.Url}");
            orderer.Channel = this;
            orderers.AddLast(orderer);
            return this;
        }

        public PeerOptions GetPeersOptions(Peer peer)
        {
            PeerOptions ret = null;
            peerOptionsMap.TryGetValue(peer, out ret);
            return ret?.Clone();
        }

        /**
         * Add an Event Hub to this channel.
         *
         * @param eventHub
         * @return this channel
         * @throws InvalidArgumentException
         */

        public Channel AddEventHub(EventHub eventHub)
        {
            if (IsShutdown)
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            if (null == eventHub)
                throw new InvalidArgumentException("EventHub is invalid can not be null.");
            logger.Debug($"Channel {Name} adding event hub {eventHub.Name}, url: {eventHub.Url}");
            eventHub.Channel = this;
            eventHub.SetEventQue(ChannelEventQueue);
            eventHubs.AddLast(eventHub);
            return this;
        }

        /**
         * Get the peers for this channel.
         *
         * @return the peers.
         */
        public List<Peer> GetPeers(IEnumerable<PeerRole> roles)
        {
            HashSet<Peer> ret = new HashSet<Peer>();

            foreach (PeerRole peerRole in roles)
            {
                if (peerRoleSetMap.ContainsKey(peerRole))
                {
                    foreach (Peer p in peerRoleSetMap[peerRole])
                    {
                        if (!ret.Contains(p))
                            ret.Add(p);
                    }
                }
            }
            return ret.ToList();
        }

        public List<Peer> GetPeers(params PeerRole[] roles)
        {
            return GetPeers((IEnumerable<PeerRole>) roles);
        }
        /**
         * Set peerOptions in the channel that has not be initialized yet.
         *
         * @param peer        the peer to set options on.
         * @param peerOptions see {@link PeerOptions}
         * @return old options.
         */

        public PeerOptions SetPeerOptions(Peer peer, PeerOptions peerOptions)
        {
            if (initialized)
                throw new InvalidArgumentException($"Channel {Name} already initialized.");
            CheckPeer(peer);
            PeerOptions ret = GetPeersOptions(peer);
            RemovePeerInternal(peer);
            AddPeer(peer, peerOptions);
            return ret;
        }

        /**
         * Initialize the Channel.  Starts the channel. event hubs will connect.
         *
         * @return this channel.
         * @throws InvalidArgumentException
         * @throws TransactionException
         */
        public Channel Initialize()
        {
            return InitializeAsync().RunAndUnwarp();
        }

        public async Task<Channel> InitializeAsync(CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"Channel {Name} initialize shutdown {IsShutdown}");

            if (IsShutdown)
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            if (string.IsNullOrEmpty(Name))
                throw new InvalidArgumentException("Can not initialize channel without a valid name.");
            if (client == null)
                throw new InvalidArgumentException("Can not initialize channel without a client object.");
            client.UserContext.UserContextCheck();
            try
            {
                await LoadCACertificatesAsync(token); // put all MSP certs into cryptoSuite if this fails here we'll try again later.
            }
            catch (Exception e)
            {
                logger.Warn($"Channel {Name} could not load peer CA certificates from any peers.");
            }
            try
            {
                logger.Debug($"Eventque started");
                foreach (EventHub eh in eventHubs)
                {
                    //Connect all event hubs
                    await eh.Connect(GetTransactionContext(), token);
                }
                foreach (Peer peer in GetEventingPeers())
                    await peer.InitiateEventing(GetTransactionContext(), GetPeersOptions(peer), token);
                logger.Debug($"{eventHubs.Count} eventhubs initialized");
                RegisterTransactionListenerProcessor(); //Manage transactions.
                logger.Debug($"Channel {Name} registerTransactionListenerProcessor completed");
                StartEventQue(); //Run the event for event messages from event hubs.
                initialized = true;
                logger.Debug($"Channel {Name} initialized");
                return this;
                //        } catch (TransactionException e) {
                //            logger.error(e.getMessage(), e);
                //            throw e;
            }
            catch (Exception e)
            {
                TransactionException exp = new TransactionException(e);
                logger.ErrorException(exp.Message, exp);
                throw exp;
            }
        }

        protected async Task LoadCACertificatesAsync(CancellationToken token)
        {
            using (_certificatelock.LockAsync(token))
            {
                if (msps != null && msps.Count > 0)
                    return;
                logger.Debug($"Channel {Name} loadCACertificates");
                await ParseConfigBlockAsync(token);
                if (msps == null || msps.Count == 0)
                    throw new InvalidArgumentException("Unable to load CA certificates. Channel " + Name + " does not have any MSPs.");
                List<byte[]> certList;
                foreach (MSP msp in msps.Values)
                {
                    logger.Debug($"loading certificates for MSP {msp.ID}: ");
                    certList = msp.RootCerts.ToList();
                    if (certList.Count > 0)
                        certList.ForEach(a=> client.CryptoSuite.Store.AddCertificate(a.ToUTF8String()));
                    certList = msp.IntermediateCerts.ToList();
                    if (certList.Count > 0)
                        certList.ForEach(a => client.CryptoSuite.Store.AddCertificate(a.ToUTF8String()));
                    // not adding admin certs. Admin certs should be signed by the CA
                }
                logger.Debug($"Channel {Name} loadCACertificates completed ");
            }
        }

        private async Task<Block> GetGenesisBlockAsync(Orderer orderer, CancellationToken token)
        {
            try
            {
                if (genesisBlock != null)
                    logger.Debug($"Channel {Name} getGenesisBlock already present");
                else
                {
                    long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    SeekSpecified seekSpecified = new SeekSpecified {Number = 0};
                    SeekPosition seekPosition = new SeekPosition {Specified = seekSpecified};
                    SeekSpecified seekStopSpecified = new SeekSpecified {Number = 0};
                    SeekPosition seekStopPosition = new SeekPosition {Specified = seekStopSpecified};
                    SeekInfo seekInfo = new SeekInfo {Start = seekPosition, Stop = seekStopPosition, Behavior = SeekInfo.Types.SeekBehavior.BlockUntilReady};
                    List<DeliverResponse> deliverResponses = new List<DeliverResponse>();
                    await SeekBlockAsync(seekInfo, deliverResponses, orderer, token);
                    DeliverResponse blockresp = deliverResponses[1];
                    Block configBlock = blockresp.Block;
                    if (configBlock == null)
                        throw new TransactionException($"In getGenesisBlock newest block for channel {Name} fetch bad deliver returned null:");
                    int dataCount = configBlock.Data.Data.Count;
                    if (dataCount < 1)
                        throw new TransactionException($"In getGenesisBlock bad config block data count {dataCount}");
                    genesisBlock = blockresp.Block;
                }
            }
            catch (TransactionException e)
            {
                logger.ErrorException(e.Message, e);
                throw e;
            }
            catch (Exception e)
            {
                TransactionException exp = new TransactionException("getGenesisBlock " + e.Message, e);
                logger.ErrorException(exp.Message, exp);
                throw exp;
            }
            if (genesisBlock == null)
            {
                //make sure it was really set.
                TransactionException exp = new TransactionException("getGenesisBlock returned null");
                logger.Error(exp.Message, exp);
                throw exp;
            }
            logger.Debug($"Channel {Name} getGenesisBlock done.");
            return genesisBlock;
        }

        /**
         * Get signed byes of the update channel.
         *
         * @param updateChannelConfiguration
         * @param signer
         * @return
         * @throws InvalidArgumentException
         */
        public byte[] GetUpdateChannelConfigurationSignature(UpdateChannelConfiguration updateChannelConfiguration, IUser signer)
        {
            signer.UserContextCheck();
            if (null == updateChannelConfiguration)
                throw new InvalidArgumentException("channelConfiguration is null");
            try
            {
                TransactionContext transactionContext = GetTransactionContext(signer);
                ByteString configUpdate = ByteString.CopyFrom(updateChannelConfiguration.UpdateChannelConfigurationBytes);
                ByteString sigHeaderByteString = ProtoUtils.GetSignatureHeaderAsByteString(signer, transactionContext);
                ByteString signatureByteSting = transactionContext.SignByteStrings(new IUser[] {signer}, sigHeaderByteString, configUpdate)[0];
                return new ConfigSignature {SignatureHeader = sigHeaderByteString, Signature = signatureByteSting}.ToByteArray();
            }
            catch (Exception e)
            {
                throw new InvalidArgumentException(e);
            }
            finally
            {
                logger.Debug("finally done");
            }
        }

        protected async Task ParseConfigBlockAsync(CancellationToken token)
        {
            IReadOnlyDictionary<string, MSP> lmsps = msps;
            if (lmsps != null && lmsps.Count > 0)
                return;
            try
            {
                Block parseFrom = await GetConfigBlockAsync(GetShuffledPeers(), token);
                // final Block configBlock = getConfigurationBlock();
                logger.Debug($"Channel {Name} Got config block getting MSP data and anchorPeers data");
                Envelope envelope = Envelope.Parser.ParseFrom(parseFrom.Data.Data[0]);
                Payload payload = Payload.Parser.ParseFrom(envelope.Payload);
                ConfigEnvelope configEnvelope = ConfigEnvelope.Parser.ParseFrom(payload.Data);
                ConfigGroup channelGroup = configEnvelope.Config.ChannelGroup;
                Dictionary<string, MSP> newMSPS = TraverseConfigGroupsMSP(string.Empty, channelGroup, new Dictionary<string, MSP>());
                msps = newMSPS;
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new TransactionException(e);
            }
        }

        private Dictionary<string, MSP> TraverseConfigGroupsMSP(string name, ConfigGroup configGroup, Dictionary<string, MSP> msps)
        {
            ConfigValue mspv = configGroup.Values.ContainsKey("MSP") ? configGroup.Values["MSP"] : null;
            if (null != mspv)
            {
                if (!msps.ContainsKey(name))
                {
                    MSPConfig mspConfig = MSPConfig.Parser.ParseFrom(mspv.Value);
                    FabricMSPConfig fabricMSPConfig = FabricMSPConfig.Parser.ParseFrom(mspConfig.Config);
                    msps.Add(name, new MSP(name, fabricMSPConfig));
                }
            }
            foreach (string key in configGroup.Groups.Keys)
                TraverseConfigGroupsMSP(key, configGroup.Groups[key], msps);
            return msps;
        }

        /**
         * Provide the Channel's latest raw Configuration Block.
         *
         * @return Channel configuration block.
         * @throws TransactionException
         */

        private async Task<Block> GetConfigurationBlockAsync(CancellationToken token)
        {
            logger.Debug($"getConfigurationBlock for channel {Name}");
            try
            {
                Orderer orderer = GetRandomOrderer();
                long lastConfigIndex = await GetLastConfigIndexAsync(orderer, token);
                logger.Debug($"Last config index is {lastConfigIndex}x");
                Block configBlock = await GetBlockByNumberAsync(lastConfigIndex, token);
                //Little extra parsing but make sure this really is a config block for this channel.
                Envelope envelopeRet = Envelope.Parser.ParseFrom(configBlock.Data.Data[0]);
                Payload payload = Payload.Parser.ParseFrom(envelopeRet.Payload);
                ChannelHeader channelHeader = ChannelHeader.Parser.ParseFrom(payload.Header.ChannelHeader);
                if (channelHeader.Type != (int) HeaderType.Config)
                    throw new TransactionException($"Bad last configuration block type {channelHeader.Type}, expected {(int) HeaderType.Config}");
                if (!Name.Equals(channelHeader.ChannelId))
                    throw new TransactionException($"Bad last configuration block channel id {channelHeader.ChannelId}, expected {Name}");
                if (null != diagnosticFileDumper)
                    logger.Trace($"Channel {Name} getConfigurationBlock returned {diagnosticFileDumper.CreateDiagnosticFile(configBlock.ToString().ToBytes())}");
                if (!logger.IsTraceEnabled())
                    logger.Debug($"Channel {Name} getConfigurationBlock returned");
                return configBlock;
            }
            catch (TransactionException e)
            {
                logger.ErrorException(e.Message, e);
                throw e;
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new TransactionException(e);
            }
        }

        /**
         * Channel Configuration bytes. Bytes that can be used with configtxlator tool to upgrade the channel.
         * Convert to Json for editing  with:
         * {@code
         * <p>
         * curl -v   POST --data-binary @fooConfig http://host/protolator/decode/common.Config
         * <p>
         * }
         * See http://hyperledger-fabric.readthedocs.io/en/latest/configtxlator.html
         *
         * @return Channel configuration bytes.
         * @throws TransactionException
         */
        public byte[] GetChannelConfigurationBytes()
        {
            return GetChannelConfigurationBytesAsync().RunAndUnwarp();
        }

        public async Task<byte[]> GetChannelConfigurationBytesAsync(CancellationToken token = default(CancellationToken))
        {
            try
            {
                Block configBlock = await GetConfigBlockAsync(GetShuffledPeers(), token);
                Envelope envelopeRet = Envelope.Parser.ParseFrom(configBlock.Data.Data[0]);
                Payload payload = Payload.Parser.ParseFrom(envelopeRet.Payload);
                ConfigEnvelope configEnvelope = ConfigEnvelope.Parser.ParseFrom(payload.Data);
                return configEnvelope.Config.ToByteArray();
            }
            catch (Exception e)
            {
                throw new TransactionException(e);
            }
        }

        private async Task<long> GetLastConfigIndexAsync(Orderer orderer, CancellationToken token)
        {
            Block latestBlock = await GetLatestBlockAsync(orderer, token);
            BlockMetadata blockMetadata = latestBlock.Metadata;
            Metadata metaData = Metadata.Parser.ParseFrom(blockMetadata.Metadata[1]);
            LastConfig lastConfig = LastConfig.Parser.ParseFrom(metaData.Value);
            return (long) lastConfig.Index;
        }

        private async Task<Block> GetBlockByNumberAsync(long number, CancellationToken token)
        {
            logger.Trace($"getConfigurationBlock for channel {Name}");
            try
            {
                logger.Trace($"Last config index is {number}");
                SeekSpecified seekSpecified = new SeekSpecified {Number = (ulong) number};
                SeekPosition seekPosition = new SeekPosition {Specified = seekSpecified};
                SeekInfo seekInfo = new SeekInfo {Start = seekPosition, Stop = seekPosition, Behavior = SeekInfo.Types.SeekBehavior.BlockUntilReady};
                List<DeliverResponse> deliverResponses = new List<DeliverResponse>();
                await SeekBlockAsync(seekInfo, deliverResponses, GetRandomOrderer(), token);
                DeliverResponse blockresp = deliverResponses[1];
                Block retBlock = blockresp.Block;
                if (retBlock == null)
                    throw new TransactionException($"newest block for channel {Name} fetch bad deliver returned null:");
                int dataCount = retBlock.Data.Data.Count;
                if (dataCount < 1)
                    throw new TransactionException($"Bad config block data count {dataCount}");
                logger.Trace($"Received  block for channel {Name}, block no:{retBlock.Header.Number}, transaction count: {retBlock.Data.Data.Count}");
                return retBlock;
            }
            catch (TransactionException e)
            {
                logger.ErrorException(e.Message, e);
                throw e;
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new TransactionException(e);
            }
        }

        private async Task<int> SeekBlockAsync(SeekInfo seekInfo, List<DeliverResponse> deliverResponses, Orderer ordererIn, CancellationToken token = default(CancellationToken))
        {
            logger.Trace($"seekBlock for channel {Name}");
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int statusRC = 404;
            try
            {
                do
                {
                    statusRC = 404;
                    Orderer orderer = ordererIn ?? GetRandomOrderer();
                    TransactionContext txContext = GetTransactionContext();
                    List<DeliverResponse> deliver = await orderer.SendDeliverAsync(ProtoUtils.CreateSeekInfoEnvelope(txContext, seekInfo, orderer.ClientTLSCertificateDigest), token);
                    if (deliver.Count < 1)
                    {
                        logger.Warn($"Genesis block for channel {Name} fetch bad deliver missing status block only got blocks:{deliver.Count}");
                        //odd so lets try again....
                        statusRC = 404;
                    }
                    else
                    {
                        DeliverResponse status = deliver[0];
                        statusRC = (int) status.Status;
                        if (statusRC == 404 || statusRC == 503)
                        {
                            //404 - block not found.  503 - service not available usually means kafka is not ready but starting.
                            logger.Warn($"Bad deliver expected status 200  got  {status.Status}, Channel {Name}");
                            // keep trying... else
                            statusRC = 404;
                        }
                        else if (statusRC != 200)
                        {
                            // Assume for anything other than 200 we have a non retryable situation
                            throw new TransactionException($"Bad newest block expected status 200  got  {status.Status}, Channel {Name}");
                        }
                        else
                        {
                            if (deliver.Count < 2)
                                throw new TransactionException($"Newest block for channel {Name} fetch bad deliver missing genesis block only got {deliver.Count}:");
                            deliverResponses.AddRange(deliver);
                        }
                    }

                    // Not 200 so sleep to try again

                    if (200 != statusRC)
                    {
                        long duration = watch.ElapsedMilliseconds;
                        if (duration > Config.Instance.GetGenesisBlockWaitTime())
                            throw new TransactionException($"Getting block time exceeded {duration / 1000} seconds for channel {Name}");

                        try
                        {
                            Thread.Sleep((int) ORDERER_RETRY_WAIT_TIME); //try again
                        }
                        catch (Exception e)
                        {
                            TransactionException te = new TransactionException("seekBlock thread Sleep", e);
                            logger.WarnException(te.Message, te);
                        }
                    }
                } while (statusRC != 200);
            }
            catch (TransactionException e)
            {
                logger.ErrorException(e.Message, e);
                throw e;
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new TransactionException(e);
            }
            return statusRC;
        }

        private async Task<Block> GetLatestBlockAsync(Orderer orderer, CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"getConfigurationBlock for channel {Name}");
            SeekPosition seekPosition = new SeekPosition {Newest = new SeekNewest()};
            SeekInfo seekInfo = new SeekInfo {Start = seekPosition, Stop = seekPosition, Behavior = SeekInfo.Types.SeekBehavior.BlockUntilReady};
            List<DeliverResponse> deliverResponses = new List<DeliverResponse>();
            await SeekBlockAsync(seekInfo, deliverResponses, orderer, token);
            DeliverResponse blockresp = deliverResponses[1];
            Block latestBlock = blockresp.Block;
            if (latestBlock == null)
                throw new TransactionException($"newest block for channel {Name} fetch bad deliver returned null:");
            logger.Trace($"Received latest  block for channel {Name}, block no:{latestBlock.Header.Number}");
            return latestBlock;
        }

        /**
         * Send instantiate request to the channel. Chaincode is created and initialized.
         *
         * @param instantiateProposalRequest send instantiate chaincode proposal request.
         * @return Collections of proposal responses
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public List<ProposalResponse> SendInstantiationProposal(InstantiateProposalRequest instantiateProposalRequest)
        {
            return SendInstantiationProposal(instantiateProposalRequest, GetChaincodePeers());
        }

        public Task<List<ProposalResponse>> SendInstantiationProposalAsync(InstantiateProposalRequest instantiateProposalRequest, CancellationToken token = default(CancellationToken))
        {
            return SendInstantiationProposalAsync(instantiateProposalRequest, GetChaincodePeers(), token);
        }

        /**
         * Send instantiate request to the channel. Chaincode is created and initialized.
         *
         * @param instantiateProposalRequest
         * @param peers
         * @return responses from peers.
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public List<ProposalResponse> SendInstantiationProposal(InstantiateProposalRequest instantiateProposalRequest, IEnumerable<Peer> peers)
        {
            return SendInstantiationProposalAsync(instantiateProposalRequest, peers).RunAndUnwarp();
        }

        public async Task<List<ProposalResponse>> SendInstantiationProposalAsync(InstantiateProposalRequest instantiateProposalRequest, IEnumerable<Peer> peers, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            if (null == instantiateProposalRequest)
                throw new InvalidArgumentException("InstantiateProposalRequest is null");
            instantiateProposalRequest.SetSubmitted();
            CheckPeers(peers);
            try
            {
                TransactionContext transactionContext = GetTransactionContext(instantiateProposalRequest.UserContext);
                transactionContext.ProposalWaitTime = instantiateProposalRequest.ProposalWaitTime;
                InstantiateProposalBuilder instantiateProposalbuilder = InstantiateProposalBuilder.Create();
                instantiateProposalbuilder.Context(transactionContext);
                instantiateProposalbuilder.Argss(instantiateProposalRequest.Args);
                instantiateProposalbuilder.ChaincodeName(instantiateProposalRequest.ChaincodeName);
                instantiateProposalbuilder.ChaincodeType(instantiateProposalRequest.ChaincodeLanguage);
                instantiateProposalbuilder.ChaincodePath(instantiateProposalRequest.ChaincodePath);
                instantiateProposalbuilder.SetChaincodeVersion(instantiateProposalRequest.ChaincodeVersion);
                instantiateProposalbuilder.ChaincodeEndorsementPolicy(instantiateProposalRequest.ChaincodeEndorsementPolicy);
                instantiateProposalbuilder.SetTransientMap(instantiateProposalRequest.TransientMap);
                Proposal instantiateProposal = instantiateProposalbuilder.Build();
                SignedProposal signedProposal = GetSignedProposal(transactionContext, instantiateProposal);
                return await SendProposalToPeersAsync(peers, signedProposal, transactionContext, token);
            }
            catch (Exception e)
            {
                throw new ProposalException(e);
            }
        }

        private TransactionContext GetTransactionContext()
        {
            return GetTransactionContext(client.UserContext);
        }

        private TransactionContext GetTransactionContext(IUser userContext)
        {
            userContext = userContext ?? client.UserContext;
            userContext.UserContextCheck();
            return new TransactionContext(this, userContext, client.CryptoSuite);
        }

        /**
         * Send install chaincode request proposal to all the channels on the peer.
         *
         * @param installProposalRequest
         * @return
         * @throws ProposalException
         * @throws InvalidArgumentException
         */
        public List<ProposalResponse> SendInstallProposal(InstallProposalRequest installProposalRequest)
        {
            return SendInstallProposal(installProposalRequest, GetChaincodePeers());
        }

        public Task<List<ProposalResponse>> SendInstallProposalAsync(InstallProposalRequest installProposalRequest, CancellationToken token = default(CancellationToken))
        {
            return SendInstallProposalAsync(installProposalRequest, GetChaincodePeers(), token);
        }

        /**
         * Send install chaincode request proposal to the channel.
         *
         * @param installProposalRequest
         * @param peers
         * @return
         * @throws ProposalException
         * @throws InvalidArgumentException
         */
        public List<ProposalResponse> SendInstallProposal(InstallProposalRequest installProposalRequest, IEnumerable<Peer> peers)
        {
            return SendInstallProposalAsync(installProposalRequest, peers).RunAndUnwarp();
        }

        public async Task<List<ProposalResponse>> SendInstallProposalAsync(InstallProposalRequest installProposalRequest, IEnumerable<Peer> peers, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(peers);
            if (null == installProposalRequest)
                throw new InvalidArgumentException("InstallProposalRequest is null");
            try
            {
                TransactionContext transactionContext = GetTransactionContext(installProposalRequest.UserContext);
                transactionContext.Verify = false; // Install will have no signing cause it's not really targeted to a channel.
                transactionContext.ProposalWaitTime = installProposalRequest.ProposalWaitTime;
                InstallProposalBuilder installProposalbuilder = InstallProposalBuilder.Create();
                installProposalbuilder.Context(transactionContext);
                installProposalbuilder.ChaincodeLanguage(installProposalRequest.ChaincodeLanguage);
                installProposalbuilder.ChaincodeName(installProposalRequest.ChaincodeName);
                installProposalbuilder.ChaincodePath(installProposalRequest.ChaincodePath);
                installProposalbuilder.ChaincodeVersion(installProposalRequest.ChaincodeVersion);
                installProposalbuilder.ChaincodeSource(installProposalRequest.ChaincodeSourceLocation);
                installProposalbuilder.SetChaincodeInputStream(installProposalRequest.ChaincodeInputStream);
                installProposalbuilder.ChaincodeMetaInfLocation(installProposalRequest.ChaincodeMetaInfLocation);
                Proposal deploymentProposal = installProposalbuilder.Build();
                SignedProposal signedProposal = GetSignedProposal(transactionContext, deploymentProposal);

                return await SendProposalToPeersAsync(peers, signedProposal, transactionContext, token);
            }
            catch (Exception e)
            {
                throw new ProposalException(e);
            }
        }

        /**
         * Send Upgrade proposal proposal to upgrade chaincode to a new version.
         *
         * @param upgradeProposalRequest
         * @return Collection of proposal responses.
         * @throws ProposalException
         * @throws InvalidArgumentException
         */
        public List<ProposalResponse> SendUpgradeProposal(UpgradeProposalRequest upgradeProposalRequest)
        {
            return SendUpgradeProposal(upgradeProposalRequest, GetChaincodePeers());
        }

        public Task<List<ProposalResponse>> SendUpgradeProposalAsync(UpgradeProposalRequest upgradeProposalRequest, CancellationToken token = default(CancellationToken))
        {
            return SendUpgradeProposalAsync(upgradeProposalRequest, GetChaincodePeers(), token);
        }

        /**
         * Send Upgrade proposal proposal to upgrade chaincode to a new version.
         *
         * @param upgradeProposalRequest
         * @param peers                  the specific peers to send to.
         * @return Collection of proposal responses.
         * @throws ProposalException
         * @throws InvalidArgumentException
         */
        public List<ProposalResponse> SendUpgradeProposal(UpgradeProposalRequest upgradeProposalRequest, IEnumerable<Peer> peers)
        {
            return SendUpgradeProposalAsync(upgradeProposalRequest, peers).RunAndUnwarp();
        }

        public async Task<List<ProposalResponse>> SendUpgradeProposalAsync(UpgradeProposalRequest upgradeProposalRequest, IEnumerable<Peer> peers, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(peers);
            if (null == upgradeProposalRequest)
                throw new InvalidArgumentException("Upgradeproposal is null");
            try
            {
                TransactionContext transactionContext = GetTransactionContext(upgradeProposalRequest.UserContext);
                //transactionContext.verify(false);  // Install will have no signing cause it's not really targeted to a channel.
                transactionContext.ProposalWaitTime = upgradeProposalRequest.ProposalWaitTime;
                UpgradeProposalBuilder upgradeProposalBuilder = UpgradeProposalBuilder.Create();
                upgradeProposalBuilder.Context(transactionContext);
                upgradeProposalBuilder.Argss(upgradeProposalRequest.Args);
                upgradeProposalBuilder.ChaincodeName(upgradeProposalRequest.ChaincodeName);
                upgradeProposalBuilder.ChaincodePath(upgradeProposalRequest.ChaincodePath);
                upgradeProposalBuilder.SetChaincodeVersion(upgradeProposalRequest.ChaincodeVersion);
                upgradeProposalBuilder.ChaincodeEndorsementPolicy(upgradeProposalRequest.ChaincodeEndorsementPolicy);
                SignedProposal signedProposal = GetSignedProposal(transactionContext, upgradeProposalBuilder.Build());
                return await SendProposalToPeersAsync(peers, signedProposal, transactionContext, token);
            }
            catch (Exception e)
            {
                throw new ProposalException(e);
            }
        }

        private SignedProposal GetSignedProposal(TransactionContext transactionContext, Proposal proposal)
        {
            return new SignedProposal {ProposalBytes = proposal.ToByteString(), Signature = transactionContext.SignByteString(proposal.ToByteArray())};
        }

        private void CheckChannelState()
        {
            if (IsShutdown)
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            if (!initialized)
                throw new InvalidArgumentException($"Channel {Name} has not been initialized.");
            client.UserContext.UserContextCheck();
        }

        /**
         * query this channel for a Block by the block hash.
         * The request is retried on each peer on the channel till successful.
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param blockHash the hash of the Block in the chain
         * @return the {@link BlockInfo} with the given block Hash
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByHash(byte[] blockHash)
        {
            return QueryBlockByHash(GetShuffledPeers(new[] { PeerRole.LEDGER_QUERY }), blockHash);
        }

        public Task<BlockInfo> QueryBlockByHashAsync(byte[] blockHash, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByHashAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockHash, token);
        }

        /**
         * query this channel for a Block by the block hash.
         * The request is tried on multiple peers.
         *
         * @param blockHash   the hash of the Block in the chain
         * @param userContext the user context.
         * @return the {@link BlockInfo} with the given block Hash
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByHash(byte[] blockHash, IUser userContext)
        {
            return QueryBlockByHash(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockHash, userContext);
        }
        public Task<BlockInfo> QueryBlockByHashAsync(byte[] blockHash, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByHashAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockHash, userContext, token);
        }

        /**
         * Query a peer in this channel for a Block by the block hash.
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param peer      the Peer to query.
         * @param blockHash the hash of the Block in the chain.
         * @return the {@link BlockInfo} with the given block Hash
         * @throws InvalidArgumentException if the channel is shutdown or any of the arguments are not valid.
         * @throws ProposalException        if an error occurred processing the query.
         */
        public BlockInfo QueryBlockByHash(Peer peer, byte[] blockHash)
        {
            return QueryBlockByHash(new[] { peer }, blockHash);
        }
        public Task<BlockInfo> QueryBlockByHashAsync(Peer peer, byte[] blockHash, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByHashAsync(new[] {peer}, blockHash, token);
        }

        /**
         * Query a peer in this channel for a Block by the block hash.
         * Each peer is tried until successful response.
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param peers     the Peers to query.
         * @param blockHash the hash of the Block in the chain.
         * @return the {@link BlockInfo} with the given block Hash
         * @throws InvalidArgumentException if the channel is shutdown or any of the arguments are not valid.
         * @throws ProposalException        if an error occurred processing the query.
         */
        public BlockInfo QueryBlockByHash(IEnumerable<Peer> peers, byte[] blockHash)
        {
            return QueryBlockByHash(peers, blockHash, client.UserContext);
        }
        public Task<BlockInfo> QueryBlockByHashAsync(IEnumerable<Peer> peers, byte[] blockHash, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByHashAsync(peers, blockHash, client.UserContext, token);
        }

        /**
         * Query a peer in this channel for a Block by the block hash.
         *
         * @param peers       the Peers to query.
         * @param blockHash   the hash of the Block in the chain.
         * @param userContext the user context
         * @return the {@link BlockInfo} with the given block Hash
         * @throws InvalidArgumentException if the channel is shutdown or any of the arguments are not valid.
         * @throws ProposalException        if an error occurred processing the query.
         */
        public BlockInfo QueryBlockByHash(IEnumerable<Peer> peers, byte[] blockHash, IUser userContext)
        {
            return QueryBlockByHashAsync(peers, blockHash, userContext).RunAndUnwarp();
        }
        public async Task<BlockInfo> QueryBlockByHashAsync(IEnumerable<Peer> peers, byte[] blockHash, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();
            if (blockHash == null)
                throw new InvalidArgumentException("blockHash parameter is null.");
            try
            {
                logger.Trace("queryBlockByHash with hash : " + blockHash.ToHexString() + " on channel " + Name);
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETBLOCKBYHASH);
                querySCCRequest.SetArgs(Name);
                querySCCRequest.SetArgBytes(new byte[][] {blockHash});
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, peers, token);
                return new BlockInfo(Block.Parser.ParseFrom(proposalResponse.ProtoProposalResponse.Response.Payload));
            }
            catch (InvalidProtocolBufferException e)
            {
                ProposalException proposalException = new ProposalException(e);
                logger.ErrorException(proposalException.Message, proposalException);
                throw proposalException;
            }
        }

        private Peer GetRandomLedgerQueryPeer()
        {
            List<Peer> ledgerQueryPeers = GetLedgerQueryPeers().ToList();
            if (ledgerQueryPeers.Count == 0)
                throw new InvalidArgumentException("Channel " + Name + " does not have any ledger querying peers associated with it.");
            return ledgerQueryPeers[RANDOM.Next(ledgerQueryPeers.Count)];
        }

        private Peer GetRandomPeer()
        {
            List<Peer> randPicks = Peers.ToList(); //copy to avoid unlikely changes
            if (randPicks.Count == 0)
                throw new InvalidArgumentException("Channel " + Name + " does not have any peers associated with it.");
            return randPicks[RANDOM.Next(randPicks.Count)];
        }

        private List<Peer> GetShuffledPeers()
        {
            return Peers.ToList().Shuffle().ToList();
        }

        private List<Peer> GetShuffledPeers(IEnumerable<PeerRole> roles)
        {
            return GetPeers(roles).ToList().Shuffle().ToList();
        }

        private Orderer GetRandomOrderer()
        {
            List<Orderer> randPicks = Orderers.ToList();
            if (randPicks.Count == 0)
                throw new InvalidArgumentException("Channel " + Name + " does not have any orderers associated with it.");
            return randPicks[RANDOM.Next(randPicks.Count)];
        }

        private void CheckPeer(Peer peer)
        {
            if (peer == null)
                throw new InvalidArgumentException("Peer value is null.");
            if (IsSystemChannel)
                return; // System owns no peers
            if (!Peers.Contains(peer))
                throw new InvalidArgumentException("Channel " + Name + " does not have peer " + peer.Name);
            if (peer.Channel != this)
                throw new InvalidArgumentException("Peer " + peer.Name + " not set for channel " + Name);
        }

        private void CheckOrderer(Orderer orderer)
        {
            if (orderer == null)
                throw new InvalidArgumentException("Orderer value is null.");
            if (IsSystemChannel)
                return; // System owns no Orderers
            if (!Orderers.Contains(orderer))
                throw new InvalidArgumentException("Channel " + Name + " does not have orderer " + orderer.Name);
            if (orderer.Channel != this)
                throw new InvalidArgumentException("Orderer " + orderer.Name + " not set for channel " + Name);
        }

        private void CheckPeers(IEnumerable<Peer> peers)
        {
            if (peers == null)
                throw new InvalidArgumentException("Collection of peers is null.");
            if (!peers.Any())
                throw new InvalidArgumentException("Collection of peers is empty.");
            foreach (Peer peer in peers)
                CheckPeer(peer);
        }

        /**
         * query this channel for a Block by the blockNumber.
         * The request is retried on all peers till successful
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>.
         *
         * @param blockNumber index of the Block in the chain
         * @return the {@link BlockInfo} with the given blockNumber
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByNumber(long blockNumber)
        {
            return QueryBlockByNumber(GetShuffledPeers(new[] { PeerRole.LEDGER_QUERY }), blockNumber);
        }
        public Task<BlockInfo> QueryBlockByNumberAsync(long blockNumber, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByNumberAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockNumber, token);
        }

        /**
         * query this channel for a Block by the blockNumber.
         * The request is sent to a random peer in the channel.
         *
         * @param blockNumber index of the Block in the chain
         * @param userContext the user context to be used.
         * @return the {@link BlockInfo} with the given blockNumber
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByNumber(long blockNumber, IUser userContext)
        {
            return QueryBlockByNumber(GetShuffledPeers(new[] { PeerRole.LEDGER_QUERY }), blockNumber, userContext);
        }
        public Task<BlockInfo> QueryBlockByNumberAsync(long blockNumber, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByNumberAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockNumber, userContext, token);
        }

        /**
         * Query a peer in this channel for a Block by the blockNumber
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param peer        the peer to send the request to
         * @param blockNumber index of the Block in the chain
         * @return the {@link BlockInfo} with the given blockNumber
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByNumber(Peer peer, long blockNumber)
        {
            return QueryBlockByNumber(new[] { peer }, blockNumber);
        }
        public Task<BlockInfo> QueryBlockByNumberAsync(Peer peer, long blockNumber, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByNumberAsync(new[] {peer}, blockNumber, token);
        }

        /**
         * query a peer in this channel for a Block by the blockNumber
         *
         * @param peer        the peer to send the request to
         * @param blockNumber index of the Block in the chain
         * @param userContext the user context.
         * @return the {@link BlockInfo} with the given blockNumber
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByNumber(Peer peer, long blockNumber, IUser userContext)
        {
            return QueryBlockByNumber(new[] { peer }, blockNumber, userContext);
        }
        public Task<BlockInfo> QueryBlockByNumberAsync(Peer peer, long blockNumber, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByNumberAsync(new[] {peer}, blockNumber, userContext, token);
        }

        /**
         * query a peer in this channel for a Block by the blockNumber
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param peers       the peers to try and send the request to
         * @param blockNumber index of the Block in the chain
         * @return the {@link BlockInfo} with the given blockNumber
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByNumber(IEnumerable<Peer> peers, long blockNumber)
        {
            return QueryBlockByNumber(peers, blockNumber, client.UserContext);
        }
        public Task<BlockInfo> QueryBlockByNumberAsync(IEnumerable<Peer> peers, long blockNumber, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByNumberAsync(peers, blockNumber, client.UserContext, token);
        }

        /**
         * query a peer in this channel for a Block by the blockNumber
         *
         * @param peers       the peers to try and send the request to
         * @param blockNumber index of the Block in the chain
         * @param userContext the user context to use.
         * @return the {@link BlockInfo} with the given blockNumber
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByNumber(IEnumerable<Peer> peers, long blockNumber, IUser userContext)
        {
            return QueryBlockByNumberAsync(peers, blockNumber, userContext).RunAndUnwarp();
        }
        public async Task<BlockInfo> QueryBlockByNumberAsync(IEnumerable<Peer> peers, long blockNumber, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();
            try
            {
                logger.Debug($"QueryBlockByNumber with blockNumber {blockNumber} on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETBLOCKBYNUMBER);
                querySCCRequest.SetArgs(Name, ((ulong) blockNumber).ToString(CultureInfo.InvariantCulture));
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, peers, token);
                return new BlockInfo(Block.Parser.ParseFrom(proposalResponse.ProtoProposalResponse.Response.Payload));
            }
            catch (InvalidProtocolBufferException e)
            {
                logger.ErrorException(e.Message, e);
                throw new ProposalException(e);
            }
        }

        /**
         * query this channel for a Block by a TransactionID contained in the block
         * The request is tried on on each peer till successful.
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param txID the transactionID to query on
         * @return the {@link BlockInfo} for the Block containing the transaction
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByTransactionID(string txID)
        {
            return QueryBlockByTransactionID(GetShuffledPeers(new[] { PeerRole.LEDGER_QUERY }), txID);
        }
        public Task<BlockInfo> QueryBlockByTransactionIDAsync(string txID, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByTransactionIDAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, token);
        }

        /**
         * query this channel for a Block by a TransactionID contained in the block
         * The request is sent to a random peer in the channel
         *
         * @param txID        the transactionID to query on
         * @param userContext the user context.
         * @return the {@link BlockInfo} for the Block containing the transaction
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByTransactionID(string txID, IUser userContext)
        {
            return QueryBlockByTransactionID(GetShuffledPeers(new[] { PeerRole.LEDGER_QUERY }), txID, userContext);
        }
        public Task<BlockInfo> QueryBlockByTransactionIDAsync(string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByTransactionIDAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, userContext, token);
        }

        /**
         * query a peer in this channel for a Block by a TransactionID contained in the block
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param peer the peer to send the request to
         * @param txID the transactionID to query on
         * @return the {@link BlockInfo} for the Block containing the transaction
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByTransactionID(Peer peer, string txID)
        {
            return QueryBlockByTransactionID(new[] { peer }, txID);
        }
        public Task<BlockInfo> QueryBlockByTransactionIDAsync(Peer peer, string txID, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByTransactionIDAsync(new[] {peer}, txID, token);
        }

        /**
         * query a peer in this channel for a Block by a TransactionID contained in the block
         *
         * @param peer        the peer to send the request to
         * @param txID        the transactionID to query on
         * @param userContext the user context.
         * @return the {@link BlockInfo} for the Block containing the transaction
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByTransactionID(Peer peer, string txID, IUser userContext)
        {
            return QueryBlockByTransactionID(new[] { peer }, txID, userContext);
        }
        public Task<BlockInfo> QueryBlockByTransactionIDAsync(Peer peer, string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByTransactionIDAsync(new[] {peer}, txID, userContext, token);
        }

        /**
         * query a peer in this channel for a Block by a TransactionID contained in the block
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param peers the peers to try to send the request to.
         * @param txID  the transactionID to query on
         * @return the {@link BlockInfo} for the Block containing the transaction
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByTransactionID(IEnumerable<Peer> peers, string txID)
        {
            return QueryBlockByTransactionID(peers, txID, client.UserContext);
        }
        public Task<BlockInfo> QueryBlockByTransactionIDAsync(IEnumerable<Peer> peers, string txID, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByTransactionIDAsync(peers, txID, client.UserContext, token);
        }

        /**
         * query a peer in this channel for a Block by a TransactionID contained in the block
         *
         * @param peers       the peer to try to send the request to
         * @param txID        the transactionID to query on
         * @param userContext the user context.
         * @return the {@link BlockInfo} for the Block containing the transaction
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByTransactionID(IEnumerable<Peer> peers, string txID, IUser userContext)
        {
            return QueryBlockByTransactionIDAsync(peers, txID, userContext).RunAndUnwarp();
        }
        public async Task<BlockInfo> QueryBlockByTransactionIDAsync(IEnumerable<Peer> peers, string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();
            if (txID == null)
                throw new InvalidArgumentException("TxID parameter is null.");
            try
            {
                logger.Debug($"QueryBlockByTransactionID with txID {txID}\n     on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETBLOCKBYTXID);
                querySCCRequest.SetArgs(Name, txID);
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, peers, token);
                return new BlockInfo(Block.Parser.ParseFrom(proposalResponse.ProtoProposalResponse.Response.Payload));
            }
            catch (InvalidProtocolBufferException e)
            {
                throw new ProposalException(e);
            }
        }

        /**
         * query this channel for chain information.
         * The request is sent to a random peer in the channel
         * <p>
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @return a {@link BlockchainInfo} object containing the chain info requested
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockchainInfo QueryBlockchainInfo()
        {
            return QueryBlockchainInfo(GetShuffledPeers(new[] { PeerRole.LEDGER_QUERY }), client.UserContext);
        }
        public Task<BlockchainInfo> QueryBlockchainInfoAsync(CancellationToken token = default(CancellationToken))
        {
            return QueryBlockchainInfoAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), client.UserContext, token);
        }

        /**
         * query this channel for chain information.
         * The request is sent to a random peer in the channel
         *
         * @param userContext the user context to use.
         * @return a {@link BlockchainInfo} object containing the chain info requested
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockchainInfo QueryBlockchainInfo(IUser userContext)
        {
            return QueryBlockchainInfo(GetShuffledPeers(new[] { PeerRole.LEDGER_QUERY }), userContext);
        }
        public Task<BlockchainInfo> QueryBlockchainInfoAsync(IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockchainInfoAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), userContext, token);
        }

        /**
         * query for chain information
         * <p>
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param peer The peer to send the request to
         * @return a {@link BlockchainInfo} object containing the chain info requested
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockchainInfo QueryBlockchainInfo(Peer peer)
        {
            return QueryBlockchainInfo(new[] { peer }, client.UserContext);
        }
        public Task<BlockchainInfo> QueryBlockchainInfoAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockchainInfoAsync(new[] {peer}, client.UserContext, token);
        }

        /**
         * query for chain information
         *
         * @param peer        The peer to send the request to
         * @param userContext the user context to use.
         * @return a {@link BlockchainInfo} object containing the chain info requested
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockchainInfo QueryBlockchainInfo(Peer peer, IUser userContext)
        {
            return QueryBlockchainInfo(new[] { peer }, userContext);
        }
        public Task<BlockchainInfo> QueryBlockchainInfoAsync(Peer peer, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockchainInfoAsync(new[] {peer}, userContext, token);
        }

        /**
         * query for chain information
         *
         * @param peers       The peers to try send the request.
         * @param userContext the user context.
         * @return a {@link BlockchainInfo} object containing the chain info requested
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockchainInfo QueryBlockchainInfo(IEnumerable<Peer> peers, IUser userContext)
        {
            return QueryBlockchainInfoAsync(peers, userContext).RunAndUnwarp();
        }
        public async Task<BlockchainInfo> QueryBlockchainInfoAsync(IEnumerable<Peer> peers, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();
            try
            {
                logger.Debug($"QueryBlockchainInfo to peer on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETCHAININFO);
                querySCCRequest.SetArgs(Name);
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, peers, token);
                return new BlockchainInfo(Protos.Common.BlockchainInfo.Parser.ParseFrom(proposalResponse.ProtoProposalResponse.Response.Payload));
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new ProposalException(e);
            }
        }

        /**
         * Query this channel for a Fabric Transaction given its transactionID.
         * The request is sent to a random peer in the channel.
         * <p>
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param txID the ID of the transaction
         * @return a {@link TransactionInfo}
         * @throws ProposalException
         * @throws InvalidArgumentException
         */
        public TransactionInfo QueryTransactionByID(string txID)
        {
            return QueryTransactionByID(GetShuffledPeers(new[] { PeerRole.LEDGER_QUERY }), txID, client.UserContext);
        }
        public Task<TransactionInfo> QueryTransactionByIDAsync(string txID, CancellationToken token = default(CancellationToken))
        {
            return QueryTransactionByIDAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, client.UserContext, token);
        }

        /**
         * Query this channel for a Fabric Transaction given its transactionID.
         * The request is sent to a random peer in the channel.
         * <p>
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param txID        the ID of the transaction
         * @param userContext the user context used.
         * @return a {@link TransactionInfo}
         * @throws ProposalException
         * @throws InvalidArgumentException
         */
        public TransactionInfo QueryTransactionByID(string txID, IUser userContext)
        {
            return QueryTransactionByID(GetShuffledPeers(new[] { PeerRole.LEDGER_QUERY }), txID, userContext);
        }
        public Task<TransactionInfo> QueryTransactionByIDAsync(string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryTransactionByIDAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, userContext, token);
        }

        /**
         * Query for a Fabric Transaction given its transactionID
         * <p>
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param txID the ID of the transaction
         * @param peer the peer to send the request to
         * @return a {@link TransactionInfo}
         * @throws ProposalException
         * @throws InvalidArgumentException
         */
        public TransactionInfo QueryTransactionByID(Peer peer, string txID)
        {
            return QueryTransactionByID(new[] { peer }, txID, client.UserContext);
        }
        public Task<TransactionInfo> QueryTransactionByIDAsync(Peer peer, string txID, CancellationToken token = default(CancellationToken))
        {
            return QueryTransactionByIDAsync(new[] {peer}, txID, client.UserContext, token);
        }

        /**
         * Query for a Fabric Transaction given its transactionID
         *
         * @param peer        the peer to send the request to
         * @param txID        the ID of the transaction
         * @param userContext the user context
         * @return a {@link TransactionInfo}
         * @throws ProposalException
         * @throws InvalidArgumentException
         */
        public TransactionInfo QueryTransactionByID(Peer peer, string txID, IUser userContext)
        {
            return QueryTransactionByID(new[] { peer }, txID, userContext);
        }
        public Task<TransactionInfo> QueryTransactionByIDAsync(Peer peer, string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryTransactionByIDAsync(new[] {peer}, txID, userContext, token);
        }

        /**
         * Query for a Fabric Transaction given its transactionID
         *
         * @param txID        the ID of the transaction
         * @param peers       the peers to try to send the request.
         * @param userContext the user context
         * @return a {@link TransactionInfo}
         * @throws ProposalException
         * @throws InvalidArgumentException
         */
        public TransactionInfo QueryTransactionByID(IEnumerable<Peer> peers, string txID, IUser userContext)
        {
            return QueryTransactionByIDAsync(peers, txID, userContext).RunAndUnwarp();
        }
        public async Task<TransactionInfo> QueryTransactionByIDAsync(IEnumerable<Peer> peers, string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();
            if (txID == null)
                throw new InvalidArgumentException("TxID parameter is null.");
            TransactionInfo transactionInfo;
            try
            {
                logger.Debug($"QueryTransactionByID with txID {txID}\n    from peer on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETTRANSACTIONBYID);
                querySCCRequest.SetArgs(Name, txID);
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, peers, token);
                return new TransactionInfo(txID, ProcessedTransaction.Parser.ParseFrom(proposalResponse.ProtoProposalResponse.Response.Payload));
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new ProposalException(e);
            }
        }

        /////////////////////////////////////////////////////////
        // transactions order
        public HashSet<string> QueryChannels(Peer peer)
        {
            return QueryChannelsAsync(peer).RunAndUnwarp();
        }
        public async Task<HashSet<string>> QueryChannelsAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            CheckPeer(peer);
            if (!IsSystemChannel)
                throw new InvalidArgumentException("queryChannels should only be invoked on system channel.");
            try
            {
                TransactionContext context = GetTransactionContext();
                Proposal q = QueryPeerChannelsBuilder.Create().Context(context).Build();
                SignedProposal qProposal = GetSignedProposal(context, q);
                List<ProposalResponse> proposalResponses = await SendProposalToPeersAsync(new[] {peer}, qProposal, context, token);
                if (null == proposalResponses)
                    throw new ProposalException($"Peer {peer.Name} channel query return with null for responses");
                if (proposalResponses.Count != 1)
                    throw new ProposalException($"Peer {peer.Name} channel query expected one response but got back {proposalResponses.Count}  responses ");
                ProposalResponse proposalResponse = proposalResponses.First();
                if (proposalResponse.Status != ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    throw new ProposalException($"Failed exception message is {proposalResponse.Message}, status is {proposalResponse.Status}");
                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse = proposalResponse.ProtoProposalResponse;
                if (null == fabricResponse)
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabric response");
                Response fabricResponseResponse = fabricResponse.Response;
                if (null == fabricResponseResponse)
                {
                    //not likely but check it.
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabricResponseResponse");
                }

                if (200 != fabricResponseResponse.Status)
                    throw new ProposalException($"Peer {peer.Name} channel query expected 200, actual returned was: {fabricResponseResponse.Status}. {fabricResponseResponse.Message}");
                ChannelQueryResponse qr = ChannelQueryResponse.Parser.ParseFrom(fabricResponseResponse.Payload);
                HashSet<string> ret = new HashSet<string>();
                foreach (ChannelInfo x in qr.Channels)
                    ret.Add(x.ChannelId);
                return ret;
            }
            catch (ProposalException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new ProposalException($"Query for peer {peer.Name} channels failed. {e.Message}", e);
            }
        }

        public List<ChaincodeInfo> QueryInstalledChaincodes(Peer peer)
        {
            return QueryInstalledChaincodesAsync(peer).RunAndUnwarp();
        }
        public async Task<List<ChaincodeInfo>> QueryInstalledChaincodesAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            CheckPeer(peer);
            if (!IsSystemChannel)
                throw new InvalidArgumentException("queryInstalledChaincodes should only be invoked on system channel.");
            try
            {
                TransactionContext context = GetTransactionContext();
                Proposal q = QueryInstalledChaincodesBuilder.Create().Context(context).Build();
                SignedProposal qProposal = GetSignedProposal(context, q);
                List<ProposalResponse> proposalResponses = await SendProposalToPeersAsync(new[] {peer}, qProposal, context, token);
                if (null == proposalResponses)
                    throw new ProposalException($"Peer {peer.Name} channel query return with null for responses");
                if (proposalResponses.Count != 1)
                    throw new ProposalException($"Peer {peer.Name} channel query expected one response but got back {proposalResponses.Count}  responses ");
                ProposalResponse proposalResponse = proposalResponses.First();
                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse = proposalResponse.ProtoProposalResponse;
                if (null == fabricResponse)
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabric response");
                Response fabricResponseResponse = fabricResponse.Response;
                if (null == fabricResponseResponse)
                {
                    //not likely but check it.
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabricResponseResponse");
                }

                if (200 != fabricResponseResponse.Status)
                    throw new ProposalException($"Peer {peer.Name} channel query expected 200, actual returned was: {fabricResponseResponse.Status}. {fabricResponseResponse.Message}");
                ChaincodeQueryResponse chaincodeQueryResponse = ChaincodeQueryResponse.Parser.ParseFrom(fabricResponseResponse.Payload);
                return chaincodeQueryResponse.Chaincodes.ToList();
            }
            catch (ProposalException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new ProposalException($"Query for peer {peer.Name} channels failed. {e.Message}", e);
            }
        }

        /**
         * Query peer for chaincode that has been instantiated
         * <p>
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * </P>
         *
         * @param peer The peer to query.
         * @return A list of ChaincodeInfo @see {@link ChaincodeInfo}
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public List<ChaincodeInfo> QueryInstantiatedChaincodes(Peer peer)
        {
            return QueryInstantiatedChaincodes(peer, client.UserContext);
        }

        public Task<List<ChaincodeInfo>> QueryInstantiatedChaincodesAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            return QueryInstantiatedChaincodesAsync(peer, client.UserContext, token);
        }

        /**
         * Query peer for chaincode that has been instantiated
         *
         * @param peer        The peer to query.
         * @param userContext the user context.
         * @return A list of ChaincodeInfo @see {@link ChaincodeInfo}
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public List<ChaincodeInfo> QueryInstantiatedChaincodes(Peer peer, IUser userContext)
        {
            return QueryInstantiatedChaincodesAsync(peer, userContext).RunAndUnwarp();
        }
        public async Task<List<ChaincodeInfo>> QueryInstantiatedChaincodesAsync(Peer peer, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeer(peer);
            userContext.UserContextCheck();
            try
            {
                TransactionContext context = GetTransactionContext(userContext);
                Proposal q = QueryInstantiatedChaincodesBuilder.Create().Context(context).Build();
                SignedProposal qProposal = GetSignedProposal(context, q);
                List<ProposalResponse> proposalResponses = await SendProposalToPeersAsync(new[] {peer}, qProposal, context, token);
                if (null == proposalResponses)
                    throw new ProposalException($"Peer {peer.Name} channel query return with null for responses");
                if (proposalResponses.Count != 1)
                    throw new ProposalException($"Peer {peer.Name} channel query expected one response but got back {proposalResponses.Count}  responses ");
                ProposalResponse proposalResponse = proposalResponses.First();
                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse = proposalResponse.ProtoProposalResponse;
                if (null == fabricResponse)
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabric response");
                Response fabricResponseResponse = fabricResponse.Response;
                if (null == fabricResponseResponse)
                {
                    //not likely but check it.
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabricResponseResponse");
                }
                if (200 != fabricResponseResponse.Status)
                    throw new ProposalException($"Peer {peer.Name} channel query expected 200, actual returned was: {fabricResponseResponse.Status}. {fabricResponseResponse.Message}");
                ChaincodeQueryResponse chaincodeQueryResponse = ChaincodeQueryResponse.Parser.ParseFrom(fabricResponseResponse.Payload);
                return chaincodeQueryResponse.Chaincodes.ToList();
            }
            catch (ProposalException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new ProposalException($"Query for peer {peer.Name} channels failed. {e.Message}", e);
            }
        }

        /**
         * Send a transaction  proposal.
         *
         * @param transactionProposalRequest The transaction proposal to be sent to all the peers.
         * @return responses from peers.
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public List<ProposalResponse> SendTransactionProposal(TransactionProposalRequest transactionProposalRequest)
        {
            return SendTransactionProposalAsync(transactionProposalRequest).RunAndUnwarp();
        }
        public async Task<List<ProposalResponse>> SendTransactionProposalAsync(TransactionProposalRequest transactionProposalRequest, CancellationToken token = default(CancellationToken))
        {
            return await SendProposalAsync(transactionProposalRequest, GetEndorsingPeers(), token);
        }

        /**
         * Send a transaction proposal to specific peers.
         *
         * @param transactionProposalRequest The transaction proposal to be sent to the peers.
         * @param peers
         * @return responses from peers.
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public List<ProposalResponse> SendTransactionProposal(TransactionProposalRequest transactionProposalRequest, IEnumerable<Peer> peers)
        {
            return SendTransactionProposalAsync(transactionProposalRequest,peers).RunAndUnwarp();
        }
        public Task<List<ProposalResponse>> SendTransactionProposalAsync(TransactionProposalRequest transactionProposalRequest, IEnumerable<Peer> peers, CancellationToken token = default(CancellationToken))
        {
            return SendProposalAsync(transactionProposalRequest, peers, token);
        }

        /**
         * Send Query proposal
         *
         * @param queryByChaincodeRequest
         * @return Collection proposal responses.
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public List<ProposalResponse> QueryByChaincode(QueryByChaincodeRequest queryByChaincodeRequest)
        {
            return QueryByChaincode(queryByChaincodeRequest, GetChaincodeQueryPeers());
        }
        public Task<List<ProposalResponse>> QueryByChaincodeAsync(QueryByChaincodeRequest queryByChaincodeRequest, CancellationToken token = default(CancellationToken))
        {
            return QueryByChaincodeAsync(queryByChaincodeRequest, GetChaincodeQueryPeers(), token);
        }

        /**
         * Send Query proposal
         *
         * @param queryByChaincodeRequest
         * @param peers
         * @return responses from peers.
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public List<ProposalResponse> QueryByChaincode(QueryByChaincodeRequest queryByChaincodeRequest, IEnumerable<Peer> peers)
        {
            return QueryByChaincodeAsync(queryByChaincodeRequest, peers).RunAndUnwarp();
        }
        public Task<List<ProposalResponse>> QueryByChaincodeAsync(QueryByChaincodeRequest queryByChaincodeRequest, IEnumerable<Peer> peers, CancellationToken token = default(CancellationToken))
        {
            return SendProposalAsync(queryByChaincodeRequest, peers, token);
        }
        ////////////////  Channel Block monitoring //////////////////////////////////

        private async Task<ProposalResponse> SendProposalSeriallyAsync(TransactionRequest proposalRequest, IEnumerable<Peer> peers, CancellationToken token)
        {
            ProposalException lastException = new ProposalException("ProposalRequest failed.");
            foreach (Peer peer in peers)
            {
                try
                {
                    List<ProposalResponse> proposalResponses = await SendProposalAsync(proposalRequest, new[] {peer}, token);
                    if (proposalResponses.Count == 0)
                        logger.Warn($"Proposal request to peer {peer.Name} failed");
                    ProposalResponse proposalResponse = proposalResponses.First();
                    ChaincodeResponse.ChaincodeResponseStatus status = proposalResponse.Status;
                    if ((int) status < 400)
                        return proposalResponse;
                    if ((int) status > 499)
                    {
                        // server error may work on other peer.
                        lastException = new ProposalException($"Channel {Name} got exception on peer {peer.Name} {status}. {proposalResponse.Message} ");
                    }
                    else
                    {
                        // 400 to 499
                        throw new ProposalException($"Channel {Name} got exception on peer {peer.Name} {status}. {proposalResponse.Message} ");
                    }
                }
                catch (Exception e)
                {
                    lastException = new ProposalException($"Channel {Name} failed proposal on peer {peer.Name}  {e.Message}", e);
                    logger.Warn(lastException.Message);
                }
            }
            throw lastException;
        }
        
        private async Task<List<ProposalResponse>> SendProposalAsync(TransactionRequest proposalRequest, IEnumerable<Peer> peers, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(peers);
            if (null == proposalRequest)
                throw new InvalidArgumentException("The proposalRequest is null");
            if (string.IsNullOrEmpty(proposalRequest.Fcn))
                throw new InvalidArgumentException("The proposalRequest's fcn is null or empty.");
            if (proposalRequest.ChaincodeID == null)
                throw new InvalidArgumentException("The proposalRequest's chaincode ID is null");
            proposalRequest.SetSubmitted();
            try
            {
                TransactionContext transactionContext = GetTransactionContext(proposalRequest.UserContext);
                transactionContext.Verify = proposalRequest.DoVerify;
                transactionContext.ProposalWaitTime = proposalRequest.ProposalWaitTime;
                // Protobuf message builder
                Proposal proposal = ProposalBuilder.Create().Context(transactionContext).Request(proposalRequest).Build();
                SignedProposal invokeProposal = GetSignedProposal(transactionContext, proposal);
                return await SendProposalToPeersAsync(peers, invokeProposal, transactionContext, token);
            }
            catch (ProposalException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                ProposalException exp = new ProposalException(e);
                logger.ErrorException(exp.Message, exp);
                throw exp;
            }
        }

        private async Task<List<ProposalResponse>> SendProposalToPeersAsync(IEnumerable<Peer> peers, SignedProposal signedProposal, TransactionContext transactionContext, CancellationToken token = default(CancellationToken))
        {
            CheckPeers(peers);
            List<ProposalResponse> responses = new List<ProposalResponse>();
            if (transactionContext.Verify)
            {
                try
                {
                    await LoadCACertificatesAsync(token);
                }
                catch (Exception e)
                {
                    throw new ProposalException(e);
                }
            }
            Dictionary<Peer, Task<Protos.Peer.FabricProposalResponse.ProposalResponse>> tasks = new Dictionary<Peer, Task<Protos.Peer.FabricProposalResponse.ProposalResponse>>();
            using (CancellationTokenSource stoken = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                foreach (Peer peer in peers)
                {
                    logger.Debug($"Channel {Name} send proposal to peer {peer.Name} at url {peer.Url}");
                    if (null != diagnosticFileDumper)
                        logger.Trace($"Sending to channel {Name}, peer: {peer.Name}, proposal: {diagnosticFileDumper.CreateDiagnosticProtobufFile(signedProposal.ToByteArray())}");
                    tasks.Add(peer, peer.SendProposalAsync(signedProposal, stoken.Token));
                }

                using (var timeoutCancellationTokenSource = new CancellationTokenSource())
                {
                    Task ret = Task.WhenAll(tasks.Values);
                    var completedTask = await Task.WhenAny(ret, Task.Delay((int) transactionContext.ProposalWaitTime, timeoutCancellationTokenSource.Token));
                    if (completedTask == ret)
                        timeoutCancellationTokenSource.Cancel();
                    else
                        stoken.Cancel();
                }
            }
            token.ThrowIfCancellationRequested();
            foreach (Peer peer in tasks.Keys)
            {
                Task<Protos.Peer.FabricProposalResponse.ProposalResponse> ctask = tasks[peer];
                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse = null;
                string message;
                int status = 500;
                string peerName = peer.Name;
                if (ctask.IsCompleted)
                {
                    fabricResponse = ctask.Result;
                    message = fabricResponse.Response.Message;
                    status = fabricResponse.Response.Status;
                    logger.Debug($"Channel {Name} got back from peer {peerName} status: {status}, message: {message}");
                    if (null != diagnosticFileDumper)
                        logger.Trace($"Got back from channel {Name}, peer: {peerName}, proposal response: {diagnosticFileDumper.CreateDiagnosticProtobufFile(fabricResponse.ToByteArray())}");
                    ProposalResponse proposalResponse = new ProposalResponse(transactionContext.TxID, transactionContext.ChannelID, status, message);
                    proposalResponse.ProtoProposalResponse = fabricResponse;
                    proposalResponse.SetProposal(signedProposal);
                    proposalResponse.Peer = peer;
                    if (fabricResponse != null && transactionContext.Verify)
                        proposalResponse.Verify(client.CryptoSuite);
                    responses.Add(proposalResponse);
                }
                else if (ctask.IsFaulted)
                {
                    AggregateException ex = ctask.Exception;
                    Exception e = ex.InnerException ?? ex;
                    if (e is RpcException)
                    {
                        RpcException rpce = (RpcException) e;
                        message = $"Sending proposal to {peerName} failed because of: gRPC failure={rpce.Status}";
                    }
                    else
                        message = $"Sending proposal to {peerName}  failed because of: {e.Message}";

                    logger.ErrorException(message, e);
                }
                else if (ctask.IsCanceled)
                {
                    message = $"Sending proposal to {peerName} failed because of timeout({transactionContext.ProposalWaitTime} milliseconds) expiration";
                    logger.Error(message);
                }
            }
            return responses;
        }

        /**
         * Send transaction to one of the orderers on the channel using a specific user context.
         *
         * @param proposalResponses The proposal responses to be sent to the orderer.
         * @param userContext       The usercontext used for signing transaction.
         * @return a future allowing access to the result of the transaction invocation once complete.
         */
        public BlockEvent.TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, IUser userContext, int? waittimeinmilliseconds=10000)
        {
            return SendTransaction(proposalResponses, orderers, userContext, waittimeinmilliseconds);
        }
        public Task<BlockEvent.TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, IUser userContext, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            return SendTransactionAsync(proposalResponses, orderers, userContext, waittimeinmilliseconds, token);
        }

        /**
         * Send transaction to one of the orderers on the channel using the usercontext set on the client.
         *
         * @param proposalResponses .
         * @return a future allowing access to the result of the transaction invocation once complete.
         */
        public BlockEvent.TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, int? waittimeinmilliseconds = 10000)
        {
            return SendTransaction(proposalResponses, orderers, waittimeinmilliseconds);
        }
        public Task<BlockEvent.TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            return SendTransactionAsync(proposalResponses, orderers, waittimeinmilliseconds, token);
        }

        /**
         * Send transaction to one of the specified orderers using the usercontext set on the client..
         *
         * @param proposalResponses The proposal responses to be sent to the orderer
         * @param orderers          The orderers to send the transaction to.
         * @return a future allowing access to the result of the transaction invocation once complete.
         */
        public BlockEvent.TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> orderers, int? waittimeinmilliseconds = 10000)
        {
            return SendTransaction(proposalResponses, orderers, client.UserContext, waittimeinmilliseconds);
        }
        public Task<BlockEvent.TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> orderers, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            return SendTransactionAsync(proposalResponses, orderers, client.UserContext, waittimeinmilliseconds, token);
        }


        /**
         * Send transaction to one of a specified set of orderers with the specified user context.
         * IF there are no event hubs or eventing peers this future returns immediately completed
         * indicating that orderer has accepted the transaction only.
         *
         * @param proposalResponses
         * @param orderers
         * @return Future allowing access to the result of the transaction invocation.
         */
        public BlockEvent.TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> orderers, IUser userContext, int? waittimeinmilliseconds = 10000)
        {
            return SendTransaction(proposalResponses, TransactionOptions.Create().SetOrderers(orderers).SetUserContext(userContext), waittimeinmilliseconds = 10000);
        }
        public Task<BlockEvent.TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> orderers, IUser userContext, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            return SendTransactionAsync(proposalResponses, TransactionOptions.Create().SetOrderers(orderers).SetUserContext(userContext), waittimeinmilliseconds = 10000, token);
        }


        /**
         * Send transaction to one of a specified set of orderers with the specified user context.
         * IF there are no event hubs or eventing peers this future returns immediately completed
         * indicating that orderer has accepted the transaction only.
         *
         * @param proposalResponses
         * @param transactionOptions
         * @return Future allowing access to the result of the transaction invocation.
         */
        public BlockEvent.TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, TransactionOptions transactionOptions, int? waittimeinmilliseconds = 10000)
        {
            return SendTransactionAsync(proposalResponses, transactionOptions, waittimeinmilliseconds).RunAndUnwarp();
        }
        public async Task<BlockEvent.TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, TransactionOptions transactionOptions, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            try
            {
                if (null == transactionOptions)
                    throw new InvalidArgumentException("Parameter transactionOptions can't be null");
                CheckChannelState();
                IUser userContext = transactionOptions.UserContext ?? client.UserContext;
                userContext.UserContextCheck();
                if (null == proposalResponses)
                    throw new InvalidArgumentException("sendTransaction proposalResponses was null");
                List<Orderer> orderers = transactionOptions.Orderers ?? Orderers.ToList();
                // make certain we have our own copy
                List<Orderer> shuffeledOrderers = orderers.Shuffle().ToList();
                if (Config.Instance.GetProposalConsistencyValidation())
                {
                    HashSet<ProposalResponse> invalid = new HashSet<ProposalResponse>();
                    int consistencyGroups = SDKUtils.GetProposalConsistencySets(proposalResponses.ToList(), invalid).Count;
                    if (consistencyGroups != 1 || invalid.Count > 0)
                        throw new IllegalArgumentException($"The proposal responses have {consistencyGroups} inconsistent groups with {invalid.Count} that are invalid." + " Expected all to be consistent and none to be invalid.");
                }

                List<Endorsement> ed = new List<Endorsement>();
                Proposal proposal = null;
                ByteString proposalResponsePayload = null;
                string proposalTransactionID = null;
                foreach (ProposalResponse sdkProposalResponse in proposalResponses)
                {
                    ed.Add(sdkProposalResponse.ProtoProposalResponse.Endorsement);
                    if (proposal == null)
                    {
                        proposal = sdkProposalResponse.Proposal;
                        proposalTransactionID = sdkProposalResponse.TransactionID;
                        proposalResponsePayload = sdkProposalResponse.ProtoProposalResponse.Payload;
                    }
                }
                Payload transactionPayload = TransactionBuilder.Create().ChaincodeProposal(proposal).Endorsements(ed).ProposalResponsePayload(proposalResponsePayload).Build();
                Envelope transactionEnvelope = CreateTransactionEnvelope(transactionPayload, userContext);
                NOfEvents nOfEvents = transactionOptions.NOfEvents;
                if (nOfEvents == null)
                {
                    nOfEvents = NOfEvents.CreateNofEvents();
                    IReadOnlyList<Peer> eventingPeers = GetEventingPeers();
                    bool anyAdded = false;
                    if (eventingPeers.Count > 0)
                    {
                        anyAdded = true;
                        nOfEvents.AddPeers(eventingPeers);
                    }
                    IReadOnlyList<EventHub> eventHubs = EventHubs;
                    if (eventHubs.Count > 0)
                    {
                        anyAdded = true;
                        nOfEvents.AddEventHubs(eventHubs);
                    }
                    if (!anyAdded)
                    {
                        nOfEvents = NOfEvents.CreateNoEvents();
                    }
                }
                else if (nOfEvents != NOfEvents.NofNoEvents)
                {
                    StringBuilder issues = new StringBuilder(100);
                    IReadOnlyList<Peer> eventingPeers = GetEventingPeers();
                    nOfEvents.UnSeenPeers().ForEach(peer =>
                    {
                        if (peer.Channel != this)
                            issues.Append($"Peer {peer.Name} added to NOFEvents does not belong this channel. ");
                        else if (!eventingPeers.Contains(peer))
                            issues.Append($"Peer {peer.Name} added to NOFEvents is not a eventing Peer in this channel. ");
                    });
                    nOfEvents.UnSeenEventHubs().ForEach(eventHub =>
                    {
                        if (!eventHubs.Contains(eventHub))
                            issues.Append($"Eventhub {eventHub.Name} added to NOFEvents does not belong this channel. ");
                    });

                    if (nOfEvents.UnSeenEventHubs().Count == 0 && nOfEvents.UnSeenPeers().Count == 0)
                        issues.Append("NofEvents had no Eventhubs added or Peer eventing services.");
                    string foundIssues = issues.ToString();
                    if (!string.IsNullOrEmpty(foundIssues))
                        throw new InvalidArgumentException(foundIssues);
                }

                bool replyonly = nOfEvents == NOfEvents.NofNoEvents || EventHubs.Count == 0 && GetEventingPeers().Count == 0;
                TaskCompletionSource<BlockEvent.TransactionEvent> sret;
                if (replyonly)
                {
                    //If there are no eventhubs to complete the future, complete it
                    // immediately but give no transaction event
                    logger.Debug($"Completing transaction id {proposalTransactionID} immediately no event hubs or peer eventing services found in channel {Name}.");
                    sret = new TaskCompletionSource<BlockEvent.TransactionEvent>();
                }
                else
                    sret = RegisterTxListener(proposalTransactionID, nOfEvents, transactionOptions.FailFast);
                logger.Debug($"Channel {Name} sending transaction to orderer(s) with TxID {proposalTransactionID} ");
                bool success = false;
                Exception lException = null; // Save last exception to report to user .. others are just logged.
                BroadcastResponse resp = null;
                Orderer failed = null;
                foreach (Orderer orderer in shuffeledOrderers)
                {
                    if (failed != null)
                        logger.Warn($"Channel {Name}  {failed} failed. Now trying {orderer}.");
                    failed = orderer;
                    try
                    {
                        if (null != diagnosticFileDumper)
                            logger.Trace($"Sending to channel {Name}, orderer: {orderer.Name}, transaction: {diagnosticFileDumper.CreateDiagnosticProtobufFile(transactionEnvelope.ToByteArray())}");
                        resp = await orderer.SendTransactionAsync(transactionEnvelope, token);
                        lException = null; // no longer last exception .. maybe just failed.
                        if (resp.Status == Status.Success)
                        {
                            success = true;
                            break;
                        }
                        logger.Warn($"Channel {Name} {orderer.Name} failed. Status returned {GetRespData(resp)}");
                    }
                    catch (Exception en)
                    {
                        string emsg = $"Channel {Name} unsuccessful sendTransaction to orderer {orderer.Name} ({orderer.Url})";
                        if (resp != null)
                            emsg = $"Channel {Name} unsuccessful sendTransaction to orderer {orderer.Name} ({orderer.Url}).  {GetRespData(resp)}";
                        logger.Error(emsg);
                        lException = new Exception(emsg, en);
                    }
                }
                if (success)
                {
                    logger.Debug($"Channel {Name} successful sent to Orderer transaction id: {proposalTransactionID}");
                    if (replyonly)
                        return null;
                    if (!waittimeinmilliseconds.HasValue)
                        await sret.Task;
                    else
                    {
                        using (var timeoutCancellationTokenSource = new CancellationTokenSource())
                        {
                            var completedTask = await Task.WhenAny(sret.Task, Task.Delay(waittimeinmilliseconds.Value, timeoutCancellationTokenSource.Token));
                            if (completedTask == sret.Task)
                                timeoutCancellationTokenSource.Cancel();
                            else
                            {
                                UnregisterTxListener(proposalTransactionID);
                                Exception ex = new OperationCanceledException("The operation has timed out.");
                                throw new TransactionException(ex.Message, ex);
                            }
                        }
                    }
                    if (sret.Task.IsCanceled)
                    {
                        token.ThrowIfCancellationRequested();
                        Exception ex2 = new OperationCanceledException("The operation has timed out.");
                        throw new TransactionException(ex2.Message, ex2);
                    }
                    if (sret.Task.IsFaulted)
                    {
                        AggregateException ex = sret.Task.Exception;
                        Exception e3 = ex.InnerException ?? ex;
                        string message;
                        if (e3 is RpcException)
                        {
                            RpcException rpce = (RpcException) e3;
                            message = $"Channel {Name} failed to recieve event from {proposalTransactionID}. gRPC failure={rpce.Status}";
                        }
                        else
                            message = $"Channel {Name} failed to recieve event from {proposalTransactionID}. {e3.Message}";

                        logger.ErrorException(message, e3);
                        throw new TransactionException(message, ex);
                    }
                    return sret.Task.Result;
                }
                string femsg = $"Channel {Name} failed to place transaction {proposalTransactionID} on Orderer. Cause: UNSUCCESSFUL. {GetRespData(resp)}";
                UnregisterTxListener(proposalTransactionID);
                Exception e = new TransactionException(femsg, lException);
                logger.ErrorException(e.Message, e);
                throw e;
            }
            catch (Exception e)
            {
                string frmsg = $"Channel {Name} failed to place transaction on Orderer. {e.Message}";
                Exception e3 = new TransactionException(frmsg, e);
                logger.ErrorException(e3.Message, e3);
                throw e;
            }
        }

        /**
         * Build response details
         *
         * @param resp
         * @return
         */
        private string GetRespData(BroadcastResponse resp)
        {
            StringBuilder respdata = new StringBuilder(400);
            if (resp != null)
            {
                Status status = resp.Status;
                respdata.Append(status.ToString());
                respdata.Append("-");
                respdata.Append((int) status);
                string info = resp.Info;
                if (!string.IsNullOrEmpty(info))
                {
                    if (respdata.Length > 0)
                        respdata.Append(", ");
                    respdata.Append("Additional information: ").Append(info);
                }
            }
            return respdata.ToString();
        }

        private Envelope CreateTransactionEnvelope(Payload transactionPayload, IUser user)
        {
            return new Envelope {Payload = transactionPayload.ToByteString(), Signature = ByteString.CopyFrom(client.CryptoSuite.Sign(user.Enrollment.GetKeyPair(), transactionPayload.ToByteArray()))};
        }

        public byte[] GetChannelConfigurationSignature(ChannelConfiguration channelConfiguration, IUser signer)
        {
            signer.UserContextCheck();
            if (null == channelConfiguration)
                throw new InvalidArgumentException("channelConfiguration is null");
            try
            {
                Envelope ccEnvelope = Envelope.Parser.ParseFrom(channelConfiguration.ChannelConfigurationBytes);
                Payload ccPayload = Payload.Parser.ParseFrom(ccEnvelope.Payload);
                TransactionContext transactionContext = GetTransactionContext(signer);
                ConfigUpdateEnvelope configUpdateEnv = ConfigUpdateEnvelope.Parser.ParseFrom(ccPayload.Data);
                ByteString configUpdate = configUpdateEnv.ConfigUpdate;
                ByteString sigHeaderByteString = ProtoUtils.GetSignatureHeaderAsByteString(signer, transactionContext);
                ByteString signatureByteSting = transactionContext.SignByteStrings(new IUser[] {signer}, sigHeaderByteString, configUpdate)[0];
                return new ConfigSignature {SignatureHeader = sigHeaderByteString, Signature = signatureByteSting}.ToByteArray();
            }
            catch (Exception e)
            {
                throw new InvalidArgumentException(e);
            }
            finally
            {
                logger.Debug("finally done");
            }
        }

        /**
         * Register a block listener.
         *
         * @param listener function with single argument with type {@link BlockEvent}
         * @return The handle of the registered block listener.
         * @throws InvalidArgumentException if the channel is shutdown.
         */
        public string RegisterBlockListener(Action<BlockEvent> listenerAction)
        {
            if (IsShutdown)
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            return new BL(this, listenerAction).Handle;
        }

        /**
         * Unregister a block listener.
         *
         * @param handle of Block listener to remove.
         * @return false if not found.
         * @throws InvalidArgumentException if the channel is shutdown or invalid arguments.
         */
        public bool UnregisterBlockListener(string handle)
        {
            if (IsShutdown)
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            CheckHandle(BL.BLOCK_LISTENER_TAG, handle);
            lock (blockListeners)
            {
                if (blockListeners.ContainsKey(handle))
                {
                    blockListeners.Remove(handle);
                    return true;
                }
                return false;
            }
        }
        //////////  Transaction monitoring  /////////////////////////////

        private void StartEventQue()
        {
            if (eventQueueTokenSource != null)
                return;
            eventQueueTokenSource = new CancellationTokenSource();
            CancellationToken ct = eventQueueTokenSource.Token;

            Task.Run(() =>
            {
                while (!IsShutdown)
                {
                    if (!initialized)
                    {
                        try
                        {
                            logger.Debug("not intialized:" + initialized);
                            Thread.Sleep(1);
                        }
                        catch (Exception e)
                        {
                            logger.WarnException(e.Message, e);
                        }
                        continue; //wait on sending events till the channel is initialized.
                    }

                    BlockEvent blockEvent;
                    try
                    {
                        blockEvent = ChannelEventQueue.GetNextEvent();
                    }
                    catch (EventHubException e)
                    {
                        if (!IsShutdown)
                            logger.ErrorException(e.Message, e);
                        continue;
                    }

                    if (blockEvent == null)
                    {
                        logger.Warn("GOT null block event.");
                        continue;
                    }

                    try
                    {
                        string blockchainID = blockEvent.ChannelId;
                        string tp = blockEvent.Peer != null ? "Peer: " + blockEvent.Peer.Name : "Eventhub: " + blockEvent.EventHub.Name;
                        string from = $"Channel {Name} eventqueue got block event with block number: {blockEvent.BlockNumber} for channel: {blockchainID}, from {tp}";
                        logger.Trace(from);
                        if (!Name.Equals(blockchainID))
                        {
                            logger.Warn($"Channel {Name} eventqueue got block event NOT FOR ME  channelId {blockchainID} from {from}");
                            continue; // not targeted for this channel
                        }
                        List<BL> blcopy = new List<BL>();
                        lock (blockListeners)
                        {
                            blcopy.AddRange(blockListeners.Values);
                        }
                        foreach (BL l in blcopy)
                        {
                            try
                            {
                                logger.Trace($"Sending block event '{from}' to block listener {l.Handle}");
                                Task.Run(() =>
                                {
                                    l.ListenerAction(blockEvent);
                                },ct);
                            }
                            catch (Exception e)
                            {
                                //Don't let one register stop rest.
                                logger.ErrorException($"Error calling block listener {l.Handle} on channel: {Name} event: {from} ", e);
                            }
                        }
                    }
                    catch (Exception e2)
                    {
                        logger.ErrorException("Unable to parse event", e2);
                        logger.Debug("event:\n)");
                        logger.Debug(blockEvent.ToString());
                    }

                    ct.ThrowIfCancellationRequested();
                }
            }, ct);
        }

        private string RegisterTransactionListenerProcessor()
        {
            logger.Debug($"Channel {Name} registerTransactionListenerProcessor starting");
            // Transaction listener is internal Block listener for transactions
            return RegisterBlockListener(TransactionBlockReceived);
        }

        private void RunSweeper()
        {
            if (IsShutdown || DELTA_SWEEP < 1)
                return;
            if (sweeper == null)
            {
                sweeper = new Timer(state =>
                {
                    try
                    {
                        if (txListeners != null)
                        {
                            lock (txListeners)
                            {
                                foreach (string st in txListeners.Keys.ToList())
                                {
                                    txListeners[st].Where(a => a.sweepMe()).ToList().ForEach(a => txListeners[st].Remove(a));
                                    if (txListeners[st].Count == 0)
                                        txListeners.Remove(st);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Warn($"Sweeper got error: {e.Message}", e);
                    }
                }, null, 0, DELTA_SWEEP);
            }
        }

        /**
         * Register a transactionId that to get notification on when the event is seen in the block chain.
         *
         * @param txid
         * @param nOfEvents
         * @return
         */

        private TaskCompletionSource<BlockEvent.TransactionEvent> RegisterTxListener(string txid, NOfEvents nOfEvents, bool failFast)
        {
            TaskCompletionSource<BlockEvent.TransactionEvent> future = new TaskCompletionSource<BlockEvent.TransactionEvent>();
            var _ = new TL(this, txid, future, nOfEvents, failFast);
            return future;
        }

        /**
         * Unregister a transactionId
         *
         * @param txid
         */
        private void UnregisterTxListener(string txid)
        {
            lock (txListeners)
            {
                txListeners.Remove(txid);
            }
        }

        /**
         * Register a chaincode event listener. Both chaincodeId pattern AND eventName pattern must match to invoke
         * the chaincodeEventListener
         *
         * @param chaincodeId            Java pattern for chaincode identifier also know as chaincode name. If ma
         * @param eventName              Java pattern to match the event name.
         * @param chaincodeEventListener The listener to be invoked if both chaincodeId and eventName pattern matches.
         * @return Handle to be used to unregister the event listener {@link #unregisterChaincodeEventListener(String)}
         * @throws InvalidArgumentException
         */
        public string RegisterChaincodeEventListener(Regex chaincodeId, Regex eventName, Action<string, BlockEvent, ChaincodeEventDeserializer> listenerAction)
        {
            if (IsShutdown)
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            if (chaincodeId == null)
                throw new InvalidArgumentException("The chaincodeId argument may not be null.");
            if (eventName == null)
                throw new InvalidArgumentException("The eventName argument may not be null.");
            if (listenerAction == null)
                throw new InvalidArgumentException("The chaincodeListenerAction argument may not be null.");
            ChaincodeEventListenerEntry chaincodeEventListenerEntry = new ChaincodeEventListenerEntry(this, chaincodeId, eventName, listenerAction);
            lock (this)
            {
                if (null == blh)
                    blh = RegisterChaincodeListenerProcessor();
            }
            return chaincodeEventListenerEntry.Handle;
        }

        /**
         * Unregister an existing chaincode event listener.
         *
         * @param handle Chaincode event listener handle to be unregistered.
         * @return True if the chaincode handler was found and removed.
         * @throws InvalidArgumentException
         */

        public bool UnregisterChaincodeEventListener(string handle)
        {
            bool ret;
            if (IsShutdown)
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            CheckHandle(ChaincodeEventListenerEntry.CHAINCODE_EVENTS_TAG, handle);
            lock (chainCodeListeners)
            {
                if (chainCodeListeners.ContainsKey(handle))
                {
                    chainCodeListeners.Remove(handle);
                    ret = true;
                }
                else
                {
                    ret = false;
                }
            }
            lock (this)
            {
                if (null != blh && chainCodeListeners.Count == 0)
                {
                    UnregisterBlockListener(blh);
                    blh = null;
                }
            }
            return ret;
        }
        ////////////////////////////////////////////////////////////////////////
        ////////////////  Chaincode Events..  //////////////////////////////////

        private string RegisterChaincodeListenerProcessor()
        {
            logger.Debug($"Channel {Name} registerChaincodeListenerProcessor starting");
            // Chaincode event listener is internal Block listener for chaincode events.
            return RegisterBlockListener(ChaincodeBlockReceived);
        }

        /**
         * Shutdown the channel with all resources released.
         *
         * @param force force immediate shutdown.
         */

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (IsShutdown)
                return;
            initialized = false;
            IsShutdown = true;
            if (chainCodeListeners != null)
                chainCodeListeners.Clear();
            if (blockListeners != null)
                blockListeners.Clear();
            if (client != null)
                client.RemoveChannel(this);
            client = null;
            foreach (EventHub eh in eventHubs)
            {
                try
                {
                    eh.Shutdown();
                }
                catch (Exception e)
                {
                    // Best effort.
                }
            }
            eventHubs.Clear();
            foreach (Peer peer in Peers.ToList())
            {
                try
                {
                    RemovePeerInternal(peer);
                    peer.Shutdown(force);
                }
                catch (Exception e)
                {
                    // Best effort.
                }
            }
            peers.Clear(); // make sure.
            //Make sure
            foreach (List<Peer> peerRoleSet in peerRoleSetMap.Values)
                peerRoleSet.Clear();
            foreach (Orderer orderer in Orderers)
                orderer.Shutdown(force);
            orderers.Clear();
            if (null != eventQueueTokenSource)
            {
                eventQueueTokenSource.Cancel();
                eventQueueTokenSource = null;
            }
            if (sweeper != null)
            {
                sweeper.Dispose();
                sweeper = null;
            }
        }

        /**
         * Serialize channel to a file using Java serialization.
         * Deserialized channel will NOT be in an initialized state.
         *
         * @param file file
         * @throws IOException
         * @throws InvalidArgumentException
         */
        /*
        public void serializeChannel(File file) {

            if (null == file) {
                throw new InvalidArgumentException("File parameter may not be null");
            }

            Files.write(Paths.get(file.getAbsolutePath()), serializeChannel(),
                    StandardOpenOption.CREATE, StandardOpenOption.TRUNCATE_EXISTING, StandardOpenOption.WRITE);

        }

        /**
         * Serialize channel to a byte array using Java serialization.
         * Deserialized channel will NOT be in an initialized state.
         *
         * @throws InvalidArgumentException
         * @throws IOException
         */
        /*
        public byte[] serializeChannel() {

            if (isShutdown()) {
                throw new InvalidArgumentException(format("Channel %s has been shutdown.", getName()));
            }

            ObjectOutputStream out = null;

            try {
                ByteArrayOutputStream bai = new ByteArrayOutputStream();
                out = new ObjectOutputStream(bai);
                out.writeObject(this);
                out.flush();
                return bai.toByteArray();
            } finally {
                if (null != out) {
                    try {
                        out.close();
                    } catch (IOException e) {
                        logger.error(e); // best effort.
                    }
                }
            }

        }
            */
        ~Channel()
        {
            Shutdown(true);
        }

        /**
         * Own block listener to manage transactions.
         *
         * @return
         */
        internal void TransactionBlockReceived(BlockEvent blockEvent)
        {
            if (txListeners.Count == 0)
                return;
            foreach (BlockEvent.TransactionEvent transactionEvent in blockEvent.TransactionEvents)
            {
                logger.Debug($"Channel {Name} got event for transaction {transactionEvent.TransactionID}");
                List<TL> txL = new List<TL>();
                lock (txListeners)
                {
                    LinkedList<TL> list = txListeners[transactionEvent.TransactionID];
                    if (null != list)
                    {
                        txL.AddRange(list);
                    }
                }
                foreach (TL l in txL)
                {
                    try
                    {
                        // only if we get events from each eventhub on the channel fire the transactions event.
                        //   if (getEventHubs().containsAll(l.eventReceived(transactionEvent.getEventHub()))) {
                        if (l.EventReceived(transactionEvent))
                        {
                            l.Fire(transactionEvent);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.ErrorException(e.Message, e); // Don't let one register stop rest.
                    }
                }
            }
        }

        internal void ChaincodeBlockReceived(BlockEvent blockEvent)
        {
            if (chainCodeListeners.Count == 0)
                return;
            List<ChaincodeEventDeserializer> chaincodeEvents = new List<ChaincodeEventDeserializer>();
            //Find the chaincode events in the transactions.
            foreach (BlockEvent.TransactionEvent transactionEvent in blockEvent.TransactionEvents)
            {
                logger.Debug($"Channel {Name} got event for transaction {transactionEvent.TransactionID}");
                foreach (BlockInfo.TransactionEnvelopeInfo.TransactionActionInfo info in transactionEvent.TransactionActionInfos)
                {
                    ChaincodeEventDeserializer evnt = info.Event;
                    if (null != evnt)
                        chaincodeEvents.Add(evnt);
                }
            }
            if (chaincodeEvents.Count > 0)
            {
                List<(ChaincodeEventListenerEntry EventListener, ChaincodeEventDeserializer Event)> matches = new List<(ChaincodeEventListenerEntry EventListener, ChaincodeEventDeserializer Event)>(); //Find matches.
                lock (chainCodeListeners)
                {
                    foreach (ChaincodeEventListenerEntry chaincodeEventListenerEntry in chainCodeListeners.Values)
                    {
                        foreach (ChaincodeEventDeserializer chaincodeEvent in chaincodeEvents)
                        {
                            if (chaincodeEventListenerEntry.IsMatch(chaincodeEvent))
                                matches.Add((chaincodeEventListenerEntry, chaincodeEvent));
                        }
                    }
                }

                //fire events
                foreach ((ChaincodeEventListenerEntry EventListener, ChaincodeEventDeserializer Event) match in matches)
                {
                    ChaincodeEventListenerEntry chaincodeEventListenerEntry = match.EventListener;
                    ChaincodeEventDeserializer ce = match.Event;
                    chaincodeEventListenerEntry.Fire(blockEvent, ce);
                }
            }
        }


        /**
     * Options for the peer.
     * These options are channel based.
     */

        
        public class PeerOptions
        {
            protected List<PeerRole> peerRoles;
            protected PeerOptions()
            {
            }

            /**
             * Get newest block on startup of peer eventing service.
             *
             * @return
             */
            
            public bool? Newest { get; private set; } = true;
            /**
             * The block number to start getting events from on start up of the peer eventing service..
             *
             * @return the start number
             */
            
            public long? StartEventsBlock { get; private set; }

            /**
             * The stopping block number when the peer eventing service will stop sending blocks.
             *
             * @return the stop block number.
             */

            
            public long StopEventsBlock { get; private set; } = long.MaxValue;

            /**
             * Clone.
             *
             * @return return a duplicate of this instance.
             */


            /**
             * Is the peer eventing service registered for filtered blocks
             *
             * @return true if filtered blocks will be returned by the peer eventing service.
             */
            
            public bool IsRegisterEventsForFilteredBlocks { get; protected set; } = false;

            /**
             * Return the roles the peer has.
             *
             * @return the roles {@link PeerRole}
             */
            
            public List<PeerRole> PeerRoles
            {
                get
                {
                    if (peerRoles == null)
                        return PeerRoleExtensions.All();
                    return peerRoles;
                }
            }

            public PeerOptions Clone()
            {
                PeerOptions p = new PeerOptions();
                p.peerRoles = peerRoles?.ToList();
                p.IsRegisterEventsForFilteredBlocks = IsRegisterEventsForFilteredBlocks;
                p.Newest = Newest;
                p.StartEventsBlock = StartEventsBlock;
                p.StopEventsBlock = StopEventsBlock;
                return p;
            }

            /**
             * Register the peer eventing services to return filtered blocks.
             *
             * @return the PeerOptions instance.
             */

            public PeerOptions RegisterEventsForFilteredBlocks()
            {
                IsRegisterEventsForFilteredBlocks = true;
                return this;
            }

            /**
             * Register the peer eventing services to return full event blocks.
             *
             * @return the PeerOptions instance.
             */

            public PeerOptions RegisterEventsForBlocks()
            {
                IsRegisterEventsForFilteredBlocks = false;
                return this;
            }

            /**
             * Create an instance of PeerOptions.
             *
             * @return the PeerOptions instance.
             */

            public static PeerOptions CreatePeerOptions()
            {
                return new PeerOptions();
            }

            public bool HasPeerRoles()
            {
                return peerRoles != null && peerRoles.Count > 0;
            }
            /**
             * Set the roles this peer will have on the chain it will added or joined.
             *
             * @param peerRoles {@link PeerRole}
             * @return This PeerOptions.
             */

            public PeerOptions SetPeerRoles(List<PeerRole> peerRoles)
            {
                this.peerRoles = peerRoles;
                return this;
            }

            public PeerOptions SetPeerRoles(params PeerRole[] peerRoles)
            {
                this.peerRoles = peerRoles.ToList();
                return this;
            }
            /**
             * Add to the roles this peer will have on the chain it will added or joined.
             *
             * @param peerRole see {@link PeerRole}
             * @return This PeerOptions.
             */

            public PeerOptions AddPeerRole(PeerRole peerRole)
            {
                if (peerRoles == null)
                    peerRoles = new List<PeerRole>();
                if (!peerRoles.Contains(peerRole))
                    peerRoles.Add(peerRole);
                return this;
            }

            /**
             * Set the block number the eventing peer will start relieving events.
             *
             * @param start The staring block number.
             * @return This PeerOptions.
             */
            public PeerOptions StartEvents(long start)
            {
                StartEventsBlock = start;
                Newest = null;
                return this;
            }


            /**
             * This is the default. It will start retrieving events with the newest. Note this is not the
             * next block that is added to the chain  but the current block on the chain.
             *
             * @return This PeerOptions.
             */

            public PeerOptions StartEventsNewest()
            {
                StartEventsBlock = null;
                Newest = true;
                return this;
            }

            /**
             * The block number to stop sending events.
             *
             * @param stop the number to stop sending events.
             * @return This PeerOptions.
             */
            public PeerOptions StopEvents(long stop)
            {
                StopEventsBlock = stop;
                return this;
            }
        }


        public class NOfEvents
        {
            private readonly HashSet<EventHub> eventHubs = new HashSet<EventHub>();
            private readonly HashSet<NOfEvents> nOfEvents = new HashSet<NOfEvents>();
            private readonly HashSet<Peer> peers = new HashSet<Peer>();
            private long n = long.MaxValue; //all
            private bool started = false;

            public NOfEvents(NOfEvents nof)
            {
                // Deep Copy.
                if (nof == NofNoEvents)
                    throw new IllegalArgumentException("nofNoEvents may not be copied.");
                Ready = false; // no use in one set to ready.
                started = false;
                n = nof.n;
                peers = new HashSet<Peer>(nof.peers);
                eventHubs = new HashSet<EventHub>(nof.eventHubs);
                foreach (NOfEvents nofc in nof.nOfEvents)
                    nOfEvents.Add(new NOfEvents(nofc));
            }

            private NOfEvents()
            {
            }

            public bool Ready { get; private set; } = false;

            public static NOfEvents NofNoEvents { get; } = new NoEvents();

            public virtual NOfEvents SetN(int n)
            {
                if (n < 1)
                    throw new IllegalArgumentException($"N was {n} but needs to be greater than 0.");
                this.n = n;
                return this;
            }

            public virtual NOfEvents AddPeers(params Peer[] peers)
            {
                if (peers == null || peers.Length == 0)
                    throw new IllegalArgumentException("Peers added must be not null or empty.");
                this.peers.AddRange(peers);
                return this;
            }

            public virtual NOfEvents AddPeers(IEnumerable<Peer> peers)
            {
                AddPeers(peers.ToArray());
                return this;
            }

            public virtual NOfEvents AddEventHubs(params EventHub[] eventHubs)
            {
                if (eventHubs == null || eventHubs.Length == 0)
                    throw new IllegalArgumentException("EventHubs added must be not null or empty.");
                this.eventHubs.AddRange(eventHubs);
                return this;
            }

            public virtual NOfEvents AddEventHubs(IEnumerable<EventHub> eventHubs)
            {
                AddEventHubs(eventHubs.ToArray());
                return this;
            }

            public virtual NOfEvents AddNOfs(params NOfEvents[] nOfEvents)
            {
                if (nOfEvents == null || nOfEvents.Length == 0)
                    throw new IllegalArgumentException("nofEvents added must be not null or empty.");
                foreach (NOfEvents n in nOfEvents)
                {
                    if (NofNoEvents == n)
                        throw new IllegalArgumentException("nofNoEvents may not be added as an event.");
                    if (InHayStack(n))
                        throw new IllegalArgumentException("nofEvents already was added..");
                    this.nOfEvents.Add(new NOfEvents(n));
                }
                return this;
            }

            private bool InHayStack(NOfEvents needle)
            {
                if (this == needle)
                    return true;
                foreach (NOfEvents straw in nOfEvents)
                {
                    if (straw.InHayStack(needle))
                        return true;
                }
                return false;
            }

            public NOfEvents AddNOfs(IEnumerable<NOfEvents> nofs)
            {
                AddNOfs(nofs.ToArray());
                return this;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public List<Peer> UnSeenPeers()
            {
                HashSet<Peer> unseen = new HashSet<Peer>();
                unseen.AddRange(peers);
                foreach (NOfEvents nOfEvents in nOfEvents)
                    unseen.AddRange(NofNoEvents.UnSeenPeers());
                return unseen.ToList();
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public List<EventHub> UnSeenEventHubs()
            {
                HashSet<EventHub> unseen = new HashSet<EventHub>();
                unseen.AddRange(eventHubs);
                foreach (NOfEvents nOfEvents in nOfEvents)
                    unseen.AddRange(NofNoEvents.UnSeenEventHubs());
                return unseen.ToList();
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public bool Seen(EventHub eventHub)
            {
                if (!started)
                {
                    started = true;
                    n = Math.Min(eventHubs.Count + peers.Count + nOfEvents.Count, n);
                }
                if (!Ready)
                {
                    if (eventHubs.Remove(eventHub))
                    {
                        if (--n == 0)
                        {
                            Ready = true;
                        }
                    }
                    if (!Ready)
                    {
                        foreach (NOfEvents e in nOfEvents.ToList())
                        {
                            if (e.Seen(eventHub))
                            {
                                nOfEvents.Remove(e);
                                if (--n == 0)
                                {
                                    Ready = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (Ready)
                {
                    eventHubs.Clear();
                    peers.Clear();
                    nOfEvents.Clear();
                }
                return Ready;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public bool Seen(Peer peer)
            {
                if (!started)
                {
                    started = true;
                    n = Math.Min(eventHubs.Count + peers.Count + nOfEvents.Count, n);
                }

                if (!Ready)
                {
                    if (peers.Remove(peer))
                    {
                        if (--n == 0)
                            Ready = true;
                    }

                    if (!Ready)
                    {
                        if (!Ready)
                        {
                            foreach (NOfEvents e in nOfEvents.ToList())
                            {
                                if (e.Seen(peer))
                                {
                                    nOfEvents.Remove(e);
                                    if (--n == 0)
                                    {
                                        Ready = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                if (Ready)
                {
                    eventHubs.Clear();
                    peers.Clear();
                    nOfEvents.Clear();
                }

                return Ready;
            }

            public static NOfEvents CreateNofEvents()
            {
                return new NOfEvents();
            }


            public static NOfEvents CreateNoEvents()
            {
                return NofNoEvents;
            }

            public class NoEvents : NOfEvents
            {
                public NoEvents() : base()
                {
                    Ready = true;
                }

                public override NOfEvents AddNOfs(params NOfEvents[] nOfEvents)
                {
                    throw new IllegalArgumentException("Can not add any events.");
                }

                public override NOfEvents AddEventHubs(params EventHub[] eventHub)
                {
                    throw new IllegalArgumentException("Can not add any events.");
                }

                public override NOfEvents AddPeers(params Peer[] peers)
                {
                    throw new IllegalArgumentException("Can not add any events.");
                }

                public override NOfEvents SetN(int n)
                {
                    throw new IllegalArgumentException("Can not set N");
                }

                public override NOfEvents AddEventHubs(IEnumerable<EventHub> eventHubs)
                {
                    throw new IllegalArgumentException("Can not add any events.");
                }

                public override NOfEvents AddPeers(IEnumerable<Peer> peers)
                {
                    throw new IllegalArgumentException("Can not add any events.");
                }
            }
        }

        public class TransactionOptions
        {
            private TransactionOptions()
            {
            }

            public List<Orderer> Orderers { get; set; }
            public bool ShuffleOrders { get; set; } = true;
            public NOfEvents NOfEvents { get; set; }
            public IUser UserContext { get; set; }
            public bool FailFast { get; set; } = true;

            /**
             * Fail fast when there is an invalid transaction received on the eventhub or eventing peer being observed.
             * The default value is true.
             *
             * @param failFast fail fast.
             * @return This TransactionOptions
             */
            public TransactionOptions SetFailFast(bool failFast)
            {
                FailFast = failFast;
                return this;
            }

            /**
             * The user context that is to be used. The default is the user context on the client.
             *
             * @param userContext
             * @return This TransactionOptions
             */
            public TransactionOptions SetUserContext(IUser userContext)
            {
                UserContext = userContext;
                return this;
            }

            /**
             * The orders to try on this transaction. Each order is tried in turn for a successful submission.
             * The default is try all orderers on the chain.
             *
             * @param orderers the orderers to try.
             * @return This TransactionOptions
             */
            public TransactionOptions SetOrderers(params Orderer[] orderers)
            {
                Orderers = orderers.ToList();
                return this;
            }

            /**
             * Shuffle the order the Orderers are tried. The default is true.
             *
             * @param shuffleOrders
             * @return This TransactionOptions
             */
            public TransactionOptions SetShuffleOrders(bool shuffleOrders)
            {
                ShuffleOrders = shuffleOrders;
                return this;
            }

            /**
             * Events reporting Eventing Peers and EventHubs to complete the transaction.
             * This maybe set to NOfEvents.nofNoEvents that will complete the future as soon as a successful submission
             * to an Orderer, but the completed Transaction event in that case will be null.
             *
             * @param nOfEvents
             * @return This TransactionOptions
             */
            public TransactionOptions SetNOfEvents(NOfEvents nOfEvents)
            {
                NOfEvents = nOfEvents == NOfEvents.NofNoEvents ? nOfEvents : new NOfEvents(nOfEvents);
                return this;
            }

            /**
             * Create transaction options.
             *
             * @return return transaction options.
             */
            public static TransactionOptions Create()
            {
                return new TransactionOptions();
            }

            /**
             * The orders to try on this transaction. Each order is tried in turn for a successful submission.
             * The default is try all orderers on the chain.
             *
             * @param orderers the orderers to try.
             * @return This TransactionOptions
             */
            public TransactionOptions SetOrderers(IEnumerable<Orderer> orderers)
            {
                return SetOrderers(orderers.ToArray());
            }
        }


        /**
          * MSPs
          */

        public class MSP
        {
            private readonly FabricMSPConfig fabricMSPConfig;
            private byte[][] adminCerts;
            private byte[][] intermediateCerts;
            private string orgName;
            private byte[][] rootCerts;

            public MSP(string orgName, FabricMSPConfig fabricMSPConfig)
            {
                this.orgName = orgName;
                this.fabricMSPConfig = fabricMSPConfig;
            }

            /**
             * Known as the MSPID internally
             *
             * @return
             */

            public string ID => fabricMSPConfig.Name;


            /**
             * AdminCerts
             *
             * @return array of admin certs in PEM bytes format.
             */
            public byte[][] AdminCerts
            {
                get
                {
                    if (null == adminCerts)
                    {
                        adminCerts = new byte[fabricMSPConfig.Admins.Count][];
                        int i = 0;
                        foreach (ByteString cert in fabricMSPConfig.Admins)
                        {
                            adminCerts[i++] = cert.ToByteArray();
                        }
                    }

                    return adminCerts;
                }
            }

            /**
             * RootCerts
             *
             * @return array of admin certs in PEM bytes format.
             */
            public byte[][] RootCerts
            {
                get
                {
                    if (null == rootCerts)
                    {
                        rootCerts = new byte[fabricMSPConfig.RootCerts.Count][];
                        int i = 0;
                        foreach (ByteString cert in fabricMSPConfig.RootCerts)
                        {
                            rootCerts[i++] = cert.ToByteArray();
                        }
                    }

                    return rootCerts;
                }
            }

            /**
             * IntermediateCerts
             *
             * @return array of intermediate certs in PEM bytes format.
             */
            public byte[][] IntermediateCerts
            {
                get
                {
                    if (null == intermediateCerts)
                    {
                        intermediateCerts = new byte[fabricMSPConfig.IntermediateCerts.Count][];
                        int i = 0;
                        foreach (ByteString cert in fabricMSPConfig.IntermediateCerts)
                        {
                            intermediateCerts[i++] = cert.ToByteArray();
                        }
                    }

                    return intermediateCerts;
                }
            }
        }

        public class ChannelEventQue
        {
            private static readonly ILog logger = LogProvider.GetLogger(typeof(ChannelEventQue));
            private readonly Channel channel;
            private readonly BlockingCollection<BlockEvent> events = new BlockingCollection<BlockEvent>(); //Thread safe
            private Exception eventException;

            public ChannelEventQue(Channel ch)
            {
                channel = ch;
            }

            public void EventError(Exception t)
            {
                eventException = t;
            }

            public bool AddBEvent(BlockEvent evnt)
            {
                if (channel.IsShutdown)
                    return false;
                //For now just support blocks --- other types are also reported as blocks.

                if (!evnt.IsBlockEvent)
                    return false;
                // May be fed by multiple eventhubs but BlockingQueue.add() is thread-safe
                events.Add(evnt);
                return true;
            }

            public BlockEvent GetNextEvent()
            {
                if (channel.IsShutdown)
                    throw new EventHubException($"Channel {channel.Name} has been shutdown");
                BlockEvent ret = null;
                if (eventException != null)
                    throw new EventHubException(eventException);
                try
                {
                    ret = events.Take();
                }
                catch (Exception e)
                {
                    if (channel.IsShutdown)
                        throw new EventHubException(eventException);
                    logger.WarnException(e.Message, e);
                    if (eventException != null)
                    {
                        EventHubException eve = new EventHubException(eventException);
                        logger.ErrorException(eve.Message, eve);
                        throw eve;
                    }
                }
                if (eventException != null)
                    throw new EventHubException(eventException);
                if (channel.IsShutdown)
                    throw new EventHubException($"Channel {channel.Name} has been shutdown.");
                return ret;
            }
        }

        public class BL
        {
            public static readonly string BLOCK_LISTENER_TAG = "BLOCK_LISTENER_HANDLE";
            private static readonly ILog logger = LogProvider.GetLogger(typeof(BL));
            private readonly Channel channel;

            public BL(Channel ch, Action<BlockEvent> listenerAction)
            {
                channel = ch;
                Handle = BLOCK_LISTENER_TAG + Utils.GenerateUUID() + BLOCK_LISTENER_TAG;
                logger.Debug($"Channel {channel.Name} blockListener {Handle} starting");
                ListenerAction = listenerAction;
                lock (channel.blockListeners)
                {
                    channel.blockListeners.Add(Handle, this);
                }
            }

            public Action<BlockEvent> ListenerAction { get; }
            public string Handle { get; }


        }

        private class TL
        {
            private static readonly ILog logger = LogProvider.GetLogger(typeof(TL));
            private readonly Channel channel;
            private readonly long createTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            private readonly long DELTA_SWEEP = Config.Instance.GetTransactionListenerCleanUpTimeout();
            private readonly HashSet<EventHub> eventHubs;
            private readonly bool failFast;
            private readonly TaskCompletionSource<BlockEvent.TransactionEvent> future;
            private readonly NOfEvents nOfEvents;
            private readonly HashSet<Peer> peers;
            private readonly string txID;
            private bool fired = false;
            private long sweepTime;

            public TL(Channel ch, string txID, TaskCompletionSource<BlockEvent.TransactionEvent> future, NOfEvents nOfEvents, bool failFast)
            {
                sweepTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long) (DELTA_SWEEP * 1.5);
                this.txID = txID;
                this.future = future;
                channel = ch;
                this.nOfEvents = new NOfEvents(nOfEvents);
                peers = new HashSet<Peer>(nOfEvents.UnSeenPeers());
                eventHubs = new HashSet<EventHub>(nOfEvents.UnSeenEventHubs());
                this.failFast = failFast;
                AddListener();
            }

            /**
             * Record transactions event.
             *
             * @param transactionEvent
             * @return True if transactions have been seen on all eventing peers and eventhubs.
             */
            public bool EventReceived(BlockEvent.TransactionEvent transactionEvent)
            {
                sweepTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + DELTA_SWEEP; //seen activity keep it active.
                Peer peer = transactionEvent.Peer;
                EventHub eventHub = transactionEvent.EventHub;
                if (peer != null && !peers.Contains(peer))
                    return false;
                if (eventHub != null && !eventHubs.Contains(eventHub))
                    return false;
                if (failFast && !transactionEvent.IsValid)
                    return true;
                if (peer != null)
                {
                    nOfEvents.Seen(peer);
                    logger.Debug($"Channel {channel.Name} seen transaction event {txID} for peer {peer.Name}");
                }
                else if (null != eventHub)
                {
                    nOfEvents.Seen(eventHub);
                    logger.Debug($"Channel {channel.Name} seen transaction event {txID} for eventHub {eventHub.Name}");
                }
                else
                    logger.Error($"Channel {channel.Name} seen transaction event {txID} with no associated peer or eventhub");
                return nOfEvents.Ready;
            }

            private void AddListener()
            {
                channel.RunSweeper();
                lock (channel.txListeners)
                {
                    LinkedList<TL> tl;
                    if (channel.txListeners.ContainsKey(txID))
                        tl = channel.txListeners[txID];
                    else
                    {
                        tl = new LinkedList<TL>();
                        channel.txListeners.Add(txID, tl);
                    }

                    tl.AddLast(this);
                }
            }

            public bool sweepMe()
            {
                // Sweeps DO NOT fire future. user needs to put timeout on their futures for timeouts.

                bool ret = sweepTime < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() || fired || future.Task.IsCompleted;
                if (logger.IsDebugEnabled() && ret)
                {
                    StringBuilder sb = new StringBuilder(10000);
                    sb.Append("Non reporting event hubs:");
                    string sep = "";
                    foreach (EventHub eh in nOfEvents.UnSeenEventHubs())
                    {
                        sb.Append(sep).Append(eh.Name);
                        sep = ",";
                    }

                    if (sb.Length != 0)
                        sb.Append(". ");
                    sep = "Non reporting peers: ";
                    foreach (Peer peer in nOfEvents.UnSeenPeers())
                    {
                        sb.Append(sep).Append(peer.Name);
                        sep = ",";
                    }
                    logger.Debug($"Force removing transaction listener after {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - createTime} ms for transaction {txID}. {sb.ToString()}" + $". sweep timeout: {sweepTime < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}, fired: {fired}, future done:{future.Task.IsCompleted}");
                }
                return ret;
            }

            public void Fire(BlockEvent.TransactionEvent transactionEvent)
            {
                if (fired)
                    return;
                lock (channel.txListeners)
                {
                    if (channel.txListeners.ContainsKey(txID))
                    {
                        LinkedList<TL> l = channel.txListeners[txID];
                        l.RemoveFirst();
                        if (l.Count == 0)
                            channel.txListeners.Remove(txID);
                    }
                }
                if (future.Task.IsCompleted)
                {
                    fired = true;
                    return;
                }
                if (transactionEvent.IsValid)
                {
                    logger.Debug($"Completing future for channel {channel.Name} and transaction id: {txID}");
                    future.TrySetResult(transactionEvent);
                }
                else
                {
                    logger.Debug($"Completing future as exception for channel {channel.Name} and transaction id: {txID}, validation code: {transactionEvent.ValidationCode:02X}");
                    future.TrySetException(new TransactionEventException($"Received invalid transaction event. Transaction ID {transactionEvent.TransactionID} status {transactionEvent.ValidationCode}", transactionEvent));
                }
            }
        }

        public class ChaincodeEventListenerEntry
        {
            public static readonly string CHAINCODE_EVENTS_TAG = "CHAINCODE_EVENTS_HANDLE";
            private readonly Regex chaincodeIdPattern;
            private readonly Channel channel;
            private readonly Regex eventNamePattern;
            private readonly Action<string, BlockEvent, ChaincodeEventDeserializer> listenerAction;

            public ChaincodeEventListenerEntry(Channel ch, Regex chaincodeIdPattern, Regex eventNamePattern, Action<string, BlockEvent, ChaincodeEventDeserializer> listenerAction)
            {
                channel = ch;
                this.chaincodeIdPattern = chaincodeIdPattern;
                this.eventNamePattern = eventNamePattern;
                this.listenerAction = listenerAction;
                Handle = CHAINCODE_EVENTS_TAG + Utils.GenerateUUID() + CHAINCODE_EVENTS_TAG;
                lock (channel.chainCodeListeners)
                {
                    channel.chainCodeListeners.Add(Handle, this);
                }
            }

            public string Handle { get; }
            public bool IsMatch(ChaincodeEventDeserializer chaincodeEvent)
            {
                return chaincodeIdPattern.Match(chaincodeEvent.ChaincodeId).Success && eventNamePattern.Match(chaincodeEvent.EventName).Success;
            }
            public void Fire(BlockEvent blockEvent, ChaincodeEventDeserializer ce)
            {
        
                Task.Run(() => listenerAction(Handle, blockEvent, ce));
            }
        }
    }
}