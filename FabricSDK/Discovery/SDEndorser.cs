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
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Gossip;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;

using Properties = Hyperledger.Fabric.SDK.Helper.Properties;
// ReSharper disable UnusedMethodReturnValue.Local
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace Hyperledger.Fabric.SDK.Discovery
{
    public class SDEndorser : IEquatable<SDEndorser>, ISDAdditionInfo<Peer>
    {
        private List<Chaincode> chaincodesList;

        public SDEndorser(Channel channel)
        {
            // for testing only
            Channel = channel;
            TLSCerts = null;
            TLSIntermediateCerts = null;
        }
        internal SDEndorser()
        {
       
        }
        public SDEndorser(Channel channel, Protos.Discovery.Peer peerRet, IEnumerable<byte[]> tlsCerts, IEnumerable<byte[]> tlsIntermediateCerts)
        {
            Channel = channel;
            TLSCerts = tlsCerts.ToList();
            TLSIntermediateCerts = tlsIntermediateCerts.ToList();
            ParseEndpoint(peerRet);
            ParseLedgerHeight(peerRet);
            ParseIdentity(peerRet);
        }

        public long LedgerHeight { get; internal set; } = -1L;

        public HashSet<string> ChaincodeNames => chaincodesList == null ? new HashSet<string>() : new HashSet<string>(chaincodesList.Select(a => a.Name));
        public List<Chaincode> Chaincodes => chaincodesList?.ToList() ?? new List<Chaincode>();


        public bool Equals(SDEndorser obj)
        {
            if (obj == this)
                return true;
            if (obj == null)
                return false;
            return obj.MspId == MspId && obj.Endpoint == Endpoint;
        }

        // private final Protocol.Peer proto;
        public virtual List<byte[]> TLSCerts { get; }

        public virtual List<byte[]> TLSIntermediateCerts { get; }

        public virtual string Endpoint { get; internal set; }

        public virtual string MspId { get; internal set; }

        public virtual Channel Channel { get; }

        public virtual HFClient Client => Channel.Client;

        public IReadOnlyDictionary<string, Peer> EndpointMap => Channel.PeerEndpointMap;

        public virtual async Task<Peer> AddAsync(Properties config, CancellationToken token = default(CancellationToken))
        {
            Properties properties = new Properties();
            string protocol = this.FindClientProp(config, "protocol", "grpcs:");
            string clientCertFile = this.FindClientProp(config, "clientCertFile", null);
            Peer peer = EndpointMap.GetOrNull(Endpoint); // maybe there already.
            if (null != peer)
                return peer;
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
            peer=Client.NewPeer(Endpoint, protocol + "//" + Endpoint, properties);
            Channel.PeerOptions opts = Channel.PeerOptions.CreatePeerOptions();
            opts.SetPeerRoles(PeerRole.ENDORSING_PEER, PeerRole.EVENT_SOURCE, PeerRole.LEDGER_QUERY, PeerRole.CHAINCODE_QUERY);
            await Channel.AddPeerAsync(peer, opts, token).ConfigureAwait(false);
            return peer;
        }

        private void ParseIdentity(Protos.Discovery.Peer peerRet)
        {
            try
            {
                SerializedIdentity serializedIdentity = SerializedIdentity.Parser.ParseFrom(peerRet.Identity);
                MspId = serializedIdentity.Mspid;
            }
            catch (Exception e)
            {
                throw new InvalidProtocolBufferRuntimeException(e);
            }
        }

        private string ParseEndpoint(Protos.Discovery.Peer peerRet)
        {
            if (null == Endpoint)
            {
                try
                {
                    Envelope membershipInfo = peerRet.MembershipInfo;
                    ByteString membershipInfoPayloadBytes = membershipInfo.Payload;
                    GossipMessage gossipMessageMemberInfo = GossipMessage.Parser.ParseFrom(membershipInfoPayloadBytes);

                    if (GossipMessage.ContentOneofCase.AliveMsg != gossipMessageMemberInfo.ContentCase)
                    {
                        throw new ArgumentException("Error " + gossipMessageMemberInfo.ContentCase + " Expected AliveMsg");
                    }

                    AliveMessage aliveMsg = gossipMessageMemberInfo.AliveMsg;
                    Endpoint = aliveMsg.Membership.Endpoint;
                    if (Endpoint != null)
                    {
                        Endpoint = Endpoint.ToLowerInvariant().Trim(); //makes easier on comparing.
                    }
                }
                catch (InvalidProtocolBufferException e)
                {
                    throw new InvalidProtocolBufferRuntimeException(e);
                }
            }

            return Endpoint;
        }

        private long ParseLedgerHeight(Protos.Discovery.Peer peerRet)
        {
            if (-1L == LedgerHeight)
            {
                try
                {
                    Envelope stateInfo = peerRet.StateInfo;
                    GossipMessage stateInfoGossipMessage = GossipMessage.Parser.ParseFrom(stateInfo.Payload);
                    if (stateInfoGossipMessage.ContentCase != GossipMessage.ContentOneofCase.StateInfo)
                    {
                        throw new ArgumentException("Error " + stateInfoGossipMessage.ContentCase + " Expected StateInfo");
                    }

                    StateInfo stateInfo1 = stateInfoGossipMessage.StateInfo;
                    LedgerHeight = (long) stateInfo1.Properties.LedgerHeight;
                    chaincodesList = stateInfo1.Properties.Chaincodes.ToList();
                }
                catch (InvalidProtocolBufferException e)
                {
                    throw new InvalidProtocolBufferRuntimeException(e);
                }
            }

            return LedgerHeight;
        }

        public override int GetHashCode()
        {

            return MspId.GetHashCode() ^ Endpoint.GetHashCode();
        }

        public override string ToString()
        {
            return "SDEndorser-" + MspId + "-" + Endpoint;
        }
    }
}