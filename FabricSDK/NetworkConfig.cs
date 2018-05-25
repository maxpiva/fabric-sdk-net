/*
 *  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
/*
/**
 * Holds details of network and channel configurations typically loaded from an external config file.
 * <br>
 * Also contains convenience methods for utilizing the config details,
 * including the main {@link HFClient#getChannel(String)} method
 */


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;

using Hyperledger.Fabric.SDK.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hyperledger.Fabric.SDK
{
    public class NetworkConfig
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(NetworkConfig));


        private readonly JObject jsonConfig;

        private readonly OrgInfo clientOrganization;
        private Dictionary<string, Node> eventHubs;

        private Dictionary<string, Node> orderers;

        // Organizations, keyed on org name (and not on mspid!)
        private Dictionary<string, OrgInfo> organizations;
        private Dictionary<string, Node> peers;


        private NetworkConfig(JObject jsonConfig)
        {
            this.jsonConfig = jsonConfig;

            // Extract the main details
            string configName = jsonConfig["name"].Value<string>();
            if (string.IsNullOrEmpty(configName))
            {
                throw new InvalidArgumentException("Network config must have a name");
            }

            string configVersion = jsonConfig["version"].Value<string>();
            if (string.IsNullOrEmpty(configVersion))
            {
                throw new InvalidArgumentException("Network config must have a version");
                // TODO: Validate the version
            }

            // Preload and create all peers, orderers, etc
            CreateAllPeers();
            CreateAllOrderers();

            Dictionary<string, JToken> foundCertificateAuthorities = FindCertificateAuthorities();
            //createAllCertificateAuthorities();
            CreateAllOrganizations(foundCertificateAuthorities);

            // Validate the organization for this client
            JToken jsonClient = jsonConfig["client"];
            string orgName = jsonClient == null ? null : jsonClient["organization"].Value<string>();
            if (string.IsNullOrEmpty(orgName))
            {
                throw new InvalidArgumentException("A client organization must be specified");
            }

            clientOrganization = GetOrganizationInfo(orgName);
            if (clientOrganization == null)
            {
                throw new InvalidArgumentException("Client organization " + orgName + " is not defined");
            }
        }

        /**
         * Names of Peers found
         *
         * @return Collection of peer names found.
         */
        public List<string> PeerNames => peers == null ? new List<string>() : peers.Keys.ToList();

        /**
         * Names of Orderers found
         *
         * @return Collection of peer names found.
         */
        public List<string> OrdererNames => orderers == null ? new List<string>() : orderers.Keys.ToList();

        /**
         * Names of EventHubs found
         *
         * @return Collection of eventhubs names found.
         */

        public List<string> EventHubNames => eventHubs == null ? new List<string>() : eventHubs.Keys.ToList();


        private Properties GetNodeProperties(string type, string name, Dictionary<string, Node> nodes)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidArgumentException("Parameter name is null or empty.");
            }

            if (!nodes.ContainsKey(name))
                throw new InvalidArgumentException($"{type} {name} not found.");
            Node node = nodes[name];
            if (null == node.Properties)
            {
                return new Properties();
            }
            else
            {
                return node.Properties.Clone();
            }
        }

        private void SetNodeProperties(string type, string name, Dictionary<string, Node> nodes, Properties properties)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidArgumentException("Parameter name is null or empty.");
            }

            if (properties == null)
            {
                throw new InvalidArgumentException("Parameter properties is null.");
            }

            if (!nodes.ContainsKey(name))
                throw new InvalidArgumentException($"{type} {name} not found.");
            Node node = nodes[name];
            node.Properties = properties.Clone();
        }

        /**
         * Get properties for a specific peer.
         *
         * @param name Name of peer to get the properties for.
         * @return The peer's properties.
         * @throws InvalidArgumentException
         */
        public Properties GetPeerProperties(string name)
        {
            return GetNodeProperties("Peer", name, peers);
        }

        /**
         * Get properties for a specific Orderer.
         *
         * @param name Name of orderer to get the properties for.
         * @return The orderer's properties.
         * @throws InvalidArgumentException
         */
        public Properties GetOrdererProperties(string name)
        {
            return GetNodeProperties("Orderer", name, orderers);
        }

        /**
         * Get properties for a specific eventhub.
         *
         * @param name Name of eventhub to get the properties for.
         * @return The eventhubs's properties.
         * @throws InvalidArgumentException
         */
        public Properties GetEventHubsProperties(string name)
        {
            return GetNodeProperties("EventHub", name, eventHubs);
        }

        /**
         * Set a specific peer's properties.
         *
         * @param name       The name of the peer's property to set.
         * @param properties The properties to set.
         * @throws InvalidArgumentException
         */
        public void SetPeerProperties(string name, Properties properties)
        {
            SetNodeProperties("Peer", name, peers, properties);
        }

        /**
         * Set a specific orderer's properties.
         *
         * @param name       The name of the orderer's property to set.
         * @param properties The properties to set.
         * @throws InvalidArgumentException
         */
        public void SetOrdererProperties(string name, Properties properties)
        {
            SetNodeProperties("Orderer", name, orderers, properties);
        }


        /**
         * Set a specific eventhub's properties.
         *
         * @param name       The name of the eventhub's property to set.
         * @param properties The properties to set.
         * @throws InvalidArgumentException
         */
        public void SetEventHubProperties(string name, Properties properties)
        {
            SetNodeProperties("EventHub", name, eventHubs, properties);
        }

        /**
         * Creates a new NetworkConfig instance configured with details supplied in a YAML file.
         *
         * @param configFile The file containing the network configuration
         * @return A new NetworkConfig instance
         * @throws InvalidArgumentException
         * @throws IOException
         */
        public static NetworkConfig FromYamlFile(FileInfo configFile)
        {
            return FromFile(configFile, false);
        }

        /**
         * Creates a new NetworkConfig instance configured with details supplied in a JSON file.
         *
         * @param configFile The file containing the network configuration
         * @return A new NetworkConfig instance
         * @throws InvalidArgumentException
         * @throws IOException
         */
        public static NetworkConfig FromJsonFile(FileInfo configFile)
        {
            return FromFile(configFile, true);
        }

        /**
         * Creates a new NetworkConfig instance configured with details supplied in YAML format
         *
         * @param configStream A stream opened on a YAML document containing network configuration details
         * @return A new NetworkConfig instance
         * @throws InvalidArgumentException
         */
        public static NetworkConfig FromYamlStream(Stream configStream)
        {
            logger.Trace("NetworkConfig.fromYamlStream...");

            // Sanity check
            if (configStream == null)
            {
                throw new InvalidArgumentException("configStream must be specified");
            }

            var r = new StreamReader(configStream);
            var deserializer = new Deserializer(namingConvention: new CamelCaseNamingConvention());
            var yamlObject = deserializer.Deserialize(r);
            var serializer = new JsonSerializer();
            var w = new StringWriter();
            serializer.Serialize(w, yamlObject);
            return FromJsonObject(JObject.Parse(serializer.ToString()));
        }

        /**
         * Creates a new NetworkConfig instance configured with details supplied in JSON format
         *
         * @param configStream A stream opened on a JSON document containing network configuration details
         * @return A new NetworkConfig instance
         * @throws InvalidArgumentException
         */
        public static NetworkConfig FromJsonStream(Stream configStream)
        {
            logger.Trace("NetworkConfig.fromJsonStream...");

            // Sanity check
            if (configStream == null)
            {
                throw new InvalidArgumentException("configStream must be specified");
            }

            return FromJsonObject(JObject.Parse(Encoding.UTF8.GetString(configStream.ToByteArray())));
        }

        /**
         * Creates a new NetworkConfig instance configured with details supplied in a JSON object
         *
         * @param jsonConfig JSON object containing network configuration details
         * @return A new NetworkConfig instance
         * @throws InvalidArgumentException
         */
        public static NetworkConfig FromJsonObject(JObject jsonConfig)
        {
            // Sanity check
            if (jsonConfig == null)
            {
                throw new InvalidArgumentException("jsonConfig must be specified");
            }

            if (logger.IsTraceEnabled())
            {
                logger.Trace($"NetworkConfig.fromJsonObject: {jsonConfig.ToString()}x");
            }

            return Load(jsonConfig);
        }

        // Loads a NetworkConfig object from a Json or Yaml file
        private static NetworkConfig FromFile(FileInfo configFile, bool isJson)
        {
            // Sanity check
            if (configFile == null)
            {
                throw new InvalidArgumentException("configFile must be specified");
            }

            if (logger.IsTraceEnabled())
            {
                logger.Trace($"NetworkConfig.fromFile: {configFile.FullName}  isJson = {isJson}");
            }

            NetworkConfig config;

            // Json file
            using (Stream stream = configFile.OpenRead())
            {
                return isJson ? FromJsonStream(stream) : FromYamlStream(stream);
            }
        }

        /**
         * Returns a new NetworkConfig instance and populates it from the specified JSON object
         *
         * @param jsonConfig The JSON object containing the config details
         * @return A populated NetworkConfig instance
         * @throws InvalidArgumentException
         */
        private static NetworkConfig Load(JObject jsonConfig)
        {
            // Sanity check
            if (jsonConfig == null)
            {
                throw new InvalidArgumentException("config must be specified");
            }

            return new NetworkConfig(jsonConfig);
        }

        public OrgInfo GetClientOrganization()
        {
            return clientOrganization;
        }

        public OrgInfo GetOrganizationInfo(string orgName)
        {
            return organizations.GetOrNull(orgName);
        }

        public IReadOnlyList<OrgInfo> GetOrganizationInfos()
        {
            return organizations.Values.ToList();
        }

        /**
         * Returns the admin user associated with the client organization
         *
         * @return The admin user details
         * @throws NetworkConfigurationException
         */
        public UserInfo GetPeerAdmin()
        {
            // Get the details from the client organization
            return GetPeerAdmin(clientOrganization.Name);
        }

        /**
         * Returns the admin user associated with the specified organization
         *
         * @param orgName The name of the organization
         * @return The admin user details
         * @throws NetworkConfigurationException
         */
        public UserInfo GetPeerAdmin(string orgName)
        {
            OrgInfo org = GetOrganizationInfo(orgName);
            if (org == null)
            {
                throw new NetworkConfigurationException($"Organization {orgName} is not defined");
            }

            return org.PeerAdmin;
        }

        /**
         * Returns a channel configured using the details in the Network Configuration file
         *
         * @param client      The associated client
         * @param channelName The name of the channel
         * @return A configured Channel instance
         */
        public Channel LoadChannel(HFClient client, string channelName)
        {
            if (logger.IsTraceEnabled())
            {
                logger.Trace($"NetworkConfig.loadChannel: {channelName}");
            }

            Channel channel = null;

            JToken channels = jsonConfig["channels"];

            if (channels != null)
            {
                JToken jsonChannel = channels[channelName];
                if (jsonChannel != null)
                {
                    channel = client.GetChannel(channelName);
                    if (channel != null)
                    {
                        // The channel already exists in the client!
                        // Note that by rights this should never happen as HFClient.loadChannelFromConfig should have already checked for this!
                        throw new NetworkConfigurationException($"Channel {channelName} is already configured in the client!");
                    }

                    channel = ReconstructChannel(client, channelName, jsonChannel);
                }
                else
                {
                    List<string> channelNames = GetChannelNames();
                    if (channelNames.Count == 0)
                    {
                        throw new NetworkConfigurationException("Channel configuration has no channels defined.");
                    }

                    StringBuilder sb = new StringBuilder(1000);

                    channelNames.ForEach(s =>
                    {
                        if (sb.Length != 0)
                            sb.Append(", ");
                        sb.Append(s);
                    });
                    throw new NetworkConfigurationException($"Channel {channelName} not found in configuration file. Found channel names: {sb.ToString()}");
                }
            }
            else
            {
                throw new NetworkConfigurationException("Channel configuration has no channels defined.");
            }

            return channel;
        }

        // Creates Node instances representing all the orderers defined in the config file
        private void CreateAllOrderers()
        {
            // Sanity check
            if (orderers != null)
            {
                throw new NetworkConfigurationException("INTERNAL ERROR: orderers has already been initialized!");
            }

            orderers = new Dictionary<string, Node>();

            // orderers is a JSON object containing a nested object for each orderers
            JToken jsonOrderers = jsonConfig["orderers"];

            if (jsonOrderers != null)
            {
                foreach (JProperty prop in jsonOrderers.Children<JObject>().Select(a => a.Properties().First()))
                {
                    string ordererName = prop.Name;
                    JToken jsonOrderer = prop.Value;
                    if (jsonOrderer == null)
                    {
                        throw new NetworkConfigurationException($"Error loading config. Invalid orderer entry: {ordererName}");
                    }

                    Node orderer = CreateNode(ordererName, jsonOrderer, "url");
                    if (orderer == null)
                    {
                        throw new NetworkConfigurationException($"Error loading config. Invalid orderer entry: {ordererName}");
                    }

                    orderers.Add(ordererName, orderer);
                }
            }
        }

        // Creates Node instances representing all the peers (and associated event hubs) defined in the config file
        private void CreateAllPeers()
        {
            // Sanity checks
            if (peers != null)
            {
                throw new NetworkConfigurationException("INTERNAL ERROR: peers has already been initialized!");
            }

            if (eventHubs != null)
            {
                throw new NetworkConfigurationException("INTERNAL ERROR: eventHubs has already been initialized!");
            }

            peers = new Dictionary<string, Node>();
            eventHubs = new Dictionary<string, Node>();

            // peers is a JSON object containing a nested object for each peer
            JToken jsonPeers = jsonConfig["peers"];

            //out("Peers: " + (jsonPeers == null ? "null" : jsonPeers.toString()));
            if (jsonPeers != null)
            {
                foreach (JProperty prop in jsonPeers.Children<JObject>().Select(a => a.Properties().First()))
                {
                    string peerName = prop.Name;

                    JToken jsonPeer = prop.Value;
                    if (jsonPeer == null)
                    {
                        throw new NetworkConfigurationException($"Error loading config. Invalid peer entry: {peerName}");
                    }

                    Node peer = CreateNode(peerName, jsonPeer, "url");
                    if (peer == null)
                    {
                        throw new NetworkConfigurationException($"Error loading config. Invalid peer entry: {peerName}");
                    }

                    peers.Add(peerName, peer);

                    // Also create an event hub with the same name as the peer
                    Node eventHub = CreateNode(peerName, jsonPeer, "eventUrl"); // may not be present
                    if (null != eventHub)
                    {
                        eventHubs.Add(peerName, eventHub);
                    }
                }
            }
        }

        // Produce a map from tag to jsonobject for the CA
        private Dictionary<string, JToken> FindCertificateAuthorities()
        {
            Dictionary<string, JToken> ret = new Dictionary<string, JToken>();

            JToken jsonCertificateAuthorities = jsonConfig["certificateAuthorities"];
            if (null != jsonCertificateAuthorities)
            {
                foreach (JProperty prop in jsonCertificateAuthorities.Children<JObject>().Select(a => a.Properties().First()))
                {
                    string name = prop.Name;

                    JToken jsonCA = prop.Value;
                    if (jsonCA == null)
                    {
                        throw new NetworkConfigurationException($"Error loading config. Invalid CA entry: {name}");
                    }

                    ret.Add(name, jsonCA);
                }
            }

            return ret;
        }

        // Creates JsonObjects representing all the Organizations defined in the config file
        private void CreateAllOrganizations(Dictionary<string, JToken> foundCertificateAuthorities)
        {
            // Sanity check
            if (organizations != null)
            {
                throw new NetworkConfigurationException("INTERNAL ERROR: organizations has already been initialized!");
            }

            organizations = new Dictionary<string, OrgInfo>();

            // organizations is a JSON object containing a nested object for each Org
            JToken jsonOrganizations = jsonConfig["organizations"];

            if (jsonOrganizations != null)
            {
                foreach (JProperty prop in jsonOrganizations.Children<JObject>().Select(a => a.Properties().First()))
                {
                    string orgName = prop.Name;

                    JToken jsonOrg = prop.Value;
                    if (jsonOrg == null)
                    {
                        throw new NetworkConfigurationException($"Error loading config. Invalid Organization entry: {orgName}");
                    }

                    OrgInfo org = CreateOrg(orgName, jsonOrg, foundCertificateAuthorities);
                    organizations.Add(orgName, org);
                }
            }
        }

        // Reconstructs an existing channel
        private Channel ReconstructChannel(HFClient client, string channelName, JToken jsonChannel)
        {
            Channel channel = null;

            try
            {
                channel = client.NewChannel(channelName);

                // orderers is an array of orderer name strings
                JArray ordererNames = jsonChannel["orderers"] as JArray;
                bool foundOrderer = false;

                //out("Orderer names: " + (ordererNames == null ? "null" : ordererNames.toString()));
                if (ordererNames != null)
                {
                    foreach (JToken jsonVal in ordererNames)
                    {
                        string ordererName = jsonVal.Value<string>();
                        Orderer orderer = GetOrderer(client, ordererName);
                        if (orderer == null)
                        {
                            throw new NetworkConfigurationException($"Error constructing channel {channelName}. Orderer {ordererName} not defined in configuration");
                        }

                        channel.AddOrderer(orderer);
                        foundOrderer = true;
                    }
                }

                if (!foundOrderer)
                {
                    // orderers is a required field
                    throw new NetworkConfigurationException($"Error constructing channel {channelName}. At least one orderer must be specified");
                }

                // peers is an object containing a nested object for each peer
                JToken jsonPeers = jsonChannel["peers"];
                bool foundPeer = false;

                //out("Peers: " + (peers == null ? "null" : peers.toString()));
                if (jsonPeers != null)
                {
                    foreach (JProperty prop in jsonPeers.Children<JObject>().Select(a => a.Properties().First()))
                    {
                        string peerName = prop.Name;

                        if (logger.IsTraceEnabled())
                        {
                            logger.Trace($"NetworkConfig.reconstructChannel: Processing peer {peerName}");
                        }

                        JToken jsonPeer = prop.Value;
                        if (jsonPeer == null)
                        {
                            throw new NetworkConfigurationException($"Error constructing channel {channelName}. Invalid peer entry: {peerName}");
                        }

                        Peer peer = GetPeer(client, peerName);
                        if (peer == null)
                        {
                            throw new NetworkConfigurationException($"Error constructing channel {channelName}. Peer {peerName} not defined in configuration");
                        }

                        // Set the various roles
                        Channel.PeerOptions peerOptions = Channel.PeerOptions.CreatePeerOptions();
                        SetPeerRole(channelName, peerOptions, jsonPeer, PeerRole.ENDORSING_PEER);
                        SetPeerRole(channelName, peerOptions, jsonPeer, PeerRole.CHAINCODE_QUERY);
                        SetPeerRole(channelName, peerOptions, jsonPeer, PeerRole.LEDGER_QUERY);
                        SetPeerRole(channelName, peerOptions, jsonPeer, PeerRole.EVENT_SOURCE);

                        foundPeer = true;

                        // Add the event hub associated with this peer
                        EventHub eventHub = GetEventHub(client, peerName);
                        if (eventHub != null)
                        {
                            channel.AddEventHub(eventHub);
                            if (!peerOptions.HasPeerRoles())
                            {
                                // means no roles were found but there is an event hub so define all roles but eventing.
                                peerOptions.SetPeerRoles(new List<PeerRole> {PeerRole.ENDORSING_PEER, PeerRole.CHAINCODE_QUERY, PeerRole.LEDGER_QUERY});
                            }
                        }

                        channel.AddPeer(peer, peerOptions);
                    }
                }

                if (!foundPeer)
                {
                    // peers is a required field
                    throw new NetworkConfigurationException($"Error constructing channel {channelName}. At least one peer must be specified");
                }
            }
            catch (InvalidArgumentException e)
            {
                throw;
            }

            return channel;
        }

        private static void SetPeerRole(string channelName, Channel.PeerOptions peerOptions, JToken jsonPeer, PeerRole role)
        {
            string propName = role.ToValue();
            JToken val = jsonPeer[propName];
            if (val != null)
            {
                bool isSet = val.Value<bool>();
                /*if (isSet == null) {
                    // This is an invalid boolean value
                    throw new NetworkConfigurationException(format("Error constructing channel %s. Role %s has invalid boolean value: %s", channelName, propName, val.toString()));
                }*/
                if (isSet)
                {
                    peerOptions.AddPeerRole(role);
                }
            }
        }

        // Returns a new Orderer instance for the specified orderer name
        private Orderer GetOrderer(HFClient client, string ordererName)
        {
            Orderer orderer = null;
            Node o = orderers.GetOrNull(ordererName);
            if (o != null)
            {
                orderer = client.NewOrderer(o.Name, o.Url, o.Properties);
            }

            return orderer;
        }

        // Creates a new Node instance from a JSON object
        private Node CreateNode(string nodeName, JToken jsonNode, string urlPropName)
        {
            //        jsonNode.
            //        if (jsonNode.isNull(urlPropName)) {
            //            return  null;
            //        }

            string url = jsonNode[urlPropName].Value<string>();
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            Properties props = ExtractProperties(jsonNode, "grpcOptions");

            // Extract the pem details
            GetTLSCerts(nodeName, jsonNode, props);

            return new Node(nodeName, url, props);
        }

        private void GetTLSCerts(string nodeName, JToken jsonOrderer,Properties props)
        {
            JToken jsonTlsCaCerts = jsonOrderer["tlsCACerts"];
            if (jsonTlsCaCerts != null)
            {
                string pemFilename = jsonTlsCaCerts["path"].Value<string>();
                string pemBytes = jsonTlsCaCerts["pem"].Value<string>();

                if (!string.IsNullOrEmpty(pemFilename))
                {
                    // let the sdk handle non existing errors could be they don't exist during parsing but are there later.
                    props.Set("pemFile", pemFilename);
                }

                if (!string.IsNullOrEmpty(pemBytes))
                {
                    props.Set("pemBytes", pemBytes);
                }
            }
        }

        // Creates a new OrgInfo instance from a JSON object
        private OrgInfo CreateOrg(string orgName, JToken jsonOrg, Dictionary<string, JToken> foundCertificateAuthorities)
        {
            string msgPrefix = $"Organization {orgName}";

            string mspId = jsonOrg["mspid"].Value<string>();

            OrgInfo org = new OrgInfo(orgName, mspId);

            // Peers
            JArray jsonPeers = jsonOrg["peers"] as JArray;
            if (jsonPeers != null)
            {
                foreach (JToken peer in jsonPeers)
                {
                    string peerName = peer.Value<string>();
                    if (!string.IsNullOrEmpty(peerName))
                    {
                        org.PeerName.Add(peerName);
                    }
                }
            }

            // CAs
            JArray jsonCertificateAuthorities = jsonOrg["certificateAuthorities"] as JArray;
            if (jsonCertificateAuthorities != null)
            {
                foreach (JToken jsonCA in jsonCertificateAuthorities)
                {
                    string caName = jsonCA.Value<string>();

                    if (!string.IsNullOrEmpty(caName))
                    {
                        JToken jsonObject = foundCertificateAuthorities[caName];
                        if (jsonObject != null)
                        {
                            org.CertificateAuthorities.Add(CreateCA(caName, jsonObject, org));
                        }
                        else
                        {
                            throw new NetworkConfigurationException($"{msgPrefix}: Certificate Authority {caName} is not defined");
                        }
                    }
                }
            }

            string adminPrivateKeyString = ExtractPemString(jsonOrg, "adminPrivateKey", msgPrefix);
            string signedCert = ExtractPemString(jsonOrg, "signedCert", msgPrefix);

            if (!string.IsNullOrEmpty(adminPrivateKeyString) && !string.IsNullOrEmpty(signedCert))
            {
                AsymmetricAlgorithm privateKey = null;

                try
                {
                    privateKey = GetPrivateKeyFromString(adminPrivateKeyString);
                }
                catch (IOException ioe)
                {
                    throw new NetworkConfigurationException($"{msgPrefix}: Invalid private key", ioe);
                }


                org.PeerAdmin = new UserInfo(mspId, "PeerAdmin_" + mspId + "_" + orgName, null);
                org.PeerAdmin.Enrollment = new Enrollment(privateKey, signedCert);
            }

            return org;
        }

        private static AsymmetricAlgorithm GetPrivateKeyFromString(string data)
        {
            CryptoPrimitives cr = new CryptoPrimitives();
            return cr.BytesToPrivateKey(Encoding.UTF8.GetBytes(data));
        }

        // Returns the PEM (as a String) from either a path or a pem field
        private static string ExtractPemString(JToken json, string fieldName, string msgPrefix)
        {
            string path = null;
            string pemString = null;

            JToken jsonField = json[fieldName];
            if (jsonField != null)
            {
                path = jsonField["path"].Value<string>();
                pemString = jsonField["pem"].Value<string>();
            }

            if (path != null && pemString != null)
            {
                throw new NetworkConfigurationException($"{msgPrefix} should not specify both {fieldName} path and pem");
            }

            if (path != null)
            {
                // Determine full pathname and ensure the file exists
                FileInfo pemFile = new FileInfo(path);
                string fullPathname = pemFile.FullName;
                if (!pemFile.Exists)
                {
                    throw new NetworkConfigurationException($"{msgPrefix}: {fieldName} file {fullPathname} does not exist");
                }

                try
                {
                    pemString = File.ReadAllText(pemFile.FullName, Encoding.UTF8);
                }
                catch (Exception ioe)
                {
                    throw new NetworkConfigurationException($"Failed to read file: {fullPathname}", ioe);
                }
            }

            return pemString;
        }

        // Creates a new CAInfo instance from a JSON object
        private CAInfo CreateCA(string name, JToken jsonCA, OrgInfo org)
        {
            string url = jsonCA["url"].Value<string>();
            Properties httpOptions = ExtractProperties(jsonCA, "httpOptions");

            string enrollId = null;
            string enrollSecret = null;

            List<JToken> registrars = jsonCA["registar"].Children<JToken>().ToList();
            List<UserInfo> regUsers = new List<UserInfo>();
            if (registrars != null)
            {
                foreach (JToken reg in registrars)
                {
                    enrollId = reg["enrollId"].Value<string>();
                    enrollSecret = reg["enrollSecret"].Value<string>();
                    regUsers.Add(new UserInfo(org.MspId, enrollId, enrollSecret));
                }
            }

            CAInfo caInfo = new CAInfo(name, org.MspId, url, regUsers, httpOptions);

            string caName = jsonCA["caName"].Value<string>();
            if (!string.IsNullOrEmpty(caName))
            {
                caInfo.CaName = caName;
            }

            Properties properties = new Properties();
            if (null != httpOptions && "false".Equals((string) httpOptions["verify"], StringComparison.CurrentCultureIgnoreCase))
            {
                properties["allowAllHostNames"] = "true";
            }

            GetTLSCerts(name, jsonCA, properties);
            caInfo.Properties = properties;

            return caInfo;
        }

        // Extracts all defined properties of the specified field and returns a Properties object
        private static Properties ExtractProperties(JToken json, string fieldName)
        {
            Properties props = new Properties();

            // Extract any other grpc options
            JToken options = json[fieldName];
            if (options != null)
            {
                foreach (JProperty prop in options.Children<JObject>().Select(a => a.Properties().First()))
                {
                    string key = prop.Name;
                    props[key] = prop.Value<string>();
                }
            }

            return props;
        }

        // Returns a new Peer instance for the specified peer name
        private Peer GetPeer(HFClient client, string peerName)
        {
            Peer peer = null;
            Node p = peers.GetOrNull(peerName);
            if (p != null)
            {
                peer = client.NewPeer(p.Name, p.Url, p.Properties);
            }

            return peer;
        }

        // Returns a new EventHub instance for the specified name
        private EventHub GetEventHub(HFClient client, string name)
        {
            EventHub ehub = null;
            Node e = eventHubs.GetOrNull(name);
            if (e != null)
            {
                ehub = client.NewEventHub(e.Name, e.Url, e.Properties);
            }

            return ehub;
        }
        /*
        // Returns the specified JsonValue in a suitable format
        // If it's a JsonString - it returns the string
        // If it's a number = it returns the string representation of that number
        // If it's TRUE or FALSE - it returns "true" and "false" respectively
        // If it's anything else it returns null
        private static String getJsonValue(JsonValue value) {
            String s = null;
            if (value != null) {
                s = getJsonValueAsString(value);
                if (s == null) {
                    s = getJsonValueAsNumberString(value);
                }
                if (s == null) {
                    Boolean b = getJsonValueAsBoolean(value);
                    if (b != null) {
                        s = b ? "true" : "false";
                    }
                }
            }
            return s;
        }

        // Returns the specified JsonValue as a JsonObject, or null if it's not an object
        private static JsonObject getJsonValueAsObject(JsonValue value) {
            return (value != null && value.getValueType() == ValueType.OBJECT) ? value.asJsonObject() : null;
        }

        // Returns the specified JsonValue as a JsonArray, or null if it's not an array
        private static JsonArray getJsonValueAsArray(JsonValue value) {
            return (value != null && value.getValueType() == ValueType.ARRAY) ? value.asJsonArray() : null;
        }

        // Returns the specified JsonValue as a List. Allows single or array
        private static List<JsonObject> getJsonValueAsList(JsonValue value) {
            if (value != null) {
                if (value.getValueType() == ValueType.ARRAY) {
                    return value.asJsonArray().getValuesAs(JsonObject.class);

                } else if (value.getValueType() == ValueType.OBJECT) {
                    List<JsonObject> ret = new ArrayList<>();
                    ret.add(value.asJsonObject());

                    return ret;
                }
            }
            return null;
        }

        // Returns the specified JsonValue as a String, or null if it's not a string
        private static String getJsonValueAsString(JsonValue value) {
            return (value != null && value.getValueType() == ValueType.STRING) ? ((JsonString) value).getString() : null;
        }

        // Returns the specified JsonValue as a String, or null if it's not a string
        private static String getJsonValueAsNumberString(JsonValue value) {
            return (value != null && value.getValueType() == ValueType.NUMBER) ? value.toString() : null;
        }

        // Returns the specified JsonValue as a Boolean, or null if it's not a boolean
        private static Boolean getJsonValueAsBoolean(JsonValue value) {
            if (value != null) {
                if (value.getValueType() == ValueType.TRUE) {
                    return true;
                } else if (value.getValueType() == ValueType.FALSE) {
                    return false;
                }
            }
            return null;
        }

        // Returns the specified property as a JsonObject
        private static JsonObject getJsonObject(JsonObject object, String propName) {
            JsonObject obj = null;
            JsonValue val = object.get(propName);
            if (val != null && val.getValueType() == ValueType.OBJECT) {
                obj = val.asJsonObject();
            }
            return obj;
        }

        /**
         * Get the channel names found.
         *
         * @return A set of the channel names found in the configuration file or empty set if none found.
         */

        public List<string> GetChannelNames()
        {
            List<string> ret = new List<string>();

            JToken channels = jsonConfig["channels"];
            if (channels != null)
            {
                foreach (JProperty prop in channels.Children<JObject>().Select(a => a.Properties().First()))
                {
                    ret.Add(prop.Name);
                }
            }

            return ret;
        }


        // Holds a network "node" (eg. Peer, Orderer, EventHub)
        public class Node
        {
            public Node(string name, string url, Properties properties)
            {
                Url = url;
                Name = name;
                Properties = properties;
            }

            public string Name { get; }
            public string Url { get; }
            public Properties Properties { get; set; }
        }

        /**
         * Holds details of a User
         */
        public class UserInfo : IUser
        {
            public UserInfo(string mspid, string name, string enrollSecret)
            {
                Name = name;
                EnrollSecret = enrollSecret;
                MspId = mspid;
            }

            public string EnrollSecret { get; set; }

            public string Name { get; set; }
            public HashSet<string> Roles { get; set; }
            public string Account { get; set; }
            public string Affiliation { get; set; }
            public IEnrollment Enrollment { get; set; }
            public string MspId { get; set; }
        }


        /**
         * Holds details of an Organization
         */
        public class OrgInfo
        {
            public OrgInfo(string orgName, string mspId)
            {
                Name = orgName;
                MspId = mspId;
            }

            public string Name { get; set; }
            public string MspId { get; set; }
            public List<string> PeerName { get; } = new List<string>();
            public List<CAInfo> CertificateAuthorities { get; } = new List<CAInfo>();

            public UserInfo PeerAdmin { get; set; }
        }

        /**
     * Holds the details of a Certificate Authority
     */
        public class CAInfo
        {
            public CAInfo(string name, string mspid, string url, List<UserInfo> registrars, Properties httpOptions)
            {
                Name = name;
                Url = url;
                HttpOptions = httpOptions;
                Registrars = registrars;
                MspId = mspid;
            }

            public string Name { get; set; }
            public string Url { get; set; }
            public Properties HttpOptions { get; set; } = new Properties();

            public string MspId { get; set; }
            public string CaName { get; set; }
            public Properties Properties { get; set; } = new Properties();

            public List<UserInfo> Registrars { get; set; }
        }
    }
}