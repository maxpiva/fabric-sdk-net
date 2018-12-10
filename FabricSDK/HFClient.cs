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
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.SDK.Configuration;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Responses;
using Hyperledger.Fabric.SDK.Security;
using Nito.AsyncEx;


namespace Hyperledger.Fabric.SDK
{
    public class HFClient
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(HFClient));

        /**
     * Create a new channel
     *
     * @param name                           The channel's name
     * @param orderer                        Orderer to create the channel with.
     * @param channelConfiguration           Channel configuration data.
     * @param channelConfigurationSignatures byte arrays containing ConfigSignature's proto serialized.
     *                                       See {@link Channel#getChannelConfigurationSignature} on how to create
     * @return a new channel.
     * @throws TransactionException
     * @throws InvalidArgumentException
     */
        private static readonly AsyncLock _channelLock = new AsyncLock();
        private readonly Dictionary<string, Channel> channels = new Dictionary<string, Channel>();
        private ICryptoSuite cryptoSuite;
        private IUser userContext;
       

        private HFClient()
        {
        }

        public ICryptoSuite CryptoSuite
        {
            get => cryptoSuite;
            set
            {
                if (null == value)
                    throw new ArgumentException("CryptoSuite paramter is null.");
                if (cryptoSuite != null && value != cryptoSuite)
                    throw new ArgumentException("CryptoSuite may only be set once.");
                cryptoSuite = value;
            }
        }


        public IUser UserContext
        {
            get => userContext;
            set
            {
                if (null == cryptoSuite)
                    throw new ArgumentException("No cryptoSuite has been set.");
                value.UserContextCheck();
                userContext = value;
                logger.Debug($"Setting user context to MSPID: {userContext.MspId} user: {userContext.Name}");
            }
        }


        /**
     * createNewInstance create a new instance of the HFClient
     *
     * @return client
     */
        public static HFClient Create()
        {
            return new HFClient();
        }

        /**
     * Configures a channel based on information loaded from a Network Config file.
     * Note that it is up to the caller to initialize the returned channel.
     *
     * @param channelName The name of the channel to be configured
     * @param networkConfig The network configuration to use to configure the channel
     * @return The configured channel, or null if the channel is not defined in the configuration
     * @throws InvalidArgumentException
     */
        public Channel LoadChannelFromConfig(string channelName, NetworkConfig networkConfig)
        {
            return LoadChannelFromConfigAsync(channelName, networkConfig).RunAndUnwrap();
        }

        public Task<Channel> LoadChannelFromConfigAsync(string channelName, NetworkConfig networkConfig, CancellationToken token=default(CancellationToken))
        {
            ClientCheck();
            // Sanity checks
            if (string.IsNullOrEmpty(channelName))
                throw new ArgumentException("channelName must be specified");
            if (networkConfig == null)
                throw new ArgumentException("networkConfig must be specified");
            using (_channelLock.Lock())
            {
                if (channels.ContainsKey(channelName))
                    throw new ArgumentException($"Channel with name {channelName} already exists");
            }
            return networkConfig.LoadChannelAsync(this, channelName, token);
        }


        /**
        * newChannel - already configured channel.
        *
        * @param name
        * @return a new channel.
        * @throws InvalidArgumentException
        */
        public Channel NewChannel(string name)
        {
            ClientCheck();
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Channel name can not be null or empty string.");
            using (_channelLock.Lock())
            {
                if (channels.ContainsKey(name))
                    throw new ArgumentException($"Channel by the name {name} already exists");
                logger.Trace($"Creating channel : {name}");
                Channel newChannel = Channel.Create(name, this);
                channels.Add(name, newChannel);
                return newChannel;
            }
        }
        public Channel NewChannel(string name, Orderer orderer, ChannelConfiguration channelConfiguration, params byte[][] channelConfigurationSignatures) => NewChannelAsync(name, orderer, channelConfiguration, new CancellationToken(), channelConfigurationSignatures).RunAndUnwrap();
        public async Task<Channel> NewChannelAsync(string name, Orderer orderer, ChannelConfiguration channelConfiguration, CancellationToken token = default(CancellationToken), params byte[][] channelConfigurationSignatures)
        {
            ClientCheck();
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Channel name can not be null or empty string.");

            using (await _channelLock.LockAsync(token).ConfigureAwait(false))
            {
                if (channels.ContainsKey(name))
                    throw new ArgumentException($"Channel by the name {name} already exits");
                logger.Trace("Creating channel :" + name);
                Channel newChannel = await Channel.CreateAsync(name, this, orderer, channelConfiguration, token, channelConfigurationSignatures).ConfigureAwait(false);
                channels.Add(name, newChannel);
                return newChannel;
            }
        }

     /**
     * Deserialize a channel serialized by {@link Channel#serializeChannel()}
     *
     * @param file a file which contains the bytes to be deserialized.
     * @return A Channel that has not been initialized.
     * @throws IOException
     * @throws ClassNotFoundException
     * @throws InvalidArgumentException
     */

        public Channel DeSerializeChannelFromFile(string file)
        {
            if (string.IsNullOrEmpty(file))
                throw new ArgumentException("File parameter may not be null");
            return DeSerializeChannel(File.ReadAllText(file, Encoding.UTF8));
        }


    /**
    * Deserialize a channel serialized by {@link Channel#serializeChannel()}
    *
    * @param channelBytes bytes to be deserialized.
    * @return A Channel that has not been initialized.
    * @throws IOException
    * @throws ClassNotFoundException
    * @throws InvalidArgumentException
    */
        public Channel DeSerializeChannel(string str)
        {
            Channel channel;
            channel = Channel.Deserialize(str);
            string name = channel.Name;
            using (_channelLock.Lock())
            {
                if (null != GetChannel(name))
                {
                    channel.Shutdown(true);
                    throw new ArgumentException($"Channel {name} already exists in the client");
                }
                channels.Add(name, channel);
                channel.client = this;
            }
            return channel;
        }
        /**
        * newPeer create a new peer
        *
        * @param name       name of peer.
        * @param grpcURL    to the peer's location
        * @param properties <p>
        *                   Supported properties
        *                   <ul>
        *                   <li>pemFile - File location for x509 pem certificate for SSL.</li>
        *                   <li>pemBytes - byte array for x509 pem certificates for SSL</li>
        *                   <li>trustServerCertificate - boolen(true/false) override CN to match pemFile certificate -- for development only.
        *                   If the pemFile has the target server's certificate (instead of a CA Root certificate),
        *                   instruct the TLS client to trust the CN value of the certificate in the pemFile,
        *                   useful in development to get past default server hostname verification during
        *                   TLS handshake, when the server host name does not match the certificate.
        *                   </li>
        *                   <li>clientKeyFile - File location for private key pem for mutual TLS</li>
        *                   <li>clientCertFile - File location for x509 pem certificate for mutual TLS</li>
        *                   <li>clientKeyBytes - Private key pem bytes for mutual TLS</li>
        *                   <li>clientCertBytes - x509 pem certificate bytes for mutual TLS</li>
        *                   <li>hostnameOverride - Specify the certificates CN -- for development only.
        *                   <li>sslProvider - Specify the SSL provider, openSSL or JDK.</li>
        *                   <li>negotiationType - Specify the type of negotiation, TLS or plainText.</li>
        *                   <li>If the pemFile does not represent the server certificate, use this property to specify the URI authority
        *                   (a.k.a hostname) expected in the target server's certificate. This is required to get past default server
        *                   hostname verifications during TLS handshake.
        *                   </li>
        *                   <li>
        *                   peerEventRegistrationWaitTime - Time in milliseconds to wait for peer eventing service registration.
        *                   </li>
        *                   <li>
        *                   grpc.NettyChannelBuilderOption.&lt;methodName&gt;  where methodName is any method on
        *                   grpc ManagedChannelBuilder.  If more than one argument to the method is needed then the
        *                   parameters need to be supplied in an array of Objects.
        *                   </li>
        *                   </ul>
        * @return Peer
        * @throws InvalidArgumentException
        */

        public Peer NewPeer(string name, string grpcURL, Properties properties)
        {
            ClientCheck();
            return Peer.Create(name, grpcURL, properties);
        }

     /**
     * newPeer create a new peer
     *
     * @param name
     * @param grpcURL to the peer's location
     * @return Peer
     * @throws InvalidArgumentException
     */

        public Peer NewPeer(string name, string grpcURL)
        {
            ClientCheck();
            return Peer.Create(name, grpcURL, null);
        }

     /**
     * getChannel by name
     *
     * @param name The channel name
     * @return a channel (or null if the channel does not exist)
     */

        public Channel GetChannel(string name)
        {
            return channels.GetOrNull(name);
        }

        /**
     * newInstallProposalRequest get new Install proposal request.
     *
     * @return InstallProposalRequest
     */
        public InstallProposalRequest NewInstallProposalRequest() => new InstallProposalRequest(UserContext);

        /**
     * newInstantiationProposalRequest get new instantiation proposal request.
     *
     * @return InstantiateProposalRequest
     */

        public InstantiateProposalRequest NewInstantiationProposalRequest() => new InstantiateProposalRequest(UserContext);

        public UpgradeProposalRequest NewUpgradeProposalRequest() => new UpgradeProposalRequest(UserContext);

        /**
     * newTransactionProposalRequest  get new transaction proposal request.
     *
     * @return TransactionProposalRequest
     */

        public TransactionProposalRequest NewTransactionProposalRequest() => TransactionProposalRequest.Create(UserContext);

        /**
     * newQueryProposalRequest get new query proposal request.
     *
     * @return QueryByChaincodeRequest
     */

        public QueryByChaincodeRequest NewQueryProposalRequest() => QueryByChaincodeRequest.Create(UserContext);

        /**
     * Set the User context for this client.
     *
     * @param userContext
     * @return the old user context. Maybe null if never set!
     * @throws InvalidArgumentException
     */


        /**
     * Create a new Eventhub.
     *
     * @param name       name of EventHub.
     * @param grpcURL    url location of orderer grpc or grpcs protocol.
     * @param properties <p>
     *                   Supported properties
     *                   <ul>
     *                   <li>pemFile - File location for x509 pem certificate for SSL.</li>
     *                   <li>pemBytes - byte array for x509 pem certificates for SSL</li>
     *                   <li>trustServerCertificate - boolean(true/false) override CN to match pemFile certificate -- for development only.
     *                   If the pemFile has the target server's certificate (instead of a CA Root certificate),
     *                   instruct the TLS client to trust the CN value of the certificate in the pemFile,
     *                   useful in development to get past default server hostname verification during
     *                   TLS handshake, when the server host name does not match the certificate.
     *                   </li>
     *                   <li>clientKeyFile - File location for PKCS8-encoded private key pem for mutual TLS</li>
     *                   <li>clientCertFile - File location for x509 pem certificate for mutual TLS</li>
     *                   <li>hostnameOverride - Specify the certificates CN -- for development only.
     *                   <li>sslProvider - Specify the SSL provider, openSSL or JDK.</li>
     *                   <li>negotiationType - Specify the type of negotiation, TLS or plainText.</li>
     *                   <li>If the pemFile does not represent the server certificate, use this property to specify the URI authority
     *                   (a.k.a hostname) expected in the target server's certificate. This is required to get past default server
     *                   hostname verifications during TLS handshake.
     *                   </li>
     *                   <li>
     *                   grpc.NettyChannelBuilderOption.&lt;methodName&gt;  where methodName is any method on
     *                   grpc ManagedChannelBuilder.  If more than one argument to the method is needed then the
     *                   parameters need to be supplied in an array of Objects.
     *                   </li>
     *                   </ul>
     * @return The orderer.
     * @throws InvalidArgumentException
     */

        public EventHub NewEventHub(string name, string grpcURL, Properties properties)
        {
            ClientCheck();
            return EventHub.Create(name, grpcURL, properties);
        }

        /**
     * Create a new event hub
     *
     * @param name    Name of eventhup should match peer's name it's associated with.
     * @param grpcURL The http url location of the event hub
     * @return event hub
     * @throws InvalidArgumentException
     */

        public EventHub NewEventHub(string name, string grpcURL) => NewEventHub(name, grpcURL, null);

        /**
     * Create a new urlOrderer.
     *
     * @param name    name of the orderer.
     * @param grpcURL url location of orderer grpc or grpcs protocol.
     * @return a new Orderer.
     * @throws InvalidArgumentException
     */

        public Orderer NewOrderer(string name, string grpcURL) => NewOrderer(name, grpcURL, null);

        /**
     * Create a new orderer.
     *
     * @param name       name of Orderer.
     * @param grpcURL    url location of orderer grpc or grpcs protocol.
     * @param properties <p>
     *                   Supported properties
     *                   <ul>
     *                   <li>pemFile - File location for x509 pem certificate for SSL.</li>
     *                   <li>pemBytes - byte array for x509 pem certificates for SSL</li>
     *                   <li>trustServerCertificate - boolean(true/false) override CN to match pemFile certificate -- for development only.
     *                   If the pemFile has the target server's certificate (instead of a CA Root certificate),
     *                   instruct the TLS client to trust the CN value of the certificate in the pemFile,
     *                   useful in development to get past default server hostname verification during
     *                   TLS handshake, when the server host name does not match the certificate.
     *                   </li>
     *                   <li>clientKeyFile - File location for private key pem for mutual TLS</li>
     *                   <li>clientCertFile - File location for x509 pem certificate for mutual TLS</li>
     *                   <li>clientKeyBytes - Private key pem bytes for mutual TLS</li>
     *                   <li>clientCertBytes - x509 pem certificate bytes for mutual TLS</li>
     *                   <li>sslProvider - Specify the SSL provider, openSSL or JDK.</li>
     *                   <li>negotiationType - Specify the type of negotiation, TLS or plainText.</li>
     *                   <li>hostnameOverride - Specify the certificates CN -- for development only.
     *                   If the pemFile does not represent the server certificate, use this property to specify the URI authority
     *                   (a.k.a hostname) expected in the target server's certificate. This is required to get past default server
     *                   hostname verifications during TLS handshake.
     *                   </li>
     *                   <li>
     *                   grpc.NettyChannelBuilderOption.&lt;methodName&gt;  where methodName is any method on
     *                   grpc ManagedChannelBuilder.  If more than one argument to the method is needed then the
     *                   parameters need to be supplied in an array of Objects.
     *                   </li>
     *                   <li>
     *                   ordererWaitTimeMilliSecs Time to wait in milliseconds for the
     *                   Orderer to accept requests before timing out. The default is two seconds.
     *                   </li>
     *                   </ul>
     * @return The orderer.
     * @throws InvalidArgumentException
     */

        public Orderer NewOrderer(string name, string grpcURL, Properties properties)
        {
            ClientCheck();
            return Orderer.Create(name, grpcURL, properties);
        }

        /**
     * Query the joined channels for peers
     *
     * @param peer the peer to query
     * @return A set of strings with the peer names of the channels the peer has joined.
     * @throws InvalidArgumentException
     * @throws ProposalException
     */
        public HashSet<string> QueryChannels(Peer peer) => QueryChannelsAsync(peer).RunAndUnwrap();

        public async Task<HashSet<string>> QueryChannelsAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            ClientCheck();
            if (null == peer)
                throw new ArgumentException("peer set to null");
            //Run this on a system channel.
            try
            {
                Channel systemChannel = Channel.CreateSystemChannel(this);
                return await systemChannel.QueryChannelsAsync(peer, token).ConfigureAwait(false);
            }
            catch (ProposalException e)
            {
                logger.ErrorException($"queryChannels for peer {peer.Name} failed. {e.Message}", e);
                throw;
            }
        }

        /**
     * Query the peer for installed chaincode information
     *
     * @param peer The peer to query.
     * @return List of ChaincodeInfo on installed chaincode @see {@link ChaincodeInfo}
     * @throws InvalidArgumentException
     * @throws ProposalException
     */

        public List<ChaincodeInfo> QueryInstalledChaincodes(Peer peer) => QueryInstalledChaincodesAsync(peer).RunAndUnwrap();

        public async Task<List<ChaincodeInfo>> QueryInstalledChaincodesAsync(Peer peer, CancellationToken token = default(CancellationToken))
        {
            ClientCheck();

            if (null == peer)
                throw new ArgumentException("peer set to null");
            try
            {
                //Run this on a system channel.
                Channel systemChannel = Channel.CreateSystemChannel(this);
                return await systemChannel.QueryInstalledChaincodesAsync(peer, token).ConfigureAwait(false);
            }
            catch (ProposalException e)
            {
                logger.ErrorException($"queryInstalledChaincodes for peer {peer.Name} failed. {e.Message}", e);
                throw;
            }
        }

        /**
     * Get signature for channel configuration
     *
     * @param channelConfiguration
     * @param signer
     * @return byte array with the signature
     * @throws InvalidArgumentException
     */

        public byte[] GetChannelConfigurationSignature(ChannelConfiguration channelConfiguration, IUser signer)
        {
            ClientCheck();
            Channel systemChannel = Channel.CreateSystemChannel(this);
            return systemChannel.GetChannelConfigurationSignature(channelConfiguration, signer);
        }

        /**
     * Get signature for update channel configuration
     *
     * @param updateChannelConfiguration
     * @param signer
     * @return byte array with the signature
     * @throws InvalidArgumentException
     */

        public byte[] GetUpdateChannelConfigurationSignature(UpdateChannelConfiguration updateChannelConfiguration, IUser signer)
        {
            ClientCheck();
            Channel systemChannel = Channel.CreateSystemChannel(this);
            return systemChannel.GetUpdateChannelConfigurationSignature(updateChannelConfiguration, signer);
        }

        /**
     * Send install chaincode request proposal to peers.
     *
     * @param installProposalRequest
     * @param peers                  Collection of peers to install on.
     * @return responses from peers.
     * @throws InvalidArgumentException
     * @throws ProposalException
     */

        public List<ProposalResponse> SendInstallProposal(InstallProposalRequest installProposalRequest, IEnumerable<Peer> peers) => SendInstallProposalAsync(installProposalRequest, peers).RunAndUnwrap();

        public Task<List<ProposalResponse>> SendInstallProposalAsync(InstallProposalRequest installProposalRequest, IEnumerable<Peer> peers, CancellationToken token = default(CancellationToken))
        {
            ClientCheck();
            installProposalRequest.SetSubmitted();
            Channel systemChannel = Channel.CreateSystemChannel(this);
            return systemChannel.SendInstallProposalAsync(installProposalRequest, peers, token);
        }


        private void ClientCheck()
        {
            if (null == cryptoSuite)
                throw new ArgumentException("No cryptoSuite has been set.");
            userContext.UserContextCheck();
        }

        public void RemoveChannel(Channel channel)
        {

            using (_channelLock.Lock())
            {
                string name = channel.Name;
                Channel ch = channels.GetOrNull(name);
                if (ch == channel)
                {
                    // Only remove if it's the same instance.
                    channels.Remove(name);
                }
            }

        }
    }
}