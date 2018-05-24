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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Force.DeepCloner;
using Google.Protobuf;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.NetExtensions;
using Hyperledger.Fabric.SDK.Transaction;
using Config = Hyperledger.Fabric.SDK.Helper.Config;
using Metadata = Hyperledger.Fabric.Protos.Common.Metadata;
using Status = Hyperledger.Fabric.Protos.Common.Status;
using Utils = Hyperledger.Fabric.SDK.Helper.Utils;

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
        private static readonly bool IS_DEBUG_LEVEL = logger.IsDebugEnabled();
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private static readonly Config config = Config.GetConfig();

        private static readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL ? config.GetDiagnosticFileDumper() : null;

        private static readonly string SYSTEM_CHANNEL_NAME = "";

        private static readonly long ORDERER_RETRY_WAIT_TIME = config.GetOrdererRetryWaitTime();
        private static readonly long CHANNEL_CONFIG_WAIT_TIME = config.GetChannelConfigWaitTime();

        private static readonly RNGCryptoServiceProvider RANDOM = new RNGCryptoServiceProvider();

        // final Set<Peer> eventingPeers = Collections.synchronizedSet(new HashSet<>());
        public static readonly long DELTA_SWEEP = config.GetTransactionListenerCleanUpTimeout();
        private readonly LinkedHashMap<string, ChaincodeEventListenerEntry> chainCodeListeners = new LinkedHashMap<string, ChaincodeEventListenerEntry>();

        private readonly LinkedList<EventHub> eventHubs = new LinkedList<EventHub>();

        // Name of the channel is only meaningful to the client
        private readonly ConcurrentDictionary<Peer, PeerOptions> peerOptionsMap = new ConcurrentDictionary<Peer, PeerOptions>();

        private readonly ConcurrentDictionary<PeerRole, List<Peer>> peerRoleSetMap = new ConcurrentDictionary<PeerRole, List<Peer>>();

        // The peers on this channel to which the client can connect
        private readonly List<Peer> peers = new List<Peer>();
        private readonly bool systemChannel;
        private string blh = null;

        private readonly LinkedHashMap<string, BL> blockListeners = new LinkedHashMap<string, BL>();
        /**
         * A queue each eventing hub will write events to.
         */

        private readonly ChannelEventQue channelEventQue;

        private HFClient client;
        /**
         * Runs processing events from event hubs.
         */

        private CancellationTokenSource eventQueueTokenSource = null;
        private Block genesisBlock;
        private volatile bool initialized = false;
        private IReadOnlyDictionary<string, MSP> msps = new Dictionary<string, MSP>();
        private readonly LinkedList<Orderer> orderers = new LinkedList<Orderer>();

        private bool shutdown = false;

        //Cleans up any transaction listeners that will probably never complete.
        private Timer sweeper = null;
        private readonly LinkedHashMap<string, LinkedList<TL>> txListeners = new LinkedHashMap<string, LinkedList<TL>>();


        private Channel(string name, HFClient hfClient, Orderer orderer, ChannelConfiguration channelConfiguration, params byte[][] signers) : this(name, hfClient, false)
        {
            logger.Debug($"Creating new channel {name} on the Fabric");

            Channel ordererChannel = orderer.Channel;

            try
            {
                AddOrderer(orderer);

                //-----------------------------------------
                Envelope ccEnvelope = Envelope.Parser.ParseFrom(channelConfiguration.ChannelConfigurationBytes);

                Payload ccPayload = Payload.Parser.ParseFrom(ccEnvelope.Payload);
                ChannelHeader ccChannelHeader = ChannelHeader.Parser.ParseFrom(ccPayload.Header.ChannelHeader);

                if (ccChannelHeader.Type != (int) HeaderType.ConfigUpdate)
                {
                    throw new InvalidArgumentException($"Creating channel; {name} expected config block type {HeaderType.ConfigUpdate}, but got: {ccChannelHeader.Type}");
                }

                if (!name.Equals(ccChannelHeader.ChannelId))
                {
                    throw new InvalidArgumentException($"Expected config block for channel: {name}, but got: {ccChannelHeader.ChannelId}");
                }

                ConfigUpdateEnvelope configUpdateEnv = ConfigUpdateEnvelope.Parser.ParseFrom(ccPayload.Data);
                ByteString configUpdate = configUpdateEnv.ConfigUpdate;

                SendUpdateChannel(configUpdate.ToByteArray(), signers, orderer);
                //         final ConfigUpdateEnvelope.Builder configUpdateEnvBuilder = configUpdateEnv.toBuilder();`

                //---------------------------------------

                //          sendUpdateChannel(channelConfiguration, signers, orderer);

                GetGenesisBlock(orderer); // get Genesis block to make sure channel was created.
                if (genesisBlock == null)
                {
                    throw new TransactionException($"New channel {name} error. Genesis bock returned null");
                }

                logger.Debug($"Created new channel {name} on the Fabric done.");
            }
            catch (TransactionException e)
            {
                orderer.UnsetChannel();
                if (null != ordererChannel)
                {
                    orderer.Channel = ordererChannel;
                }

                logger.ErrorException($"Channel {name} error: {e.Message}", e);
                throw e;
            }
            catch (Exception e)
            {
                orderer.UnsetChannel();
                if (null != ordererChannel)
                {
                    orderer.Channel = ordererChannel;
                }

                string msg = $"Channel {name} error: {e.Message}";

                logger.ErrorException(msg, e);
                throw new TransactionException(msg, e);
            }
        }

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
            channelEventQue = new ChannelEventQue(this);
            FillRoles();
            this.systemChannel = systemChannel;

            if (systemChannel)
            {
                name = SYSTEM_CHANNEL_NAME; //It's special !
                initialized = true;
            }
            else
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new InvalidArgumentException("Channel name is invalid can not be null or empty.");
                }
            }

            if (null == client)
            {
                throw new InvalidArgumentException("Channel client is invalid can not be null.");
            }

            Name = name;
            this.client = client;
            logger.Debug($"Creating channel: {(IsSystemChannel() ? "SYSTEM_CHANNEL" : name)}, client context {client.UserContext.Name}");
        }


        /**
         * Get all Event Hubs on this channel.
         *
         * @return Event Hubs
         */
        public IReadOnlyList<EventHub> EventHubs => eventHubs.ToList();

        /**
         * Get the channel name
         *
         * @return The name of the channel
         */
        public string Name { get; }

        public TaskScheduler ExecutorService => client.ExecutorService;


        public void FillRoles()
        {
            foreach (PeerRole r in Enum.GetValues(typeof(PeerRole)))
            {
                peerRoleSetMap.TryAdd(r, new List<Peer>());
            }
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

        public static Channel Create(string name, HFClient hfClient, Orderer orderer, ChannelConfiguration channelConfiguration, params byte[][] signers)
        {
            return new Channel(name, hfClient, orderer, channelConfiguration, signers);
        }

        private static void CheckHandle(string tag, string handle)
        {
            if (string.IsNullOrEmpty(handle))
            {
                throw new InvalidArgumentException("Handle is invalid.");
            }

            if (!handle.StartsWith(tag) || !handle.EndsWith(tag))
            {
                throw new InvalidArgumentException("Handle is wrong type.");
            }
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
            CheckChannelState();

            CheckOrderer(orderer);

            try
            {
                long startLastConfigIndex = GetLastConfigIndex(orderer);
                logger.Trace("startLastConfigIndex: {startLastConfigIndex}. Channel config wait time is: {CHANNEL_CONFIG_WAIT_TIME}");
                SendUpdateChannel(updateChannelConfiguration.UpdateChannelConfigurationBytes, signers, orderer);

                long currentLastConfigIndex = -1;
                Stopwatch timer = new Stopwatch();
                timer.Start();

                //Try to wait to see the channel got updated but don't fail if we don't see it.
                do
                {
                    currentLastConfigIndex = GetLastConfigIndex(orderer);
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
                logger.ErrorException("Channel {name} error: {e.Message}", e);
                throw e;
            }
            catch (Exception e)
            {
                string msg = $"Channel {Name} error: {e.Message}";

                logger.ErrorException(msg, e);
                throw new TransactionException(msg, e);
            }
        }

        private void SendUpdateChannel(byte[] configupdate, byte[][] signers, Orderer orderer)
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
                    {
                        configUpdateEnv.Signatures.Add(ConfigSignature.Parser.ParseFrom(signer));
                    }

                    //--------------
                    // Construct Payload Envelope.

                    ByteString sigHeaderByteString = ProtoUtils.GetSignatureHeaderAsByteString(transactionContext);

                    ChannelHeader payloadChannelHeader = ProtoUtils.CreateChannelHeader(HeaderType.ConfigUpdate, transactionContext.TxID, Name, transactionContext.Epoch, transactionContext.FabricTimestamp, null, null);

                    Header payloadHeader = new Header {ChannelHeader = payloadChannelHeader.ToByteString(), SignatureHeader = sigHeaderByteString};


                    ByteString payloadByteString = new Payload {Header = payloadHeader, Data = configUpdateEnv.ToByteString()}.ToByteString();

                    ByteString payloadSignature = transactionContext.SignByteStrings(payloadByteString);

                    Envelope payloadEnv = new Envelope {Signature = payloadSignature, Payload = payloadByteString};

                    BroadcastResponse trxResult = orderer.SendTransaction(payloadEnv);

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
                logger.ErrorException("Channel {name} error: {e.Message}", e);
                throw e;
            }
            catch (Exception e)
            {
                string msg = $"Channel {Name} error: {e.Message}";

                logger.ErrorException(msg, e);
                throw new TransactionException(msg, e);
            }
        }

        public IEnrollment GetEnrollment()
        {
            return client.UserContext.Enrollment;
        }

        /**
         * Is channel initialized.
         *
         * @return true if the channel has been initialized.
         */

        public bool IsInitialized()
        {
            return initialized;
        }

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
            if (shutdown)
            {
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            }

            if (initialized)
            {
                throw new InvalidArgumentException($"Channel {Name} has been initialized.");
            }

            if (null == peer)
            {
                throw new InvalidArgumentException("Peer is invalid can not be null.");
            }

            if (peer.GetChannel() != null && peer.GetChannel() != this)
            {
                throw new InvalidArgumentException($"Peer already connected to channel {peer.GetChannel().Name}");
            }

            if (null == peerOptions)
            {
                throw new InvalidArgumentException("Peer is invalid can not be null.");
            }

            peer.SetChannel(this);

            peers.Add(peer);
            peerOptionsMap[peer] = peerOptions.DeepClone();
            foreach (PeerRole peerRole in peerRoleSetMap.Keys)
            {
                if (peerOptions.GetPeerRoles().Contains(peerRole))
                    peerRoleSetMap[peerRole].Add(peer);
            }

            return this;
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

        public IReadOnlyList<EventHub> GetEventHubs()
        {
            return eventHubs.ToList();
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
         * @param peer        the peer to join the channel.
         * @param peerOptions see {@link PeerOptions}
         * @return
         * @throws ProposalException
         */

        public Channel JoinPeer(Peer peer, PeerOptions peerOptions)
        {
            try
            {
                return JoinPeer(GetRandomOrderer(), peer, peerOptions);
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
            logger.Debug($"Channel {Name} joining peer {peer.Name}, url: {peer.Url}");

            if (shutdown)
            {
                throw new ProposalException($"Channel {Name} has been shutdown.");
            }

            Channel peerChannel = peer.GetChannel();
            if (null != peerChannel && peerChannel != this)
            {
                throw new ProposalException($"Can not add peer {peer.Name} to channel {Name} because it already belongs to channel {peerChannel.Name}.");
            }

            if (genesisBlock == null && orderers.Count == 0)
            {
                ProposalException e = new ProposalException("Channel missing genesis block and no orderers configured");
                logger.ErrorException(e.Message, e);
            }

            try
            {
                genesisBlock = GetGenesisBlock(orderer);
                logger.Debug("Channel {name} got genesis block");

                Channel systemChannel = CreateSystemChannel(client); //channel is not really created and this is targeted to system channel

                TransactionContext transactionContext = systemChannel.GetTransactionContext();
                Proposal joinProposal = JoinPeerProposalBuilder.Create().Context(transactionContext).GenesisBlock(genesisBlock).Build();

                logger.Debug("Getting signed proposal.");
                SignedProposal signedProposal = GetSignedProposal(transactionContext, joinProposal);
                logger.Debug("Got signed proposal.");

                AddPeer(peer, peerOptions); //need to add peer.

                List<ProposalResponse> resp = SendProposalToPeers(new[] {peer}, signedProposal, transactionContext);

                ProposalResponse pro = resp.First();

                if (pro.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                {
                    logger.Info($"Peer {peer.Name} joined into channel {Name}");
                }
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

        private Block GetConfigBlock(List<Peer> peers)
        {
            //   logger.debug(format("getConfigBlock for channel %s with peer %s, url: %s", name, peer.getName(), peer.getUrl()));

            if (shutdown)
            {
                throw new ProposalException($"Channel {Name} has been shutdown.");
            }

            if (peers.Count == 0)
            {
                throw new ProposalException("No peers go get config block");
            }

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
                    List<ProposalResponse> resp = SendProposalToPeers(new[] {peer}, signedProposal, transactionContext);

                    if (resp.Count > 0)
                    {
                        ProposalResponse pro = resp.First();

                        if (pro.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                        {
                            logger.Trace($"getConfigBlock from peer {peer.Name} on channel {Name} success");
                            return Block.Parser.ParseFrom(pro.ProtoProposalResponse.Response.Payload.ToByteArray());
                        }
                        else
                        {
                            lastException = new ProposalException($"GetConfigBlock for channel {Name} failed with peer {peer.Name}.  Status {pro.Status}, details: {pro.Message}");
                            logger.Warn(lastException.Message);
                        }
                    }
                    else
                    {
                        logger.Warn("Got empty proposals from {peer}");
                    }
                }
                catch (Exception e)
                {
                    lastException = new ProposalException("GetConfigBlock for channel {name} failed with peer {peer.Name}.", e);
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
            {
                throw new InvalidArgumentException($"Can not remove peer from channel {Name} already initialized.");
            }

            if (shutdown)
            {
                throw new InvalidArgumentException($"Can not remove peer from channel {Name} already shutdown.");
            }

            CheckPeer(peer);
            RemovePeerInternal(peer);
        }

        private void RemovePeerInternal(Peer peer)
        {
            peers.Remove(peer);
            peerOptionsMap.TryRemove(peer, out _);

            foreach (List<Peer> peerRoleSet in peerRoleSetMap.Values)
            {
                peerRoleSet.Remove(peer);
            }

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
            if (shutdown)
            {
                throw new InvalidArgumentException("Channel {name} has been shutdown.");
            }

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
            return ret?.DeepClone();
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
            if (shutdown)
            {
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            }

            if (null == eventHub)
            {
                throw new InvalidArgumentException("EventHub is invalid can not be null.");
            }

            logger.Debug($"Channel {Name} adding event hub {eventHub.Name}, url: {eventHub.GetUrl()}");
            eventHub.SetChannel(this);
            eventHub.SetEventQue(channelEventQue);
            eventHubs.AddLast(eventHub);
            return this;
        }

        /**
         * Get the peers for this channel.
         *
         * @return the peers.
         */
        public IReadOnlyList<Peer> GetPeers()
        {
            return peers;
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
            {
                throw new InvalidArgumentException("Channel {name} already initialized.");
            }

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
            logger.Debug($"Channel {Name} initialize shutdown {shutdown}");

            if (shutdown)
            {
                throw new InvalidArgumentException("Channel {name} has been shutdown.");
            }

            if (string.IsNullOrEmpty(Name))
            {
                throw new InvalidArgumentException("Can not initialize channel without a valid name.");
            }

            if (client == null)
            {
                throw new InvalidArgumentException("Can not initialize channel without a client object.");
            }

            client.UserContext.UserContextCheck();

            try
            {
                LoadCACertificates(); // put all MSP certs into cryptoSuite if this fails here we'll try again later.
            }
            catch (Exception e)
            {
                logger.Warn($"Channel {Name} could not load peer CA certificates from any peers.");
            }

            try
            {
                logger.Debug("Eventque started {eventQueueThread}");

                foreach (EventHub eh in eventHubs)
                {
                    //Connect all event hubs
                    eh.Connect(GetTransactionContext());
                }

                foreach (Peer peer in GetEventingPeers())
                {
                    peer.InitiateEventing(GetTransactionContext(), GetPeersOptions(peer));
                }

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

        /**
         * load the peer organizations CA certificates into the channel's trust store so that we
         * can verify signatures from peer messages
         *
         * @throws InvalidArgumentException
         * @throws CryptoException
         */
        [MethodImpl(MethodImplOptions.Synchronized)]
        protected void LoadCACertificates()
        {
            if (msps != null && msps.Count > 0)
            {
                return;
            }

            logger.Debug($"Channel {Name} loadCACertificates");

            ParseConfigBlock();

            if (msps == null || msps.Count == 0)
            {
                throw new InvalidArgumentException("Unable to load CA certificates. Channel " + Name + " does not have any MSPs.");
            }

            List<byte[]> certList;
            foreach (MSP msp in msps.Values)
            {
                logger.Debug($"loading certificates for MSP {msp.ID}: ");
                certList = msp.GetRootCerts().ToList();
                if (certList.Count > 0)
                {
                    client.CryptoSuite.LoadCACertificatesAsBytes(certList);
                }

                certList = msp.GetIntermediateCerts().ToList();
                if (certList.Count > 0)
                {
                    client.CryptoSuite.LoadCACertificatesAsBytes(certList);
                }

                // not adding admin certs. Admin certs should be signed by the CA
            }

            logger.Debug($"Channel {Name} loadCACertificates completed ");
        }

        private Block GetGenesisBlock(Orderer orderer)
        {
            try
            {
                if (genesisBlock != null)
                {
                    logger.Debug($"Channel {Name} getGenesisBlock already present");
                }
                else
                {
                    long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    SeekSpecified seekSpecified = new SeekSpecified {Number = 0};
                    SeekPosition seekPosition = new SeekPosition {Specified = seekSpecified};
                    SeekSpecified seekStopSpecified = new SeekSpecified {Number = 0};
                    SeekPosition seekStopPosition = new SeekPosition {Specified = seekStopSpecified};
                    SeekInfo seekInfo = new SeekInfo {Start = seekPosition, Stop = seekStopPosition, Behavior = SeekInfo.Types.SeekBehavior.BlockUntilReady};
                    List<DeliverResponse> deliverResponses = new List<DeliverResponse>();

                    SeekBlock(seekInfo, deliverResponses, orderer);

                    DeliverResponse blockresp = deliverResponses[1];
                    Block configBlock = blockresp.Block;
                    if (configBlock == null)
                    {
                        throw new TransactionException($"In getGenesisBlock newest block for channel {Name} fetch bad deliver returned null:");
                    }

                    int dataCount = configBlock.Data.Data.Count;
                    if (dataCount < 1)
                    {
                        throw new TransactionException($"In getGenesisBlock bad config block data count {dataCount}");
                    }

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

        private bool IsSystemChannel()
        {
            return systemChannel;
        }

        /**
         * Is the channel shutdown.
         *
         * @return return true if the channel is shutdown.
         */
        public bool IsShutdown()
        {
            return shutdown;
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
            {
                throw new InvalidArgumentException("channelConfiguration is null");
            }

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

        public ChannelEventQue GetChannelEventQue()
        {
            return channelEventQue;
        }

        protected void ParseConfigBlock()
        {
            IReadOnlyDictionary<string, MSP> lmsps = msps;

            if (lmsps != null && lmsps.Count > 0)
            {
                return;
            }

            try
            {
                Block parseFrom = GetConfigBlock(GetShuffledPeers());

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
            {
                TraverseConfigGroupsMSP(key, configGroup.Groups[key], msps);
            }

            return msps;
        }

        /**
         * Provide the Channel's latest raw Configuration Block.
         *
         * @return Channel configuration block.
         * @throws TransactionException
         */

        private Block GetConfigurationBlock()
        {
            logger.Debug($"getConfigurationBlock for channel {Name}");
            try
            {
                Orderer orderer = GetRandomOrderer();

                long lastConfigIndex = GetLastConfigIndex(orderer);

                logger.Debug($"Last config index is {lastConfigIndex}x");

                Block configBlock = GetBlockByNumber(lastConfigIndex);

                //Little extra parsing but make sure this really is a config block for this channel.
                Envelope envelopeRet = Envelope.Parser.ParseFrom(configBlock.Data.Data[0]);
                Payload payload = Payload.Parser.ParseFrom(envelopeRet.Payload);
                ChannelHeader channelHeader = ChannelHeader.Parser.ParseFrom(payload.Header.ChannelHeader);
                if (channelHeader.Type != (int) HeaderType.Config)
                {
                    throw new TransactionException($"Bad last configuration block type {channelHeader.Type}, expected {(int) HeaderType.Config}");
                }

                if (!Name.Equals(channelHeader.ChannelId))
                {
                    throw new TransactionException($"Bad last configuration block channel id {channelHeader.ChannelId}, expected {Name}");
                }

                if (null != diagnosticFileDumper)
                {
                    logger.Trace($"Channel {Name} getConfigurationBlock returned {diagnosticFileDumper.CreateDiagnosticFile(configBlock.ToString().ToBytes())}");
                }

                if (!logger.IsTraceEnabled())
                {
                    logger.Debug($"Channel {Name} getConfigurationBlock returned");
                }

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
            try
            {
                Block configBlock = GetConfigBlock(GetShuffledPeers());

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

        private long GetLastConfigIndex(Orderer orderer)
        {
            Block latestBlock = GetLatestBlock(orderer);

            BlockMetadata blockMetadata = latestBlock.Metadata;

            Metadata metaData = Metadata.Parser.ParseFrom(blockMetadata.Metadata[1]);

            LastConfig lastConfig = LastConfig.Parser.ParseFrom(metaData.Value);

            return (long) lastConfig.Index;
        }

        private Block GetBlockByNumber(long number)
        {
            logger.Trace($"getConfigurationBlock for channel {Name}");

            try
            {
                logger.Trace("Last config index is {number}");

                SeekSpecified seekSpecified = new SeekSpecified {Number = (ulong) number};

                SeekPosition seekPosition = new SeekPosition {Specified = seekSpecified};

                SeekInfo seekInfo = new SeekInfo {Start = seekPosition, Stop = seekPosition, Behavior = SeekInfo.Types.SeekBehavior.BlockUntilReady};

                List<DeliverResponse> deliverResponses = new List<DeliverResponse>();

                SeekBlock(seekInfo, deliverResponses, GetRandomOrderer());

                DeliverResponse blockresp = deliverResponses[1];

                Block retBlock = blockresp.Block;
                if (retBlock == null)
                {
                    throw new TransactionException($"newest block for channel {Name} fetch bad deliver returned null:");
                }

                int dataCount = retBlock.Data.Data.Count;
                if (dataCount < 1)
                {
                    throw new TransactionException($"Bad config block data count {dataCount}");
                }

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

        private int SeekBlock(SeekInfo seekInfo, List<DeliverResponse> deliverResponses, Orderer ordererIn)
        {
            logger.Trace("seekBlock for channel {name}");
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

                    DeliverResponse[] deliver = orderer.SendDeliver(ProtoUtils.CreateSeekInfoEnvelope(txContext, seekInfo, orderer.ClientTLSCertificateDigest));

                    if (deliver.Length < 1)
                    {
                        logger.Warn($"Genesis block for channel {Name} fetch bad deliver missing status block only got blocks:{deliver.Length}");
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
                            if (deliver.Length < 2)
                            {
                                throw new TransactionException($"Newest block for channel {Name} fetch bad deliver missing genesis block only got {deliver.Length}:");
                            }
                            else
                            {
                                deliverResponses.AddRange(deliver);
                            }
                        }
                    }

                    // Not 200 so sleep to try again

                    if (200 != statusRC)
                    {
                        long duration = watch.ElapsedMilliseconds;

                        if (duration > config.GetGenesisBlockWaitTime())
                        {
                            throw new TransactionException($"Getting block time exceeded {duration / 1000} seconds for channel {Name}");
                        }

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

        private Block GetLatestBlock(Orderer orderer)
        {
            logger.Debug($"getConfigurationBlock for channel {Name}");

            SeekPosition seekPosition = new SeekPosition {Newest = new SeekNewest()};

            SeekInfo seekInfo = new SeekInfo {Start = seekPosition, Stop = seekPosition, Behavior = SeekInfo.Types.SeekBehavior.BlockUntilReady};

            List<DeliverResponse> deliverResponses = new List<DeliverResponse>();

            SeekBlock(seekInfo, deliverResponses, orderer);

            DeliverResponse blockresp = deliverResponses[1];

            Block latestBlock = blockresp.Block;

            if (latestBlock == null)
            {
                throw new TransactionException($"newest block for channel {Name} fetch bad deliver returned null:");
            }

            logger.Trace($"Received latest  block for channel {Name}, block no:{latestBlock.Header.Number}");
            return latestBlock;
        }

        public IReadOnlyList<Orderer> GetOrderers() => orderers.ToList();

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
            CheckChannelState();
            if (null == instantiateProposalRequest)
            {
                throw new InvalidArgumentException("InstantiateProposalRequest is null");
            }

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
                instantiateProposalbuilder.ChaincodeEndorsementPolicy(instantiateProposalRequest.EndorsementPolicy);
                instantiateProposalbuilder.SetTransientMap(instantiateProposalRequest.TransientMap);

                Proposal instantiateProposal = instantiateProposalbuilder.Build();
                SignedProposal signedProposal = GetSignedProposal(transactionContext, instantiateProposal);

                return SendProposalToPeers(peers, signedProposal, transactionContext);
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
            CheckChannelState();
            CheckPeers(peers);
            if (null == installProposalRequest)
            {
                throw new InvalidArgumentException("InstallProposalRequest is null");
            }

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

                return SendProposalToPeers(peers, signedProposal, transactionContext);
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
            CheckChannelState();
            CheckPeers(peers);

            if (null == upgradeProposalRequest)
            {
                throw new InvalidArgumentException("Upgradeproposal is null");
            }

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
                upgradeProposalBuilder.ChaincodeEndorsementPolicy(upgradeProposalRequest.EndorsementPolicy);

                SignedProposal signedProposal = GetSignedProposal(transactionContext, upgradeProposalBuilder.Build());

                return SendProposalToPeers(peers, signedProposal, transactionContext);
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
            if (shutdown)
            {
                throw new InvalidArgumentException("Channel {name} has been shutdown.");
            }

            if (!initialized)
            {
                throw new InvalidArgumentException($"Channel {Name} has not been initialized.");
            }

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
            return QueryBlockByHash(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockHash);
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
            return QueryBlockByHash(new[] {peer}, blockHash);
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
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();

            if (blockHash == null)
            {
                throw new InvalidArgumentException("blockHash parameter is null.");
            }

            try
            {
                logger.Trace("queryBlockByHash with hash : " + blockHash.ToHexString() + " on channel " + Name);
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETBLOCKBYHASH);
                querySCCRequest.SetArgs(Name);
                querySCCRequest.SetArgBytes(new byte[][] {blockHash});

                ProposalResponse proposalResponse = SendProposalSerially(querySCCRequest, peers);

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
            {
                throw new InvalidArgumentException("Channel " + Name + " does not have any ledger querying peers associated with it.");
            }

            return ledgerQueryPeers[RANDOM.Next(ledgerQueryPeers.Count)];
        }

        private Peer GetRandomPeer()
        {
            List<Peer> randPicks = GetPeers().ToList(); //copy to avoid unlikely changes

            if (randPicks.Count == 0)
            {
                throw new InvalidArgumentException("Channel " + Name + " does not have any peers associated with it.");
            }

            return randPicks[RANDOM.Next(randPicks.Count)];
        }

        private List<Peer> GetShuffledPeers()
        {
            return GetPeers().ToList().Shuffle(RANDOM).ToList();
        }

        private List<Peer> GetShuffledPeers(IEnumerable<PeerRole> roles)
        {
            return GetPeers(roles).ToList().Shuffle(RANDOM).ToList();
        }

        private Orderer GetRandomOrderer()
        {
            List<Orderer> randPicks = GetOrderers().ToList();

            if (randPicks.Count == 0)
            {
                throw new InvalidArgumentException("Channel " + Name + " does not have any orderers associated with it.");
            }

            return randPicks[RANDOM.Next(randPicks.Count)];
        }

        private void CheckPeer(Peer peer)
        {
            if (peer == null)
            {
                throw new InvalidArgumentException("Peer value is null.");
            }

            if (IsSystemChannel())
            {
                return; // System owns no peers
            }

            if (!GetPeers().Contains(peer))
            {
                throw new InvalidArgumentException("Channel " + Name + " does not have peer " + peer.Name);
            }

            if (peer.GetChannel() != this)
            {
                throw new InvalidArgumentException("Peer " + peer.Name + " not set for channel " + Name);
            }
        }

        private void CheckOrderer(Orderer orderer)
        {
            if (orderer == null)
            {
                throw new InvalidArgumentException("Orderer value is null.");
            }

            if (IsSystemChannel())
            {
                return; // System owns no Orderers
            }

            if (!GetOrderers().Contains(orderer))
            {
                throw new InvalidArgumentException("Channel " + Name + " does not have orderer " + orderer.Name);
            }

            if (orderer.Channel != this)
            {
                throw new InvalidArgumentException("Orderer " + orderer.Name + " not set for channel " + Name);
            }
        }

        private void CheckPeers(IEnumerable<Peer> peers)
        {
            if (peers == null)
            {
                throw new InvalidArgumentException("Collection of peers is null.");
            }

            if (!peers.Any())
            {
                throw new InvalidArgumentException("Collection of peers is empty.");
            }

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
            return QueryBlockByNumber(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockNumber);
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
            return QueryBlockByNumber(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockNumber, userContext);
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
            return QueryBlockByNumber(new[] {peer}, blockNumber);
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
            return QueryBlockByNumber(new[] {peer}, blockNumber, userContext);
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
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();

            try
            {
                logger.Debug($"QueryBlockByNumber with blockNumber {blockNumber} on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETBLOCKBYNUMBER);
                querySCCRequest.SetArgs(Name, ((ulong) blockNumber).ToString(CultureInfo.InvariantCulture));

                ProposalResponse proposalResponse = SendProposalSerially(querySCCRequest, peers);

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
            return QueryBlockByTransactionID(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID);
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
            return QueryBlockByTransactionID(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, userContext);
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
            return QueryBlockByTransactionID(new[] {peer}, txID);
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
            return QueryBlockByTransactionID(new[] {peer}, txID, userContext);
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
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();

            if (txID == null)
            {
                throw new InvalidArgumentException("TxID parameter is null.");
            }

            try
            {
                logger.Debug($"QueryBlockByTransactionID with txID {txID}\n     on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETBLOCKBYTXID);
                querySCCRequest.SetArgs(Name, txID);

                ProposalResponse proposalResponse = SendProposalSerially(querySCCRequest, peers);

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
            return QueryBlockchainInfo(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), client.UserContext);
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
            return QueryBlockchainInfo(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), userContext);
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
            return QueryBlockchainInfo(new[] {peer}, client.UserContext);
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
            return QueryBlockchainInfo(new[] {peer}, userContext);
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
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();

            try
            {
                logger.Debug($"QueryBlockchainInfo to peer on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETCHAININFO);
                querySCCRequest.SetArgs(Name);

                ProposalResponse proposalResponse = SendProposalSerially(querySCCRequest, peers);

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
            return QueryTransactionByID(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, client.UserContext);
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
            return QueryTransactionByID(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, userContext);
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
            return QueryTransactionByID(new[] {peer}, txID, client.UserContext);
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
            return QueryTransactionByID(new[] {peer}, txID, userContext);
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
            CheckChannelState();
            CheckPeers(peers);
            userContext.UserContextCheck();

            if (txID == null)
            {
                throw new InvalidArgumentException("TxID parameter is null.");
            }

            TransactionInfo transactionInfo;
            try
            {
                logger.Debug($"QueryTransactionByID with txID {txID}\n    from peer on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETTRANSACTIONBYID);
                querySCCRequest.SetArgs(Name, txID);

                ProposalResponse proposalResponse = SendProposalSerially(querySCCRequest, peers);

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
            CheckPeer(peer);

            if (!IsSystemChannel())
            {
                throw new InvalidArgumentException("queryChannels should only be invoked on system channel.");
            }

            try
            {
                TransactionContext context = GetTransactionContext();

                Proposal q = QueryPeerChannelsBuilder.Create().Context(context).Build();

                SignedProposal qProposal = GetSignedProposal(context, q);
                List<ProposalResponse> proposalResponses = SendProposalToPeers(new[] {peer}, qProposal, context);

                if (null == proposalResponses)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query return with null for responses");
                }

                if (proposalResponses.Count != 1)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query expected one response but got back {proposalResponses.Count}  responses ");
                }

                ProposalResponse proposalResponse = proposalResponses.First();
                if (proposalResponse.Status != ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                {
                    throw new ProposalException($"Failed exception message is {proposalResponse.Message}, status is {proposalResponse.Status}");
                }

                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse = proposalResponse.ProtoProposalResponse;
                if (null == fabricResponse)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabric response");
                }

                Response fabricResponseResponse = fabricResponse.Response;

                if (null == fabricResponseResponse)
                {
                    //not likely but check it.
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabricResponseResponse");
                }

                if (200 != fabricResponseResponse.Status)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query expected 200, actual returned was: {fabricResponseResponse.Status}. {fabricResponseResponse.Message}");
                }

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
            CheckPeer(peer);

            if (!IsSystemChannel())
            {
                throw new InvalidArgumentException("queryInstalledChaincodes should only be invoked on system channel.");
            }

            try
            {
                TransactionContext context = GetTransactionContext();

                Proposal q = QueryInstalledChaincodesBuilder.Create().Context(context).Build();

                SignedProposal qProposal = GetSignedProposal(context, q);
                List<ProposalResponse> proposalResponses = SendProposalToPeers(new[] {peer}, qProposal, context);

                if (null == proposalResponses)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query return with null for responses");
                }

                if (proposalResponses.Count != 1)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query expected one response but got back {proposalResponses.Count}  responses ");
                }

                ProposalResponse proposalResponse = proposalResponses.First();
                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse = proposalResponse.ProtoProposalResponse;
                if (null == fabricResponse)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabric response");
                }

                Response fabricResponseResponse = fabricResponse.Response;

                if (null == fabricResponseResponse)
                {
                    //not likely but check it.
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabricResponseResponse");
                }

                if (200 != fabricResponseResponse.Status)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query expected 200, actual returned was: {fabricResponseResponse.Status}. {fabricResponseResponse.Message}");
                }

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
            CheckChannelState();
            CheckPeer(peer);
            userContext.UserContextCheck();

            try
            {
                TransactionContext context = GetTransactionContext(userContext);

                Proposal q = QueryInstantiatedChaincodesBuilder.Create().Context(context).Build();

                SignedProposal qProposal = GetSignedProposal(context, q);
                List<ProposalResponse> proposalResponses = SendProposalToPeers(new[] {peer}, qProposal, context);

                if (null == proposalResponses)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query return with null for responses");
                }

                if (proposalResponses.Count != 1)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query expected one response but got back {proposalResponses.Count}  responses ");
                }

                ProposalResponse proposalResponse = proposalResponses.First();

                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse = proposalResponse.ProtoProposalResponse;
                if (null == fabricResponse)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabric response");
                }

                Response fabricResponseResponse = fabricResponse.Response;

                if (null == fabricResponseResponse)
                {
                    //not likely but check it.
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabricResponseResponse");
                }

                if (200 != fabricResponseResponse.Status)
                {
                    throw new ProposalException($"Peer {peer.Name} channel query expected 200, actual returned was: {fabricResponseResponse.Status}. {fabricResponseResponse.Message}");
                }

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
            return SendProposal(transactionProposalRequest, GetEndorsingPeers());
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
            return SendProposal(transactionProposalRequest, peers);
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
            return SendProposal(queryByChaincodeRequest, peers);
        }
        ////////////////  Channel Block monitoring //////////////////////////////////

        private ProposalResponse SendProposalSerially(TransactionRequest proposalRequest, IEnumerable<Peer> peers)
        {
            ProposalException lastException = new ProposalException("ProposalRequest failed.");

            foreach (Peer peer in peers)
            {
                try
                {
                    List<ProposalResponse> proposalResponses = SendProposal(proposalRequest, new[] {peer});

                    if (proposalResponses.Count == 0)
                    {
                        logger.Warn($"Proposal request to peer {peer.Name} failed");
                    }

                    ProposalResponse proposalResponse = proposalResponses.First();
                    ChaincodeResponse.ChaincodeResponseStatus status = proposalResponse.Status;

                    if ((int) status < 400)
                    {
                        return proposalResponse;
                    }
                    else if ((int) status > 499)
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

        private List<ProposalResponse> SendProposal(TransactionRequest proposalRequest, IEnumerable<Peer> peers)
        {
            CheckChannelState();
            CheckPeers(peers);

            if (null == proposalRequest)
            {
                throw new InvalidArgumentException("The proposalRequest is null");
            }

            if (string.IsNullOrEmpty(proposalRequest.Fcn))
            {
                throw new InvalidArgumentException("The proposalRequest's fcn is null or empty.");
            }

            if (proposalRequest.ChaincodeID == null)
            {
                throw new InvalidArgumentException("The proposalRequest's chaincode ID is null");
            }

            proposalRequest.SetSubmitted();

            try
            {
                TransactionContext transactionContext = GetTransactionContext(proposalRequest.UserContext);
                transactionContext.Verify = proposalRequest.DoVerify;
                transactionContext.ProposalWaitTime = proposalRequest.ProposalWaitTime;

                // Protobuf message builder
                Proposal proposal = ProposalBuilder.Create().Context(transactionContext).Request(proposalRequest).Build();

                SignedProposal invokeProposal = GetSignedProposal(transactionContext, proposal);
                return SendProposalToPeers(peers, invokeProposal, transactionContext);
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

        private List<ProposalResponse> SendProposalToPeers(IEnumerable<Peer> peers, SignedProposal signedProposal, TransactionContext transactionContext)
        {
            CheckPeers(peers);

            if (transactionContext.Verify)
            {
                try
                {
                    LoadCACertificates();
                }
                catch (Exception e)
                {
                    throw new ProposalException(e);
                }
            }


            List<(Peer Peer, TaskCompletionSource<Protos.Peer.FabricProposalResponse.ProposalResponse> Future)> peerFuturePairs = new List<(Peer, TaskCompletionSource<Protos.Peer.FabricProposalResponse.ProposalResponse>)>();
            foreach (Peer peer in peers)
            {
                logger.Debug($"Channel {Name} send proposal to peer {peer.Name} at url {peer.Url}");

                if (null != diagnosticFileDumper)
                {
                    logger.Trace($"Sending to channel {Name}, peer: {peer.Name}, proposal: {diagnosticFileDumper.CreateDiagnosticProtobufFile(signedProposal.ToByteArray())}");
                }

                TaskCompletionSource<Protos.Peer.FabricProposalResponse.ProposalResponse> proposalResponseListenableFuture;
                try
                {
                    proposalResponseListenableFuture = peer.SendProposalAsync(signedProposal);
                }
                catch (Exception e)
                {
                    proposalResponseListenableFuture = new TaskCompletionSource<Protos.Peer.FabricProposalResponse.ProposalResponse>();
                    proposalResponseListenableFuture.SetException(e);
                }

                peerFuturePairs.Add((peer, proposalResponseListenableFuture));
            }

            List<ProposalResponse> proposalResponses = new List<ProposalResponse>();
            //Porting...Wait all tasks to finish, the alloted time.
            try
            {
                Task.WhenAll(peerFuturePairs.Select(a => a.Future.Task)).Wait((int) transactionContext.ProposalWaitTime);
            }
            catch (AggregateException)
            {
                //Lets continue, and trap errors in the next loop
            }


            foreach ((Peer peer, TaskCompletionSource<Protos.Peer.FabricProposalResponse.ProposalResponse> future) in peerFuturePairs)
            {
                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse = null;
                string message;
                int status = 500;
                string peerName = peer.Name;
                TaskStatus tstatus = future.Task.Status;
                if (tstatus == TaskStatus.RanToCompletion)
                {
                    fabricResponse = future.Task.Result;
                    message = fabricResponse.Response.Message;
                    status = fabricResponse.Response.Status;
                    logger.Debug($"Channel {Name} got back from peer {peerName} status: {status}, message: {message}");
                    if (null != diagnosticFileDumper)
                    {
                        logger.Trace($"Got back from channel {Name}, peer: {peerName}, proposal response: {diagnosticFileDumper.CreateDiagnosticProtobufFile(fabricResponse.ToByteArray())}");
                    }
                }
                else if (tstatus == TaskStatus.Faulted)
                {
                    AggregateException ex = future.Task.Exception;
                    Exception e = ex.InnerException ?? ex;
                    if (e is RpcException)
                    {
                        RpcException rpce = (RpcException) e;
                        message = $"Sending proposal to {peerName} failed because of: gRPC failure={rpce.Status}";
                    }
                    else
                    {
                        message = $"Sending proposal to {peerName}  failed because of: {e.Message}";
                    }

                    logger.ErrorException(message, e);
                }
                else if (tstatus == TaskStatus.Canceled)
                {
                    message = "Sending proposal to " + peerName + " failed because of interruption";
                    logger.Error(message);
                }
                else
                {
                    message = $"Sending proposal to {peerName} failed because of timeout({transactionContext.ProposalWaitTime} milliseconds) expiration";
                    logger.Error(message);
                }


                ProposalResponse proposalResponse = new ProposalResponse(transactionContext.TxID, transactionContext.ChannelID, status, message);
                proposalResponse.ProtoProposalResponse = fabricResponse;
                proposalResponse.SetProposal(signedProposal);
                proposalResponse.Peer = peer;

                if (fabricResponse != null && transactionContext.Verify)
                {
                    proposalResponse.Verify(client.CryptoSuite);
                }

                proposalResponses.Add(proposalResponse);
            }

            return proposalResponses;
        }

        /**
         * Send transaction to one of the orderers on the channel using a specific user context.
         *
         * @param proposalResponses The proposal responses to be sent to the orderer.
         * @param userContext       The usercontext used for signing transaction.
         * @return a future allowing access to the result of the transaction invocation once complete.
         */
        public TaskCompletionSource<BlockEvent.TransactionEvent> SendTransaction(IEnumerable<ProposalResponse> proposalResponses, IUser userContext)
        {
            return SendTransaction(proposalResponses, orderers, userContext);
        }

        /**
         * Send transaction to one of the orderers on the channel using the usercontext set on the client.
         *
         * @param proposalResponses .
         * @return a future allowing access to the result of the transaction invocation once complete.
         */
        public TaskCompletionSource<BlockEvent.TransactionEvent> SendTransaction(IEnumerable<ProposalResponse> proposalResponses)
        {
            return SendTransaction(proposalResponses, orderers);
        }

        /**
         * Send transaction to one of the specified orderers using the usercontext set on the client..
         *
         * @param proposalResponses The proposal responses to be sent to the orderer
         * @param orderers          The orderers to send the transaction to.
         * @return a future allowing access to the result of the transaction invocation once complete.
         */

        public TaskCompletionSource<BlockEvent.TransactionEvent> SendTransaction(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> orderers)
        {
            return SendTransaction(proposalResponses, orderers, client.UserContext);
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

        public TaskCompletionSource<BlockEvent.TransactionEvent> SendTransaction(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> orderers, IUser userContext)
        {
            return SendTransaction(proposalResponses, TransactionOptions.Create().WithOrderers(orderers).WithUserContext(userContext));
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

        public TaskCompletionSource<BlockEvent.TransactionEvent> SendTransaction(IEnumerable<ProposalResponse> proposalResponses, TransactionOptions transactionOptions)
        {
            try
            {
                if (null == transactionOptions)
                {
                    throw new InvalidArgumentException("Parameter transactionOptions can't be null");
                }

                CheckChannelState();
                IUser userContext = transactionOptions.UserContext ?? client.UserContext;
                userContext.UserContextCheck();
                if (null == proposalResponses)
                {
                    throw new InvalidArgumentException("sendTransaction proposalResponses was null");
                }

                List<Orderer> orderers = transactionOptions.Orderers ?? GetOrderers().ToList();

                // make certain we have our own copy
                List<Orderer> shuffeledOrderers = orderers.Shuffle(RANDOM).ToList();


                if (config.GetProposalConsistencyValidation())
                {
                    HashSet<ProposalResponse> invalid = new HashSet<ProposalResponse>();
                    int consistencyGroups = SDKUtils.GetProposalConsistencySets(proposalResponses.ToList(), invalid).Count;

                    if (consistencyGroups != 1 || invalid.Count > 0)
                    {
                        throw new IllegalArgumentException($"The proposal responses have {consistencyGroups} inconsistent groups with {invalid.Count} that are invalid." + " Expected all to be consistent and none to be invalid.");
                    }
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

                    IReadOnlyList<EventHub> eventHubs = GetEventHubs();
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
                        if (peer.GetChannel() != this)
                        {
                            issues.Append($"Peer {peer.Name} added to NOFEvents does not belong this channel. ");
                        }
                        else if (!eventingPeers.Contains(peer))
                        {
                            issues.Append("Peer {peer.Name} added to NOFEvents is not a eventing Peer in this channel. ");
                        }
                    });
                    nOfEvents.UnSeenEventHubs().ForEach(eventHub =>
                    {
                        if (!eventHubs.Contains(eventHub))
                        {
                            issues.Append($"Eventhub {eventHub.Name} added to NOFEvents does not belong this channel. ");
                        }
                    });

                    if (nOfEvents.UnSeenEventHubs().Count == 0 && nOfEvents.UnSeenPeers().Count == 0)
                    {
                        issues.Append("NofEvents had no Eventhubs added or Peer eventing services.");
                    }

                    string foundIssues = issues.ToString();
                    if (!string.IsNullOrEmpty(foundIssues))
                    {
                        throw new InvalidArgumentException(foundIssues);
                    }
                }

                bool replyonly = nOfEvents == NOfEvents.NofNoEvents || GetEventHubs().Count == 0 && GetEventingPeers().Count == 0;

                TaskCompletionSource<BlockEvent.TransactionEvent> sret;
                if (replyonly)
                {
                    //If there are no eventhubs to complete the future, complete it
                    // immediately but give no transaction event
                    logger.Debug($"Completing transaction id {proposalTransactionID} immediately no event hubs or peer eventing services found in channel {Name}.");
                    sret = new TaskCompletionSource<BlockEvent.TransactionEvent>();
                }
                else
                {
                    sret = RegisterTxListener(proposalTransactionID, nOfEvents, transactionOptions.FailFast);
                }

                logger.Debug($"Channel {Name} sending transaction to orderer(s) with TxID {proposalTransactionID} ");
                bool success = false;
                Exception lException = null; // Save last exception to report to user .. others are just logged.

                BroadcastResponse resp = null;
                Orderer failed = null;
                foreach (Orderer orderer in shuffeledOrderers)
                {
                    if (failed != null)
                    {
                        logger.Warn("Channel {name}  {failed} failed. Now trying {orderer}.");
                    }

                    failed = orderer;
                    try
                    {
                        if (null != diagnosticFileDumper)
                        {
                            logger.Trace($"Sending to channel {Name}, orderer: {orderer.Name}, transaction: {diagnosticFileDumper.CreateDiagnosticProtobufFile(transactionEnvelope.ToByteArray())}");
                        }

                        resp = orderer.SendTransaction(transactionEnvelope);
                        lException = null; // no longer last exception .. maybe just failed.
                        if (resp.Status == Status.Success)
                        {
                            success = true;
                            break;
                        }
                        else
                        {
                            logger.Warn($"Channel {Name} {orderer.Name} failed. Status returned {GetRespData(resp)}");
                        }
                    }
                    catch (Exception e)
                    {
                        string emsg = $"Channel {Name} unsuccessful sendTransaction to orderer {orderer.Name} ({orderer.Url})";
                        if (resp != null)
                        {
                            emsg = $"Channel {Name} unsuccessful sendTransaction to orderer {orderer.Name} ({orderer.Url}).  {GetRespData(resp)}";
                        }

                        logger.Error(emsg);
                        lException = new Exception(emsg, e);
                    }
                }

                if (success)
                {
                    logger.Debug($"Channel {Name} successful sent to Orderer transaction id: {proposalTransactionID}");
                    if (replyonly)
                    {
                        sret.SetResult(null); // just say we're done.
                    }

                    return sret;
                }
                else
                {
                    string emsg = $"Channel {Name} failed to place transaction {proposalTransactionID} on Orderer. Cause: UNSUCCESSFUL. {GetRespData(resp)}";

                    UnregisterTxListener(proposalTransactionID);

                    TaskCompletionSource<BlockEvent.TransactionEvent> ret = new TaskCompletionSource<BlockEvent.TransactionEvent>();
                    ret.SetException(lException != null ? new Exception(emsg, lException) : new Exception(emsg));
                    return ret;
                }
            }
            catch (Exception e)
            {
                TaskCompletionSource<BlockEvent.TransactionEvent> future = new TaskCompletionSource<BlockEvent.TransactionEvent>();
                future.SetException(e);
                return future;
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
                    {
                        respdata.Append(", ");
                    }

                    respdata.Append("Additional information: ").Append(info);
                }
            }

            return respdata.ToString();
        }

        private Envelope CreateTransactionEnvelope(Payload transactionPayload, IUser user)
        {
            return new Envelope {Payload = transactionPayload.ToByteString(), Signature = ByteString.CopyFrom(client.CryptoSuite.Sign(user.Enrollment.Key, transactionPayload.ToByteArray()))};
        }

        public byte[] GetChannelConfigurationSignature(ChannelConfiguration channelConfiguration, IUser signer)
        {
            signer.UserContextCheck();

            if (null == channelConfiguration)
            {
                throw new InvalidArgumentException("channelConfiguration is null");
            }

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
        public string RegisterBlockListener(IBlockListener listener)
        {
            if (shutdown)
            {
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            }

            return new BL(this, listener).GetHandle();
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
            if (shutdown)
            {
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            }

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
            {
                return;
            }

            eventQueueTokenSource = new CancellationTokenSource();
            CancellationToken ct = eventQueueTokenSource.Token;
            TaskScheduler scheduler = client.ExecutorService;
            Task.Factory.StartNew(() =>
            {
                while (!shutdown)
                {
                    try
                    {
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }

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
                        blockEvent = channelEventQue.GetNextEvent();
                    }
                    catch (EventHubException e)
                    {
                        if (!shutdown)
                        {
                            logger.ErrorException(e.Message, e);
                        }

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
                                Task.Factory.StartNew((listener) =>
                                {
                                    BL lis = (BL) listener;
                                    lis.Listener.Received(blockEvent);
                                }, l, default(CancellationToken), TaskCreationOptions.None, scheduler);
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
            }, ct, TaskCreationOptions.None, scheduler);
        }

        private string RegisterTransactionListenerProcessor()
        {
            logger.Debug("Channel {name} registerTransactionListenerProcessor starting");

            // Transaction listener is internal Block listener for transactions

            return RegisterBlockListener(new TransactionListener(this));
        }

        private void RunSweeper()
        {
            if (shutdown || DELTA_SWEEP < 1)
            {
                return;
            }

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

        public string RegisterChaincodeEventListener(Regex chaincodeId, Regex eventName, IChaincodeEventListener chaincodeEventListener)
        {
            if (shutdown)
            {
                throw new InvalidArgumentException($"Channel {Name} has been shutdown.");
            }

            if (chaincodeId == null)
            {
                throw new InvalidArgumentException("The chaincodeId argument may not be null.");
            }

            if (eventName == null)
            {
                throw new InvalidArgumentException("The eventName argument may not be null.");
            }

            if (chaincodeEventListener == null)
            {
                throw new InvalidArgumentException("The chaincodeEventListener argument may not be null.");
            }

            ChaincodeEventListenerEntry chaincodeEventListenerEntry = new ChaincodeEventListenerEntry(this, chaincodeId, eventName, chaincodeEventListener);
            lock (this)
            {
                if (null == blh)
                {
                    blh = RegisterChaincodeListenerProcessor();
                }
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

            if (shutdown)
            {
                throw new InvalidArgumentException("Channel {name} has been shutdown.");
            }

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

            return RegisterBlockListener(new ChaincodeListener(this));
        }

        /**
         * Shutdown the channel with all resources released.
         *
         * @param force force immediate shutdown.
         */

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (shutdown)
            {
                return;
            }

            initialized = false;
            shutdown = true;
            if (chainCodeListeners != null)
            {
                chainCodeListeners.Clear();
            }

            if (blockListeners != null)
            {
                blockListeners.Clear();
            }

            if (client != null)
            {
                client.RemoveChannel(this);
            }

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
            foreach (Peer peer in GetPeers().ToList())
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
            {
                peerRoleSet.Clear();
            }

            foreach (Orderer orderer in GetOrderers())
            {
                orderer.Shutdown(force);
            }

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
        public void serializeChannel(File file) throws IOException, InvalidArgumentException {

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
        public byte[] serializeChannel() throws IOException, InvalidArgumentException {

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
        public class TransactionListener : IBlockListener
        {
            private readonly Channel channel;

            public TransactionListener(Channel ch)
            {
                channel = ch;
            }

            public void Received(BlockEvent blockEvent)
            {
                if (channel.txListeners.Count == 0)
                {
                    return;
                }

                foreach (BlockEvent.TransactionEvent transactionEvent in blockEvent.GetTransactionEventsList)
                {
                    logger.Debug($"Channel {channel.Name} got event for transaction {transactionEvent.TransactionID}");

                    List<TL> txL = new List<TL>();
                    lock (channel.txListeners)
                    {
                        LinkedList<TL> list = channel.txListeners[transactionEvent.TransactionID];
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
        }

        public class ChaincodeListener : IBlockListener
        {
            private readonly Channel channel;

            public ChaincodeListener(Channel ch)
            {
                channel = ch;
            }

            public void Received(BlockEvent blockEvent)
            {
                if (channel.chainCodeListeners.Count == 0)
                {
                    return;
                }

                List<ChaincodeEventDeserializer> chaincodeEvents = new List<ChaincodeEventDeserializer>();

                //Find the chaincode events in the transactions.

                foreach (BlockEvent.TransactionEvent transactionEvent in blockEvent.GetTransactionEventsList)
                {
                    logger.Debug($"Channel {channel.Name} got event for transaction {transactionEvent.TransactionID}");

                    foreach (BlockInfo.TransactionEnvelopeInfo.TransactionActionInfo info in transactionEvent.TransactionActionInfos)
                    {
                        ChaincodeEventDeserializer evnt = info.Event;
                        if (null != evnt)
                        {
                            chaincodeEvents.Add(evnt);
                        }
                    }
                }

                if (chaincodeEvents.Count > 0)
                {
                    List<(ChaincodeEventListenerEntry EventListener, ChaincodeEventDeserializer Event)> matches = new List<(ChaincodeEventListenerEntry EventListener, ChaincodeEventDeserializer Event)>(); //Find matches.

                    lock (channel.chainCodeListeners)
                    {
                        foreach (ChaincodeEventListenerEntry chaincodeEventListenerEntry in channel.chainCodeListeners.Values)
                        {
                            foreach (ChaincodeEventDeserializer chaincodeEvent in chaincodeEvents)
                            {
                                if (chaincodeEventListenerEntry.IsMatch(chaincodeEvent))
                                {
                                    matches.Add((chaincodeEventListenerEntry, chaincodeEvent));
                                }
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
        }


        /**
     * Options for the peer.
     * These options are channel based.
     */

        [Serializable]
        public class PeerOptions : ICloneable
        {
            protected List<PeerRole> peerRoles;


            protected bool registerEventsForFilteredBlocks = false;


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


            public object Clone()
            {
                return this.DeepClone();
            }

            /**
             * Is the peer eventing service registered for filtered blocks
             *
             * @return true if filtered blocks will be returned by the peer eventing service.
             */
            public bool IsRegisterEventsForFilteredBlocks()
            {
                return registerEventsForFilteredBlocks;
            }

            /**
             * Register the peer eventing services to return filtered blocks.
             *
             * @return the PeerOptions instance.
             */

            public PeerOptions RegisterEventsForFilteredBlocks()
            {
                registerEventsForFilteredBlocks = true;
                return this;
            }

            /**
             * Register the peer eventing services to return full event blocks.
             *
             * @return the PeerOptions instance.
             */

            public PeerOptions RegisterEventsForBlocks()
            {
                registerEventsForFilteredBlocks = false;
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

            /**
             * Return the roles the peer has.
             *
             * @return the roles {@link PeerRole}
             */

            public List<PeerRole> GetPeerRoles()
            {
                if (peerRoles == null)
                {
                    return PeerRoleExtensions.All();
                }

                return peerRoles;
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

            /**
             * Add to the roles this peer will have on the chain it will added or joined.
             *
             * @param peerRole see {@link PeerRole}
             * @return This PeerOptions.
             */

            public PeerOptions AddPeerRole(PeerRole peerRole)
            {
                if (peerRoles == null)
                {
                    peerRoles = new List<PeerRole>();
                }

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
            private long n = long.MaxValue; //all
            private readonly HashSet<NOfEvents> nOfEvents = new HashSet<NOfEvents>();
            private readonly HashSet<Peer> peers = new HashSet<Peer>();

            private bool started = false;

            public NOfEvents(NOfEvents nof)
            {
                // Deep Copy.
                if (nof == NofNoEvents)
                {
                    throw new IllegalArgumentException("nofNoEvents may not be copied.");
                }

                Ready = false; // no use in one set to ready.
                started = false;
                n = nof.n;
                peers = new HashSet<Peer>(nof.peers);
                eventHubs = new HashSet<EventHub>(nof.eventHubs);
                foreach (NOfEvents nofc in nof.nOfEvents)
                {
                    nOfEvents.Add(new NOfEvents(nofc));
                }
            }

            private NOfEvents()
            {
            }

            public bool Ready { get; private set; } = false;

            public static NOfEvents NofNoEvents { get; } = new NoEvents();

            public virtual NOfEvents SetN(int n)
            {
                if (n < 1)
                {
                    throw new IllegalArgumentException("N was {n} but needs to be greater than 0.");
                }

                this.n = n;
                return this;
            }

            public virtual NOfEvents AddPeers(params Peer[] peers)
            {
                if (peers == null || peers.Length == 0)
                {
                    throw new IllegalArgumentException("Peers added must be not null or empty.");
                }

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
                {
                    throw new IllegalArgumentException("EventHubs added must be not null or empty.");
                }

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
                {
                    throw new IllegalArgumentException("nofEvents added must be not null or empty.");
                }

                foreach (NOfEvents n in nOfEvents)
                {
                    if (NofNoEvents == n)
                    {
                        throw new IllegalArgumentException("nofNoEvents may not be added as an event.");
                    }

                    if (InHayStack(n))
                    {
                        throw new IllegalArgumentException("nofEvents already was added..");
                    }

                    this.nOfEvents.Add(new NOfEvents(n));
                }

                return this;
            }

            private bool InHayStack(NOfEvents needle)
            {
                if (this == needle)
                {
                    return true;
                }

                foreach (NOfEvents straw in nOfEvents)
                {
                    if (straw.InHayStack(needle))
                    {
                        return true;
                    }
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
                {
                    unseen.AddRange(NofNoEvents.UnSeenPeers());
                }

                return unseen.ToList();
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            public List<EventHub> UnSeenEventHubs()
            {
                HashSet<EventHub> unseen = new HashSet<EventHub>();
                unseen.AddRange(eventHubs);
                foreach (NOfEvents nOfEvents in nOfEvents)
                {
                    unseen.AddRange(NofNoEvents.UnSeenEventHubs());
                }

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
                        {
                            Ready = true;
                        }
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
                return NofNoEvents;
            }


            public static NOfEvents CreateNoEvents()
            {
                return new NoEvents();
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
            public TransactionOptions WithFailFast(bool failFast)
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
            public TransactionOptions WithUserContext(IUser userContext)
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
            public TransactionOptions WithOrderers(params Orderer[] orderers)
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
            public TransactionOptions WithShuffleOrders(bool shuffleOrders)
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
            public TransactionOptions WithNOfEvents(NOfEvents nOfEvents)
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
            public TransactionOptions WithOrderers(IEnumerable<Orderer> orderers)
            {
                return WithOrderers(orderers.ToArray());
            }
        }


        /**
          * MSPs
          */

        public class MSP
        {
            private byte[][] adminCerts;
            private readonly FabricMSPConfig fabricMSPConfig;
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
            public byte[][] GetAdminCerts()
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

            /**
             * RootCerts
             *
             * @return array of admin certs in PEM bytes format.
             */
            public byte[][] GetRootCerts()
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

            /**
             * IntermediateCerts
             *
             * @return array of intermediate certs in PEM bytes format.
             */
            public byte[][] GetIntermediateCerts()
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

        public class ChannelEventQue
        {
            private static readonly ILog logger = LogProvider.GetLogger(typeof(ChannelEventQue));
            private readonly Channel channel;
            private Exception eventException;

            private readonly BlockingCollection<BlockEvent> events = new BlockingCollection<BlockEvent>(); //Thread safe

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
                if (channel.shutdown)
                {
                    return false;
                }

                //For now just support blocks --- other types are also reported as blocks.

                if (!evnt.IsBlockEvent)
                {
                    return false;
                }

                // May be fed by multiple eventhubs but BlockingQueue.add() is thread-safe
                events.Add(evnt);

                return true;
            }

            public BlockEvent GetNextEvent()
            {
                if (channel.shutdown)
                {
                    throw new EventHubException($"Channel {channel.Name} has been shutdown");
                }

                BlockEvent ret = null;
                if (eventException != null)
                {
                    throw new EventHubException(eventException);
                }

                try
                {
                    ret = events.Take();
                }
                catch (Exception e)
                {
                    if (channel.shutdown)
                    {
                        throw new EventHubException(eventException);
                    }
                    else
                    {
                        logger.WarnException(e.Message, e);
                        if (eventException != null)
                        {
                            EventHubException eve = new EventHubException(eventException);
                            logger.ErrorException(eve.Message, eve);
                            throw eve;
                        }
                    }
                }

                if (eventException != null)
                {
                    throw new EventHubException(eventException);
                }

                if (channel.shutdown)
                {
                    throw new EventHubException($"Channel {channel.Name} has been shutdown.");
                }

                return ret;
            }
        }

        public class BL
        {
            public static readonly string BLOCK_LISTENER_TAG = "BLOCK_LISTENER_HANDLE";
            private static readonly ILog logger = LogProvider.GetLogger(typeof(BL));

            private readonly Channel channel;

            public BL(Channel ch, IBlockListener listener)
            {
                channel = ch;
                Handle = BLOCK_LISTENER_TAG + Utils.GenerateUUID() + BLOCK_LISTENER_TAG;
                logger.Debug($"Channel {channel.Name} blockListener {Handle} starting");

                Listener = listener;
                lock (channel.blockListeners)
                {
                    channel.blockListeners.Add(Handle, this);
                }
            }

            public IBlockListener Listener { get; }
            public string Handle { get; }

            public string GetHandle()
            {
                return Handle;
            }
        }

        private class TL
        {
            private static readonly ILog logger = LogProvider.GetLogger(typeof(TL));
            private readonly Channel channel;
            private readonly long createTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            private readonly HashSet<EventHub> eventHubs;
            private readonly bool failFast;
            private bool fired = false;
            private readonly TaskCompletionSource<BlockEvent.TransactionEvent> future;
            private readonly NOfEvents nOfEvents;
            private readonly HashSet<Peer> peers;
            private long sweepTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long) (DELTA_SWEEP * 1.5);
            private readonly string txID;

            public TL(Channel ch, string txID, TaskCompletionSource<BlockEvent.TransactionEvent> future, NOfEvents nOfEvents, bool failFast)
            {
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
                {
                    return false;
                }

                if (eventHub != null && !eventHubs.Contains(eventHub))
                {
                    return false;
                }

                if (failFast && !transactionEvent.IsValid)
                {
                    return true;
                }

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
                {
                    logger.Error($"Channel {channel.Name} seen transaction event {txID} with no associated peer or eventhub");
                }

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
                    {
                        sb.Append(". ");
                    }

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
            private readonly IChaincodeEventListener chaincodeEventListener;

            private readonly Regex chaincodeIdPattern;
            private readonly Channel channel;
            private readonly Regex eventNamePattern;

            public ChaincodeEventListenerEntry(Channel ch, Regex chaincodeIdPattern, Regex eventNamePattern, IChaincodeEventListener chaincodeEventListener)
            {
                channel = ch;
                this.chaincodeIdPattern = chaincodeIdPattern;
                this.eventNamePattern = eventNamePattern;
                this.chaincodeEventListener = chaincodeEventListener;
                Handle = CHAINCODE_EVENTS_TAG + Utils.GenerateUUID() + CHAINCODE_EVENTS_TAG;
                lock (channel.chainCodeListeners)
                {
                    channel.chainCodeListeners.Add(Handle, this);
                }
            }

            public string Handle { get; }


            public bool IsMatch(ChaincodeEventDeserializer chaincodeEvent)
            {
                return chaincodeIdPattern.Match(chaincodeEvent.ChaincodeId).Success && eventNamePattern.Match(chaincodeEvent.Name).Success;
            }

            public void Fire(BlockEvent blockEvent, ChaincodeEventDeserializer ce)
            {
                TaskScheduler sch = channel.client.ExecutorService;
                Task.Factory.StartNew(() => chaincodeEventListener.Received(Handle, blockEvent, ce), default(CancellationToken), TaskCreationOptions.None, sch);
            }
        }
    }
}