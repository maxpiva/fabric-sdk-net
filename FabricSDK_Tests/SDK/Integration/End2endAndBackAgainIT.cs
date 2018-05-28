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


using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Responses;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.SDK.TestUtils;
using Hyperledger.Fabric_CA.SDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlockchainInfo = Hyperledger.Fabric.SDK.BlockchainInfo;
using ChaincodeID = Hyperledger.Fabric.SDK.ChaincodeID;
using Config = Hyperledger.Fabric.Protos.Common.Config;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    /**
     * Test end to end scenario
     */
    [TestClass]
    [TestCategory("SDK_INTEGRATION")]
    public class End2endAndBackAgainIT
    {
        private static readonly TestConfig testConfig = TestConfig.Instance;
        private static readonly bool IS_FABRIC_V10 = testConfig.IsRunningAgainstFabric10();
        private static readonly string TEST_ADMIN_NAME = "admin";
        private static readonly string TESTUSER_1_NAME = "user1";
        private static readonly string TEST_FIXTURES_PATH = "fixture";

        private static readonly string FOO_CHANNEL_NAME = "foo";
        private static readonly string BAR_CHANNEL_NAME = "bar";
        internal static readonly string CHAIN_CODE_VERSION_11 = "11";
        internal static readonly string CHAIN_CODE_VERSION = "1";
        private readonly TestConfigHelper configHelper = new TestConfigHelper();

        internal readonly string sampleStoreFile = Path.Combine(Path.GetTempPath() + "HFCSampletest.properties");
        internal SampleStore sampleStore;
        private IReadOnlyList<SampleOrg> testSampleOrgs;
        private string testTxID = null; // save the CC invoke TxID and use in queries

        internal virtual string CHAIN_CODE_FILEPATH { get; } = "sdkintegration/gocc/sample_11";

        internal virtual string CHAIN_CODE_NAME { get; } = "example_cc_go";
        internal virtual string CHAIN_CODE_PATH { get; } = "github.com/example_cc";
        internal virtual TransactionRequest.Type CHAIN_CODE_LANG { get; } = TransactionRequest.Type.GO_LANG;

        internal virtual ChaincodeID chaincodeID => new ChaincodeID().SetName(CHAIN_CODE_NAME).SetVersion(CHAIN_CODE_VERSION).SetPath(CHAIN_CODE_PATH);

        internal virtual ChaincodeID chaincodeID_11 => new ChaincodeID().SetName(CHAIN_CODE_NAME).SetVersion(CHAIN_CODE_VERSION_11).SetPath(CHAIN_CODE_PATH);

        internal virtual string testName { get; } = "End2endAndBackAgainIT";


        private static bool CheckInstalledChaincode(HFClient client, Peer peer, string ccName, string ccPath, string ccVersion)
        {
            Util.COut("Checking installed chaincode: {0}, at version: {1}, on peer: {2}", ccName, ccVersion, peer.Name);
            List<ChaincodeInfo> ccinfoList = client.QueryInstalledChaincodes(peer);

            bool found = false;

            foreach (ChaincodeInfo ccifo in ccinfoList)
            {
                if (ccPath != null)
                {
                    found = ccName.Equals(ccifo.Name) && ccPath.Equals(ccifo.Path) && ccVersion.Equals(ccifo.Version);
                    if (found)
                    {
                        break;
                    }
                }

                found = ccName.Equals(ccifo.Name) && ccVersion.Equals(ccifo.Version);
                if (found)
                {
                    break;
                }
            }

            return found;
        }

        private static bool CheckInstantiatedChaincode(Channel channel, Peer peer, string ccName, string ccPath, string ccVersion)
        {
            Util.COut("Checking instantiated chaincode: {0}, at version: {1}, on peer: {2}", ccName, ccVersion, peer.Name);
            List<ChaincodeInfo> ccinfoList = channel.QueryInstantiatedChaincodes(peer);

            bool found = false;

            foreach (ChaincodeInfo ccifo in ccinfoList)
            {
                if (ccPath != null)
                {
                    found = ccName.Equals(ccifo.Name) && ccPath.Equals(ccifo.Path) && ccVersion.Equals(ccifo.Version);
                    if (found)
                    {
                        break;
                    }
                }

                found = ccName.Equals(ccifo.Name) && ccVersion.Equals(ccifo.Version);
                if (found)
                {
                    break;
                }
            }

            return found;
        }


        [TestInitialize]
        public void CheckConfig()
        {
            Util.COut("\n\n\nRUNNING: {0}.\n", testName);

            //      configHelper.clearConfig();
            TestUtils.TestUtils.ResetConfig();
            configHelper.CustomizeConfig();
            //      Assert.AreEqual(256, Config.getConfig().getSecurityLevel());

            testSampleOrgs = testConfig.GetIntegrationTestsSampleOrgs();
            //Set up hfca for each sample org

            foreach (SampleOrg sampleOrg in testSampleOrgs)
            {
                string caURL = sampleOrg.CALocation;
                sampleOrg.CAClient = HFCAClient.Create(caURL, null);
            }
        }

        [TestMethod]
        public virtual void Setup()
        {
            try
            {
                // client.setMemberServices(peerOrg1FabricCA);

                //Persistence is not part of SDK. Sample file store is for demonstration purposes only!
                //   MUST be replaced with more robust application implementation  (Database, LDAP)

//            if (sampleStoreFile.exists()) { //For testing start fresh
//                sampleStoreFile.delete();
//            }
                sampleStore = new SampleStore(sampleStoreFile);

                SetupUsers(sampleStore);
                RunFabricTest(sampleStore);
            }
            catch (System.Exception e)
            {
                Assert.Fail(e.Message);
            }
        }

        /**
         * Will register and enroll users persisting them to samplestore.
         *
         * @param sampleStore
         * @throws Exception
         */
        public void SetupUsers(SampleStore sampleStore)
        {
            //SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface

            ////////////////////////////
            // get users for all orgs
            foreach (SampleOrg sampleOrg in testSampleOrgs)
            {
                string orgName = sampleOrg.Name;

                SampleUser admin = sampleStore.GetMember(TEST_ADMIN_NAME, orgName);
                sampleOrg.Admin = admin; // The admin of this org.

                // No need to enroll or register all done in End2endIt !
                SampleUser user = sampleStore.GetMember(TESTUSER_1_NAME, orgName);
                sampleOrg.AddUser(user); //Remember user belongs to this Org

                sampleOrg.PeerAdmin = sampleStore.GetMember(orgName + "Admin", orgName);
            }
        }

        public void RunFabricTest(SampleStore sampleStore)
        {
            ////////////////////////////
            // Setup client

            //Create instance of client.
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();

            ////////////////////////////
            //Reconstruct and run the channels
            SampleOrg sampleOrg = testConfig.GetIntegrationTestsSampleOrg("peerOrg1");
            Channel fooChannel = ReconstructChannel(FOO_CHANNEL_NAME, client, sampleOrg);
            RunChannel(client, fooChannel, sampleOrg, 0);
            Assert.IsFalse(fooChannel.IsShutdown);
            Assert.IsTrue(fooChannel.IsInitialized);
            fooChannel.Shutdown(true); //clean up resources no longer needed.
            Assert.IsTrue(fooChannel.IsShutdown);
            Util.COut("\n");

            sampleOrg = testConfig.GetIntegrationTestsSampleOrg("peerOrg2");
            Channel barChannel = ReconstructChannel(BAR_CHANNEL_NAME, client, sampleOrg);
            RunChannel(client, barChannel, sampleOrg, 100); //run a newly constructed foo channel with different b value!
            Assert.IsFalse(barChannel.IsShutdown);
            Assert.IsTrue(barChannel.IsInitialized);

            if (!testConfig.IsRunningAgainstFabric10())
            {
                //Peer eventing service support started with v1.1

                // Now test replay feature of V1.1 peer eventing services.
                string json = barChannel.Serialize();
                barChannel.Shutdown(true);

                Channel replayChannel = client.DeSerializeChannel(json);

                Util.COut("doing testPeerServiceEventingReplay,0,-1,false");
                TestPeerServiceEventingReplay(client, replayChannel, 0L, -1L, false);

                replayChannel = client.DeSerializeChannel(json);
                Util.COut("doing testPeerServiceEventingReplay,0,-1,true"); // block 0 is import to test
                TestPeerServiceEventingReplay(client, replayChannel, 0L, -1L, true);

                //Now do it again starting at block 1
                replayChannel = client.DeSerializeChannel(json);
                Util.COut("doing testPeerServiceEventingReplay,1,-1,false");
                TestPeerServiceEventingReplay(client, replayChannel, 1L, -1L, false);

                //Now do it again starting at block 2 to 3
                replayChannel = client.DeSerializeChannel(json);
                Util.COut("doing testPeerServiceEventingReplay,2,3,false");
                TestPeerServiceEventingReplay(client, replayChannel, 2L, 3L, false);
            }

            Util.COut("That's all folks!");
        }

        // Disable MethodLength as this method is for instructional purposes and hence
        // we don't want to split it into smaller pieces
        // CHECKSTYLE:OFF: MethodLength
        private void RunChannel(HFClient client, Channel channel, SampleOrg sampleOrg, int delta)
        {
            string channelName = channel.Name;
            try
            {
                client.UserContext = sampleOrg.GetUser(TESTUSER_1_NAME);

//            final boolean changeContext = false; // BAR_CHANNEL_NAME.equals(channel.getName()) ? true : false;
                bool changeContext = BAR_CHANNEL_NAME.Equals(channel.Name);

                Util.COut("Running Channel {0} with a delta {1}", channelName, delta);

                Util.COut("ChaincodeID: {0}", chaincodeID);
                ////////////////////////////
                // Send Query Proposal to all peers see if it's what we expect from end of End2endIT
                //
                QueryChaincodeForExpectedValue(client, channel, "" + (300 + delta), chaincodeID);

                //Set user context on client but use explicit user contest on each call.
                if (changeContext)
                {
                    client.UserContext = sampleOrg.GetUser(TESTUSER_1_NAME);
                }

                // exercise v1 of chaincode

                BlockEvent.TransactionEvent transactionEvent = MoveAmount(client, channel, chaincodeID, "25", changeContext ? sampleOrg.PeerAdmin : null);


                WaitOnFabric();
                client.UserContext = sampleOrg.GetUser(TESTUSER_1_NAME);

                QueryChaincodeForExpectedValue(client, channel, "" + (325 + delta), chaincodeID);

                //////////////////
                // Start of upgrade first must install it.

                client.UserContext = sampleOrg.PeerAdmin;
                ///////////////
                ////
                InstallProposalRequest installProposalRequest = client.NewInstallProposalRequest();
                installProposalRequest.ChaincodeID = chaincodeID;
                ////For GO language and serving just a single user, chaincodeSource is mostly likely the users GOPATH
                installProposalRequest.ChaincodeSourceLocation =Path.GetFullPath(Path.Combine(TEST_FIXTURES_PATH, CHAIN_CODE_FILEPATH));
 
                installProposalRequest.ChaincodeVersion = CHAIN_CODE_VERSION_11;
                installProposalRequest.ProposalWaitTime = testConfig.GetProposalWaitTime();
                installProposalRequest.ChaincodeLanguage = CHAIN_CODE_LANG;

                if (changeContext)
                {
                    installProposalRequest.UserContext = sampleOrg.PeerAdmin;
                }

                Util.COut("Sending install proposal for channel: {0}", channel.Name);

                ////////////////////////////
                // only a client from the same org as the peer can issue an install request
                int numInstallProposal = 0;

                List<ProposalResponse> responses;
                List<ProposalResponse> successful = new List<ProposalResponse>();
                List<ProposalResponse> failed = new List<ProposalResponse>();
                IReadOnlyList<Peer> peersFromOrg = channel.Peers;
                numInstallProposal = numInstallProposal + peersFromOrg.Count;

                responses = client.SendInstallProposal(installProposalRequest, peersFromOrg);

                foreach (ProposalResponse response in responses)
                {
                    if (response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    {
                        Util.COut("Successful install proposal response Txid: {0} from peer {1}", response.TransactionID, response.Peer.Name);
                        successful.Add(response);
                    }
                    else
                    {
                        failed.Add(response);
                    }
                }

                Util.COut("Received {0} install proposal responses. Successful+verified: {1} . Failed: {2}", numInstallProposal, successful.Count, failed.Count);

                if (failed.Count > 0)
                {
                    ProposalResponse first = failed.First();
                    Assert.Fail($"Not enough endorsers for install : {successful.Count}.  {first.Message}");
                }

                //////////////////
                // Upgrade chaincode to ***double*** our move results.

                if (changeContext)
                {
                    installProposalRequest.UserContext = sampleOrg.PeerAdmin;
                }

                UpgradeProposalRequest upgradeProposalRequest = client.NewUpgradeProposalRequest();
                upgradeProposalRequest.ChaincodeID = chaincodeID_11;
                upgradeProposalRequest.ProposalWaitTime = testConfig.GetProposalWaitTime();
                upgradeProposalRequest.Fcn = "init";
                upgradeProposalRequest.Args = new List<string>(); // no arguments don't change the ledger see chaincode.

                ChaincodeEndorsementPolicy chaincodeEndorsementPolicy;

                chaincodeEndorsementPolicy = new ChaincodeEndorsementPolicy();
                chaincodeEndorsementPolicy.FromYamlFile(Path.GetFullPath(Path.Combine(TEST_FIXTURES_PATH, "sdkintegration/chaincodeendorsementpolicy.yaml")));

                upgradeProposalRequest.ChaincodeEndorsementPolicy = chaincodeEndorsementPolicy;
                Dictionary<string, byte[]> tmap = new Dictionary<string, byte[]>();
                tmap.Add("test", "data".ToBytes());
                upgradeProposalRequest.SetTransientMap(tmap);

                if (changeContext)
                {
                    upgradeProposalRequest.UserContext = sampleOrg.PeerAdmin;
                }

                Util.COut("Sending upgrade proposal");

                List<ProposalResponse> responses2;

                responses2 = channel.SendUpgradeProposal(upgradeProposalRequest);

                successful.Clear();
                failed.Clear();
                foreach (ProposalResponse response in responses2)
                {
                    if (response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    {
                        Util.COut("Successful upgrade proposal response Txid: {0} from peer {1}", response.TransactionID, response.Peer.Name);
                        successful.Add(response);
                    }
                    else
                    {
                        failed.Add(response);
                    }
                }

                Util.COut("Received {0} upgrade proposal responses. Successful+verified: {1} . Failed: {2}", channel.Peers.Count, successful.Count, failed.Count);

                if (failed.Count > 0)
                {
                    ProposalResponse first = failed.First();
                    Assert.Fail($"Not enough endorsers for upgrade : {successful.Count}.  {first.Message}");
                }

                if (changeContext)
                {
                    transactionEvent = channel.SendTransaction(successful, sampleOrg.PeerAdmin, testConfig.GetTransactionWaitTime() * 1000);
                }
                else
                {
                    transactionEvent = channel.SendTransaction(successful, testConfig.GetTransactionWaitTime() * 1000);
                }

                WaitOnFabric(10000);

                Util.COut("Chaincode has been upgraded to version {0}", CHAIN_CODE_VERSION_11);

                //Check to see if peers have new chaincode and old chaincode is gone.

                client.UserContext = sampleOrg.PeerAdmin;
                foreach (Peer peer in channel.Peers)
                {
                    if (!CheckInstalledChaincode(client, peer, CHAIN_CODE_NAME, CHAIN_CODE_PATH, CHAIN_CODE_VERSION_11))
                    {
                        Assert.Fail($"Peer {peer.Name} is missing chaincode name:{CHAIN_CODE_NAME}, path:{CHAIN_CODE_PATH}, version: {CHAIN_CODE_VERSION_11}");
                    }

                    //should be instantiated too..
                    if (!CheckInstantiatedChaincode(channel, peer, CHAIN_CODE_NAME, CHAIN_CODE_PATH, CHAIN_CODE_VERSION_11))
                    {
                        Assert.Fail($"Peer {peer.Name} is missing instantiated chaincode name:{CHAIN_CODE_NAME}, path:{CHAIN_CODE_PATH}, version: {CHAIN_CODE_VERSION_11}");
                    }

                    if (CheckInstantiatedChaincode(channel, peer, CHAIN_CODE_NAME, CHAIN_CODE_PATH, CHAIN_CODE_VERSION))
                    {
                        Assert.Fail($"Peer {peer.Name} is missing instantiated chaincode name:{CHAIN_CODE_NAME}, path:{CHAIN_CODE_PATH}, version: {CHAIN_CODE_VERSION}");
                    }
                }

                client.UserContext = sampleOrg.GetUser(TESTUSER_1_NAME);

                ///Check if we still get the same value on the ledger
                Util.COut("delta is {0}", delta);
                QueryChaincodeForExpectedValue(client, channel, "" + (325 + delta), chaincodeID);

                transactionEvent = MoveAmount(client, channel, chaincodeID_11, "50", changeContext ? sampleOrg.PeerAdmin : null); // really move 100
            }
            catch (TransactionEventException t)
            {
                BlockEvent.TransactionEvent te = t.TransactionEvent;
                if (te != null)
                    Assert.Fail($"Transaction with txid {te.TransactionID} failed. {t.Message}");
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Test failed with {e.GetType().Name} exception {e.Message}");
            }

            WaitOnFabric(10000);
            QueryChaincodeForExpectedValue(client, channel, "" + (425 + delta), chaincodeID_11);
            Util.COut("Running for Channel {0} done", channelName);
        }

        private BlockEvent.TransactionEvent MoveAmount(HFClient client, Channel channel, ChaincodeID chaincodeID, string moveAmount, IUser user)
        {
            try
            {
                List<ProposalResponse> successful = new List<ProposalResponse>();
                List<ProposalResponse> failed = new List<ProposalResponse>();

                ///////////////
                /// Send transaction proposal to all peers
                TransactionProposalRequest transactionProposalRequest = client.NewTransactionProposalRequest();
                transactionProposalRequest.ChaincodeID = chaincodeID;
                transactionProposalRequest.Fcn = "move";
                transactionProposalRequest.SetArgs( //test using bytes .. end2end uses Strings.
                    "a".ToBytes(), "b".ToBytes(), moveAmount.ToBytes());
                transactionProposalRequest.ProposalWaitTime = testConfig.GetProposalWaitTime();
                if (user != null)
                {
                    // specific user use that
                    transactionProposalRequest.UserContext = user;
                }

                Util.COut("sending transaction proposal to all peers with arguments: move(a,b,{0})", moveAmount);

                List<ProposalResponse> invokePropResp = channel.SendTransactionProposal(transactionProposalRequest);
                foreach (ProposalResponse response in invokePropResp)
                {
                    if (response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    {
                        Util.COut("Successful transaction proposal response Txid: {0} from peer {1}", response.TransactionID, response.Peer.Name);
                        successful.Add(response);
                    }
                    else
                    {
                        failed.Add(response);
                    }
                }

                Util.COut("Received {0} transaction proposal responses. Successful+verified: {1} . Failed: {2}", invokePropResp.Count, successful.Count, failed.Count);
                if (failed.Count > 0)
                {
                    ProposalResponse firstTransactionProposalResponse = failed.First();

                    throw new ProposalException($"Not enough endorsers for invoke(move a,b,{moveAmount}):{firstTransactionProposalResponse.Status} endorser error:{firstTransactionProposalResponse.Message}. Was verified:{firstTransactionProposalResponse.IsVerified}x");
                }

                Util.COut("Successfully received transaction proposal responses.");

                ////////////////////////////
                // Send transaction to orderer
                Util.COut("Sending chaincode transaction(move a,b,{0}) to orderer.", moveAmount);
                if (user != null)
                    return channel.SendTransaction(successful, user, testConfig.GetTransactionWaitTime() * 1000);
                return channel.SendTransaction(successful, testConfig.GetTransactionWaitTime() * 1000);
            }
            catch (System.Exception e)
            {
                throw new TaskCanceledException();
            }
        }

        private Channel ReconstructChannel(string name, HFClient client, SampleOrg sampleOrg)
        {
            Util.COut("Reconstructing {0} channel", name);

            client.UserContext = sampleOrg.GetUser(TESTUSER_1_NAME);

            Channel newChannel;

            if (BAR_CHANNEL_NAME.Equals(name))
            {
                // bar channel was stored in samplestore in End2endIT testcase.

                /**
                 *  sampleStore.getChannel uses {@link HFClient#deSerializeChannel(byte[])}
                 */
                newChannel = sampleStore.GetChannel(client, name);

                if (!IS_FABRIC_V10)
                {
                    // Make sure there is one of each type peer at the very least. see End2end for how peers were constructed.
                    Assert.IsFalse(newChannel.GetPeers(new[] {PeerRole.EVENT_SOURCE}).Count == 0);
                    Assert.IsFalse(newChannel.GetPeers(new[] {PeerRole.EVENT_SOURCE}).Count == 0);
                }

                Assert.AreEqual(2, newChannel.EventHubs.Count);
                Util.COut("Retrieved channel {0} from sample store.", name);
            }
            else
            {
                newChannel = client.NewChannel(name);

                foreach (string ordererName in sampleOrg.GetOrdererNames())
                {
                    newChannel.AddOrderer(client.NewOrderer(ordererName, sampleOrg.GetOrdererLocation(ordererName), testConfig.GetOrdererProperties(ordererName)));
                }

                bool everyOther = false;

                foreach (string peerName in sampleOrg.GetPeerNames())
                {
                    string peerLocation = sampleOrg.GetPeerLocation(peerName);
                    Properties peerProperties = testConfig.GetPeerProperties(peerName);
                    Peer peer = client.NewPeer(peerName, peerLocation, peerProperties);
                    Channel.PeerOptions peerEventingOptions = // we have two peers on one use block on other use filtered
                        everyOther ? Channel.PeerOptions.CreatePeerOptions().RegisterEventsForBlocks() : Channel.PeerOptions.CreatePeerOptions().RegisterEventsForFilteredBlocks();

                    newChannel.AddPeer(peer, IS_FABRIC_V10 ? Channel.PeerOptions.CreatePeerOptions().SetPeerRoles(PeerRoleExtensions.NoEventSource()) : peerEventingOptions);

                    everyOther = !everyOther;
                }

                //For testing mix it up. For v1.1 use just peer eventing service for foo channel.
                if (IS_FABRIC_V10)
                {
                    //Should have no peers with event sources.
                    Assert.IsTrue(newChannel.GetPeers(new[] {PeerRole.EVENT_SOURCE}).Count == 0);
                    //Should have two peers with all roles but event source.
                    Assert.AreEqual(2, newChannel.Peers.Count);
                    foreach (string eventHubName in sampleOrg.GetEventHubNames())
                    {
                        EventHub eventHub = client.NewEventHub(eventHubName, sampleOrg.GetEventHubLocation(eventHubName), testConfig.GetEventHubProperties(eventHubName));
                        newChannel.AddEventHub(eventHub);
                    }
                }
                else
                {
                    //Peers should have all roles. Do some sanity checks that they do.

                    //Should have two peers with event sources.
                    Assert.AreEqual(2, newChannel.GetPeers(new[] {PeerRole.EVENT_SOURCE}).Count);
                    //Check some other roles too..
                    Assert.AreEqual(2, newChannel.GetPeers(new[] {PeerRole.CHAINCODE_QUERY, PeerRole.LEDGER_QUERY}).Count);
                    Assert.AreEqual(2, newChannel.GetPeers(PeerRoleExtensions.All()).Count); //really same as newChannel.getPeers()
                }

                Assert.AreEqual(IS_FABRIC_V10 ? sampleOrg.GetEventHubNames().Count : 0, newChannel.EventHubs.Count);
            }

            //Just some sanity check tests
            Assert.IsTrue(newChannel == client.GetChannel(name));
            Assert.IsTrue(client == newChannel.client);
            Assert.AreEqual(name, newChannel.Name);
            Assert.AreEqual(2, newChannel.Peers.Count);
            Assert.AreEqual(1, newChannel.Orderers.Count);
            Assert.IsFalse(newChannel.IsShutdown);
            Assert.IsFalse(newChannel.IsInitialized);
            string serializedChannelBytes = newChannel.Serialize();

            //Just checks if channel can be serialized and deserialized .. otherwise this is just a waste :)
            // Get channel back.

            newChannel.Shutdown(true);
            newChannel = client.DeSerializeChannel(serializedChannelBytes);

            Assert.AreEqual(2, newChannel.Peers.Count);

            Assert.AreEqual(1, newChannel.Orderers.Count);
            Assert.IsNotNull(client.GetChannel(name));
            Assert.AreEqual(newChannel, client.GetChannel(name));
            Assert.IsFalse(newChannel.IsInitialized);
            Assert.IsFalse(newChannel.IsShutdown);
            Assert.AreEqual(TESTUSER_1_NAME, client.UserContext.Name);
            newChannel.Initialize();
            Assert.IsTrue(newChannel.IsInitialized);
            Assert.IsFalse(newChannel.IsShutdown);

            //Begin tests with de-serialized channel.

            //Query the actual peer for which channels it belongs to and check it belongs to this channel
            foreach (Peer peer in newChannel.Peers)
            {
                HashSet<string> channels = client.QueryChannels(peer);
                if (!channels.Contains(name))
                {
                    Assert.Fail($"Peer {peer.Name} does not appear to belong to channel {name}");
                }
            }

            //Just see if we can get channelConfiguration. Not required for the rest of scenario but should work.
            byte[] channelConfigurationBytes = newChannel.GetChannelConfigurationBytes();
            Config channelConfig = Config.Parser.ParseFrom(channelConfigurationBytes);

            Assert.IsNotNull(channelConfig);

            ConfigGroup channelGroup = channelConfig.ChannelGroup;

            Assert.IsNotNull(channelGroup);

            Dictionary<string, ConfigGroup> groupsMap = channelGroup.Groups.ToDictionary(a => a.Key, a => a.Value);

            Assert.IsNotNull(groupsMap.GetOrNull("Orderer"));

            Assert.IsNotNull(groupsMap.GetOrNull("Application"));

            //Before return lets see if we have the chaincode on the peers that we expect from End2endIT
            //And if they were instantiated too. this requires peer admin user

            client.UserContext = sampleOrg.PeerAdmin;

            foreach (Peer peer in newChannel.Peers)
            {
                if (!CheckInstalledChaincode(client, peer, CHAIN_CODE_NAME, CHAIN_CODE_PATH, CHAIN_CODE_VERSION))
                {
                    Assert.Fail($"Peer {peer.Name} is missing chaincode name: {CHAIN_CODE_NAME}, path:{CHAIN_CODE_PATH}, version: {CHAIN_CODE_VERSION}");
                }

                if (!CheckInstantiatedChaincode(newChannel, peer, CHAIN_CODE_NAME, CHAIN_CODE_PATH, CHAIN_CODE_VERSION))
                {
                    Assert.Fail($"Peer {peer.Name} is missing instantiated chaincode name: {CHAIN_CODE_NAME}, path:{CHAIN_CODE_PATH}, version: {CHAIN_CODE_VERSION}");
                }
            }

            client.UserContext = sampleOrg.GetUser(TESTUSER_1_NAME);

            Assert.IsTrue(newChannel.IsInitialized);
            Assert.IsFalse(newChannel.IsShutdown);

            Util.COut("Finished reconstructing channel {0}.", name);

            return newChannel;
        }

        private void TestPeerServiceEventingReplay(HFClient client, Channel replayTestChannel, long start, long stop, bool useFilteredBlocks)
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // not supported for v1.0
            }

            Assert.IsFalse(replayTestChannel.IsInitialized); //not yet initialized
            Assert.IsFalse(replayTestChannel.IsShutdown); // not yet shutdown.

            //Remove all peers just have one ledger peer and one eventing peer.
            List<Peer> savedPeers = new List<Peer>(replayTestChannel.Peers);
            foreach (Peer peer in savedPeers)
            {
                replayTestChannel.RemovePeer(peer);
            }

            Assert.IsTrue(savedPeers.Count > 1); //need at least two
            Peer eventingPeer = savedPeers[0];
            Peer ledgerPeer = savedPeers[1];
            savedPeers.RemoveAt(0);
            savedPeers.RemoveAt(0);

            Assert.IsTrue(replayTestChannel.Peers.Count == 0); // no more peers.
            Assert.IsTrue(replayTestChannel.GetPeers(new[] {PeerRole.CHAINCODE_QUERY, PeerRole.ENDORSING_PEER}).Count == 0); // just checking :)
            Assert.IsTrue(replayTestChannel.GetPeers(new[] {PeerRole.LEDGER_QUERY}).Count == 0); // just checking

            Assert.IsNotNull(client.GetChannel(replayTestChannel.Name)); // should be known by client.

            Channel.PeerOptions eventingPeerOptions = Channel.PeerOptions.CreatePeerOptions().SetPeerRoles(new List<PeerRole> {PeerRole.EVENT_SOURCE});
            if (useFilteredBlocks)
            {
                eventingPeerOptions.RegisterEventsForFilteredBlocks();
            }

            if (-1L == stop)
            {
                //the height of the blockchain

                replayTestChannel.AddPeer(eventingPeer, eventingPeerOptions.StartEvents(start)); // Eventing peer start getting blocks from block 0
            }
            else
            {
                replayTestChannel.AddPeer(eventingPeer, eventingPeerOptions.StartEvents(start).StopEvents(stop)); // Eventing peer start getting blocks from block 0
            }

            //add a ledger peer
            replayTestChannel.AddPeer(ledgerPeer, Channel.PeerOptions.CreatePeerOptions().SetPeerRoles(new List<PeerRole> {PeerRole.LEDGER_QUERY}));

            TaskCompletionSource<long> done = new TaskCompletionSource<long>(); // future to set when done.
            // some variable used by the block listener being set up.

            long bcount = 0;
            long stopValue = stop == -1L ? long.MaxValue : stop;
            Channel finalChannel = replayTestChannel;
            ConcurrentDictionary<long, BlockEvent> blockEvents = new ConcurrentDictionary<long, BlockEvent>();

            string blockListenerHandle = replayTestChannel.RegisterBlockListener((blockEvent) =>
            {
                try
                {
                    long blockNumber = blockEvent.BlockNumber;
                    if (blockEvents.ContainsKey(blockNumber))
                        Assert.Fail($"Block number {blockNumber} seen twice");
                    blockEvents[blockNumber] = blockEvent;


                    Assert.IsTrue(useFilteredBlocks ? blockEvent.IsFiltered : !blockEvent.IsFiltered, $"Wrong type of block seen block number {blockNumber}. expected filtered block {useFilteredBlocks} but got {blockEvent.IsFiltered}");
                    long count = Interlocked.Increment(ref bcount);


                    //out("Block count: %d, block number: %d  received from peer: %s", count, blockNumber, blockEvent.getPeer().getName());

                    if (count == 0 && stop == -1L)
                    {
                        BlockchainInfo blockchainInfo = finalChannel.QueryBlockchainInfo();

                        long lh = blockchainInfo.Height;
                        stopValue = lh - 1L; // blocks 0L 9L are on chain height 10 .. stop on 9
                        //  out("height: %d", lh);
                        if (Interlocked.Read(ref bcount) + start > stopValue)
                        {
                            // test with latest count.

                            done.SetResult(Interlocked.Read(ref bcount));
                        }
                    }
                    else
                    {
                        if (Interlocked.Read(ref bcount) + start > stopValue)
                        {
                            done.SetResult(count);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    done.SetException(e);
                }
            });


            try
            {
                replayTestChannel.Initialize(); // start it all up.
                done.Task.Wait(30 * 1000);
                Thread.Sleep(1000); // sleep a little to see if more blocks trickle in .. they should not
                replayTestChannel.UnregisterBlockListener(blockListenerHandle);

                long expectNumber = stopValue - start + 1L; // Start 2 and stop is 3  expect 2

                Assert.AreEqual(expectNumber, blockEvents.Count, $"Didn't get number we expected {expectNumber} but got {blockEvents.Count} block events. Start: {start}, end: {stop}, height: {stopValue}");

                for (long i = stopValue; i >= start; i--)
                {
                    //make sure all are there.
                    BlockEvent blockEvent = blockEvents[i];
                    Assert.IsNotNull(blockEvent, $"Missing block event for block number {i}. Start= {start}", blockEvent);
                }

                //light weight test just see if we get reasonable values for traversing the block. Test just whats common between
                // Block and FilteredBlock.

                int transactionEventCounts = 0;
                int chaincodeEventsCounts = 0;

                for (long i = stopValue; i >= start; i--)
                {
                    BlockEvent blockEvent = blockEvents[i];
//                out("blockwalker %b, start: %d, stop: %d, i: %d, block %d", useFilteredBlocks, start, stopValue.longValue(), i, blockEvent.getBlockNumber());
                    Assert.AreEqual(useFilteredBlocks, blockEvent.IsFiltered); // check again

                    if (useFilteredBlocks)
                    {
                        Assert.IsNull(blockEvent.Block); // should not have raw block event.
                        Assert.IsNotNull(blockEvent.FilteredBlock); // should have raw filtered block.
                    }
                    else
                    {
                        Assert.IsNotNull(blockEvent.Block); // should not have raw block event.
                        Assert.IsNull(blockEvent.FilteredBlock); // should have raw filtered block.
                    }

                    Assert.AreEqual(replayTestChannel.Name, blockEvent.ChannelId);

                    foreach (BlockInfo.EnvelopeInfo envelopeInfo in blockEvent.EnvelopeInfos)
                    {
                        if (envelopeInfo.EnvelopeType == BlockInfo.EnvelopeType.TRANSACTION_ENVELOPE)
                        {
                            BlockInfo.TransactionEnvelopeInfo transactionEnvelopeInfo = (BlockInfo.TransactionEnvelopeInfo) envelopeInfo;
                            Assert.IsTrue(envelopeInfo.IsValid); // only have valid blocks.
                            Assert.AreEqual(envelopeInfo.ValidationCode, 0);

                            ++transactionEventCounts;
                            foreach (BlockInfo.TransactionEnvelopeInfo.TransactionActionInfo ta in transactionEnvelopeInfo.TransactionActionInfos)
                            {
                                //    out("\nTA:", ta + "\n\n");
                                ChaincodeEventDeserializer evnt = ta.Event;
                                if (evnt != null)
                                {
                                    Assert.IsNotNull(evnt.ChaincodeId);
                                    Assert.IsNotNull(evnt.EventName);
                                    chaincodeEventsCounts++;
                                }
                            }
                        }
                        else
                        {
                            Assert.AreEqual(blockEvent.BlockNumber, 0, "Only non transaction block should be block 0.");
                        }
                    }
                }

                Assert.IsTrue(transactionEventCounts > 0);

                if (expectNumber > 4)
                {
                    // this should be enough blocks with CC events.

                    Assert.IsTrue(chaincodeEventsCounts > 0);
                }

                replayTestChannel.Shutdown(true); //all done.
            }
            catch (System.Exception e)
            {
                Assert.Fail(e.Message);
            }
        }

        private void QueryChaincodeForExpectedValue(HFClient client, Channel channel, string expect, ChaincodeID chaincodeID)
        {
            Util.COut("Now query chaincode {0} on channel {1} for the value of b expecting to see: {2}", chaincodeID, channel.Name, expect);
            QueryByChaincodeRequest queryByChaincodeRequest = client.NewQueryProposalRequest();
            queryByChaincodeRequest.SetArgs("b".ToBytes()); // test using bytes as args. End2end uses Strings.
            queryByChaincodeRequest.SetFcn("query");
            queryByChaincodeRequest.ChaincodeID = chaincodeID;

            List<ProposalResponse> queryProposals;

            try
            {
                queryProposals = channel.QueryByChaincode(queryByChaincodeRequest);
            }
            catch (System.Exception)
            {
                throw;
            }

            ;

            foreach (ProposalResponse proposalResponse in queryProposals)
            {
                if (!proposalResponse.IsVerified || proposalResponse.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                {
                    Assert.Fail($"Failed query proposal from peer {proposalResponse.Peer.Name} status: {proposalResponse.Status}. Messages: {proposalResponse.Message}. Was verified : {proposalResponse.IsVerified}");
                }
                else
                {
                    string payload = proposalResponse.ProtoProposalResponse.Response.Payload.ToStringUtf8();
                    Util.COut("Query payload of b from peer {0} returned {1}", proposalResponse.Peer.Name, payload);
                    Assert.AreEqual(expect, payload, $"Failed compare on channel {channel.Name} chaincode id {chaincodeID} expected value:'{expect}', but got:'{payload}'");
                }
            }
        }

        private void WaitOnFabric()
        {
            WaitOnFabric(0);
        }

        ///// NO OP ... leave in case it's needed.
        private void WaitOnFabric(int additional)
        {
        }
    }
}