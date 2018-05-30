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

/**
 * Integration test for the Network Configuration YAML (/JSON) file
 * <p>
 * This test requires that End2endIT has previously been run in order to set up the channel.
 * It has no dependencies on any of the other integration tests.
 * That is, it can be run with or without having run the other End to End tests (apart from End2EndIT).
 * <br>
 * Furthermore, it can be executed multiple times without having to restart the blockchain.
 * <p>
 * One other requirement is that the network configuration file matches the topology
 * that is set up by End2endIT.
 * <p>
 * It first examines the "foo" channel and checks that CHAIN_CODE_NAME has been instantiated on the channel,
 * and if not it deploys the chaincode with that name.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Responses;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.TestUtils;
using Hyperledger.Fabric_CA.SDK;
using Hyperledger.Fabric_CA.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ChaincodeID = Hyperledger.Fabric.SDK.ChaincodeID;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    [TestClass]
    [TestCategory("SDK_INTEGRATION")]
    [TestCategory("SDK_INTEGRATION_NODE")]
    public class NetworkConfigIT
    {
        private static readonly TestConfig testConfig = TestConfig.Instance;

        private static readonly string TEST_ORG = "Org1";

        private static readonly string TEST_FIXTURES_PATH = "fixture";

        private static readonly string CHAIN_CODE_PATH = "github.com/example_cc";

        private static readonly string CHAIN_CODE_NAME = "cc-NetworkConfigTest-001";

        private static readonly string CHAIN_CODE_VERSION = "1";

        private static readonly string FOO_CHANNEL_NAME = "foo";

        private static readonly TestConfigHelper configHelper = new TestConfigHelper();

        private static NetworkConfig networkConfig;

        private static readonly Dictionary<string, IUser> orgRegisteredUsers = new Dictionary<string, IUser>();

        [ClassInitialize]
        public static void doMainSetup(TestContext context)
        {
            Util.COut("\n\n\nRUNNING: NetworkConfigIT.\n");

            TestUtils.TestUtils.ResetConfig();
            configHelper.CustomizeConfig();

            // Use the appropriate TLS/non-TLS network config file
            networkConfig = NetworkConfig.FromYamlFile(testConfig.GetTestNetworkConfigFileYAML());

            networkConfig.OrdererNames.ForEach(ordererName =>
            {
                try
                {
                    Properties ordererProperties = networkConfig.GetOrdererProperties(ordererName);
                    Properties testProp = testConfig.GetEndPointProperties("orderer", ordererName);
                    ordererProperties.Set("clientCertFile", testProp.Get("clientCertFile"));
                    ordererProperties.Set("clientKeyFile", testProp.Get("clientKeyFile"));
                    networkConfig.SetOrdererProperties(ordererName, ordererProperties);
                }
                catch (InvalidArgumentException e)
                {
                    throw new System.Exception(e.Message, e);
                }
            });

            networkConfig.PeerNames.ForEach(peerName =>
            {
                try
                {
                    Properties peerProperties = networkConfig.GetPeerProperties(peerName);
                    Properties testProp = testConfig.GetEndPointProperties("peer", peerName);
                    peerProperties.Set("clientCertFile", testProp.Get("clientCertFile"));
                    peerProperties.Set("clientKeyFile", testProp.Get("clientKeyFile"));
                    networkConfig.SetPeerProperties(peerName, peerProperties);
                }
                catch (InvalidArgumentException e)
                {
                    throw new System.Exception(e.Message, e);
                }
            });

            networkConfig.EventHubNames.ForEach(eventhubName =>
            {
                try
                {
                    Properties eventHubsProperties = networkConfig.GetEventHubsProperties(eventhubName);
                    Properties testProp = testConfig.GetEndPointProperties("peer", eventhubName);
                    eventHubsProperties.Set("clientCertFile", testProp.Get("clientCertFile"));
                    eventHubsProperties.Set("clientKeyFile", testProp.Get("clientKeyFile"));
                    networkConfig.SetEventHubProperties(eventhubName, eventHubsProperties);
                }
                catch (InvalidArgumentException e)
                {
                    throw new System.Exception(e.Message, e);
                }
            });

            //Check if we get access to defined CAs!
            NetworkConfig.OrgInfo org = networkConfig.GetOrganizationInfo("Org1");
            NetworkConfig.CAInfo caInfo = org.CertificateAuthorities[0];

            HFCAClient hfcaClient = HFCAClient.Create(caInfo);
            Assert.AreEqual(hfcaClient.CAName, caInfo.CAName);
            HFCAInfo info = hfcaClient.Info(); //makes actual REST call.
            Assert.AreEqual(caInfo.CAName, info.CAName);

            List<NetworkConfig.UserInfo> registrars = caInfo.Registrars;
            Assert.IsTrue(registrars.Count > 0);
            NetworkConfig.UserInfo registrar = registrars.First();
            registrar.Enrollment = hfcaClient.Enroll(registrar.Name, registrar.EnrollSecret);
            TestUtils.TestUtils.MockUser mockuser = TestUtils.TestUtils.GetMockUser(org.Name + "_mock_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), registrar.MspId);
            RegistrationRequest rr = new RegistrationRequest(mockuser.Name, "org1.department1");
            mockuser.EnrollmentSecret = hfcaClient.Register(rr, registrar);
            mockuser.Enrollment = hfcaClient.Enroll(mockuser.Name, mockuser.EnrollmentSecret);
            orgRegisteredUsers.Add(org.Name, mockuser);

            org = networkConfig.GetOrganizationInfo("Org2");
            caInfo = org.CertificateAuthorities[0];

            hfcaClient = HFCAClient.Create(caInfo);
            Assert.AreEqual(hfcaClient.CAName, caInfo.CAName);
            info = hfcaClient.Info(); //makes actual REST call.
            Assert.AreEqual(info.CAName, "");

            registrars = caInfo.Registrars;
            Assert.IsTrue(registrars.Count > 0);
            registrar = registrars.First();
            registrar.Enrollment = hfcaClient.Enroll(registrar.Name, registrar.EnrollSecret);
            mockuser = TestUtils.TestUtils.GetMockUser(org.Name + "_mock_" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), registrar.MspId);
            rr = new RegistrationRequest(mockuser.Name, "org1.department1");
            mockuser.EnrollmentSecret = hfcaClient.Register(rr, registrar);
            mockuser.Enrollment = hfcaClient.Enroll(mockuser.Name, mockuser.EnrollmentSecret);
            orgRegisteredUsers.Add(org.Name, mockuser);

            DeployChaincodeIfRequired();
        }

        // Determines whether or not the chaincode has been deployed and deploys it if necessary
        private static void DeployChaincodeIfRequired()
        {
            ////////////////////////////
            // Setup client
            HFClient client = GetTheClient();

            Channel channel = ConstructChannel(client, FOO_CHANNEL_NAME);

            // Use any old peer...
            Peer peer = channel.GetPeers().First();
            if (!CheckInstantiatedChaincode(channel, peer, CHAIN_CODE_NAME, CHAIN_CODE_PATH, CHAIN_CODE_VERSION))
            {
                // The chaincode we require does not exist, so deploy it...
                DeployChaincode(client, channel, CHAIN_CODE_NAME, CHAIN_CODE_PATH, CHAIN_CODE_VERSION);
            }
        }

        // Returns a new client instance
        private static HFClient GetTheClient()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = Factory.Instance.GetCryptoSuite();

            IUser peerAdmin = GetAdminUser(TEST_ORG);
            client.UserContext = peerAdmin;

            return client;
        }

        private static IUser GetAdminUser(string orgName)
        {
            return networkConfig.GetPeerAdmin(orgName);
        }

        [TestMethod]
        public void TestUpdate1()
        {
            // Setup client and channel instances
            HFClient client = GetTheClient();
            Channel channel = ConstructChannel(client, FOO_CHANNEL_NAME);

            ChaincodeID chaincodeID = new ChaincodeID().SetName(CHAIN_CODE_NAME).SetVersion(CHAIN_CODE_VERSION).SetPath(CHAIN_CODE_PATH);

            string channelName = channel.Name;

            Util.COut("Running testUpdate1 - Channel %s", channelName);

            int moveAmount = 5;
            string originalVal = QueryChaincodeForCurrentValue(client, channel, chaincodeID);
            string newVal = "" + (int.Parse(originalVal) + moveAmount);

            Util.COut("Original value = {0}", originalVal);

            //user registered user
            client.UserContext = orgRegisteredUsers.GetOrNull("Org1"); // only using org1

            // Move some assets
            try
            {

                MoveAmount(client, channel, chaincodeID, "a", "b", "" + moveAmount, null);
                bool erro = false;
                QueryChaincodeForExpectedValue(client, channel, newVal, chaincodeID);
                MoveAmount(client, channel, chaincodeID, "b", "a", "" + moveAmount, null);
                QueryChaincodeForExpectedValue(client, channel, originalVal, chaincodeID);
            }
            catch (TransactionEventException t)
            {
                BlockEvent.TransactionEvent te = t.TransactionEvent;
                if (te != null)
                    Assert.Fail($"Transaction with txid {te.TransactionID} failed. {t.Message}");
                Assert.Fail($"Transaction failed with exception message {t.Message}");

            }
            catch (System.Exception e)
            {
                Assert.Fail($"Test failed with {e.GetType().Name} exception {e.Message}");

            }

            channel.Shutdown(true); // Force channel to shutdown clean up resources.

            Util.COut("testUpdate1 - done");
        }

        private static void QueryChaincodeForExpectedValue(HFClient client, Channel channel, string expect, ChaincodeID chaincodeID)
        {
            Util.COut("Now query chaincode on channel {0} for the value of b expecting to see: {1}", channel.Name, expect);

            string value = QueryChaincodeForCurrentValue(client, channel, chaincodeID);
            Assert.AreEqual(expect, value);
        }

        // Returns the current value of b's assets
        private static string QueryChaincodeForCurrentValue(HFClient client, Channel channel, ChaincodeID chaincodeID)
        {
            Util.COut("Now query chaincode on channel {0} for the current value of {1}", channel.Name);

            QueryByChaincodeRequest queryByChaincodeRequest = client.NewQueryProposalRequest();
            queryByChaincodeRequest.SetArgs("b");
            queryByChaincodeRequest.SetFcn("query");
            queryByChaincodeRequest.SetChaincodeID(chaincodeID);

            List<ProposalResponse> queryProposals;

            queryProposals = channel.QueryByChaincode(queryByChaincodeRequest);
            string expect = null;
            foreach (ProposalResponse proposalResponse in queryProposals)
            {
                if (!proposalResponse.IsVerified || proposalResponse.Status != ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                {
                    Assert.Fail($"Failed query proposal from peer {proposalResponse.Peer.Name} status: {proposalResponse.Status}. Messages: {proposalResponse.Message}. Was verified : {proposalResponse.IsVerified}");
                }
                else
                {
                    string payload = proposalResponse.ProtoProposalResponse.Response.Payload.ToStringUtf8();
                    Util.COut("Query payload of b from peer {0} returned {1}", proposalResponse.Peer.Name, payload);
                    if (expect != null)
                    {
                        Assert.AreEqual(expect, payload);
                    }
                    else
                    {
                        expect = payload;
                    }
                }
            }

            return expect;
        }

        private static BlockEvent.TransactionEvent MoveAmount(HFClient client, Channel channel, ChaincodeID chaincodeID, string from, string to, string moveAmount, IUser user)
        {
            List<ProposalResponse> successful = new List<ProposalResponse>();
            List<ProposalResponse> failed = new List<ProposalResponse>();

            ///////////////
            /// Send transaction proposal to all peers
            TransactionProposalRequest transactionProposalRequest = client.NewTransactionProposalRequest();
            transactionProposalRequest.SetChaincodeID(chaincodeID);
            transactionProposalRequest.SetFcn("move");
            transactionProposalRequest.SetArgs(from, to, moveAmount);
            transactionProposalRequest.SetProposalWaitTime(testConfig.GetProposalWaitTime());
            if (user != null)
            {
                // specific user use that
                transactionProposalRequest.SetUserContext(user);
            }

            Util.COut("sending transaction proposal to all peers with arguments: move(%s,%s,%s)", from, to, moveAmount);

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

            // Check that all the proposals are consistent with each other. We should have only one set
            // where all the proposals above are consistent.
            List<HashSet<ProposalResponse>> proposalConsistencySets = SDKUtils.GetProposalConsistencySets(invokePropResp);
            if (proposalConsistencySets.Count != 1)
            {
                Assert.Fail($"Expected only one set of consistent move proposal responses but got {proposalConsistencySets.Count}");
            }

            Util.COut("Received {0} transaction proposal responses. Successful+verified: {1} . Failed: {2}", invokePropResp.Count, successful.Count, failed.Count);
            if (failed.Count > 0)
            {
                ProposalResponse firstTransactionProposalResponse = failed.First();

                throw new ProposalException($"Not enough endorsers for invoke(move {from},{to},{moveAmount}):{firstTransactionProposalResponse.Status} endorser error:{firstTransactionProposalResponse.Message}. Was verified:{firstTransactionProposalResponse.IsVerified}");
            }

            Util.COut("Successfully received transaction proposal responses.");

            ////////////////////////////
            // Send transaction to orderer
            Util.COut("Sending chaincode transaction(move %s,%s,%s) to orderer.", from, to, moveAmount);
            if (user != null)
            {
                return channel.SendTransaction(successful, user, testConfig.GetTransactionWaitTime() * 1000);
            }

            return channel.SendTransaction(successful, testConfig.GetTransactionWaitTime() * 1000);
        }

        private static ChaincodeID DeployChaincode(HFClient client, Channel channel, string ccName, string ccPath, string ccVersion)
        {
            Util.COut("deployChaincode - enter");
            ChaincodeID chaincodeID = null;

            try
            {
                string channelName = channel.Name;
                Util.COut("deployChaincode - channelName = " + channelName);

                IReadOnlyList<Orderer> orderers = channel.Orderers;
                List<ProposalResponse> responses;
                List<ProposalResponse> successful = new List<ProposalResponse>();
                List<ProposalResponse> failed = new List<ProposalResponse>();

                chaincodeID = new ChaincodeID().SetName(ccName).SetVersion(ccVersion).SetPath(ccPath);

                ////////////////////////////
                // Install Proposal Request
                //
                Util.COut("Creating install proposal");

                InstallProposalRequest installProposalRequest = client.NewInstallProposalRequest();
                installProposalRequest.SetChaincodeID(chaincodeID);

                ////For GO language and serving just a single user, chaincodeSource is mostly likely the users GOPATH
                installProposalRequest.SetChaincodeSourceLocation(Path.Combine(TEST_FIXTURES_PATH, "sdkintegration/gocc/sample1").Locate());

                installProposalRequest.SetChaincodeVersion(ccVersion);

                Util.COut("Sending install proposal");

                ////////////////////////////
                // only a client from the same org as the peer can issue an install request
                int numInstallProposal = 0;

                List<Peer> peersFromOrg = channel.GetPeers();
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

                ///////////////
                //// Instantiate chaincode.
                //
                // From the docs:
                // The instantiate transaction invokes the lifecycle System Chaincode (LSCC) to create and initialize a chaincode on a channel
                // After being successfully instantiated, the chaincode enters the active state on the channel and is ready to process any transaction proposals of type ENDORSER_TRANSACTION

                InstantiateProposalRequest instantiateProposalRequest = client.NewInstantiationProposalRequest();
                instantiateProposalRequest.SetProposalWaitTime(testConfig.GetProposalWaitTime());
                instantiateProposalRequest.SetChaincodeID(chaincodeID);
                instantiateProposalRequest.SetFcn("init");
                instantiateProposalRequest.SetArgs("a", "500", "b", "999");

                Dictionary<string, byte[]> tm = new Dictionary<string, byte[]>();
                tm.Add("HyperLedgerFabric", "InstantiateProposalRequest:JavaSDK".ToBytes());
                tm.Add("method", "InstantiateProposalRequest".ToBytes());
                instantiateProposalRequest.SetTransientMap(tm);

                /*
                  policy OR(Org1MSP.member, Org2MSP.member) meaning 1 signature from someone in either Org1 or Org2
                  See README.md Chaincode endorsement policies section for more details.
                */
                ChaincodeEndorsementPolicy chaincodeEndorsementPolicy = new ChaincodeEndorsementPolicy();
                chaincodeEndorsementPolicy.FromYamlFile(Path.Combine(TEST_FIXTURES_PATH, "sdkintegration/chaincodeendorsementpolicy.yaml").Locate());
                instantiateProposalRequest.SetChaincodeEndorsementPolicy(chaincodeEndorsementPolicy);

                Util.COut("Sending instantiateProposalRequest to all peers...");
                successful.Clear();
                failed.Clear();

                responses = channel.SendInstantiationProposal(instantiateProposalRequest);

                foreach (ProposalResponse response in responses)
                {
                    if (response.IsVerified && response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    {
                        successful.Add(response);
                        Util.COut("Succesful instantiate proposal response Txid:{0} from peer {1}", response.TransactionID, response.Peer.Name);
                    }
                    else
                    {
                        failed.Add(response);
                    }
                }

                Util.COut("Received {0} instantiate proposal responses. Successful+verified: {1} . Failed: {2}", responses.Count, successful.Count, failed.Count);
                if (failed.Count > 0)
                {
                    ProposalResponse first = failed.First();
                    Assert.Fail($"Not enough endorsers for instantiate :{successful.Count} endorser failed with {first.Message}. Was verified:{first.IsVerified}");
                }

                ///////////////
                /// Send instantiate transaction to orderer
                Util.COut("Sending instantiateTransaction to orderer...");
                Util.COut("calling get...");
                BlockEvent.TransactionEvent evnt = channel.SendTransaction(successful, orderers, 30 * 1000);
                Util.COut("get done...");

                Assert.IsTrue(evnt.IsValid); // must be valid to be here.
                Util.COut("Finished instantiate transaction with transaction id {0}", evnt.TransactionID);
            }
            catch (System.Exception e)
            {
                Util.COut("Caught an exception running channel {0}", channel.Name);
                Assert.Fail($"Test failed with error : {e.Message}");
            }

            return chaincodeID;
        }

        private static Channel ConstructChannel(HFClient client, string channelName)
        {
            //Channel newChannel = client.GetChannel(channelName);
            Channel newChannel = client.LoadChannelFromConfig(channelName, networkConfig);
            if (newChannel == null)
            {
                throw new System.Exception("Channel " + channelName + " is not defined in the config file!");
            }

            return newChannel.Initialize();
        }

        // Determines if the specified chaincode has been instantiated on the channel
        private static bool CheckInstantiatedChaincode(Channel channel, Peer peer, string ccName, string ccPath, string ccVersion)
        {
            Util.COut("Checking instantiated chaincode: {0}, at version: {1}, on peer: {2}", ccName, ccVersion, peer.Name);
            List<ChaincodeInfo> ccinfoList = channel.QueryInstantiatedChaincodes(peer);

            bool found = false;

            foreach (ChaincodeInfo ccifo in ccinfoList)
            {
                found = ccName.Equals(ccifo.Name) && ccPath.Equals(ccifo.Path) && ccVersion.Equals(ccifo.Version);
                if (found)
                {
                    break;
                }
            }

            return found;
        }
    }
}