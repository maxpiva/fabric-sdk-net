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
using Google.Protobuf;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Msp.MspConfig;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.Protos.Peer.FabricTransaction;
using Hyperledger.Fabric.SDK.Blocks;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Configuration;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Discovery;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Responses;
using Hyperledger.Fabric.SDK.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nito.AsyncEx;
using Config = Hyperledger.Fabric.SDK.Helper.Config;
using Metadata = Hyperledger.Fabric.Protos.Common.Metadata;
using ProposalResponse = Hyperledger.Fabric.SDK.Responses.ProposalResponse;
using Status = Hyperledger.Fabric.Protos.Common.Status;

// ReSharper disable UnusedVariable

// ReSharper disable UnusedMember.Local
// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable UnusedMethodReturnValue.Local
// ReSharper disable LocalVariableHidesMember

namespace Hyperledger.Fabric.SDK.Channels
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
        private static readonly bool IS_WARN_LEVEL = logger.IsWarnEnabled();
        private static readonly bool IS_DEBUG_LEVEL = logger.IsDebugEnabled();

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
        /*
        private static JsonSerializerSettings _defaultSerialization = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
            PreserveReferencesHandling = PreserveReferencesHandling.All,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            ContractResolver = new PeerPeerOptionsResolver()
        };*/

        internal readonly LinkedHashMap<string, BlockListener> blockListeners = new LinkedHashMap<string, BlockListener>();

        // final Set<Peer> eventingPeers = Collections.synchronizedSet(new HashSet<>());

        internal readonly LinkedHashMap<string, ChaincodeEventListenerEntry> chainCodeListeners = new LinkedHashMap<string, ChaincodeEventListenerEntry>();

        private readonly long CHANNEL_CONFIG_WAIT_TIME = Config.Instance.GetChannelConfigWaitTime();
        /**
         * A queue each eventing hub will write events to.
         */

        private readonly long DELTA_SWEEP = Config.Instance.GetTransactionListenerCleanUpTimeout();


        private readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL ? Config.Instance.GetDiagnosticFileDumper() : null;
        private readonly ConcurrentHashSet<string> discoveryEndpoints = new ConcurrentHashSet<string>();

        private readonly long ORDERER_RETRY_WAIT_TIME = Config.Instance.GetOrdererRetryWaitTime();
        private readonly ConcurrentDictionary<string, Orderer> ordererEndpointMap = new ConcurrentDictionary<string, Orderer>();
        private readonly ConcurrentDictionary<string, Peer> peerEndpointMap = new ConcurrentDictionary<string, Peer>();
        internal readonly LinkedHashMap<string, LinkedList<TransactionListener>> txListeners = new LinkedHashMap<string, LinkedList<TransactionListener>>();
        private string blh;
        private string chaincodeEventUpgradeListenerHandle;

        internal HFClient client;
        private Func<SDChaindcode, SDEndorserState> endorsementSelector = ServiceDiscovery.DEFAULT_ENDORSEMENT_SELECTION;

        private ConcurrentHashSet<EventHub> eventHubs = new ConcurrentHashSet<EventHub>();

        //////////  Transaction monitoring  /////////////////////////////
        private Task eventQueueThread;
        /**
         * Runs processing events from event hubs.
         */

        private CancellationTokenSource eventQueueTokenSource;
        private Block genesisBlock;
        internal volatile bool initialized;
        private long lastBlock = -1L;
        private long lastChaincodeUpgradeEventBlock;
        private IReadOnlyDictionary<string, MSP> msps = new Dictionary<string, MSP>();

        internal ConcurrentHashSet<Orderer> orderers = new ConcurrentHashSet<Orderer>();

        // Name of the channel is only meaningful to the client
        private ConcurrentDictionary<Peer, PeerOptions> peerOptionsMap = new ConcurrentDictionary<Peer, PeerOptions>();

        private ConcurrentDictionary<PeerRole, List<Peer>> peerRoleSetMap = new ConcurrentDictionary<PeerRole, List<Peer>>();

        // The peers on this channel to which the client can connect
        internal ConcurrentHashSet<Peer> peers = new ConcurrentHashSet<Peer>();
        private ServiceDiscovery serviceDiscovery;

        //Cleans up any transaction listeners that will probably never complete.
        private Timer sweeper;
        private volatile string toString;
        private string transactionListenerProcessorHandle;

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
                    throw new ArgumentException("Channel name is invalid can not be null or empty.");
            }

            Name = name;
            this.client = client ?? throw new ArgumentException("Channel client is invalid can not be null.");
            toString = $"Channel{{id: {Config.Instance.GetNextID()}, name: {name} }}";
            logger.Debug($"Creating channel: {(IsSystemChannel ? "SYSTEM_CHANNEL" : name)}, client context {client.UserContext.Name}");
        }

        public Channel()
        {
            initialized = false;
            IsShutdown = false;
            FillRoles();
            msps = new Dictionary<string, MSP>();
            txListeners = new LinkedHashMap<string, LinkedList<TransactionListener>>();
            ChannelEventQueue = new ChannelEventQue(this);
            blockListeners = new LinkedHashMap<string, BlockListener>();
        }

        public HFClient Client => client;

        /**
         * Get all Event Hubs on this channel.
         *
         * @return Event Hubs
         */

        public IReadOnlyList<EventHub> EventHubs
        {
            get { return eventHubs.ToList(); }
            private set { eventHubs = new ConcurrentHashSet<EventHub>(value); }
        }


        public IReadOnlyList<Orderer> Orderers
        {
            get { return orderers.ToList(); }
            private set { orderers = new ConcurrentHashSet<Orderer>(value); }
        }

        public IReadOnlyDictionary<string, Orderer> OrdererEndpointMap => ordererEndpointMap.ToDictionary(a => a.Key, a => a.Value);

        public IReadOnlyDictionary<string, Peer> PeerEndpointMap => peerEndpointMap.ToDictionary(a => a.Key, a => a.Value);
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
            get { return peers.ToList(); }
            private set { peers = new ConcurrentHashSet<Peer>(value); }
        }


        /**
         * Is the channel shutdown.
         *
         * @return return true if the channel is shutdown.
         */
        [JsonIgnore]
        public bool IsShutdown { get; internal set; }

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

        [JsonIgnore]
        public IEnrollment Enrollment => client.UserContext.Enrollment;

        public Properties ServiceDiscoveryProperties { get; set; }

        public override string ToString()
        {
            return toString;
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
                    throw new ArgumentException($"Creating channel; {Name} expected config block type {HeaderType.ConfigUpdate}, but got: {ccChannelHeader.Type}");
                if (!Name.Equals(ccChannelHeader.ChannelId))
                    throw new ArgumentException($"Expected config block for channel: {Name}, but got: {ccChannelHeader.ChannelId}");
                ConfigUpdateEnvelope configUpdateEnv = ConfigUpdateEnvelope.Parser.ParseFrom(ccPayload.Data);
                ByteString configUpdate = configUpdateEnv.ConfigUpdate;
                await SendUpdateChannelAsync(configUpdate.ToByteArray(), signers, orderer, token).ConfigureAwait(false);
                //         final ConfigUpdateEnvelope.Builder configUpdateEnvBuilder = configUpdateEnv.toBuilder();`
                //---------------------------------------
                //          sendUpdateChannel(channelConfiguration, signers, orderer);
                await GetGenesisBlockAsync(orderer, token).ConfigureAwait(false); // get Genesis block to make sure channel was created.
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
                throw;
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
            JObject j = JObject.Parse(json);
            Channel ch = new Channel();
            ch.lastChaincodeUpgradeEventBlock = 0;
            ch.Name = j["Name"].Value<string>();
            ch.IsSystemChannel = j["SystemChannel"].Value<bool>();
            JArray ordarray = j["Orderers"] as JArray ?? new JArray();
            List<Orderer> orderers = new List<Orderer>();
            foreach (JToken m in ordarray)
            {
                Orderer o = m.ToObject<Orderer>();
                o.Channel = ch;
                orderers.Add(o);
            }

            ch.orderers = new ConcurrentHashSet<Orderer>(orderers);
            JArray evearray = j["EventHubs"] as JArray ?? new JArray();
            List<EventHub> events = new List<EventHub>();
            foreach (JToken m in evearray)
            {
                EventHub o = m.ToObject<EventHub>();
                o.Channel = ch;
                events.Add(o);
            }

            ch.eventHubs = new ConcurrentHashSet<EventHub>(events);
            JArray pearray = j["Peers"] as JArray ?? new JArray();
            List<Peer> peers = new List<Peer>();
            foreach (JToken mm in pearray)
            {
                JObject m = mm as JObject;
                if (m == null)
                    continue;
                Peer o = m["Peer"].ToObject<Peer>();
                o.Channel = ch;
                peers.Add(o);
                if (m.ContainsKey("Options"))
                {
                    PeerOptions opt = m["Options"].ToObject<PeerOptions>();
                    ch.peerOptionsMap[o] = opt;
                }

                if (m.ContainsKey("Roles"))
                {
                    List<int> rol = m["Roles"].ToObject<List<int>>();
                    foreach (int n in rol)
                    {
                        PeerRole pr = (PeerRole) n;
                        if (!ch.peerRoleSetMap.ContainsKey(pr))
                            ch.peerRoleSetMap[pr] = new List<Peer>();
                        ch.peerRoleSetMap[pr].Add(o);
                    }
                }
            }

            ch.peers = new ConcurrentHashSet<Peer>(peers);

            // sdOrdererAddition = DEFAULT_ORDERER_ADDITION;
            ch.endorsementSelector = ServiceDiscovery.DEFAULT_ENDORSEMENT_SELECTION;

            foreach (Peer peer in ch.peers)
            {
                ch.peerEndpointMap.TryAdd(peer.Endpoint, peer);
            }

            foreach (Orderer orderer in ch.orderers)
            {
                ch.ordererEndpointMap.TryAdd(orderer.Endpoint, orderer);
            }


            foreach (EventHub eventHub in ch.eventHubs.ToList())
                eventHub.SetEventQue(ch.ChannelEventQueue);

            ch.toString = $"Channel{{id: {Config.Instance.GetNextID()}, name: {ch.Name}";

            return ch;
        }

        public string Serialize()
        {
            JObject obj = new JObject();

            List<JObject> l = new List<JObject>();
            foreach (Peer p in Peers)
            {
                JObject pobj = new JObject();
                List<int> Roles = new List<int>();
                pobj.Add("Peer", JObject.FromObject(p, new JsonSerializer {ReferenceLoopHandling = ReferenceLoopHandling.Ignore}));
                if (PeerOptionsMap.ContainsKey(p))
                    pobj.Add("Options", JObject.FromObject(PeerOptionsMap[p]));
                foreach (PeerRole r in PeerRoleMap.Keys)
                {
                    if (PeerRoleMap[r].Contains(p))
                        Roles.Add((int) r);
                }

                if (Roles.Count > 0)
                    pobj.Add("Roles", JArray.FromObject(Roles));
                l.Add(pobj);
            }

            obj.Add("Peers", new JArray(l));
            obj.Add("EventHubs", new JArray(EventHubs.Select(a => JObject.FromObject(a, new JsonSerializer {ReferenceLoopHandling = ReferenceLoopHandling.Ignore}))));
            obj.Add("Orderers", new JArray(Orderers.Select(a => JObject.FromObject(a, new JsonSerializer {ReferenceLoopHandling = ReferenceLoopHandling.Ignore}))));
            obj.Add("Name", Name);
            obj.Add("SystemChannel", IsSystemChannel);
            return obj.ToString();
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
            await ch.InitChannelAsync(orderer, channelConfiguration, token, signers).ConfigureAwait(false);
            return ch;
        }


        private static void CheckHandle(string tag, string handle)
        {
            if (string.IsNullOrEmpty(handle))
                throw new ArgumentException("Handle is invalid.");
            if (!handle.StartsWith(tag) || !handle.EndsWith(tag))
                throw new ArgumentException("Handle is wrong type.");
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
            UpdateChannelConfigurationAsync(updateChannelConfiguration, orderer, new CancellationToken(), signers).RunAndUnwrap();
        }

        public async Task UpdateChannelConfigurationAsync(UpdateChannelConfiguration updateChannelConfiguration, Orderer orderer, CancellationToken token = default(CancellationToken), params byte[][] signers)
        {
            CheckChannelState();
            CheckOrderer(orderer);
            try
            {
                long startLastConfigIndex = await GetLastConfigIndexAsync(orderer, token).ConfigureAwait(false);
                logger.Trace($"startLastConfigIndex: {startLastConfigIndex}. Channel config wait time is: {CHANNEL_CONFIG_WAIT_TIME}");
                await SendUpdateChannelAsync(updateChannelConfiguration.UpdateChannelConfigurationBytes, signers, orderer, token).ConfigureAwait(false);
                long currentLastConfigIndex;
                Stopwatch timer = new Stopwatch();
                timer.Start();
                //Try to wait to see the channel got updated but don't fail if we don't see it.
                do
                {
                    currentLastConfigIndex = await GetLastConfigIndexAsync(orderer, token).ConfigureAwait(false);
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
                                await Task.Delay((int) ORDERER_RETRY_WAIT_TIME, token).ConfigureAwait(false); //try again sleep
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
                throw;
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
                Status statusCode;
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
                    BroadcastResponse trxResult = await orderer.SendTransactionAsync(payloadEnv, token).ConfigureAwait(false);
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
                            await Task.Delay((int) ORDERER_RETRY_WAIT_TIME, token).ConfigureAwait(false); //try again sleep
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
                throw;
            }
            catch (Exception e)
            {
                string msg = $"Channel {Name} error: {e.Message}";

                logger.ErrorException(msg, e);
                throw new TransactionException(msg, e);
            }
        }

        /**
         * Add a peer to the channel
         *
         * @param peer The Peer to add.
         * @return Channel The current channel added.
         * @throws InvalidArgumentException
         */
        public Task<Channel> AddPeerAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            return AddPeerAsync(peer, PeerOptions.CreatePeerOptions(), token);
        }

        public Channel AddPeer(Peer peer)
        {
            return AddPeerAsync(peer).RunAndUnwrap();
        }

        public Channel AddPeer(Peer peer, PeerOptions peerOptions)
        {
            return AddPeerAsync(peer, peerOptions).RunAndUnwrap();
        }

        /**
         * Add a peer to the channel
         *
         * @param peer        The Peer to add.
         * @param peerOptions see {@link PeerRole}
         * @return Channel The current channel added.
         * @throws InvalidArgumentException
         */
        public async Task<Channel> AddPeerAsync(Peer peer, PeerOptions peerOptions, CancellationToken token = default(CancellationToken))
        {
            if (IsShutdown)
                throw new ArgumentException($"Channel {Name} has been shutdown.");
            if (null == peer)
                throw new ArgumentException("Peer is invalid can not be null.");
            if (peer.Channel != null && peer.Channel != this)
                throw new ArgumentException($"Peer already connected to channel {peer.Channel.Name}");
            if (null == peerOptions)
                throw new ArgumentException("Peer is invalid can not be null.");
            logger.Debug($"{this} adding peer: {peer}, peerOptions: {peerOptions}");
            peer.Channel = this;
            peers.TryAdd(peer);
            peerOptionsMap[peer] = peerOptions.Clone();
            peerEndpointMap[peer.Endpoint] = peer;
            if (peerOptions.PeerRoles.Contains(PeerRole.SERVICE_DISCOVERY))
            {
                Properties properties = peer.Properties;
                if (properties == null || string.IsNullOrEmpty(properties["clientCertFile"]) && string.IsNullOrEmpty(properties["clientCertBytes"]))
                {
                    TLSCertificateKeyPair tlsCertificateKeyPair = TLSCertificateKeyPair.CreateClientCert();
                    peer.SetTLSCertificateKeyPair(tlsCertificateKeyPair);
                }

                discoveryEndpoints.TryAdd(peer.Endpoint);
            }

            foreach (PeerRole peerRole in peerRoleSetMap.Keys)
            {
                if (peerOptions.PeerRoles.Contains(peerRole))
                    peerRoleSetMap[peerRole].Add(peer);
            }

            if (IsInitialized && peerOptions.PeerRoles.Contains(PeerRole.EVENT_SOURCE))
            {
                try
                {
                    await peer.InitiateEventingAsync(GetTransactionContext(), GetPeersOptions(peer), token).ConfigureAwait(false);
                }
                catch (TransactionException)
                {
                    logger.Error($"Error channel {this} enabling eventing on peer {peer}");
                }
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

        private IReadOnlyList<Peer> GetServiceDiscoveryPeers()
        {
            return peerRoleSetMap[PeerRole.SERVICE_DISCOVERY]?.ToList() ?? new List<Peer>();
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
            return JoinPeerAsync(peer, peerOptions).RunAndUnwrap();
        }

        public async Task<Channel> JoinPeerAsync(Peer peer, PeerOptions peerOptions, CancellationToken token = default(CancellationToken))
        {
            try
            {
                return await JoinPeerAsync(GetRandomOrderer(), peer, peerOptions, token).ConfigureAwait(false);
            }
            catch (ProposalException)
            {
                throw;
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
            return JoinPeerAsync(orderer, peer, peerOptions).RunAndUnwrap();
        }

        public async Task<Channel> JoinPeerAsync(Orderer orderer, Peer peer, PeerOptions peerOptions, CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"Channel {Name} joining peer {peer.Name}, url: {peer.Url}");
            if (IsShutdown)
                throw new ProposalException($"Channel {Name} has been shutdown.");
            Channel peerChannel = peer.Channel;
            if (null != peerChannel && peerChannel != this)
                throw new ProposalException($"Can not add peer {peer.Name} to channel {Name} because it already belongs to channel {peerChannel.Name}.");

            logger.Info($"{this} joining {peer}.");

            if (genesisBlock == null && orderers.Count == 0)
            {
                ProposalException e = new ProposalException("Channel missing genesis block and no orderers configured");
                logger.ErrorException(e.Message, e);
            }

            try
            {
                genesisBlock = await GetGenesisBlockAsync(orderer, token).ConfigureAwait(false);
                logger.Debug($"Channel {Name} got genesis block");
                Channel systemChannel = CreateSystemChannel(client); //channel is not really created and this is targeted to system channel
                TransactionContext transactionContext = systemChannel.GetTransactionContext();
                Proposal joinProposal = JoinPeerProposalBuilder.Create().Context(transactionContext).GenesisBlock(genesisBlock).Build();
                logger.Debug("Getting signed proposal.");
                SignedProposal signedProposal = GetSignedProposal(transactionContext, joinProposal);
                logger.Debug("Got signed proposal.");
                await AddPeerAsync(peer, peerOptions, token).ConfigureAwait(false); //need to add peer.
                List<ProposalResponse> resp = await SendProposalToPeersAsync(new[] {peer}, signedProposal, transactionContext, token).ConfigureAwait(false);
                ProposalResponse pro = resp.First();
                if (pro.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    logger.Info($"Peer {peer.Name} joined into channel {this}");
                else
                {
                    RemovePeerInternal(peer);
                    throw new ProposalException($"Join peer to channel {Name} failed.  Status {pro.Status}, details: {pro.Message}");
                }
            }
            catch (ProposalException e)
            {
                logger.Error($"{this} removing peer {peer} due to exception {e.Message}");
                RemovePeerInternal(peer);
                logger.ErrorException(e.Message, e);
                throw;
            }
            catch (Exception e)
            {
                logger.Error($"{this} removing peer {peer} due to exception {e.Message}");
                peers.TryRemove(peer);
                logger.ErrorException(e.Message, e);
                throw new ProposalException(e.Message, e);
            }

            return this;
        }

        private async Task<Block> GetConfigBlockAsync(List<Peer> pers, CancellationToken token)
        {
            if (IsShutdown)
                throw new ProposalException($"Channel {Name} has been shutdown.");
            if (pers.Count == 0)
                throw new ProposalException("No peers go get config block");
            TransactionContext transactionContext;
            SignedProposal signedProposal;
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
            foreach (Peer peer in pers)
            {
                try
                {
                    List<ProposalResponse> resp = await SendProposalToPeersAsync(new[] {peer}, signedProposal, transactionContext, token).ConfigureAwait(false);
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
            if (IsShutdown)
                throw new ArgumentException($"Can not remove peer from channel {Name} already shutdown.");
            logger.Debug($"removePeer {peer} from channel {this}");
            CheckPeer(peer);
            RemovePeerInternal(peer);
            peer.Shutdown(true);
        }

        private void RemovePeerInternal(Peer peer)
        {
            logger.Debug($"removePeerInternal {peer} from channel {this}");

            peers.TryRemove(peer);
            peerOptionsMap.TryRemove(peer, out _);
            peerEndpointMap.TryRemove(peer.Endpoint, out _);
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
                throw new ArgumentException($"Channel {Name} has been shutdown.");
            if (null == orderer)
            {
                throw new ArgumentException("Orderer is invalid can not be null.");
            }

            logger.Debug($"Channel {this} adding {orderer}");

            orderer.Channel = this;
            ordererEndpointMap[orderer.Endpoint] = orderer;
            orderers.TryAdd(orderer);
            return this;
        }

        public void RemoveOrderer(Orderer orderer)
        {
            if (IsShutdown)
                throw new ArgumentException($"Channel {Name} has been shutdown.");
            if (null == orderer)
                throw new ArgumentException("Orderer is invalid can not be null.");
            logger.Debug($"Channel {this} removing {orderer}");
            ordererEndpointMap.TryRemove(orderer.Endpoint, out _);
            orderers.TryRemove(orderer);
            orderer.Shutdown(true);
        }

        public PeerOptions GetPeersOptions(Peer peer)
        {
            PeerOptions ret;
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
            return AddEventHubAsync(eventHub).RunAndUnwrap();
        }

        public async Task<Channel> AddEventHubAsync(EventHub eventHub, CancellationToken token = default(CancellationToken))
        {
            if (IsShutdown)
                throw new ArgumentException($"Channel {Name} has been shutdown.");
            if (null == eventHub)
                throw new ArgumentException("EventHub is invalid can not be null.");
            logger.Debug($"Channel {this} adding event hub {eventHub}");
            eventHub.Channel = this;
            eventHub.SetEventQue(ChannelEventQueue);
            eventHubs.TryAdd(eventHub);
            if (IsInitialized)
            {
                try
                {
                    await eventHub.ConnectAsync(GetTransactionContext(), token).ConfigureAwait(false);
                }
                catch (EventHubException e)
                {
                    throw new ArgumentException(e.Message, e);
                }
            }

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
            if (roles.Length == 0)
                return peers.ToList();
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
            return SetPeerOptionsAsync(peer, peerOptions).RunAndUnwrap();
        }

        public async Task<PeerOptions> SetPeerOptionsAsync(Peer peer, PeerOptions peerOptions, CancellationToken token = default(CancellationToken))
        {
            if (initialized)
                throw new ArgumentException($"Channel {Name} already initialized.");
            CheckPeer(peer);
            PeerOptions ret = GetPeersOptions(peer);
            RemovePeerInternal(peer);
            await AddPeerAsync(peer, peerOptions, token).ConfigureAwait(false);
            return ret;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool IsChaincodeUpgradeEvent(long blockNumber)
        {
            bool ret = false;
            if (blockNumber > lastChaincodeUpgradeEventBlock)
            {
                lastChaincodeUpgradeEventBlock = blockNumber;
                ret = true;
            }

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
            return InitializeAsync().RunAndUnwrap();
        }

        public async Task<Channel> InitializeAsync(CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"Channel {Name} initialize shutdown {IsShutdown}");
            if (IsInitialized)
                return this;
            if (IsShutdown)
                throw new ArgumentException($"Channel {Name} has been shutdown.");
            if (string.IsNullOrEmpty(Name))
                throw new ArgumentException("Can not initialize channel without a valid name.");
            if (client == null)
                throw new ArgumentException("Can not initialize channel without a client object.");
            client.UserContext.UserContextCheck();

            try
            {
                await LoadCACertificatesAsync(false, token).ConfigureAwait(false); // put all MSP certs into cryptoSuite if this fails here we'll try again later.
            }
            catch (Exception)
            {
                logger.Warn($"Channel {Name} could not load peer CA certificates from any peers.");
            }

            IReadOnlyList<Peer> serviceDiscoveryPeers = GetServiceDiscoveryPeers();
            if (serviceDiscoveryPeers == null || serviceDiscoveryPeers.Count == 0)
            {
                logger.Trace("Starting service discovery.");
                serviceDiscovery = new ServiceDiscovery(this, serviceDiscoveryPeers, GetTransactionContext());
                await serviceDiscovery.FullNetworkDiscoveryAsync(true, token).ConfigureAwait(false);
                serviceDiscovery.Init();
                logger.Trace("Completed. service discovery.");
            }


            try
            {
                logger.Debug($"Eventque started");
                foreach (EventHub eh in eventHubs)
                {
                    //Connect all event hubs
                    await eh.ConnectAsync(GetTransactionContext(), token).ConfigureAwait(false);
                }

                foreach (Peer peer in GetEventingPeers())
                    await peer.InitiateEventingAsync(GetTransactionContext(), GetPeersOptions(peer), token).ConfigureAwait(false);
                logger.Debug($"{eventHubs.Count} eventhubs initialized");
                transactionListenerProcessorHandle = RegisterTransactionListenerProcessor(); //Manage transactions.
                logger.Debug($"Channel {Name} registerTransactionListenerProcessor completed");
                if (serviceDiscovery != null)
                {
                    chaincodeEventUpgradeListenerHandle = RegisterChaincodeEventListener(new Regex("^lscc$", RegexOptions.Compiled), new Regex("^upgrade$", RegexOptions.Compiled), (handle, blockEvent, chaincodeEvent) =>
                    {
                        logger.Debug($"Channel {Name} got upgrade chaincode event");
                        if (!IsShutdown && IsChaincodeUpgradeEvent(blockEvent.BlockNumber))
                        {
#pragma warning disable 4014
                            serviceDiscovery.FullNetworkDiscoveryAsync(true, token);
#pragma warning restore 4014
                        }
                    });
                }

                StartEventQue(); //Run the event for event messages from event hubs.
                string tskname = eventQueueThread == null ? "null" : eventQueueThread.Id.ToString();
                logger.Info($"Channel {this} eventThread started shutdown: {IsShutdown} Task Id: {tskname} ");

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

        public async Task SdUpdateAsync(SDNetwork sdNetwork, CancellationToken token = default(CancellationToken))
        {
            if (IsShutdown)
            {
                return;
            }

            logger.Debug($"Channel {Name} doing channel update for service discovery.");
            List<Orderer> remove = new List<Orderer>();
            foreach (Orderer orderer in Orderers)
            {
                if (!sdNetwork.OrdererEndpoints.Contains(orderer.Endpoint))
                {
                    remove.Add(orderer);
                }
            }

            remove.ForEach(orderer =>
            {
                try
                {
                    RemoveOrderer(orderer);
                }
                catch (ArgumentException e)
                {
                    logger.ErrorException(e.Message, e);
                }
            });

            foreach (SDOrderer sdOrderer in sdNetwork.SDOrderers)
            {
                Orderer orderer = ordererEndpointMap.GetOrNull(sdOrderer.Endpoint);
                if (IsShutdown)
                {
                    return;
                }

                if (null == orderer)
                {
                    logger.Debug($"Channel {Name} doing channel update adding new orderer endpoint: {sdOrderer.Endpoint}");
                    await sdOrderer.AddAsync(ServiceDiscoveryProperties, token).ConfigureAwait(false);
                }
            }

            remove.Clear();
            List<Peer> removePeers = new List<Peer>();

            foreach (Peer peer in Peers)
            {
                if (!sdNetwork.PeerEndpoints.Contains(peer.Endpoint))
                {
                    if (!discoveryEndpoints.Contains(peer.Endpoint))
                    {
                        // never remove discovery endpoints.
                        logger.Debug($"Channel {Name} doing channel update remove unfound peer endpoint {peer.Endpoint} ");
                        removePeers.Add(peer);
                    }
                }
            }

            removePeers.ForEach(peer =>
            {
                try
                {
                    RemovePeer(peer);
                }
                catch (ArgumentException e)
                {
                    logger.ErrorException(e.Message, e);
                }
            });

            foreach (SDEndorser sdEndorser in sdNetwork.Endorsers)
            {
                Peer peer = peerEndpointMap.GetOrNull(sdEndorser.Endpoint);
                if (null == peer)
                {
                    if (IsShutdown)
                    {
                        return;
                    }

                    logger.Debug($"Channel {Name} doing channel update found new peer endpoint {sdEndorser.Endpoint}");
                    await sdEndorser.AddAsync(ServiceDiscoveryProperties, token).ConfigureAwait(false);
                }
            }
        }


        protected virtual async Task LoadCACertificatesAsync(bool force, CancellationToken token)
        {
            using (await _certificatelock.LockAsync(token).ConfigureAwait(false))
            {
                if (!force && msps != null && msps.Count > 0)
                    return;
                logger.Debug($"Channel {Name} loadCACertificates");
                Dictionary<string, MSP> lmsp = await ParseConfigBlockAsync(force, token).ConfigureAwait(false);
                if (lmsp == null || lmsp.Count == 0)
                    throw new ArgumentException("Unable to load CA certificates. Channel " + Name + " does not have any MSPs.");
                List<byte[]> certList;
                foreach (MSP msp in lmsp.Values)
                {
                    logger.Debug($"loading certificates for MSP {msp.ID}: ");
                    certList = msp.RootCerts.ToList();
                    if (certList.Count > 0)
                        certList.ForEach(a => client.CryptoSuite.Store.AddCertificate(a.ToUTF8String()));
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
                    //long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    SeekSpecified seekSpecified = new SeekSpecified {Number = 0};
                    SeekPosition seekPosition = new SeekPosition {Specified = seekSpecified};
                    SeekSpecified seekStopSpecified = new SeekSpecified {Number = 0};
                    SeekPosition seekStopPosition = new SeekPosition {Specified = seekStopSpecified};
                    SeekInfo seekInfo = new SeekInfo {Start = seekPosition, Stop = seekStopPosition, Behavior = SeekInfo.Types.SeekBehavior.BlockUntilReady};
                    List<DeliverResponse> deliverResponses = new List<DeliverResponse>();
                    await SeekBlockAsync(seekInfo, deliverResponses, orderer, token).ConfigureAwait(false);
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
                throw;
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
                throw new ArgumentException("channelConfiguration is null");
            try
            {
                TransactionContext transactionContext = GetTransactionContext(signer);
                ByteString configUpdate = ByteString.CopyFrom(updateChannelConfiguration.UpdateChannelConfigurationBytes);
                ByteString sigHeaderByteString = ProtoUtils.GetSignatureHeaderAsByteString(signer, transactionContext);
                ByteString signatureByteSting = transactionContext.SignByteStrings(new[] {signer}, sigHeaderByteString, configUpdate)[0];
                return new ConfigSignature {SignatureHeader = sigHeaderByteString, Signature = signatureByteSting}.ToByteArray();
            }
            catch (Exception e)
            {
                throw new ArgumentException(e.Message, e);
            }
            finally
            {
                logger.Debug("finally done");
            }
        }

        protected virtual async Task<Dictionary<string, MSP>> ParseConfigBlockAsync(bool force, CancellationToken token)
        {
            IReadOnlyDictionary<string, MSP> lmsps = msps;
            if (!force && lmsps != null && lmsps.Count > 0)
                return lmsps.ToDictionary(a => a.Key, a => a.Value);
            try
            {
                Block parseFrom = await GetConfigBlockAsync(GetShuffledPeers(), token).ConfigureAwait(false);
                // final Block configBlock = getConfigurationBlock();
                logger.Debug($"Channel {Name} Got config block getting MSP data and anchorPeers data");
                Envelope envelope = Envelope.Parser.ParseFrom(parseFrom.Data.Data[0]);
                Payload payload = Payload.Parser.ParseFrom(envelope.Payload);
                ConfigEnvelope configEnvelope = ConfigEnvelope.Parser.ParseFrom(payload.Data);
                ConfigGroup channelGroup = configEnvelope.Config.ChannelGroup;
                Dictionary<string, MSP> newMSPS = TraverseConfigGroupsMSP(string.Empty, channelGroup, new Dictionary<string, MSP>());
                msps = newMSPS;
                return newMSPS.ToDictionary(a => a.Key, a => a.Value);
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new TransactionException(e);
            }
        }

        private Dictionary<string, MSP> TraverseConfigGroupsMSP(string name, ConfigGroup configGroup, Dictionary<string, MSP> dicmsps)
        {
            ConfigValue mspv = configGroup.Values.ContainsKey("MSP") ? configGroup.Values["MSP"] : null;
            if (null != mspv)
            {
                if (!dicmsps.ContainsKey(name))
                {
                    MSPConfig mspConfig = MSPConfig.Parser.ParseFrom(mspv.Value);
                    if (mspConfig.Type == 0)
                    {
                        FabricMSPConfig fabricMSPConfig = FabricMSPConfig.Parser.ParseFrom(mspConfig.Config);
                        dicmsps.Add(name, new MSP(name, fabricMSPConfig));
                    }
                }
            }

            foreach (string key in configGroup.Groups.Keys)
                TraverseConfigGroupsMSP(key, configGroup.Groups[key], dicmsps);
            return dicmsps;
        }

        /**
         * Get a channel configuration update to add or remove peers.
         * If both peersToAdd AND peersToRemove are null then only the current anchor peers are reported with @see {@link AnchorPeersConfigUpdateResult#getCurrentPeers()}
         *
         * @param peer          peer to use to the channel configuration from.
         * @param userContext   The usercontext to use.
         * @param peersToAdd    Peers to add as Host:Port peer1.org2.com:7022
         * @param peersToRemove Peers to remove as Host:Port peer1.org2.com:7022
         * @return The AnchorPeersConfigUpdateResult @see {@link AnchorPeersConfigUpdateResult}
         * @throws Exception
         */
        public AnchorPeersConfigUpdateResult GetConfigUpdateAnchorPeers(Peer peer, IUser userContext, List<string> peersToAdd, List<string> peersToRemove)
        {
            return GetConfigUpdateAnchorPeersAsync(peer, userContext, peersToAdd, peersToRemove).RunAndUnwrap();
        }

        public async Task<AnchorPeersConfigUpdateResult> GetConfigUpdateAnchorPeersAsync(Peer peer, IUser userContext, List<string> peersToAdd, List<string> peersToRemove, CancellationToken token = default(CancellationToken))
        {
            userContext.UserContextCheck();

            CheckPeer(peer);

            CheckChannelState();

            bool reportOnly = peersToAdd == null && peersToRemove == null;

            if (!reportOnly && (peersToAdd == null || peersToAdd.Count == 0) && (peersToRemove == null || peersToRemove.Count == 0))
                throw new ArgumentException("No anchor peers to add or remove!");

            if (IS_TRACE_LEVEL)
            {
                StringBuilder sbp = new StringBuilder("null");
                string sep = "";
                if (peersToAdd != null)
                {
                    sbp = new StringBuilder("[");
                    foreach (string s in peersToAdd)
                    {
                        sbp.Append(sep).Append("'").Append(s).Append("'");
                        sep = ", ";
                    }

                    sbp.Append("]");
                }

                StringBuilder sbr = new StringBuilder("null");
                sep = "";
                if (peersToRemove != null)
                {
                    sbr = new StringBuilder("[");

                    foreach (string s in peersToRemove)
                    {
                        sbr.Append(sep).Append("'").Append(s).Append("'");
                        sep = ", ";
                    }

                    sbr.Append("]");
                }

                logger.Trace($"getConfigUpdateAnchorPeers channel {Name}, peer: {peer}, user: {userContext.MspId}, peers to add: {sbp}, peers to remove: {sbr}");
            }

            HashSet<string> peersToAddHS = new HashSet<string>();
            if (null != peersToAdd)
            {
                foreach (string s in peersToAdd)
                {
                    string[] ep = ParseEndpoint(s);
                    peersToAddHS.Add(ep[0] + ":" + ep[1]);
                }

                //  peersToAddHS.addAll(peersToAdd);
            }

            HashSet<string> peersToRemoveHS = new HashSet<string>();
            if (null != peersToRemove && peersToRemove.Count > 0)
            {
                foreach (string s in peersToRemove)
                {
                    string[] ep = ParseEndpoint(s);
                    peersToRemoveHS.Add(ep[0] + ":" + ep[1]);
                }

                foreach (string s in peersToAddHS)
                {
                    peersToRemoveHS.Remove(s); //add overrides remove;
                }
            }

            HashSet<string> peersRemoved = new HashSet<string>();
            HashSet<string> peersAdded = new HashSet<string>();

            Block configBlock = await GetConfigBlockAsync(new List<Peer> {peer}, token).ConfigureAwait(false);
            if (IS_TRACE_LEVEL)
            {
                logger.Trace($"getConfigUpdateAnchorPeers  configBlock: {configBlock.ToByteArray().ToHexString()}");
            }

            Envelope envelope = Envelope.Parser.ParseFrom(configBlock.Data.Data[0]);
            Payload payload = Payload.Parser.ParseFrom(envelope.Payload);
            Header header = payload.Header;
            ChannelHeader channelHeader = ChannelHeader.Parser.ParseFrom(header.ChannelHeader);
            if (!channelHeader.ChannelId.Equals(Name))
            {
                throw new ArgumentException($"Expected config block for channel: {Name}, but got: {channelHeader.ChannelId}");
            }

            ConfigEnvelope configEnvelope = ConfigEnvelope.Parser.ParseFrom(payload.Data);
            // ConfigGroup channelGroup = configEnvelope.getConfig().getChannelGroup();

            Protos.Common.Config config = configEnvelope.Config;
            Protos.Common.Config configBuilderUpdate = Protos.Common.Config.Parser.ParseFrom(config.ToByteArray());


            ConfigGroup channelGroupBuild = ConfigGroup.Parser.ParseFrom(configBuilderUpdate.ChannelGroup.ToByteArray());

            IDictionary<string, ConfigGroup> groupsMap = channelGroupBuild.Groups;
            ConfigGroup application = ConfigGroup.Parser.ParseFrom(groupsMap["Application"].ToByteArray());
            string mspid = userContext.MspId;
            ConfigGroup peerOrgConfigGroup = application.Groups.GetOrNull(mspid);
            if (null == peerOrgConfigGroup)
            {
                StringBuilder sb = new StringBuilder(1000);
                string sep = "";

                foreach (string amspid in application.Groups.Keys)
                {
                    sb.Append(sep).Append(amspid);
                    sep = ", ";
                }

                throw new ArgumentException($"Expected to find organization matching user context's mspid: {mspid}, but only found {sb}.");
            }

            ConfigGroup peerOrgConfigGroupBuilder = ConfigGroup.Parser.ParseFrom(peerOrgConfigGroup.ToByteArray());

            string modPolicy = peerOrgConfigGroup.ModPolicy != null ? peerOrgConfigGroup.ModPolicy : "Admins";

            IDictionary<string, ConfigValue> valuesMap = peerOrgConfigGroupBuilder.Values;

            ConfigValue anchorPeersCV = valuesMap.ContainsKey("AnchorPeers") ? valuesMap["AnchorPeers"] : null;

            HashSet<string> currentAP = new HashSet<string>(); // The anchor peers that exist already.

            if (null != anchorPeersCV && anchorPeersCV.Value != null)
            {
                modPolicy = anchorPeersCV.ModPolicy != null ? "Admins" : modPolicy;

                AnchorPeers anchorPeerss = AnchorPeers.Parser.ParseFrom(anchorPeersCV.Value);
                List<AnchorPeer> anchorPeersList = anchorPeerss.AnchorPeers_?.ToList();
                if (anchorPeersList != null)
                {
                    foreach (AnchorPeer anchorPeer in anchorPeersList)
                    {
                        currentAP.Add(anchorPeer.Host.ToLowerInvariant() + ":" + anchorPeer.Port);
                    }
                }
            }

            if (IS_TRACE_LEVEL)
            {
                StringBuilder sbp = new StringBuilder("[");
                string sep = "";

                foreach (string s in currentAP)
                {
                    sbp.Append(sep).Append("'").Append(s).Append("'");
                    sep = ", ";
                }

                sbp.Append("]");

                logger.Trace($"getConfigUpdateAnchorPeers channel {Name},  current anchor peers: {sbp}");
            }

            if (reportOnly)
            {
                logger.Trace("getConfigUpdateAnchorPeers reportOnly");

                AnchorPeersConfigUpdateResult ret3 = new AnchorPeersConfigUpdateResult();
                ret3.CurrentPeers = currentAP.ToList();

                if (IS_TRACE_LEVEL)
                    logger.Trace($"getConfigUpdateAnchorPeers returned: {ret3}");
                return ret3;
            }

            HashSet<string> peersFinalHS = new HashSet<string>();

            AnchorPeers anchorPeers = new AnchorPeers();
            foreach (string s in currentAP)
            {
                if (peersToRemoveHS.Contains(s))
                {
                    peersRemoved.Add(s);
                    continue;
                }

                if (!peersToAddHS.Contains(s))
                {
                    string[] split = s.Split(':');
                    anchorPeers.AnchorPeers_.Add(new AnchorPeer {Host = split[0], Port = int.Parse(split[1])});
                    peersFinalHS.Add(s);
                }
            }

            foreach (string s in peersToAddHS)
            {
                if (!currentAP.Contains(s))
                {
                    peersAdded.Add(s);
                    string[] split = s.Split(':');
                    anchorPeers.AnchorPeers_.Add(new AnchorPeer {Host = split[0], Port = int.Parse(split[1])});
                    peersFinalHS.Add(s);
                }
            }

            if (peersRemoved.Count == 0 && peersAdded.Count == 0)
            {
                logger.Trace("getConfigUpdateAnchorPeers no Peers need adding or removing.");
                AnchorPeersConfigUpdateResult ret2 = new AnchorPeersConfigUpdateResult();
                ret2.CurrentPeers = currentAP.ToList();
                if (IS_TRACE_LEVEL)
                    logger.Trace($"getConfigUpdateAnchorPeers returned: {ret2}");
                return ret2;
            }

            var m = valuesMap.ToDictionary(a => a.Key, a => a.Value);
            //       org1MSP.clearValues();

            //        if (!peersFinalHS.isEmpty()) { // if there are anchor peers to add...   LEAVE IT.

            m["AnchorPeers"] = new ConfigValue {Value = anchorPeers.ToByteString(), ModPolicy = modPolicy};
            //       }
            peerOrgConfigGroupBuilder.Values.Clear();
            peerOrgConfigGroupBuilder.Values.Add(m);

            var m2 = application.Groups.ToDictionary(a => a.Key, a => a.Value);
            m2[mspid] = peerOrgConfigGroupBuilder;
            // application.putAllValues(m);
            application.Groups.Clear();
            application.Groups.Add(m2);
            var m3 = channelGroupBuild.Groups.ToDictionary(a => a.Key, a => a.Value);
            m3["Application"] = application;
            channelGroupBuild.Groups.Clear();
            channelGroupBuild.Groups.Add(m3);
            configBuilderUpdate.ChannelGroup = channelGroupBuild;
            ConfigUpdate updateBlockBuilder = new ConfigUpdate();
            if (IS_TRACE_LEVEL)
                logger.Trace($"getConfigUpdateAnchorPeers  updated configBlock: {configBuilderUpdate.ToByteArray().ToHexString()}");

            ProtoUtils.ComputeUpdate(Name, config, configBuilderUpdate, updateBlockBuilder);

            AnchorPeersConfigUpdateResult ret = new AnchorPeersConfigUpdateResult();
            ret.CurrentPeers = currentAP.ToList();
            ret.PeersAdded = peersAdded.ToList();
            ret.PeersRemoved = peersRemoved.ToList();
            ret.UpdatedPeers = peersFinalHS.ToList();
            ret.UpdateChannelConfiguration = new UpdateChannelConfiguration(updateBlockBuilder.ToByteArray());
            if (IS_TRACE_LEVEL)
                logger.Trace($"getConfigUpdateAnchorPeers returned: {ret}");

            return ret;
        }


        /**
         * Provide the Channel's latest raw Configuration Block.
         *
         * @return Channel configuration block.
         * @throws TransactionException
         */

        // ReSharper disable once UnusedMember.Local
        private async Task<Block> GetConfigurationBlockAsync(CancellationToken token)
        {
            logger.Debug($"getConfigurationBlock for channel {Name}");
            try
            {
                Orderer orderer = GetRandomOrderer();
                long lastConfigIndex = await GetLastConfigIndexAsync(orderer, token).ConfigureAwait(false);
                logger.Debug($"Last config index is {lastConfigIndex}x");
                Block configBlock = await GetBlockByNumberAsync(lastConfigIndex, token).ConfigureAwait(false);
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
                throw;
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new TransactionException(e);
            }
        }

        private string[] ParseEndpoint(string endPoint)
        {
            if (string.IsNullOrEmpty(endPoint))
                throw new ArgumentException("Endpoint is null or empty string");
            try
            {
                Uri uri = new Uri("grpc://" + endPoint.ToLowerInvariant());
                string host = uri.Host;
                if (string.IsNullOrEmpty(host))
                    throw new ArgumentException($"Endpoint '{endPoint}' expected to be format \"host:port\". Hostname part missing");
                int port = uri.Port;
                if (port == -1)
                    throw new ArgumentException($"Endpoint '{endPoint}' expected to be format \"host:port\". Port does not seem to be a valid port number. ");
                if (port < 1)
                    throw new ArgumentException($"Endpoint '{endPoint}' expected to be format \"host:port\". Port does not seem to be a valid port number. ");
                if (port > 65535)
                    throw new ArgumentException($"Endpoint '{endPoint}' expected to be format \"host:port\". Port does not seem to be a valid port number less than 65535. ");
                return new [] {host, port + ""};
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Endpoint '{endPoint}' expected to be format \"host:port\".", e);
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
            return GetChannelConfigurationBytesAsync().RunAndUnwrap();
        }

        public async Task<byte[]> GetChannelConfigurationBytesAsync(CancellationToken token = default(CancellationToken))
        {
            try
            {
                Block configBlock = await GetConfigBlockAsync(GetShuffledPeers(), token).ConfigureAwait(false);
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
            Block latestBlock = await GetLatestBlockAsync(orderer, token).ConfigureAwait(false);
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
                await SeekBlockAsync(seekInfo, deliverResponses, GetRandomOrderer(), token).ConfigureAwait(false);
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
                throw;
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
            int statusRC;
            try
            {
                do
                {
                    Orderer orderer = ordererIn ?? GetRandomOrderer();
                    TransactionContext txContext = GetTransactionContext();
                    List<DeliverResponse> deliver = await orderer.SendDeliverAsync(ProtoUtils.CreateSeekInfoEnvelope(txContext, seekInfo, orderer.ClientTLSCertificateDigest), token).ConfigureAwait(false);
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
                            await Task.Delay((int) ORDERER_RETRY_WAIT_TIME, token).ConfigureAwait(false); //try again
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
                throw;
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
            await SeekBlockAsync(seekInfo, deliverResponses, orderer, token).ConfigureAwait(false);
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
        public List<ProposalResponse> SendInstantiationProposal(InstantiateProposalRequest instantiateProposalRequest, IEnumerable<Peer> pers)
        {
            return SendInstantiationProposalAsync(instantiateProposalRequest, pers).RunAndUnwrap();
        }

        public Task<List<ProposalResponse>> SendInstantiationProposalAsync(InstantiateProposalRequest instantiateProposalRequest, IEnumerable<Peer> pers, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            if (null == instantiateProposalRequest)
                throw new ArgumentException("InstantiateProposalRequest is null");
            instantiateProposalRequest.SetSubmitted();
            CheckPeers(pers);
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
                instantiateProposalbuilder.ChaincodeCollectionConfiguration(instantiateProposalRequest.ChaincodeCollectionConfiguration);
                instantiateProposalbuilder.SetTransientMap(instantiateProposalRequest.TransientMap);
                Proposal instantiateProposal = instantiateProposalbuilder.Build();
                SignedProposal signedProposal = GetSignedProposal(transactionContext, instantiateProposal);
                return SendProposalToPeersAsync(pers, signedProposal, transactionContext, token);
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
        public List<ProposalResponse> SendInstallProposal(InstallProposalRequest installProposalRequest, IEnumerable<Peer> pers)
        {
            return SendInstallProposalAsync(installProposalRequest, pers).RunAndUnwrap();
        }

        public Task<List<ProposalResponse>> SendInstallProposalAsync(InstallProposalRequest installProposalRequest, IEnumerable<Peer> pers, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(pers);
            if (null == installProposalRequest)
                throw new ArgumentException("InstallProposalRequest is null");
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
                return SendProposalToPeersAsync(pers, signedProposal, transactionContext, token);
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
        public List<ProposalResponse> SendUpgradeProposal(UpgradeProposalRequest upgradeProposalRequest, IEnumerable<Peer> pers)
        {
            return SendUpgradeProposalAsync(upgradeProposalRequest, pers).RunAndUnwrap();
        }

        public Task<List<ProposalResponse>> SendUpgradeProposalAsync(UpgradeProposalRequest upgradeProposalRequest, IEnumerable<Peer> pers, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(pers);
            if (null == upgradeProposalRequest)
                throw new ArgumentException("Upgradeproposal is null");
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
                upgradeProposalBuilder.ChaincodeCollectionConfiguration(upgradeProposalRequest.ChaincodeCollectionConfiguration);

                SignedProposal signedProposal = GetSignedProposal(transactionContext, upgradeProposalBuilder.Build());
                return SendProposalToPeersAsync(pers, signedProposal, transactionContext, token);
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
                throw new ArgumentException($"Channel {Name} has been shutdown.");
            if (!initialized)
                throw new ArgumentException($"Channel {Name} has not been initialized.");
            client.UserContext.UserContextCheck();
        }

        /**
         * query this channel for a Block by the block hash.
         * The request is retried on each peer on the channel till successful.
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
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
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
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

        public Task<BlockInfo> QueryBlockByHashAsync(Peer peer, byte[] blockHash, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByHashAsync(new[] {peer}, blockHash, token);
        }

        /**
         * Query a peer in this channel for a Block by the block hash.
         * Each peer is tried until successful response.
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
         *
         * @param peers     the Peers to query.
         * @param blockHash the hash of the Block in the chain.
         * @return the {@link BlockInfo} with the given block Hash
         * @throws InvalidArgumentException if the channel is shutdown or any of the arguments are not valid.
         * @throws ProposalException        if an error occurred processing the query.
         */
        public BlockInfo QueryBlockByHash(IEnumerable<Peer> pers, byte[] blockHash)
        {
            return QueryBlockByHash(pers, blockHash, client.UserContext);
        }

        public Task<BlockInfo> QueryBlockByHashAsync(IEnumerable<Peer> pers, byte[] blockHash, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByHashAsync(pers, blockHash, client.UserContext, token);
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
        public BlockInfo QueryBlockByHash(IEnumerable<Peer> pers, byte[] blockHash, IUser userContext)
        {
            return QueryBlockByHashAsync(pers, blockHash, userContext).RunAndUnwrap();
        }

        public async Task<BlockInfo> QueryBlockByHashAsync(IEnumerable<Peer> pers, byte[] blockHash, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(pers);
            userContext.UserContextCheck();
            if (blockHash == null)
                throw new ArgumentException("blockHash parameter is null.");
            try
            {
                logger.Trace("queryBlockByHash with hash : " + blockHash.ToHexString() + " on channel " + Name);
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETBLOCKBYHASH);
                querySCCRequest.SetArgs(Name);
                querySCCRequest.SetArgBytes(new[] {blockHash});
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, pers, token).ConfigureAwait(false);
                return new BlockInfo(Block.Parser.ParseFrom(proposalResponse.ProtoProposalResponse.Response.Payload));
            }
            catch (InvalidProtocolBufferException e)
            {
                ProposalException proposalException = new ProposalException(e);
                logger.ErrorException(proposalException.Message, proposalException);
                throw proposalException;
            }
        }

        // ReSharper disable once UnusedMember.Local
        private Peer GetRandomLedgerQueryPeer()
        {
            List<Peer> ledgerQueryPeers = GetLedgerQueryPeers().ToList();
            if (ledgerQueryPeers.Count == 0)
                throw new ArgumentException("Channel " + Name + " does not have any ledger querying peers associated with it.");
            return ledgerQueryPeers[RANDOM.Next(ledgerQueryPeers.Count)];
        }

        // ReSharper disable once UnusedMember.Local
        private Peer GetRandomPeer()
        {
            List<Peer> randPicks = Peers.ToList(); //copy to avoid unlikely changes
            if (randPicks.Count == 0)
                throw new ArgumentException("Channel " + Name + " does not have any peers associated with it.");
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
                throw new ArgumentException("Channel " + Name + " does not have any orderers associated with it.");
            return randPicks[RANDOM.Next(randPicks.Count)];
        }

        private void CheckPeer(Peer peer)
        {
            if (peer == null)
                throw new ArgumentException("Peer value is null.");
            if (IsSystemChannel)
                return; // System owns no peers
            if (!Peers.Contains(peer))
                throw new ArgumentException("Channel " + Name + " does not have peer " + peer.Name);
            if (peer.Channel != this)
                throw new ArgumentException("Peer " + peer.Name + " not set for channel " + Name);
        }

        private void CheckOrderer(Orderer orderer)
        {
            if (orderer == null)
                throw new ArgumentException("Orderer value is null.");
            if (IsSystemChannel)
                return; // System owns no Orderers
            if (!Orderers.Contains(orderer))
                throw new ArgumentException("Channel " + Name + " does not have orderer " + orderer.Name);
            if (orderer.Channel != this)
                throw new ArgumentException("Orderer " + orderer.Name + " not set for channel " + Name);
        }

        private void CheckPeers(IEnumerable<Peer> pers)
        {
            if (pers == null)
                throw new ArgumentException("Collection of peers is null.");

            if (!pers.Any())
                throw new ArgumentException("Collection of peers is empty.");
            foreach (Peer peer in pers)
                CheckPeer(peer);
        }

        /**
         * query this channel for a Block by the blockNumber.
         * The request is retried on all peers till successful
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
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
            return QueryBlockByNumber(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockNumber, userContext);
        }

        public Task<BlockInfo> QueryBlockByNumberAsync(long blockNumber, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByNumberAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), blockNumber, userContext, token);
        }

        /**
         * Query a peer in this channel for a Block by the blockNumber
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
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
            return QueryBlockByNumber(new[] {peer}, blockNumber, userContext);
        }

        public Task<BlockInfo> QueryBlockByNumberAsync(Peer peer, long blockNumber, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByNumberAsync(new[] {peer}, blockNumber, userContext, token);
        }

        /**
         * query a peer in this channel for a Block by the blockNumber
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
         *
         * @param peers       the peers to try and send the request to
         * @param blockNumber index of the Block in the chain
         * @return the {@link BlockInfo} with the given blockNumber
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByNumber(IEnumerable<Peer> pers, long blockNumber)
        {
            return QueryBlockByNumber(pers, blockNumber, client.UserContext);
        }

        public Task<BlockInfo> QueryBlockByNumberAsync(IEnumerable<Peer> pers, long blockNumber, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByNumberAsync(pers, blockNumber, client.UserContext, token);
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
        public BlockInfo QueryBlockByNumber(IEnumerable<Peer> pers, long blockNumber, IUser userContext)
        {
            return QueryBlockByNumberAsync(pers, blockNumber, userContext).RunAndUnwrap();
        }

        public async Task<BlockInfo> QueryBlockByNumberAsync(IEnumerable<Peer> pers, long blockNumber, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(pers);
            userContext.UserContextCheck();
            try
            {
                logger.Debug($"QueryBlockByNumber with blockNumber {blockNumber} on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETBLOCKBYNUMBER);
                querySCCRequest.SetArgs(Name, ((ulong) blockNumber).ToString(CultureInfo.InvariantCulture));
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, pers, token).ConfigureAwait(false);
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
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
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
            return QueryBlockByTransactionID(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, userContext);
        }

        public Task<BlockInfo> QueryBlockByTransactionIDAsync(string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByTransactionIDAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, userContext, token);
        }

        /**
         * query a peer in this channel for a Block by a TransactionID contained in the block
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
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
            return QueryBlockByTransactionID(new[] {peer}, txID, userContext);
        }

        public Task<BlockInfo> QueryBlockByTransactionIDAsync(Peer peer, string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByTransactionIDAsync(new[] {peer}, txID, userContext, token);
        }

        /**
         * query a peer in this channel for a Block by a TransactionID contained in the block
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
         *
         * @param peers the peers to try to send the request to.
         * @param txID  the transactionID to query on
         * @return the {@link BlockInfo} for the Block containing the transaction
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockInfo QueryBlockByTransactionID(IEnumerable<Peer> pers, string txID)
        {
            return QueryBlockByTransactionID(pers, txID, client.UserContext);
        }

        public Task<BlockInfo> QueryBlockByTransactionIDAsync(IEnumerable<Peer> pers, string txID, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockByTransactionIDAsync(pers, txID, client.UserContext, token);
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
        public BlockInfo QueryBlockByTransactionID(IEnumerable<Peer> pers, string txID, IUser userContext)
        {
            return QueryBlockByTransactionIDAsync(pers, txID, userContext).RunAndUnwrap();
        }

        public async Task<BlockInfo> QueryBlockByTransactionIDAsync(IEnumerable<Peer> pers, string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(pers);
            userContext.UserContextCheck();
            if (txID == null)
                throw new ArgumentException("TxID parameter is null.");
            try
            {
                logger.Debug($"QueryBlockByTransactionID with txID {txID}\n     on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETBLOCKBYTXID);
                querySCCRequest.SetArgs(Name, txID);
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, pers, token).ConfigureAwait(false);
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
         * 
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
         *
         * @return a {@link BlockchainInfo} object containing the chain info requested
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public BlockchainInfo QueryBlockchainInfo()
        {
            return QueryBlockchainInfo(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), client.UserContext);
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
            return QueryBlockchainInfo(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), userContext);
        }

        public Task<BlockchainInfo> QueryBlockchainInfoAsync(IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryBlockchainInfoAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), userContext, token);
        }

        /**
         * query for chain information
         * 
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         *
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
            return QueryBlockchainInfo(new[] {peer}, userContext);
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
        public BlockchainInfo QueryBlockchainInfo(IEnumerable<Peer> pers, IUser userContext)
        {
            return QueryBlockchainInfoAsync(pers, userContext).RunAndUnwrap();
        }

        public async Task<BlockchainInfo> QueryBlockchainInfoAsync(IEnumerable<Peer> pers, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(pers);
            userContext.UserContextCheck();
            try
            {
                logger.Debug($"QueryBlockchainInfo to peer on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETCHAININFO);
                querySCCRequest.SetArgs(Name);
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, pers, token).ConfigureAwait(false);
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
         *
         *
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         *
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

        public Task<TransactionInfo> QueryTransactionByIDAsync(string txID, CancellationToken token = default(CancellationToken))
        {
            return QueryTransactionByIDAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, client.UserContext, token);
        }

        /**
         * Query this channel for a Fabric Transaction given its transactionID.
         * The request is sent to a random peer in the channel.
         * 
         *
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         *
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

        public Task<TransactionInfo> QueryTransactionByIDAsync(string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            return QueryTransactionByIDAsync(GetShuffledPeers(new[] {PeerRole.LEDGER_QUERY}), txID, userContext, token);
        }

        /**
         * Query for a Fabric Transaction given its transactionID
         *
         *
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         *
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
            return QueryTransactionByID(new[] {peer}, txID, userContext);
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
        public TransactionInfo QueryTransactionByID(IEnumerable<Peer> pers, string txID, IUser userContext)
        {
            return QueryTransactionByIDAsync(pers, txID, userContext).RunAndUnwrap();
        }

        public async Task<TransactionInfo> QueryTransactionByIDAsync(IEnumerable<Peer> pers, string txID, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(pers);
            userContext.UserContextCheck();
            if (txID == null)
                throw new ArgumentException("TxID parameter is null.");
            try
            {
                logger.Debug($"QueryTransactionByID with txID {txID}\n    from peer on channel {Name}");
                QuerySCCRequest querySCCRequest = new QuerySCCRequest(userContext);
                querySCCRequest.SetFcn(QuerySCCRequest.GETTRANSACTIONBYID);
                querySCCRequest.SetArgs(Name, txID);
                ProposalResponse proposalResponse = await SendProposalSeriallyAsync(querySCCRequest, pers, token).ConfigureAwait(false);
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
            return QueryChannelsAsync(peer).RunAndUnwrap();
        }

        public async Task<HashSet<string>> QueryChannelsAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            CheckPeer(peer);
            if (!IsSystemChannel)
                throw new ArgumentException("queryChannels should only be invoked on system channel.");
            try
            {
                TransactionContext context = GetTransactionContext();
                Proposal q = QueryPeerChannelsBuilder.Create().Context(context).Build();
                SignedProposal qProposal = GetSignedProposal(context, q);
                List<ProposalResponse> proposalResponses = await SendProposalToPeersAsync(new[] {peer}, qProposal, context, token).ConfigureAwait(false);
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
            catch (ProposalException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ProposalException($"Query for peer {peer.Name} channels failed. {e.Message}", e);
            }
        }

        public List<ChaincodeInfo> QueryInstalledChaincodes(Peer peer)
        {
            return QueryInstalledChaincodesAsync(peer).RunAndUnwrap();
        }

        public async Task<List<ChaincodeInfo>> QueryInstalledChaincodesAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            CheckPeer(peer);
            if (!IsSystemChannel)
                throw new ArgumentException("queryInstalledChaincodes should only be invoked on system channel.");
            try
            {
                TransactionContext context = GetTransactionContext();
                Proposal q = QueryInstalledChaincodesBuilder.Create().Context(context).Build();
                SignedProposal qProposal = GetSignedProposal(context, q);
                List<ProposalResponse> proposalResponses = await SendProposalToPeersAsync(new[] {peer}, qProposal, context, token).ConfigureAwait(false);
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
            catch (ProposalException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ProposalException($"Query for peer {peer.Name} channels failed. {e.Message}", e);
            }
        }

        /**
         * Query peer for chaincode that has been instantiated
         * 
         * <STRONG>This method may not be thread safe if client context is changed!</STRONG>
         * 
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
            return QueryInstantiatedChaincodesAsync(peer, userContext).RunAndUnwrap();
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
                List<ProposalResponse> proposalResponses = await SendProposalToPeersAsync(new[] {peer}, qProposal, context, token).ConfigureAwait(false);
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
            catch (ProposalException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ProposalException($"Query for peer {peer.Name} channels failed. {e.Message}", e);
            }
        }

        /**
         * Get information on the collections used by the chaincode.
         *
         * @param chaincodeName The name of the chaincode to query.
         * @param peer          Peer to query.
         * @param userContext   The context of the user to sign the request.
         * @return CollectionConfigPackage with information on the collection used by the chaincode.
         * @throws InvalidArgumentException
         * @throws ProposalException
         */
        public CollectionConfigPackage QueryCollectionsConfig(string chaincodeName, Peer peer, IUser userContext)
        {
            return QueryCollectionsConfigAsync(chaincodeName, peer, userContext).RunAndUnwrap();
        }

        public async Task<CollectionConfigPackage> QueryCollectionsConfigAsync(string chaincodeName, Peer peer, IUser userContext, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(chaincodeName))
                throw new ArgumentException("Parameter chaincodeName expected to be non null or empty string.");

            CheckChannelState();
            CheckPeer(peer);
            userContext.UserContextCheck();

            try
            {
                TransactionContext context = GetTransactionContext(userContext);
                QueryCollectionsConfigBuilder queryCollectionsConfigBuilder = QueryCollectionsConfigBuilder.Create();
                queryCollectionsConfigBuilder.Context(context).ChaincodeName(chaincodeName);
                Proposal q = queryCollectionsConfigBuilder.Build();

                SignedProposal qProposal = GetSignedProposal(context, q);
                List<ProposalResponse> proposalResponses = await SendProposalToPeersAsync(new[] {peer}, qProposal, context, token).ConfigureAwait(false);

                if (null == proposalResponses)
                    throw new ProposalException($"Peer {peer.Name} channel query return with null for responses");

                if (proposalResponses.Count != 1)
                    throw new ProposalException($"Peer {peer.Name} channel query expected one response but got back {proposalResponses.Count} responses ");

                ProposalResponse proposalResponse = proposalResponses.First();

                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse = proposalResponse.ProtoProposalResponse;
                if (null == fabricResponse)
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabric response");

                Response fabricResponseResponse = fabricResponse.Response;

                if (null == fabricResponseResponse) //not likely but check it.
                    throw new ProposalException($"Peer {peer.Name} channel query return with empty fabricResponseResponse");


                if (200 != fabricResponseResponse.Status)
                    throw new ProposalException($"Peer {peer.Name} channel query expected 200, actual returned was: {fabricResponseResponse.Status}. {fabricResponseResponse.Message}");
                return new CollectionConfigPackage(fabricResponseResponse.Payload);
            }
            catch (ProposalException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new ProposalException($"Query for peer {Name} channels failed. {e.Message}", e);
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
            return SendTransactionProposalAsync(transactionProposalRequest).RunAndUnwrap();
        }

        public Task<List<ProposalResponse>> SendTransactionProposalAsync(TransactionProposalRequest transactionProposalRequest, CancellationToken token = default(CancellationToken))
        {
            return SendProposalAsync(transactionProposalRequest, GetEndorsingPeers(), token);
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
        public List<ProposalResponse> SendTransactionProposal(TransactionProposalRequest transactionProposalRequest, IEnumerable<Peer> pers)
        {
            return SendTransactionProposalAsync(transactionProposalRequest, pers).RunAndUnwrap();
        }

        public Task<List<ProposalResponse>> SendTransactionProposalAsync(TransactionProposalRequest transactionProposalRequest, IEnumerable<Peer> pers, CancellationToken token = default(CancellationToken))
        {
            return SendProposalAsync(transactionProposalRequest, pers, token);
        }

        /**
        * Send a transaction  proposal.
        *
        * @param transactionProposalRequest The transaction proposal to be sent to all the required peers needed for endorsing.
        * @param discoveryOptions
        * @return responses from peers.
        * @throws InvalidArgumentException
        * @throws ProposalException
        */
        public List<ProposalResponse> SendTransactionProposalToEndorsers(TransactionProposalRequest transactionProposalRequest, DiscoveryOptions discoveryOptions)
        {
            return SendTransactionProposalToEndorsersAsync(transactionProposalRequest, discoveryOptions).RunAndUnwrap();
        }

        public async Task<List<ProposalResponse>> SendTransactionProposalToEndorsersAsync(TransactionProposalRequest transactionProposalRequest, DiscoveryOptions discoveryOptions, CancellationToken token = default(CancellationToken))
        {
            if (null == transactionProposalRequest)
                throw new ArgumentException("The proposalRequest is null");
            if (string.IsNullOrEmpty(transactionProposalRequest.Fcn))
                throw new ArgumentException("The proposalRequest's fcn is null or empty.");
            if (transactionProposalRequest.ChaincodeID == null)
                throw new ArgumentException("The proposalRequest's chaincode ID is null");
            string chaincodeName = transactionProposalRequest.ChaincodeID.Name;
            CheckChannelState();
            logger.Debug($"Channel {Name} sendTransactionProposalToEndorsers chaincode name: {chaincodeName}");
            TransactionContext transactionContext = GetTransactionContext(transactionProposalRequest.UserContext);
            transactionContext.Verify = transactionProposalRequest.DoVerify;
            transactionContext.ProposalWaitTime = transactionProposalRequest.ProposalWaitTime;

            // Protobuf message builder
            ProposalBuilder proposalBuilder = ProposalBuilder.Create();
            proposalBuilder.Context(transactionContext);
            proposalBuilder.Request(transactionProposalRequest);
            SignedProposal invokeProposal;
            try
            {
                invokeProposal = GetSignedProposal(transactionContext, proposalBuilder.Build());
            }
            catch (CryptoException e)
            {
                throw new ArgumentException(e.Message, e);
            }

            SDChaindcode sdChaindcode;
            List<ServiceDiscoveryChaincodeCalls> serviceDiscoveryChaincodeInterests = discoveryOptions.ServiceDiscoveryChaincodeInterests;

            if (null != serviceDiscoveryChaincodeInterests && serviceDiscoveryChaincodeInterests.Count > 0)
            {
                string firstname = serviceDiscoveryChaincodeInterests[0].Name;
                if (!firstname.Equals(chaincodeName))
                    serviceDiscoveryChaincodeInterests.Insert(0, new ServiceDiscoveryChaincodeCalls(chaincodeName));
                List<List<ServiceDiscoveryChaincodeCalls>> ccl = new List<List<ServiceDiscoveryChaincodeCalls>>();
                ccl.Add(serviceDiscoveryChaincodeInterests);
                Dictionary<string, SDChaindcode> sdChaindcodeMap = await serviceDiscovery.DiscoverEndorserEndpointsAsync(transactionContext, ccl, token).ConfigureAwait(false);
                if (sdChaindcodeMap == null)
                {
                    throw new ServiceDiscoveryException($"Channel {Name} failed doing service discovery for chaincode {chaincodeName}");
                }

                sdChaindcode = sdChaindcodeMap.GetOrNull(chaincodeName);
            }
            else
            {
                if (discoveryOptions.ForceDiscovery)
                {
                    logger.Trace("Forcing discovery.");
                    await serviceDiscovery.NetworkDiscoveryAsync(transactionContext, true, token).ConfigureAwait(false);
                }

                sdChaindcode = await serviceDiscovery.DiscoverEndorserEndpointAsync(transactionContext, chaincodeName, token).ConfigureAwait(false);
            }

            logger.Trace($"Channel {Name} chaincode {chaincodeName} discovered: {sdChaindcode?.ToString() ?? ""}");
            if (null == sdChaindcode)
            {
                throw new ServiceDiscoveryException($"Channel {Name} failed to find and endorsers for chaincode {chaincodeName}");
            }

            if (sdChaindcode.Layouts == null || sdChaindcode.Layouts.Count == 0)
            {
                throw new ServiceDiscoveryException($"Channel {Name} failed to find and endorsers for chaincode {chaincodeName} no layouts found.");
            }

            SDChaindcode sdChaindcodeEndorsementCopy = new SDChaindcode(sdChaindcode); //copy. no ignored.
            bool inspectResults = discoveryOptions.IsInspectResults;
            if (sdChaindcodeEndorsementCopy.IgnoreList(discoveryOptions.IgnoreList) < 1)
            {
                // apply ignore list
                throw new ServiceDiscoveryException("Applying ignore list reduced to no available endorser options.");
            }

            if (IS_TRACE_LEVEL && null != discoveryOptions.IgnoreList && discoveryOptions.IgnoreList.Count > 0)
            {
                logger.Trace($"SDchaincode after ignore list: {sdChaindcodeEndorsementCopy}");
            }

            Func<SDChaindcode, SDEndorserState> lendorsementSelector = discoveryOptions.EndorsementSelector ?? endorsementSelector;
            try
            {
                Dictionary<SDEndorser, ProposalResponse> goodResponses = new Dictionary<SDEndorser, ProposalResponse>(); // all good endorsements by endpoint
                Dictionary<SDEndorser, ProposalResponse> allTried = new Dictionary<SDEndorser, ProposalResponse>(); // all tried by endpoint
                bool done = false;
                int attempts = 1; //safety valve

                do
                {
                    if (IS_TRACE_LEVEL)
                    {
                        logger.Trace($"Attempts: {attempts},  chaincode discovery state: {sdChaindcodeEndorsementCopy}");
                    }

                    SDEndorserState sdEndorserState = lendorsementSelector(sdChaindcodeEndorsementCopy);

                    if (IS_TRACE_LEVEL)
                    {
                        StringBuilder sb = new StringBuilder(1000);
                        string sep = "";
                        foreach (SDEndorser sdEndorser in sdEndorserState.SDEndorsers)
                        {
                            sb.Append(sep).Append(sdEndorser);
                            sep = ", ";
                        }

                        logger.Trace($"Attempts: {attempts},  chaincode discovery state: {sdChaindcodeEndorsementCopy.Name}. Endorser selector picked: {sdEndorserState.PickedLayout}. With selected endorsers: {sb}");
                    }

                    List<SDEndorser> ep = sdEndorserState.SDEndorsers.ToList();
                    if (IS_TRACE_LEVEL)
                    {
                        StringBuilder sb = new StringBuilder(1000);
                        string sep = "";
                        foreach (SDEndorser sdEndorser in ep)
                        {
                            sb.Append(sep).Append(sdEndorser);
                            sep = ", ";
                        }

                        logger.Trace($"Channel {Name}, chaincode {chaincodeName} attempts: {attempts} requested endorsements: {sb}");
                    }

                    //Safety check make sure the selector isn't giving back endpoints to retry
                    foreach (SDEndorser sdEndorser in ep.ToList())
                    {
                        if (goodResponses.Keys.Contains(sdEndorser))
                            ep.Remove(sdEndorser);
                    }

                    if (ep.Count == 0)
                    {
                        // this would be odd but lets go with it.
                        logger.Debug($"Channel {Name}, chaincode {chaincodeName} attempts: {attempts} endorser selector returned no additional endorements needed.");
                        List<SDEndorser> needed = sdChaindcode.MeetsEndorsmentPolicy(goodResponses.Keys);
                        if (needed != null)
                        {
                            // means endorsment meet with those in the needed.
                            List<ProposalResponse> ret = needed.Select(a => goodResponses[a]).ToList();
                            if (IS_DEBUG_LEVEL)
                            {
                                StringBuilder sb = new StringBuilder(1000);
                                string sep = "";
                                foreach (ProposalResponse proposalResponse in ret)
                                {
                                    sb.Append(sep).Append(proposalResponse.Peer);
                                    sep = ", ";
                                }

                                logger.Debug($"Channel {Name}, chaincode {chaincodeName} attempts: {attempts} got all needed endorsements: {sb}");
                            }

                            return ret; // the happy path :)!
                        }
                        else
                        {
                            //still don't have the needed endorsements.

                            logger.Debug($"Channel {Name}, chaincode {chaincodeName} attempts: {attempts} missing needed endorsements");

                            if (inspectResults)
                            {
                                return allTried.Values.ToList();
                            }
                            else
                            {
                                throw new ServiceDiscoveryException($"Could not meet endorsement policy for chaincode {chaincodeName}");
                            }
                        }
                    }

                    Dictionary<string, Peer> lpeerEndpointMap = new Dictionary<string, Peer>(peerEndpointMap);
                    Dictionary<SDEndorser, Peer> endorsers = new Dictionary<SDEndorser, Peer>(ep.Count);
                    Dictionary<ExactMatch<Peer>, SDEndorser> peer2sdEndorser = new Dictionary<ExactMatch<Peer>, SDEndorser>(ep.Count);
                    foreach (SDEndorser sdEndorser in ep)
                    {
                        Peer epeer = lpeerEndpointMap.GetOrNull(sdEndorser.Endpoint);
                        if (epeer != null && !epeer.HasConnected)
                        {
                            // mostly because gossip may have malicious data so if we've not connected update TLS props from chaincode discovery.
                            Properties properties = epeer.Properties;
                            byte[] bytes = sdEndorser.GetAllTLSCerts();
                            properties.Set("pemBytes", bytes);
                            epeer.Properties = properties;
                        }
                        else if (null == epeer)
                        {
                            epeer = await sdEndorser.AddAsync(ServiceDiscoveryProperties, token).ConfigureAwait(false);
                        }

                        endorsers.Add(sdEndorser, epeer);
                        peer2sdEndorser.Add(new ExactMatch<Peer>(epeer), sdEndorser); // reverse
                    }

                    List<ProposalResponse> proposalResponses = await SendProposalToPeersAsync(endorsers.Values, invokeProposal, transactionContext, token).ConfigureAwait(false);
                    HashSet<SDEndorser> loopGood = new HashSet<SDEndorser>();
                    HashSet<SDEndorser> loopBad = new HashSet<SDEndorser>();

                    foreach (ProposalResponse proposalResponse in proposalResponses)
                    {
                        SDEndorser sdEndorser = peer2sdEndorser.GetOrNull(new ExactMatch<Peer>(proposalResponse.Peer));
                        allTried[sdEndorser] = proposalResponse;
                        ChaincodeResponse.ChaincodeResponseStatus status = proposalResponse.Status;

                        if (status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                        {
                            goodResponses[sdEndorser] = proposalResponse;
                            logger.Trace($"Channel {Name}, chaincode {chaincodeName} attempts {attempts} good endorsements: {sdEndorser}");
                            loopGood.Add(sdEndorser);
                        }
                        else
                        {
                            logger.Debug($"Channel {Name}, chaincode {chaincodeName} attempts {attempts} bad endorsements: {sdEndorser}");
                            loopBad.Add(sdEndorser);
                        }
                    }

                    //Always check on original
                    List<SDEndorser> required = sdChaindcode.MeetsEndorsmentPolicy(goodResponses.Keys);
                    if (required != null)
                    {
                        List<ProposalResponse> ret = new List<ProposalResponse>(required.Count);
                        required.ForEach(s => ret.Add(goodResponses.GetOrNull(s)));

                        if (IS_DEBUG_LEVEL)
                        {
                            StringBuilder sb = new StringBuilder(1000);
                            string sep = "";
                            foreach (ProposalResponse proposalResponse in ret)
                            {
                                sb.Append(sep).Append(proposalResponse.Peer);
                                sep = ", ";
                            }

                            logger.Debug($"Channel {Name}, chaincode {chaincodeName} got all needed endorsements: {sb}");
                        }

                        return ret; // the happy path :)!
                    }
                    else
                    {
                        //still don't have the needed endorsements.

                        sdChaindcodeEndorsementCopy.EndorsedList(loopGood); // mark the good ones in the working copy.

                        if (sdChaindcodeEndorsementCopy.IgnoreListSDEndorser(loopBad) < 1)
                        {
                            // apply ignore list
                            done = true; // no more layouts
                        }
                    }
                } while (!done && ++attempts <= 5);

                logger.Debug($"Endorsements not achieved chaincode: {chaincodeName}, done: {done}, attempts: {attempts}");
                if (inspectResults)
                {
                    return allTried.Values.ToList();
                }
                else
                {
                    throw new ServiceDiscoveryException($"Could not meet endorsement policy for chaincode {chaincodeName}");
                }
            }
            catch (ProposalException)
            {
                throw;
            }
            catch (Exception e)
            {
                ProposalException exp = new ProposalException(e);
                logger.Error(exp.Message, exp);
                throw exp;
            }
        }

        /**
     * Collection of discovered chaincode names.
     *
     * @return
     */
        public List<string> GetDiscoveredChaincodeNames()
        {
            return serviceDiscovery.GetDiscoveredChaincodeNamesAsync().RunAndUnwrap();
        }

        public Task<List<string>> GetDiscoveredChaincodeNamesAsync(CancellationToken token = default(CancellationToken))
        {
            if (serviceDiscovery == null)
                return Task.FromResult(new List<string>());
            return serviceDiscovery.GetDiscoveredChaincodeNamesAsync(token);
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
        public List<ProposalResponse> QueryByChaincode(QueryByChaincodeRequest queryByChaincodeRequest, IEnumerable<Peer> pers)
        {
            return QueryByChaincodeAsync(queryByChaincodeRequest, pers).RunAndUnwrap();
        }

        public Task<List<ProposalResponse>> QueryByChaincodeAsync(QueryByChaincodeRequest queryByChaincodeRequest, IEnumerable<Peer> pers, CancellationToken token = default(CancellationToken))
        {
            return SendProposalAsync(queryByChaincodeRequest, pers, token);
        }
        ////////////////  Channel Block monitoring //////////////////////////////////

        private async Task<ProposalResponse> SendProposalSeriallyAsync(TransactionRequest proposalRequest, IEnumerable<Peer> pers, CancellationToken token)
        {
            ProposalException lastException = new ProposalException("ProposalRequest failed.");
            foreach (Peer peer in pers)
            {
                proposalRequest.IsSubmitted = false;
                try
                {
                    List<ProposalResponse> proposalResponses = await SendProposalAsync(proposalRequest, new[] {peer}, token).ConfigureAwait(false);
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


        private async Task<List<ProposalResponse>> SendProposalAsync(TransactionRequest proposalRequest, IEnumerable<Peer> peerarray, CancellationToken token = default(CancellationToken))
        {
            CheckChannelState();
            CheckPeers(peerarray);
            if (null == proposalRequest)
                throw new ArgumentException("The proposalRequest is null");
            if (string.IsNullOrEmpty(proposalRequest.Fcn))
                throw new ArgumentException("The proposalRequest's fcn is null or empty.");
            if (proposalRequest.ChaincodeID == null)
                throw new ArgumentException("The proposalRequest's chaincode ID is null");
            proposalRequest.SetSubmitted();
            try
            {
                TransactionContext transactionContext = GetTransactionContext(proposalRequest.UserContext);
                transactionContext.Verify = proposalRequest.DoVerify;
                transactionContext.ProposalWaitTime = proposalRequest.ProposalWaitTime;
                // Protobuf message builder
                Proposal proposal = ProposalBuilder.Create().Context(transactionContext).Request(proposalRequest).Build();
                SignedProposal invokeProposal = GetSignedProposal(transactionContext, proposal);
                return await SendProposalToPeersAsync(peerarray, invokeProposal, transactionContext, token).ConfigureAwait(false);
            }
            catch (ProposalException)
            {
                throw;
            }
            catch (Exception e)
            {
                ProposalException exp = new ProposalException(e);
                logger.ErrorException(exp.Message, exp);
                throw exp;
            }
        }

        public Func<SDChaindcode, SDEndorserState> SetSDEndorserSelector(Func<SDChaindcode, SDEndorserState> endorsmentSelector)
        {
            Func<SDChaindcode, SDEndorserState> ret = endorsementSelector;
            endorsementSelector = endorsmentSelector;
            return ret;
        }

        private async Task<List<ProposalResponse>> SendProposalToPeersAsync(IEnumerable<Peer> pers, SignedProposal signedProposal, TransactionContext transactionContext, CancellationToken token = default(CancellationToken))
        {
            CheckPeers(pers);
            List<ProposalResponse> responses = new List<ProposalResponse>();
            if (transactionContext.Verify)
            {
                try
                {
                    await LoadCACertificatesAsync(false, token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    throw new ProposalException(e);
                }
            }

            string txID = transactionContext.TxID;

            Dictionary<Peer, Task<Protos.Peer.FabricProposalResponse.ProposalResponse>> tasks = new Dictionary<Peer, Task<Protos.Peer.FabricProposalResponse.ProposalResponse>>();
            using (CancellationTokenSource stoken = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                foreach (Peer peer in pers)
                {
                    logger.Debug($"Channel {Name} send proposal to {peer}, txID: {txID}");
                    if (null != diagnosticFileDumper)
                        logger.Trace($"Sending to channel {Name}, peer: {peer.Name}, proposal: {diagnosticFileDumper.CreateDiagnosticProtobufFile(signedProposal.ToByteArray())}, txID: {txID}");
                    tasks.Add(peer, peer.SendProposalAsync(signedProposal, stoken.Token));
                }

                using (var timeoutCancellationTokenSource = new CancellationTokenSource())
                {
                    try
                    {
                        await Task.WhenAll(tasks.Values).TimeoutAsync(TimeSpan.FromMilliseconds((int) transactionContext.ProposalWaitTime), token).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        //ignored, processed below
                    }
                }
            }

            token.ThrowIfCancellationRequested();
            foreach (Peer peer in tasks.Keys)
            {
                Task<Protos.Peer.FabricProposalResponse.ProposalResponse> ctask = tasks[peer];
                Protos.Peer.FabricProposalResponse.ProposalResponse fabricResponse;
                string message;
                int status;
                string peerName = peer.ToString();

                if (ctask.IsFaulted)
                {
                    AggregateException ex = ctask.Exception;
                    Exception e = ex?.InnerException ?? ex;
                    if (e is RpcException)
                    {
                        RpcException rpce = (RpcException) e;
                        message = $"Sending proposal with transaction: {txID} to {peerName} failed because of: gRPC failure={rpce.Status}";
                    }
                    else
                        message = $"Sending proposal to {peerName} with transaction {txID} failed because of: {e?.Message}";

                    logger.ErrorException(message, e);
                    if (e != null) throw e;
                }
                if (ctask.IsCanceled)
                {
                    message = $"Sending proposal to {peerName} with transaction {txID} failed because of timeout({transactionContext.ProposalWaitTime} milliseconds) expiration";
                    logger.Error(message);
                }
                else if (ctask.IsCompleted)
                {
                    fabricResponse = ctask.Result;
                    message = fabricResponse.Response.Message;
                    status = fabricResponse.Response.Status;
                    peer.HasConnected = true;
                    logger.Debug($"Channel {Name}, transaction: {txID} got back from peer {peerName} status: {status}, message: {message}");
                    if (null != diagnosticFileDumper)
                        logger.Trace($"Got back from channel {Name}, peer: {peerName}, proposal response: {diagnosticFileDumper.CreateDiagnosticProtobufFile(fabricResponse.ToByteArray())}");
                    ProposalResponse proposalResponse = new ProposalResponse(transactionContext, status, message);
                    proposalResponse.ProtoProposalResponse = fabricResponse;
                    proposalResponse.SetProposal(signedProposal);
                    proposalResponse.Peer = peer;
                    if (transactionContext.Verify)
                        proposalResponse.Verify(client.CryptoSuite);
                    responses.Add(proposalResponse);
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
        public TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, IUser userContext, int? waittimeinmilliseconds = 10000)
        {
            return SendTransaction(proposalResponses, Orderers, userContext, waittimeinmilliseconds);
        }

        public Task<TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, IUser userContext, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            return SendTransactionAsync(proposalResponses, Orderers, userContext, waittimeinmilliseconds, token);
        }

        /**
         * Send transaction to one of the orderers on the channel using the usercontext set on the client.
         *
         * @param proposalResponses .
         * @return a future allowing access to the result of the transaction invocation once complete.
         */
        public TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, int? waittimeinmilliseconds = 10000)
        {
            return SendTransaction(proposalResponses, Orderers, waittimeinmilliseconds);
        }

        public Task<TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            return SendTransactionAsync(proposalResponses, Orderers, waittimeinmilliseconds, token);
        }

        /**
         * Send transaction to one of the specified orderers using the usercontext set on the client..
         *
         * @param proposalResponses The proposal responses to be sent to the orderer
         * @param orderers          The orderers to send the transaction to.
         * @return a future allowing access to the result of the transaction invocation once complete.
         */
        public TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> ordrers, int? waittimeinmilliseconds = 10000)
        {
            return SendTransaction(proposalResponses, Orderers, client.UserContext, waittimeinmilliseconds);
        }

        public Task<TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> ordrers, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            return SendTransactionAsync(proposalResponses, Orderers, client.UserContext, waittimeinmilliseconds, token);
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
        public TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> ordrers, IUser userContext, int? waittimeinmilliseconds = 10000)
        {
            return SendTransaction(proposalResponses, TransactionOptions.Create().SetOrderers(ordrers).SetUserContext(userContext), waittimeinmilliseconds);
        }

        public Task<TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, IEnumerable<Orderer> ordrers, IUser userContext, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            return SendTransactionAsync(proposalResponses, TransactionOptions.Create().SetOrderers(ordrers).SetUserContext(userContext), waittimeinmilliseconds, token);
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
        public TransactionEvent SendTransaction(IEnumerable<ProposalResponse> proposalResponses, TransactionOptions transactionOptions, int? waittimeinmilliseconds = 10000)
        {
            return SendTransactionAsync(proposalResponses, transactionOptions, waittimeinmilliseconds).RunAndUnwrap();
        }

        public async Task<TransactionEvent> SendTransactionAsync(IEnumerable<ProposalResponse> proposalResponses, TransactionOptions transactionOptions, int? waittimeinmilliseconds = 10000, CancellationToken token = default(CancellationToken))
        {
            try
            {
                if (null == transactionOptions)
                    throw new ArgumentException("Parameter transactionOptions can't be null");
                CheckChannelState();
                IUser userContext = transactionOptions.UserContext ?? client.UserContext;
                userContext.UserContextCheck();
                if (null == proposalResponses)
                    throw new ArgumentException("sendTransaction proposalResponses was null");
                List<Orderer> orders = transactionOptions.Orderers ?? Orderers.ToList();
                // make certain we have our own copy
                List<Orderer> shuffeledOrderers = orders.Shuffle().ToList();
                if (Config.Instance.GetProposalConsistencyValidation())
                {
                    HashSet<ProposalResponse> invalid = new HashSet<ProposalResponse>();
                    int consistencyGroups = SDKUtils.GetProposalConsistencySets(proposalResponses.ToList(), invalid).Count;
                    if (consistencyGroups != 1 || invalid.Count > 0)
                        throw new ArgumentException($"The proposal responses have {consistencyGroups} inconsistent groups with {invalid.Count} that are invalid." + " Expected all to be consistent and none to be invalid.");
                }

                List<Endorsement> ed = new List<Endorsement>();
                Proposal proposal = null;
                ByteString proposalResponsePayload = null;
                string proposalTransactionID = null;
                TransactionContext transactionContext = null;

                foreach (ProposalResponse sdkProposalResponse in proposalResponses)
                {
                    ed.Add(sdkProposalResponse.ProtoProposalResponse.Endorsement);
                    if (proposal == null)
                    {
                        proposal = sdkProposalResponse.Proposal;
                        proposalTransactionID = sdkProposalResponse.TransactionID;
                        if (proposalTransactionID == null)
                            throw new ArgumentException("Proposals with missing transaction ID");
                        proposalResponsePayload = sdkProposalResponse.ProtoProposalResponse.Payload;
                        if (proposalResponsePayload == null)
                            throw new ArgumentException("Proposals with missing payload.");
                        transactionContext = sdkProposalResponse.TransactionContext;
                        if (transactionContext == null)
                            throw new ArgumentException("Proposals with missing transaction context.");
                    }
                    else
                    {
                        string transactionID = sdkProposalResponse.TransactionID;
                        if (transactionID == null)
                            throw new ArgumentException("Proposals with missing transaction id.");
                        if (!proposalTransactionID.Equals(transactionID))
                            throw new ArgumentException($"Proposals with different transaction IDs {proposalTransactionID},  and {transactionID}");
                    }
                }

                Payload transactionPayload = TransactionBuilder.Create().ChaincodeProposal(proposal).Endorsements(ed).ProposalResponsePayload(proposalResponsePayload).Build();
                Envelope transactionEnvelope = CreateTransactionEnvelope(transactionPayload, transactionContext);
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

                    IReadOnlyList<EventHub> eventhbs = EventHubs;
                    if (eventhbs.Count > 0)
                    {
                        anyAdded = true;
                        nOfEvents.AddEventHubs(eventhbs);
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
                        throw new ArgumentException(foundIssues);
                }

                bool replyonly = nOfEvents == NOfEvents.NofNoEvents || EventHubs.Count == 0 && GetEventingPeers().Count == 0;
                TaskCompletionSource<TransactionEvent> sret = null;
                if (replyonly)
                {
                    //If there are no eventhubs to complete the future, complete it
                    // immediately but give no transaction event
                    logger.Debug($"Completing transaction id {proposalTransactionID} immediately no event hubs or peer eventing services found in channel {Name}.");
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
                        resp = await orderer.SendTransactionAsync(transactionEnvelope, token).ConfigureAwait(false);
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
                            try
                            {
                                var completedTask = await Task.WhenAny(sret.Task, Task.Delay(waittimeinmilliseconds.Value, timeoutCancellationTokenSource.Token)).ConfigureAwait(false);
                                if (completedTask == sret.Task)
                                    timeoutCancellationTokenSource.Cancel();
                                else
                                {
                                    UnregisterTxListener(proposalTransactionID);
                                    Exception ex = new OperationCanceledException("The operation has timed out.");
                                    throw new TransactionException(ex.Message, ex);
                                }
                            }
                            catch (Exception)
                            {
                                //Ignored processed below
                            }
                        }
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

                    if (sret.Task.IsCanceled)
                    {
                        token.ThrowIfCancellationRequested();
                        Exception ex2 = new OperationCanceledException("The operation has timed out.");
                        throw new TransactionException(ex2.Message, ex2);
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
                throw;
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

        private Envelope CreateTransactionEnvelope(Payload transactionPayload, TransactionContext transactionContext)
        {
            return new Envelope {Payload = transactionPayload.ToByteString(), Signature = ByteString.CopyFrom(transactionContext.Sign(transactionPayload.ToByteArray()))};
        }

        public byte[] GetChannelConfigurationSignature(ChannelConfiguration channelConfiguration, IUser signer)
        {
            signer.UserContextCheck();
            if (null == channelConfiguration)
                throw new ArgumentException("channelConfiguration is null");
            try
            {
                Envelope ccEnvelope = Envelope.Parser.ParseFrom(channelConfiguration.ChannelConfigurationBytes);
                Payload ccPayload = Payload.Parser.ParseFrom(ccEnvelope.Payload);
                TransactionContext transactionContext = GetTransactionContext(signer);
                ConfigUpdateEnvelope configUpdateEnv = ConfigUpdateEnvelope.Parser.ParseFrom(ccPayload.Data);
                ByteString configUpdate = configUpdateEnv.ConfigUpdate;
                ByteString sigHeaderByteString = ProtoUtils.GetSignatureHeaderAsByteString(signer, transactionContext);
                ByteString signatureByteSting = transactionContext.SignByteStrings(new[] {signer}, sigHeaderByteString, configUpdate)[0];
                return new ConfigSignature {SignatureHeader = sigHeaderByteString, Signature = signatureByteSting}.ToByteArray();
            }
            catch (Exception e)
            {
                throw new ArgumentException(e.Message, e);
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
                throw new ArgumentException($"Channel {Name} has been shutdown.");
            return new BlockListener(this, listenerAction).Handle;
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
                throw new ArgumentException($"Channel {Name} has been shutdown.");
            CheckHandle(BlockListener.BLOCK_LISTENER_TAG, handle);
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

        private void StartEventQue()
        {
            if (eventQueueTokenSource != null)
                return;
            eventQueueTokenSource = new CancellationTokenSource();
            CancellationToken ct = eventQueueTokenSource.Token;

            eventQueueThread = Task.Run(() =>
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
                        string tp = blockEvent.Peer != null ? "" + blockEvent.Peer : "" + blockEvent.EventHub;
                        string from = $"Channel {Name} eventqueue got block event with block number: {blockEvent.BlockNumber} for channel: {blockchainID}, from {tp}";
                        logger.Trace(from);
                        if (!Name.Equals(blockchainID))
                        {
                            logger.Warn($"Channel {Name} eventqueue got block event NOT FOR ME  channelId {blockchainID} from {from}");
                            continue; // not targeted for this channel
                        }

                        List<BlockListener> blcopy = new List<BlockListener>();
                        lock (blockListeners)
                        {
                            blcopy.AddRange(blockListeners.Values);
                        }

                        foreach (BlockListener l in blcopy)
                        {
                            try
                            {
                                logger.Trace($"Sending block event '{from}' to block listener {l.Handle}");
                                Task.Run(() => { l.ListenerAction(blockEvent); }, ct);
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

                logger.Info($"Channel {Name} eventThread shutting down. shutdown: {IsShutdown}  task: {eventQueueThread.Id.ToString()}");
            }, ct);
        }

        private string RegisterTransactionListenerProcessor()
        {
            logger.Debug($"Channel {Name} registerTransactionListenerProcessor starting");
            // Transaction listener is internal Block listener for transactions
            return RegisterBlockListener(TransactionBlockReceived);
        }

        internal void RunSweeper()
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

        private TaskCompletionSource<TransactionEvent> RegisterTxListener(string txid, NOfEvents nOfEvents, bool failFast)
        {
            TaskCompletionSource<TransactionEvent> future = new TaskCompletionSource<TransactionEvent>();
            var _ = new TransactionListener(this, txid, future, nOfEvents, failFast);
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
                throw new ArgumentException($"Channel {Name} has been shutdown.");
            if (chaincodeId == null)
                throw new ArgumentException("The chaincodeId argument may not be null.");
            if (eventName == null)
                throw new ArgumentException("The eventName argument may not be null.");
            if (listenerAction == null)
                throw new ArgumentException("The chaincodeListenerAction argument may not be null.");
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
                throw new ArgumentException($"Channel {Name} has been shutdown.");
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
            string ltransactionListenerProcessorHandle = transactionListenerProcessorHandle;
            transactionListenerProcessorHandle = null;
            if (null != ltransactionListenerProcessorHandle)
            {
                try
                {
                    UnregisterBlockListener(ltransactionListenerProcessorHandle);
                }
                catch (Exception e)
                {
                    logger.Error($"Shutting down channel {Name} transactionListenerProcessorHandle", e);
                }
            }

            string lchaincodeEventUpgradeListenerHandle = chaincodeEventUpgradeListenerHandle;
            chaincodeEventUpgradeListenerHandle = null;
            if (null != lchaincodeEventUpgradeListenerHandle)
            {
                try
                {
                    UnregisterChaincodeEventListener(lchaincodeEventUpgradeListenerHandle);
                }
                catch (Exception e)
                {
                    logger.ErrorException($"Shutting down channel {Name} chaincodeEventUpgradeListener", e);
                }
            }

            initialized = false;
            IsShutdown = true;
            if (chainCodeListeners != null)
            {
                lock (chainCodeListeners)
                {
                    chainCodeListeners.Clear();
                }
            }

            if (blockListeners != null)
            {
                lock (blockListeners)
                {
                    blockListeners.Clear();
                }
            }

            client?.RemoveChannel(this);
            client = null;
            foreach (EventHub eh in eventHubs)
            {
                try
                {
                    eh.Shutdown();
                }
                catch (Exception)
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
                catch (Exception)
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
            HFClient lclient = client;
            if (null == lclient || IsShutdown)
            {
                //can happen if were not quite shutdown
                return;
            }

            string source = blockEvent.Peer != null ? blockEvent.Peer.ToString() : blockEvent.EventHub != null ? blockEvent.EventHub.ToString() : "not peer or eventhub!";


            logger.Debug($"is peer {blockEvent.Peer != null}, is filtered: {blockEvent.IsFiltered}");
            List<TransactionEvent> transactionEvents = blockEvent.TransactionEvents?.ToList() ?? new List<TransactionEvent>();

            if (transactionEvents.Count == 0)
            {
                // no transactions today we can assume it was a config or update block.

                if (IsLaterBlock(blockEvent.BlockNumber))
                {
                    ServiceDiscovery lserviceDiscovery = serviceDiscovery;
                    if (null != lserviceDiscovery)
                        Task.Run(() => lserviceDiscovery.FullNetworkDiscoveryAsync(true));
                }
                else
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            if (!IsShutdown)
                            {
                                await LoadCACertificatesAsync(true, default(CancellationToken)).ConfigureAwait(false);
                            }
                        }
                        catch (Exception e)
                        {
                            logger.Warn($"Channel {Name} failed to load certificates for an update", e);
                        }
                    });
                }

                return;
            }


            lock (txListeners)
            {
                if (txListeners.Count == 0 || IsShutdown || blockEvent.TransactionEvents==null)
                    return;
            }
            foreach (TransactionEvent transactionEvent in blockEvent.TransactionEvents)
            {
                logger.Debug($"Channel {Name} got event from {source} for transaction {transactionEvent.TransactionID} in block number: {blockEvent.BlockNumber}");
                List<TransactionListener> txL = new List<TransactionListener>();
                lock (txListeners)
                {
                    LinkedList<TransactionListener> list = txListeners[transactionEvent.TransactionID];
                    if (null != list)
                    {
                        txL.AddRange(list);
                    }
                }

                foreach (TransactionListener l in txL)
                {
                    try
                    {
                        // only if we get events from each eventhub on the channel fire the transactions event.
                        //   if (getEventHubs().containsAll(l.eventReceived(transactionEvent.getEventHub()))) {
                        if (IsShutdown)
                        {
                            break;
                        }

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

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool IsLaterBlock(long blockno)
        {
            if (blockno > lastBlock)
            {
                lastBlock = blockno;
                return true;
            }

            return false;
        }

        internal void ChaincodeBlockReceived(BlockEvent blockEvent)
        {
            lock (chainCodeListeners)
            {
                if (chainCodeListeners.Count == 0)
                    return;
            }

            List<ChaincodeEventDeserializer> chaincodeEvents = new List<ChaincodeEventDeserializer>();
            //Find the chaincode events in the transactions.
            foreach (TransactionEvent transactionEvent in blockEvent.TransactionEvents)
            {
                logger.Debug($"Channel {Name} got event for transaction {transactionEvent.TransactionID}");
                foreach (TransactionActionInfo info in transactionEvent.TransactionActionInfos)
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

        public class AnchorPeersConfigUpdateResult
        {
            /**
             * The actual config update @see {@link UpdateChannelConfiguration}
             *
             * @return The config update. May be null when there is an error on no change needs to be done.
             */
            public UpdateChannelConfiguration UpdateChannelConfiguration { get; set; }

            /**
             * The peers to be added.
             *
             * @return The anchor peers to be added. This is less any that may be already present.
             */
            public List<string> PeersAdded { get; set; } = new List<string>();

            /**
             * The peers to be removed..
             *
             * @return The anchor peers to be removed. This is less any peers not present.
             */
            public List<string> PeersRemoved { get; set; } = new List<string>();

            /**
             * The anchor peers found in the current channel configuration.
             *
             * @return The anchor peers found in the current channel configuration.
             */
            public List<string> CurrentPeers { get; set; } = new List<string>();

            /**
             * The anchor peers found in the updated channel configuration.
             */
            public List<string> UpdatedPeers { get; set; } = new List<string>();


            public override string ToString()
            {
                StringBuilder sb = new StringBuilder(10000);
                sb.Append("AnchorPeersConfigUpdateResult:{peersAdded= ");
                sb.Append(PeersAdded == null ? "null" : string.Join(",", PeersAdded));
                sb.Append(", peersRemoved= ");
                sb.Append(PeersRemoved == null ? "null" : string.Join(",", PeersRemoved));
                sb.Append(", currentPeers= ");
                sb.Append(CurrentPeers == null ? "null" : string.Join(",", CurrentPeers));
                sb.Append(", updatedPeers= ");
                sb.Append(UpdatedPeers == null ? "null" : string.Join(",", UpdatedPeers));
                sb.Append(", updateChannelConfiguration= ");
                sb.Append(UpdateChannelConfiguration == null ? "null" : UpdateChannelConfiguration.UpdateChannelConfigurationBytes.ToHexString());
                sb.Append("}");
                return sb.ToString();
            }
        }
    }
}