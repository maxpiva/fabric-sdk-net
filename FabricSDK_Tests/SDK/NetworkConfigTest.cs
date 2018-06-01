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


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Grpc.Core;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Channel = Hyperledger.Fabric.SDK.Channel;

namespace Hyperledger.Fabric.Tests.SDK
{
    [TestClass]
    [TestCategory("SDK")]
    public class NetworkConfigTest
    {
        private static readonly string CHANNEL_NAME = "myChannel";
        private static readonly string CLIENT_ORG_NAME = "Org1";

        private static readonly string USER_NAME = "MockMe";
        private static readonly string USER_MSP_ID = "MockMSPID";


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "configStream must be specified")]
        public void TestLoadFromConfigNullStream()
        {
            // Should not be able to instantiate a new instance of "Client" without a valid path to the configuration');


            NetworkConfig.FromJsonStream((Stream) null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "configFile must be specified")]
        public void TestLoadFromConfigNullYamlFile()
        {
            // Should not be able to instantiate a new instance of "Client" without a valid path to the configuration');


            NetworkConfig.FromYamlFile(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "configFile must be specified")]
        public void TestLoadFromConfigNullJsonFile()
        {
            // Should not be able to instantiate a new instance of "Client" without a valid path to the configuration');

            NetworkConfig.FromJsonFile(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(FileNotFoundException), "FileDoesNotExist.yaml")]
        public void TestLoadFromConfigYamlFileNotExists()
        {
            // Should not be able to instantiate a new instance of "Client" without an actual configuration file


            NetworkConfig.FromYamlFile("FileDoesNotExist.yaml");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(FileNotFoundException), "FileDoesNotExist.json")]
        public void TestLoadFromConfigJsonFileNotExists()
        {
            // Should not be able to instantiate a new instance of "Client" without an actual configuration file


            NetworkConfig.FromJsonFile("FileDoesNotExist.json");
        }

        [TestMethod]
        public void TestLoadFromConfigFileYamlBasic()
        {
            string f = RelocateFilePathsYAML("fixture/sdkintegration/network_configs/network-config.yaml".Locate());
            NetworkConfig config = NetworkConfig.FromYamlFile(f);
            Assert.IsNotNull(config);
            List<string> channelNames = config.GetChannelNames();

            Assert.IsTrue(channelNames.Contains("foo"));
        }

        [TestMethod]
        public void TestLoadFromConfigFileJsonBasic()
        {
            string f= RelocateFilePathsJSON("fixture/sdkintegration/network_configs/network-config.json".Locate());
            NetworkConfig config = NetworkConfig.FromJsonFile(f);
            Assert.IsNotNull(config);
        }

        [TestMethod]
        public void TestLoadFromConfigFileYaml()
        {
            // Should be able to instantiate a new instance of "Client" with a valid path to the YAML configuration
            string f=RelocateFilePathsYAML("fixture/sdkintegration/network_configs/network-config.yaml".Locate());
            NetworkConfig config = NetworkConfig.FromYamlFile(f);
            Assert.IsNotNull(config);

            HFClient client = HFClient.Create();
            client.CryptoSuite = Factory.Instance.GetCryptoSuite();
            client.UserContext = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);

            Channel channel = client.LoadChannelFromConfig("foo", config);
            Assert.IsNotNull(channel);
        }

        [TestMethod]
        public void TestLoadFromConfigFileJson()
        {
            // Should be able to instantiate a new instance of "Client" with a valid path to the JSON configuration
            string f = RelocateFilePathsJSON("fixture/sdkintegration/network_configs/network-config.json".Locate());
            NetworkConfig config = NetworkConfig.FromJsonFile(f);
            Assert.IsNotNull(config);

            //HFClient client = HFClient.loadFromConfig(f);
            //Assert.Assert.IsNotNull(client);

            HFClient client = HFClient.Create();
            client.CryptoSuite = Factory.Instance.GetCryptoSuite();
            client.UserContext = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);

            Channel channel = client.LoadChannelFromConfig("mychannel", config);
            Assert.IsNotNull(channel);
        }


        private string RelocateFilePathsJSON(string filename)
        {
            return RelocateFilePaths(filename, ".json", "\"path\":\\s?\"(.*?)\"");
        }
        private string RelocateFilePathsYAML(string filename)
        {
            return RelocateFilePaths(filename, ".yaml", "path:\\s?(.*?)\r");
        }
        private string RelocateFilePaths(string filename, string ext, string regex)
        {
            string tempfile = Path.GetTempFileName() + ext;
            string json = File.ReadAllText(filename);
            MatchCollection matches = new Regex(regex).Matches(json);
            Console.WriteLine("Match COUNT:"+matches.Count+" Regex:"+regex);
            foreach (Match m in matches)
            {
                if (m.Success)
                {
                    bool replace = false;
                    string path = m.Groups[1].Value;
                    Console.WriteLine("Match:"+path);
                    if (path.StartsWith("\"") && path.EndsWith("\""))
                        path = path.Substring(1, path.Length - 2);
                    string orgpath = path;
                    if (path.StartsWith("/"))
                        path = path.Substring(1);
                    if (path.StartsWith("src/test"))
                    {
                        replace = true;
                        path = path.Substring(9);
                    }

                    if (replace)
                    {
                        path = path.Locate().Replace("\\","/");
                        json = json.Replace(orgpath, path);
                        Console.WriteLine("Replace: " + orgpath+" x "+path);
                    } 
                }
            }
            Console.WriteLine("JSON: "+json);
            File.WriteAllText(tempfile,json);
            return tempfile;
        }
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "client organization must be specified")]
        public void TestLoadFromConfigNoOrganization()
        {
            // Should not be able to instantiate a new instance of "Channel" without specifying a valid client organization


            JObject jsonConfig = GetJsonConfig(0, 1, 0);

            NetworkConfig.FromJsonObject(jsonConfig);
        }

        [TestMethod]
        public void TestGetClientOrg()
        {
            JObject jsonConfig = GetJsonConfig(1, 0, 0);

            NetworkConfig config = NetworkConfig.FromJsonObject(jsonConfig);

            Assert.AreEqual(CLIENT_ORG_NAME, config.GetClientOrganization().Name);
        }

        // TODO: At least one orderer must be specified
        [TestMethod]
        [Ignore]
        public void TestNewChannel()
        {
            // Should be able to instantiate a new instance of "Channel" with the definition in the network configuration'
            JObject jsonConfig = GetJsonConfig(1, 0, 0);

            NetworkConfig config = NetworkConfig.FromJsonObject(jsonConfig);

            HFClient client = HFClient.Create();
            TestHFClient.SetupClient(client);

            Channel channel = client.LoadChannelFromConfig(CHANNEL_NAME, config);
            Assert.IsNotNull(channel);
            Assert.AreEqual(CHANNEL_NAME, channel.Name);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(NetworkConfigurationException), "Channel MissingChannel not found in configuration file. Found channel names: foo")]
        public void TestGetChannelNotExists()
        {
            // Should be able to instantiate a new instance of "Client" with a valid path to the YAML configuration
            string f = RelocateFilePathsYAML("fixture/sdkintegration/network_configs/network-config.yaml".Locate());
            NetworkConfig config = NetworkConfig.FromYamlFile(f);
            //HFClient client = HFClient.loadFromConfig(f);
            Assert.IsNotNull(config);


            HFClient client = HFClient.Create();
            client.CryptoSuite = Factory.Instance.GetCryptoSuite();
            client.UserContext = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);

            client.LoadChannelFromConfig("MissingChannel", config);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(NetworkConfigurationException), "Error constructing")]
        public void TestGetChannelNoOrderersOrPeers()
        {
            // Should not be able to instantiate a new instance of "Channel" with no orderers or peers configured
            JObject jsonConfig = GetJsonConfig(1, 0, 0);

            NetworkConfig config = NetworkConfig.FromJsonObject(jsonConfig);

            HFClient client = HFClient.Create();
            TestHFClient.SetupClient(client);

            client.LoadChannelFromConfig(CHANNEL_NAME, config);

            //HFClient client = HFClient.loadFromConfig(jsonConfig);
            //TestHFClient.setupClient(client);

            //client.getChannel(CHANNEL_NAME);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(NetworkConfigurationException), "Error constructing")]
        public void TestGetChannelNoOrderers()
        {
            // Should not be able to instantiate a new instance of "Channel" with no orderers configured
            JObject jsonConfig = GetJsonConfig(1, 0, 1);

            //HFClient client = HFClient.loadFromConfig(jsonConfig);
            //TestHFClient.setupClient(client);

            //client.getChannel(CHANNEL_NAME);

            NetworkConfig config = NetworkConfig.FromJsonObject(jsonConfig);

            HFClient client = HFClient.Create();
            TestHFClient.SetupClient(client);

            client.LoadChannelFromConfig(CHANNEL_NAME, config);
        }


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(NetworkConfigurationException), "Error constructing")]
        public void TestGetChannelNoPeers()
        {
            // Should not be able to instantiate a new instance of "Channel" with no peers configured
            JObject jsonConfig = GetJsonConfig(1, 1, 0);

            NetworkConfig config = NetworkConfig.FromJsonObject(jsonConfig);

            HFClient client = HFClient.Create();
            TestHFClient.SetupClient(client);

            client.LoadChannelFromConfig(CHANNEL_NAME, config);

            //HFClient client = HFClient.loadFromConfig(jsonConfig);
            //TestHFClient.setupClient(client);

            //client.getChannel(CHANNEL_NAME);
        }

        [TestMethod]
        public void TestLoadFromConfigFileYamlNOOverrides()
        {
            // Should be able to instantiate a new instance of "Client" with a valid path to the YAML configuration
            string f = RelocateFilePathsYAML("fixture/sdkintegration/network_configs/network-config.yaml".Locate());
            NetworkConfig config = NetworkConfig.FromYamlFile(f);

            //HFClient client = HFClient.loadFromConfig(f);
            Assert.IsNotNull(config);
            HFClient client = HFClient.Create();
            client.CryptoSuite = Factory.Instance.GetCryptoSuite();
            client.UserContext = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);

            Channel channel = client.LoadChannelFromConfig("foo", config);
            Assert.IsNotNull(channel);

            Assert.IsTrue(channel.Peers.Count != 0);

            foreach (Peer peer in channel.Peers)
            {
                Properties properties = peer.Properties;

                Assert.IsNotNull(properties);
                Assert.IsNull(properties.Get("grpc.keepalive_time_ms"));
                Assert.IsNull(properties.Get("grpc.keepalive_timeout_ms"));
                //Assert.IsNull(properties.Get("grpc.NettyChannelBuilderOption.keepAliveWithoutCalls"));
            }
        }

        [TestMethod]
        public void TestLoadFromConfigFileYamlNOOverridesButSet()
        {
            // Should be able to instantiate a new instance of "Client" with a valid path to the YAML configuration
            string f = RelocateFilePathsYAML("fixture/sdkintegration/network_configs/network-config.yaml".Locate());
            NetworkConfig config = NetworkConfig.FromYamlFile(f);

            //HFClient client = HFClient.loadFromConfig(f);
            Assert.IsNotNull(config);

            HFClient client = HFClient.Create();
            client.CryptoSuite = Factory.Instance.GetCryptoSuite();
            client.UserContext = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);


            Channel channel = client.LoadChannelFromConfig("foo", config);
            Assert.IsNotNull(channel);

            Assert.IsTrue(channel.Orderers.Count != 0);

            foreach (Orderer orderer in channel.Orderers)
            {
                Properties properties = orderer.Properties;
                string str = properties.Get("grpc.keepalive_time_ms");
                Assert.AreEqual(long.Parse(str), 360000L);

                str = properties.Get("grpc.keepalive_timeout_ms");
                Assert.AreEqual(long.Parse(str), 180000L);
            }
        }


        [TestMethod]
        public void TestLoadFromConfigFileYamlOverrides()
        {
            // Should be able to instantiate a new instance of "Client" with a valid path to the YAML configuration
            string f = RelocateFilePathsYAML("fixture/sdkintegration/network_configs/network-config.yaml".Locate());
            NetworkConfig config = NetworkConfig.FromYamlFile(f);

            foreach (string peerName in config.PeerNames)
            {
                Properties peerProperties = config.GetPeerProperties(peerName);

                //example of setting keepAlive to avoid timeouts on inactive http2 connections.
                // Under 5 minutes would require changes to server side to accept faster ping rates.
                peerProperties.Set("grpc.keepalive_time_ms", 5 * 60 * 1000);
                peerProperties.Set("grpc.keepalive_timeout_ms", 8 * 1000);
//            peerProperties.Set("grpc.NettyChannelBuilderOption.keepAliveWithoutCalls", "true");
                config.SetPeerProperties(peerName, peerProperties);
            }

            foreach (string orderName in config.OrdererNames)
            {
                Properties ordererProperties = config.GetOrdererProperties(orderName);
                ordererProperties.Set("grpc.max_receive_message_length", 9000000);
                //ordererProperties.put("grpc.NettyChannelBuilderOption.keepAliveWithoutCalls", new Object[] {false});
                config.SetOrdererProperties(orderName, ordererProperties);
            }

            //HFClient client = HFClient.loadFromConfig(f);
            Assert.IsNotNull(config);

            HFClient client = HFClient.Create();
            client.CryptoSuite = Factory.Instance.GetCryptoSuite();
            client.UserContext = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);

            Channel channel = client.LoadChannelFromConfig("foo", config);
            Assert.IsNotNull(channel);

            Assert.IsTrue(channel.Peers.Count > 0);

            foreach (Peer peer in channel.Peers)
            {
                Properties properties = peer.Properties;

                Assert.IsNotNull(properties);
                Assert.IsNotNull(properties.Get("grpc.keepalive_time_ms"));
                Assert.IsNotNull(properties.Get("grpc.keepalive_timeout_ms"));
                //Assert.IsNotNull(properties.get("grpc.NettyChannelBuilderOption.keepAliveWithoutCalls"));

                Endpoint ep = new Endpoint(peer.Url, properties);
                ChannelOption keepalive = ep.ChannelOptions.FirstOrDefault(a => a.Name == "grpc.keepalive_time_ms");
                ChannelOption keepalivetimeout = ep.ChannelOptions.FirstOrDefault(a => a.Name == "grpc.keepalive_timeout_ms");
                Assert.IsNotNull(keepalive);
                Assert.IsNotNull(keepalivetimeout);
                Assert.AreEqual(5 * 60 * 1000, keepalive.IntValue);
                Assert.AreEqual(8 * 1000, keepalivetimeout.IntValue);
            }

            foreach (Orderer orderer in channel.Orderers)
            {
                Properties properties = orderer.Properties;

                Assert.IsNotNull(properties);
                Assert.IsNotNull(properties.Get("grpc.max_receive_message_length"));
                //Assert.IsNotNull(properties.get("grpc.NettyChannelBuilderOption.keepAliveWithoutCalls"));

                Endpoint ep = new Endpoint(orderer.Url, properties);
                ChannelOption msize = ep.ChannelOptions.FirstOrDefault(a => a.Name == "grpc.max_receive_message_length");
                Assert.IsNotNull(msize);
                Assert.AreEqual(9000000, msize.IntValue);
            }
        }

        // TODO: ca-org1 not defined
        [TestMethod]
        [Ignore]
        public void TestGetChannel()
        {
            // Should be able to instantiate a new instance of "Channel" with orderer, org and peer defined in the network configuration
            JObject jsonConfig = GetJsonConfig(4, 1, 1);

            NetworkConfig config = NetworkConfig.FromJsonObject(jsonConfig);

            HFClient client = HFClient.Create();
            TestHFClient.SetupClient(client);

            Channel channel = client.LoadChannelFromConfig(CHANNEL_NAME, config);

            //HFClient client = HFClient.loadFromConfig(jsonConfig);
            //TestHFClient.setupClient(client);

            //Channel channel = client.getChannel(CHANNEL_NAME);
            Assert.IsNotNull(channel);
            Assert.AreEqual(CHANNEL_NAME, channel.Name);

            IReadOnlyList<Orderer> orderers = channel.Orderers;
            Assert.IsNotNull(orderers);
            Assert.AreEqual(1, orderers.Count);

            Orderer orderer = orderers.First();
            Assert.AreEqual("orderer1.example.com", orderer.Name);

            IReadOnlyList<Peer> peers = channel.Peers;
            Assert.IsNotNull(peers);
            Assert.AreEqual(1, peers.Count);

            Peer peer = peers.First();
            Assert.AreEqual("peer0.org1.example.com", peer.Name);
        }

        private static JObject GetJsonConfig(int nOrganizations, int nOrderers, int nPeers)
        {
            // Sanity check
            if (nPeers > nOrganizations)
            {
                // To keep things simple we require a maximum of 1 peer per organization
                throw new System.Exception("Number of peers cannot exceed number of organizations!");
            }

            JObject mainConfig = new JObject();
            mainConfig.Add("name", "myNetwork");
            mainConfig.Add("description", "My Test Network");
            mainConfig.Add("x-type", "hlf@^1.0.0");
            mainConfig.Add("version", "1.0.0");

            JObject client = new JObject();
            if (nOrganizations > 0)
            {
                client.Add("organization", CLIENT_ORG_NAME);
            }

            mainConfig.Add("client", client);

            JArray orderers = nOrderers > 0 ? CreateJsonArray("orderer1.example.com") : null;
            JArray chaincodes = nOrderers > 0 && nPeers > 0 ? CreateJsonArray("example02:v1", "marbles:1.0") : null;

            JObject peers = null;
            if (nPeers > 0)
            {
                JObject builder = new JObject();
                builder.Add("peer0.org1.example.com", CreateJsonChannelPeer("Org1", true, true, true, true));
                if (nPeers > 1)
                {
                    builder.Add("peer0.org2.example.com", CreateJsonChannelPeer("Org2", true, false, true, false));
                }

                peers = builder;
            }

            JObject channel1 = CreateJsonChannel(orderers, peers, chaincodes);

            string channelName = CHANNEL_NAME;

            JObject channels = new JObject();
            channels.Add(channelName, channel1);

            mainConfig.Add("channels", channels);

            if (nOrganizations > 0)
            {
                // Add some organizations to the config
                JObject builder = new JObject();

                for (int i = 1; i <= nOrganizations; i++)
                {
                    string orgName = "Org" + i;
                    JObject org = CreateJsonOrg(orgName + "MSP", CreateJsonArray("peer0.org" + i + ".example.com"), CreateJsonArray("ca-org" + i), CreateJsonArray(CreateJsonUser("admin" + i, "adminpw" + i)), "-----BEGIN PRIVATE KEY----- <etc>", "-----BEGIN CERTIFICATE----- <etc>");
                    builder.Add(orgName, org);
                }

                mainConfig.Add("organizations", builder);
            }

            if (nOrderers > 0)
            {
                // Add some orderers to the config
                JObject builder = new JObject();

                for (int i = 1; i <= nOrderers; i++)
                {
                    string ordererName = "orderer" + i + ".example.com";
                    int port = (6 + i) * 1000 + 50; // 7050, 8050, etc
                    JObject opts = new JObject();
                    JObject certs = new JObject();
                    opts.Add("ssl-target-name-override", "orderer" + i + ".example.com");
                    certs.Add("pem", "-----BEGIN CERTIFICATE----- <etc>");
                    JObject orderer = CreateJsonOrderer("grpcs://localhost:" + port, opts, certs);
                    builder.Add(ordererName, orderer);
                }

                mainConfig.Add("orderers", builder);
            }

            if (nPeers > 0)
            {
                // Add some peers to the config
                JObject builder = new JObject();

                for (int i = 1; i <= nPeers; i++)
                {
                    string peerName = "peer0.org" + i + ".example.com";

                    int port1 = (6 + i) * 1000 + 51; // 7051, 8051, etc
                    int port2 = (6 + i) * 1000 + 53; // 7053, 8053, etc

                    int orgNo = i;
                    int peerNo = 0;
                    JObject opts = new JObject();
                    JObject certs = new JObject();
                    opts.Add("ssl-target-name-override", "peer" + peerNo + ".org" + orgNo + ".example.com");
                    certs.Add("path", "fixtures/channel/crypto-config/peerOrganizations/org" + orgNo + ".example.com/peers/peer" + peerNo + ".org" + orgNo + ".example.com/tlscacerts/org" + orgNo + ".example.com-cert.pem");
                    JObject peer = CreateJsonPeer("grpcs://localhost:" + port1, "grpcs://localhost:" + port2, opts, certs, CreateJsonArray(channelName));
                    builder.Add(peerName, peer);
                }

                mainConfig.Add("peers", builder);
            }

            // CAs
            JObject builde = new JObject();

            string caName = "ca-org1";
            JObject ca = new JObject();
            ca.Add("url", "https://localhost:7054");
            builde.Add(caName, ca);

            mainConfig.Add("certificateAuthorities", builde);

            return mainConfig;
        }

        private static JObject CreateJsonChannelPeer(string name, bool endorsingPeer, bool chaincodeQuery, bool ledgerQuery, bool eventSource)
        {
            JObject obj = new JObject();
            obj.Add("name", name);
            obj.Add("endorsingPeer", endorsingPeer);
            obj.Add("chaincodeQuery", chaincodeQuery);
            obj.Add("ledgerQuery", ledgerQuery);
            obj.Add("eventSource", eventSource);
            return obj;
        }

        private static JObject CreateJsonChannel(JArray orderers, JObject peers, JArray chaincodes)
        {
            JObject builder = new JObject();


            if (orderers != null)
            {
                builder.Add("orderers", orderers);
            }

            if (peers != null)
            {
                builder.Add("peers", peers);
            }

            if (chaincodes != null)
            {
                builder.Add("chaincodes", chaincodes);
            }

            return builder;
        }

        private static JObject CreateJsonOrg(string mspid, JArray peers, JArray certificateAuthorities, JArray users, string adminPrivateKeyPem, string signedCertPem)
        {
            JObject obj = new JObject();
            obj.Add("mspid", mspid);
            obj.Add("peers", peers);
            obj.Add("certificateAuthorities", certificateAuthorities);
            obj.Add("users", users);
            obj.Add("adminPrivateKeyPEM", adminPrivateKeyPem);
            obj.Add("signedCertPEM", signedCertPem);
            return obj;
        }

        private static JObject CreateJsonUser(string enrollId, string enrollSecret)
        {
            JObject obj = new JObject();
            obj.Add("enrollId", enrollId);
            obj.Add("enrollSecret", enrollSecret);
            return obj;
        }

        private static JObject CreateJsonOrderer(string url, JObject grpcOptions, JObject tlsCaCerts)
        {
            JObject obj = new JObject();

            obj.Add("url", url);
            obj.Add("grpcOptions", grpcOptions);
            obj.Add("tlsCaCerts", tlsCaCerts);
            return obj;
        }

        private static JObject CreateJsonPeer(string url, string eventUrl, JObject grpcOptions, JObject tlsCaCerts, JArray channels)
        {
            JObject obj = new JObject();

            obj.Add("url", url);
            obj.Add("eventUrl", eventUrl);
            obj.Add("grpcOptions", grpcOptions);
            obj.Add("tlsCaCerts", tlsCaCerts);
            obj.Add("channels", channels);
            return obj;
        }


        private static JArray CreateJsonArray(params object[] elements)
        {
            JArray j = new JArray();
            foreach (object s in elements)
                j.Add(s);
            return j;
        }
    }
}