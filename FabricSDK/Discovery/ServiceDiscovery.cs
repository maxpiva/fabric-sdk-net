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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Discovery;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.Protos.Msp.MspConfig;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Nito.AsyncEx;


namespace Hyperledger.Fabric.SDK.Discovery
{
    public class ServiceDiscovery
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(ServiceDiscovery));
        private static readonly bool IS_DEBUG_LEVEL = logger.IsDebugEnabled();
        private static readonly Config config = Config.Instance;
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();
        private static readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL ? config.GetDiagnosticFileDumper() : null;
        private static readonly int SERVICE_DISCOVERY_WAITTIME = config.GetServiceDiscoveryWaitTime();
        private static readonly Random random = new Random();
        private static readonly int SERVICE_DISCOVER_FREQ_SECONDS = config.GetServiceDiscoveryFreqSeconds();
        public static Func<SDChaindcode, SDEndorserState> DEFAULT_ENDORSEMENT_SELECTION = ENDORSEMENT_SELECTION_LEAST_REQUIRED_BLOCKHEIGHT;
        private readonly ConcurrentHashSet<ByteString> certs = new ConcurrentHashSet<ByteString>();
        private readonly Channel channel;
        private readonly string channelName;
        private readonly AsyncLock discoverLock = new AsyncLock();
        private readonly List<Peer> serviceDiscoveryPeers;
        private readonly TransactionContext transactionContext;
        private Dictionary<string, SDChaindcode> chaindcodeMap = new Dictionary<string, SDChaindcode>();
        private volatile SDNetwork sdNetwork;
        private Timer timer;


        public ServiceDiscovery(Channel channel, IEnumerable<Peer> serviceDiscoveryPeers, TransactionContext transactionContext)
        {
            this.serviceDiscoveryPeers = serviceDiscoveryPeers.ToList();
            this.channel = channel;
            channelName = channel.Name;
            this.transactionContext = transactionContext.RetryTransactionSameContext();
        }

        public async Task<SDChaindcode> DiscoverEndorserEndpointAsync(TransactionContext tContext, string name, CancellationToken token = default(CancellationToken))
        {
            Dictionary<string, SDChaindcode> lchaindcodeMap = chaindcodeMap;
            // check if we have it already.
            SDChaindcode sd = lchaindcodeMap?.GetOrNull(name);
            if (sd != null)
                return sd;
            Channel.ServiceDiscoveryChaincodeCalls serviceDiscoveryChaincodeCalls = new Channel.ServiceDiscoveryChaincodeCalls(name);
            List<Channel.ServiceDiscoveryChaincodeCalls> cc = new List<Channel.ServiceDiscoveryChaincodeCalls>();
            cc.Add(serviceDiscoveryChaincodeCalls);
            List<List<Channel.ServiceDiscoveryChaincodeCalls>> ccl = new List<List<Channel.ServiceDiscoveryChaincodeCalls>>();
            ccl.Add(cc);
            Dictionary<string, SDChaindcode> dchaindcodeMap = await DiscoverEndorserEndpointsAsync(tContext, ccl, token).ConfigureAwait(false);
            SDChaindcode sdChaindcode = dchaindcodeMap.GetOrNull(name);
            if (null == sdChaindcode)
                throw new ServiceDiscoveryException($"Failed to find and endorsers for chaincode {name}. See logs for details");
            return sdChaindcode;
        }

        public async Task<List<string>> GetDiscoveredChaincodeNamesAsync(CancellationToken token = default(CancellationToken))
        {
            SDNetwork lsdNetwork = await FullNetworkDiscoveryAsync(false, token).ConfigureAwait(false);
            if (null == lsdNetwork)
                return new List<string>();
            return new List<string>(lsdNetwork.ChaincodesNames);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void AddCerts(HashSet<ByteString> cbbs, List<byte[]> toaddCerts)
        {
            cbbs.ToList().ForEach(bytes =>
            {
                if (certs.TryAdd(bytes))
                {
                    toaddCerts.Add(bytes.ToByteArray());
                }
            });
        }


        public async Task<SDNetwork> NetworkDiscoveryAsync(TransactionContext ltransactionContext, bool force, CancellationToken token = default(CancellationToken))
        {
            using (await discoverLock.LockAsync(token).ConfigureAwait(false))
            {
                logger.Trace($"Network discovery force: {force}");
                List<Peer> speers = serviceDiscoveryPeers.Shuffle().ToList();
                SDNetwork ret = sdNetwork;
                if (!force && null != ret && !ret.TimeElapsed)
                    return ret;
                ret = null;
                foreach (Peer serviceDiscoveryPeer in speers)
                {
                    try
                    {
                        SDNetwork lsdNetwork = new SDNetwork(SERVICE_DISCOVER_FREQ_SECONDS);
                        byte[] clientTLSCertificateDigest = serviceDiscoveryPeer.GetClientTLSCertificateDigest();
                        logger.Info($"Channel {channelName} doing discovery with peer: {serviceDiscoveryPeer}");
                        if (null == clientTLSCertificateDigest)
                            throw new ArgumentException($"Channel {channelName}, peer {serviceDiscoveryPeer} requires mutual tls for service discovery.");
                        ByteString clientIdent = ltransactionContext.Identity.ToByteString();
                        ByteString tlshash = ByteString.CopyFrom(clientTLSCertificateDigest);
                        AuthInfo authentication = new AuthInfo();
                        authentication.ClientIdentity = clientIdent;
                        authentication.ClientTlsCertHash = tlshash;
                        List<Query> fq = new List<Query>(2);
                        Query cq = new Query();
                        Query pq = new Query();
                        cq.Channel = channelName;
                        pq.Channel = channelName;
                        cq.ConfigQuery = new ConfigQuery();
                        pq.PeerQuery = new PeerMembershipQuery();
                        fq.Add(cq);
                        fq.Add(pq);
                        Request request = new Request();
                        request.Queries.AddRange(fq);
                        request.Authentication = authentication;
                        ByteString payloadBytes = request.ToByteString();
                        ByteString signatureBytes = ltransactionContext.SignByteStrings(payloadBytes);
                        SignedRequest sr = new SignedRequest();
                        sr.Payload = payloadBytes;
                        sr.Signature = signatureBytes;
                        if (IS_TRACE_LEVEL && null != diagnosticFileDumper) // dump protobuf we sent
                            logger.Trace($"Service discovery channel {channelName} {serviceDiscoveryPeer} service chaincode query sent {diagnosticFileDumper.CreateDiagnosticProtobufFile(sr.ToByteArray())}");
                        Response response = await serviceDiscoveryPeer.SendDiscoveryRequestAsync(sr, SERVICE_DISCOVERY_WAITTIME, token).ConfigureAwait(false);

                        if (IS_TRACE_LEVEL && null != diagnosticFileDumper) // dump protobuf we get
                            logger.Trace($"Service discovery channel {channelName} {serviceDiscoveryPeer} service discovery returned {diagnosticFileDumper.CreateDiagnosticProtobufFile(response.ToByteArray())}");
                        //serviceDiscoveryPeer.HasConnected; ???
                        List<QueryResult> resultsList = response.Results.ToList();
                        QueryResult queryResult = resultsList[0]; //configquery
                        if (queryResult.ResultCase == QueryResult.ResultOneofCase.Error)
                        {
                            logger.Warn($"Channel {channelName} peer: {serviceDiscoveryPeer} error during service discovery {queryResult.Error.Content}");
                            continue;
                        }

                        QueryResult queryResult2 = resultsList[1];
                        if (queryResult2.ResultCase == QueryResult.ResultOneofCase.Error)
                        {
                            logger.Warn($"Channel {channelName} peer: {serviceDiscoveryPeer} error during service discovery {queryResult2.Error.Content}");
                            continue;
                        }

                        ConfigResult configResult = queryResult.ConfigResult;
                        Dictionary<string, FabricMSPConfig> msps = configResult.Msps.ToDictionary(a => a.Key, a => a.Value);
                        HashSet<ByteString> cbbs = new HashSet<ByteString>();
                        foreach (string i in msps.Keys)
                        {
                            FabricMSPConfig value = msps[i];
                            string mspid = value.Name;
                            cbbs.AddRange(value.RootCerts);
                            cbbs.AddRange(value.IntermediateCerts);
                            value.RootCerts.ToList().ForEach(a => lsdNetwork.AddTlsCert(mspid, a.ToByteArray()));
                            value.TlsIntermediateCerts.ToList().ForEach(a => lsdNetwork.AddTlsIntermCert(mspid, a.ToByteArray()));
                        }

                        List<byte[]> toaddCerts = new List<byte[]>();
                        AddCerts(cbbs, toaddCerts);
                        if (toaddCerts.Count > 0) // add them to crypto store.
                            toaddCerts.ForEach(a => channel.Client.CryptoSuite.Store.AddCertificate(a.ToUTF8String()));
                        Dictionary<string, SDOrderer> ordererEndpoints = new Dictionary<string, SDOrderer>();
                        Dictionary<string, Endpoints> orderersMap = configResult.Orderers.ToDictionary(a => a.Key, a => a.Value);
                        foreach (string mspid in orderersMap.Keys)
                        {
                            Endpoints value = orderersMap[mspid];
                            foreach (Protos.Discovery.Endpoint l in value.Endpoint)
                            {
                                string endpoint = l.Host + ":" + l.Port.ToString().ToLowerInvariant().Trim();
                                logger.Trace($"Channel {channelName} discovered orderer MSPID: {mspid}, endpoint: {endpoint}");
                                SDOrderer sdOrderer = new SDOrderer(channel, mspid, endpoint, lsdNetwork.GetTlsCerts(mspid), lsdNetwork.GetTlsIntermediateCerts(mspid));
                                ordererEndpoints[sdOrderer.Endpoint] = sdOrderer;
                            }
                        }

                        lsdNetwork.SetOrdererEndpoints(ordererEndpoints);
                        PeerMembershipResult membership = queryResult2.Members;
                        Dictionary<string, SDEndorser> endorsers = new Dictionary<string, SDEndorser>();
                        foreach (string mspId in membership.PeersByOrg.Keys)
                        {
                            Peers peer = membership.PeersByOrg[mspId];
                            foreach (Protos.Discovery.Peer pp in peer.Peers_)
                            {
                                SDEndorser ppp = new SDEndorser(channel, pp, lsdNetwork.GetTlsCerts(mspId), lsdNetwork.GetTlsIntermediateCerts(mspId));
                                logger.Trace($"Channel {channelName} discovered peer MSPID: {mspId}, endpoint: {ppp.Endpoint}");
                                endorsers[ppp.Endpoint] = ppp;
                            }
                        }

                        lsdNetwork.SetEndorsers(endorsers);
                        lsdNetwork.End();
                        sdNetwork = lsdNetwork;
                        ret = lsdNetwork;
                        break;
                    }
                    catch (Exception e)
                    {
                        logger.Warn($"Channel {channelName} peer {serviceDiscoveryPeer} service discovery error {e.Message}");
                    }
                }

                logger.Debug($"Channel {channelName} service discovery completed: {ret != null}");
                return ret;
            }
        }


        public async Task<Dictionary<string, SDChaindcode>> DiscoverEndorserEndpointsAsync(TransactionContext tContext, List<List<Channel.ServiceDiscoveryChaincodeCalls>> chaincodeNames, CancellationToken token = default(CancellationToken))
        {
            if (null == chaincodeNames)
            {
                logger.Warn("Discover of chaincode names was null.");
                return new Dictionary<string, SDChaindcode>();
            }

            if (chaincodeNames.Count == 0)
            {
                logger.Warn("Discover of chaincode names was empty.");
                return new Dictionary<string, SDChaindcode>();
            }

            if (IS_DEBUG_LEVEL)
            {
                StringBuilder cns = new StringBuilder(1000);
                string sep = "";
                cns.Append("[");
                foreach (List<Channel.ServiceDiscoveryChaincodeCalls> s in chaincodeNames)
                {
                    Channel.ServiceDiscoveryChaincodeCalls n = s[0];
                    cns.Append(sep).Append(n.Write(s.GetRange(1, s.Count - 1)));
                    sep = ", ";
                }

                cns.Append("]");
                logger.Debug($"Channel {channelName} doing discovery for chaincodes: {cns}");
            }

            List<Peer> speers = serviceDiscoveryPeers.Shuffle().ToList();
            Dictionary<string, SDChaindcode> ret = new Dictionary<string, SDChaindcode>();
            sdNetwork = await NetworkDiscoveryAsync(tContext, false, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ServiceDiscoveryException serviceDiscoveryException = null;

            foreach (Peer serviceDiscoveryPeer in speers)
            {
                serviceDiscoveryException = null;
                try
                {
                    logger.Debug($"Channel {channelName} doing discovery for chaincodes on peer: {serviceDiscoveryPeer}");
                    TransactionContext ltransactionContext = tContext.RetryTransactionSameContext();
                    byte[] clientTLSCertificateDigest = serviceDiscoveryPeer.GetClientTLSCertificateDigest();
                    if (null == clientTLSCertificateDigest)
                    {
                        logger.Warn($"Channel {channelName} peer {serviceDiscoveryPeer} requires mutual tls for service discovery.");
                        continue;
                    }

                    ByteString clientIdent = ltransactionContext.Identity.ToByteString();
                    ByteString tlshash = ByteString.CopyFrom(clientTLSCertificateDigest);
                    AuthInfo authentication = new AuthInfo();
                    authentication.ClientIdentity = clientIdent;
                    authentication.ClientTlsCertHash = tlshash;
                    List<Query> fq = new List<Query>(chaincodeNames.Count);
                    foreach (List<Channel.ServiceDiscoveryChaincodeCalls> chaincodeName in chaincodeNames)
                    {
                        if (ret.ContainsKey(chaincodeName[0].Name))
                        {
                            continue;
                        }

                        List<ChaincodeCall> chaincodeCalls = new List<ChaincodeCall>();
                        chaincodeName.ForEach(serviceDiscoveryChaincodeCalls => chaincodeCalls.Add(serviceDiscoveryChaincodeCalls.Build()));
                        List<ChaincodeInterest> cinn = new List<ChaincodeInterest>(1);
                        //chaincodeName.ForEach(ServiceDiscoveryChaincodeCalls.Build);
                        ChaincodeInterest cci = new ChaincodeInterest();
                        cci.Chaincodes.Add(chaincodeCalls);
                        cinn.Add(cci);
                        ChaincodeQuery chaincodeQuery = new ChaincodeQuery();
                        chaincodeQuery.Interests.AddRange(cinn);
                        Query q = new Query();
                        q.Channel = channelName;
                        q.CcQuery = chaincodeQuery;
                        fq.Add(q);
                    }

                    if (fq.Count == 0)
                    {
                        //this would be odd but lets take care of it.
                        break;
                    }

                    Request request = new Request();
                    request.Queries.AddRange(fq);
                    request.Authentication = authentication;
                    ByteString payloadBytes = request.ToByteString();
                    ByteString signatureBytes = ltransactionContext.SignByteStrings(payloadBytes);
                    SignedRequest sr = new SignedRequest();
                    sr.Payload = payloadBytes;
                    sr.Signature = signatureBytes;
                    if (IS_TRACE_LEVEL && null != diagnosticFileDumper) // dump protobuf we sent
                        logger.Trace($"Service discovery channel {channelName} {serviceDiscoveryPeer} service chaincode query sent {diagnosticFileDumper.CreateDiagnosticProtobufFile(sr.ToByteArray())}");
                    logger.Debug($"Channel {channelName} peer {serviceDiscoveryPeer} sending chaincode query request");
                    Response response = await serviceDiscoveryPeer.SendDiscoveryRequestAsync(sr, SERVICE_DISCOVERY_WAITTIME, token).ConfigureAwait(false);
                    if (IS_TRACE_LEVEL && null != diagnosticFileDumper) // dump protobuf we get
                        logger.Trace($"Service discovery channel {channelName} {serviceDiscoveryPeer} query returned {diagnosticFileDumper.CreateDiagnosticProtobufFile(response.ToByteArray())}");
                    logger.Debug($"Channel {channelName} peer {serviceDiscoveryPeer} completed chaincode query request");
                    //serviceDiscoveryPeer.HasConnected();
                    foreach (QueryResult queryResult in response.Results)
                    {
                        if (queryResult.ResultCase == QueryResult.ResultOneofCase.Error)
                        {
                            ServiceDiscoveryException discoveryException = new ServiceDiscoveryException($"Error {queryResult.Error.Content}");
                            logger.Error(discoveryException.Message);
                            continue;
                        }

                        if (queryResult.ResultCase != QueryResult.ResultOneofCase.CcQueryRes)
                        {
                            ServiceDiscoveryException discoveryException = new ServiceDiscoveryException($"Error expected chaincode endorsement query but got {queryResult.ResultCase.ToString()}");
                            logger.Error(discoveryException.Message);
                            continue;
                        }

                        ChaincodeQueryResult ccQueryRes = queryResult.CcQueryRes;
                        if (ccQueryRes.Content.Count == 0)
                        {
                            throw new ServiceDiscoveryException($"Error {queryResult.Error.Content}");
                        }

                        foreach (EndorsementDescriptor es in ccQueryRes.Content)
                        {
                            string chaincode = es.Chaincode;
                            List<SDLayout> layouts = new List<SDLayout>();
                            foreach (Layout layout in es.Layouts)
                            {
                                SDLayout sdLayout = null;
                                Dictionary<string, uint> quantitiesByGroupMap = layout.QuantitiesByGroup.ToDictionary(a => a.Key, a => a.Value);
                                foreach (string key in quantitiesByGroupMap.Keys)
                                {
                                    uint quantity = quantitiesByGroupMap[key];
                                    if (quantity < 1)
                                    {
                                        continue;
                                    }

                                    Peers peers = es.EndorsersByGroups.GetOrNull(key);
                                    if (peers == null || peers.Peers_.Count == 0)
                                    {
                                        continue;
                                    }

                                    List<SDEndorser> sdEndorsers = new List<SDEndorser>();

                                    foreach (Protos.Discovery.Peer pp in peers.Peers_)
                                    {
                                        SDEndorser ppp = new SDEndorser(channel, pp, null, null);
                                        string endPoint = ppp.Endpoint;
                                        SDEndorser nppp = sdNetwork.GetEndorserByEndpoint(endPoint);
                                        if (null == nppp)
                                        {
                                            sdNetwork = await NetworkDiscoveryAsync(tContext, true, token).ConfigureAwait(false);
                                            if (null == sdNetwork)
                                            {
                                                throw new ServiceDiscoveryException("Failed to discover network resources.");
                                            }

                                            nppp = sdNetwork.GetEndorserByEndpoint(ppp.Endpoint);
                                            if (null == nppp)
                                            {
                                                throw new ServiceDiscoveryException($"Failed to discover peer endpoint information {ppp.Endpoint} for chaincode {chaincode}");
                                            }
                                        }

                                        sdEndorsers.Add(nppp);
                                    }

                                    if (sdLayout == null)
                                    {
                                        sdLayout = new SDLayout();
                                        layouts.Add(sdLayout);
                                    }

                                    sdLayout.AddGroup(key, (int) quantity, sdEndorsers);
                                }
                            }

                            if (layouts.Count == 0)
                            {
                                logger.Warn($"Channel {channelName} chaincode {chaincode} discovered no layouts!");
                            }
                            else
                            {
                                if (IS_DEBUG_LEVEL)
                                {
                                    StringBuilder sb = new StringBuilder(1000);
                                    sb.Append("Channel ").Append(channelName).Append(" found ").Append(layouts.Count).Append(" layouts for chaincode: ").Append(es.Chaincode);

                                    sb.Append(", layouts: [");

                                    string sep = "";
                                    foreach (SDLayout layout in layouts)
                                    {
                                        sb.Append(sep).Append(layout);

                                        sep = ", ";
                                    }

                                    sb.Append("]");

                                    logger.Debug(sb.ToString());
                                }

                                ret[chaincode] = new SDChaindcode(es.Chaincode, layouts);
                            }
                        }
                    }

                    if (ret.Count == chaincodeNames.Count)
                    {
                        break; // found them all.
                    }
                }
                catch (ServiceDiscoveryException e)
                {
                    logger.Warn($"Service discovery error on peer {serviceDiscoveryPeer}. Error: {e.Message}");
                    serviceDiscoveryException = e;
                }
                catch (Exception e)
                {
                    logger.Warn($"Service discovery error on peer {serviceDiscoveryPeer}. Error: {e.Message}");
                    serviceDiscoveryException = new ServiceDiscoveryException(e.Message, e);
                }
            }

            if (null != serviceDiscoveryException)
            {
                throw serviceDiscoveryException;
            }

            if (ret.Count != chaincodeNames.Count())
            {
                logger.Warn($"Channel {channelName} failed to find all layouts for chaincodes. Expected: {chaincodeNames.Count} and found: {ret.Count}");
            }

            return ret;
        }

        /**
         * Endorsement selection by layout group that has least required and block height is the highest (most up to date).
         */

        public static SDEndorserState ENDORSEMENT_SELECTION_LEAST_REQUIRED_BLOCKHEIGHT(SDChaindcode sdChaindcode)
        {
            List<SDLayout> layouts = sdChaindcode.Layouts;
            SDLayout pickedLayout = null;

            Dictionary<SDLayout, HashSet<SDEndorser>> layoutEndorsers = new Dictionary<SDLayout, HashSet<SDEndorser>>();

            // if (layouts.size() > 1) { // pick layout by least number of endorsers ..  least number of peers hit and smaller block!

            foreach (SDLayout sdLayout in layouts)
            {
                HashSet<LGroup> remainingGroups = new HashSet<LGroup>();
                foreach (SDGroup sdGroup in sdLayout.Groups)
                {
                    remainingGroups.Add(new LGroup(sdGroup));
                }

                // These are required as there is no choice.
                HashSet<SDEndorser> required = new HashSet<SDEndorser>();
                foreach (LGroup lgroup in remainingGroups)
                {
                    if (lgroup.StillRequired == lgroup.Endorsers.Count)
                    {
                        required.AddRange(lgroup.Endorsers);
                    }
                }
                //add those that there are no choice.

                if (required.Count > 0)
                {
                    List<LGroup> remove = new List<LGroup>();
                    foreach (LGroup lGroup in remainingGroups)
                    {
                        if (!lGroup.Endorsed(required))
                        {
                            remove.Add(lGroup);
                        }
                    }

                    remove.ForEach(a => remainingGroups.Remove(a));
                    if (!layoutEndorsers.ContainsKey(sdLayout))
                        layoutEndorsers[sdLayout] = new HashSet<SDEndorser>();
                    layoutEndorsers[sdLayout].AddRange(required);
                }

                if (remainingGroups.Count == 0)
                {
                    // no more groups here done for this layout.
                    continue; // done with this layout there really were no choices.
                }

                //Now go through groups finding which endorsers can satisfy the most groups.

                do
                {
                    Dictionary<SDEndorser, int> matchCount = new Dictionary<SDEndorser, int>();

                    foreach (LGroup group in remainingGroups)
                    {
                        foreach (SDEndorser sdEndorser in group.Endorsers)
                        {
                            if (matchCount.ContainsKey(sdEndorser))
                                matchCount[sdEndorser] = matchCount[sdEndorser]++;
                            else
                                matchCount[sdEndorser] = 1;
                        }
                    }

                    List<SDEndorser> theMost = new List<SDEndorser>();
                    int maxMatch = 0;
                    foreach (SDEndorser sdEndorser in matchCount.Keys)
                    {
                        int count = matchCount[sdEndorser];
                        if (count > maxMatch)
                        {
                            theMost.Clear();
                            theMost.Add(sdEndorser);
                            maxMatch = count;
                        }
                        else if (count == maxMatch)
                        {
                            theMost.Add(sdEndorser);
                        }
                    }

                    HashSet<SDEndorser> theVeryMost = new HashSet<SDEndorser>();
                    long max = 0L;
                    // Tie breaker: Pick one with greatest ledger height.
                    foreach (SDEndorser sd in theMost)
                    {
                        if (sd.LedgerHeight > max)
                        {
                            max = sd.LedgerHeight;
                            theVeryMost.Clear();
                            theVeryMost.Add(sd);
                        }
                    }

                    List<LGroup> remove2 = new List<LGroup>(remainingGroups.Count);
                    foreach (LGroup lGroup in remainingGroups)
                    {
                        if (!lGroup.Endorsed(theVeryMost))
                        {
                            remove2.Add(lGroup);
                        }
                    }

                    if (!layoutEndorsers.ContainsKey(sdLayout))
                        layoutEndorsers[sdLayout] = new HashSet<SDEndorser>();
                    layoutEndorsers[sdLayout].AddRange(theVeryMost);
                    remove2.ForEach(a => remainingGroups.Remove(a));
                } while (remainingGroups.Count > 0);

                // Now pick the layout with least endorsers
            }

            //Pick layout which needs least endorsements.
            int min = int.MaxValue;
            HashSet<SDLayout> theLeast = new HashSet<SDLayout>();

            foreach (SDLayout sdLayoutK in layoutEndorsers.Keys)
            {
                int count = layoutEndorsers[sdLayoutK].Count;
                if (count < min)
                {
                    theLeast.Clear();
                    theLeast.Add(sdLayoutK);
                    min = count;
                }
                else if (count == min)
                {
                    theLeast.Add(sdLayoutK);
                }
            }

            if (theLeast.Count == 1)
            {
                pickedLayout = theLeast.First();
            }
            else
            {
                long max = 0L;
                // Tie breaker: Pick one with greatest ledger height.
                foreach (SDLayout sdLayout in theLeast)
                {
                    long height = 0;
                    foreach (SDEndorser sdEndorser in layoutEndorsers[sdLayout])
                    {
                        height += sdEndorser.LedgerHeight;
                    }

                    if (height > max)
                    {
                        max = height;
                        pickedLayout = sdLayout;
                    }
                }
            }

            SDEndorserState sdEndorserState = new SDEndorserState();
            sdEndorserState.SDEndorsers = pickedLayout != null ? layoutEndorsers[pickedLayout].ToList() : new List<SDEndorser>();
            sdEndorserState.PickedLayout = pickedLayout;
            return sdEndorserState;
        }

        /**
         * Endorsement selection by random layout group and random endorsers there in.
         */
        public static SDEndorserState ENDORSEMENT_SELECTION_RANDOM(SDChaindcode sdChaindcode)
        {
            List<SDLayout> layouts = sdChaindcode.Layouts;

            SDLayout pickedLayout = layouts[0];

            if (layouts.Count > 1)
            {
                // more than one pick a random one.
                pickedLayout = layouts[random.Next(layouts.Count)];
            }

            Dictionary<string, SDEndorser> retMap = new Dictionary<string, SDEndorser>(); //hold results.

            foreach (SDGroup group in pickedLayout.Groups)
            {
                // go through groups getting random required endorsers
                List<SDEndorser> endorsers = group.Endorsers.Shuffle().ToList(); // randomize.
                int required = group.StillRequired; // what's needed in that group.
                List<SDEndorser> sdEndorsers = endorsers.GetRange(0, required).ToList(); // pick top endorsers.
                sdEndorsers.ForEach(sdEndorser =>
                {
                    if (!retMap.ContainsKey(sdEndorser.Endpoint))
                        retMap.Add(sdEndorser.Endpoint, sdEndorser);
                });
            }

            SDEndorserState sdEndorserState = new SDEndorserState(); //returned result.
            sdEndorserState.SDEndorsers = retMap.Values.ToList();
            sdEndorserState.PickedLayout = pickedLayout;
            return sdEndorserState;
        }

        public static List<SDEndorser> TopNbyHeight(int required, List<SDEndorser> endorsers)
        {
            return endorsers.OrderBy(a => a.LedgerHeight).Take(required).ToList();
        }


        public void Init()
        {
            if (channel.IsShutdown || SERVICE_DISCOVER_FREQ_SECONDS < 1)
                return;
            if (timer == null)
            {
                timer = new Timer(async (state)=>
                {
                    try
                    {
                        logger.Debug($"Channel {channelName} starting service rediscovery after {SERVICE_DISCOVER_FREQ_SECONDS} seconds.");
                        await FullNetworkDiscoveryAsync(true).ConfigureAwait(false);
                    }
                    catch(Exception)
                    {
                        //Ignored (Should never happen)
                    }
                }, null, SERVICE_DISCOVER_FREQ_SECONDS * 1000, SERVICE_DISCOVER_FREQ_SECONDS * 1000);
            }
        }

        public void Shutdown()
        {
            logger.Trace("Service discovery shutdown.");
            try
            {
                lock (timer)
                {
                    if (timer != null)
                    {
                        timer.Dispose();
                        timer = null;
                    }
                }
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                //best effort.
            }
        }


        public async Task<SDNetwork> FullNetworkDiscoveryAsync(bool force, CancellationToken token = default(CancellationToken))
        {
            if (channel.IsShutdown)
            {
                return null;
            }

            logger.Trace("Full network discovery force: {force}");
            try
            {
                SDNetwork osdNetwork = sdNetwork;
                SDNetwork lsdNetwork = await NetworkDiscoveryAsync(transactionContext.RetryTransactionSameContext(), force, token).ConfigureAwait(false);
                if (channel.IsShutdown || null == lsdNetwork)
                    return null;

                if (osdNetwork != lsdNetwork)
                {
                    // means it changed.
                    HashSet<string> chaincodesNames = lsdNetwork.ChaincodesNames;
                    List<List<Channel.ServiceDiscoveryChaincodeCalls>> lcc = new List<List<Channel.ServiceDiscoveryChaincodeCalls>>();
                    chaincodesNames.ToList().ForEach(s =>
                    {
                        List<Channel.ServiceDiscoveryChaincodeCalls> lc = new List<Channel.ServiceDiscoveryChaincodeCalls>();
                        lc.Add(new Channel.ServiceDiscoveryChaincodeCalls(s));
                        lcc.Add(lc);
                    });
                    chaindcodeMap = await DiscoverEndorserEndpointsAsync(transactionContext.RetryTransactionSameContext(), lcc, token).ConfigureAwait(false);
                    if (channel.IsShutdown)
                    {
                        return null;
                    }

                    await channel.SdUpdateAsync(lsdNetwork, token).ConfigureAwait(false);
                }

                return lsdNetwork;
            }
            catch (Exception e)
            {
                logger.WarnException($"Service discovery got error: {e.Message}", e);
            }
            finally
            {
                logger.Trace("Full network rediscovery completed.");
            }

            return null;
        }

        ~ServiceDiscovery()
        {
            Shutdown();
        }


        private class LGroup
        {
            public LGroup(SDGroup group)
            {
                Endorsers.AddRange(group.Endorsers);
                StillRequired = group.StillRequired;
            }

            // local book keeping.
            public int StillRequired { get; private set; }
            public HashSet<SDEndorser> Endorsers { get; } = new HashSet<SDEndorser>();

            // return true if still required
            public bool Endorsed(HashSet<SDEndorser> endorsed)
            {
                foreach (SDEndorser sdEndorser in endorsed)
                {
                    if (Endorsers.Contains(sdEndorser))
                    {
                        Endorsers.Remove(sdEndorser);
                        StillRequired = Math.Max(0, StillRequired - 1);
                    }
                }

                return StillRequired > 0;
            }
        }
    }
}