using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Configuration;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Responses;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.TestUtils;
using Hyperledger.Fabric_CA.SDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    /**
     * Test end to end scenario
     */
    [TestClass]
    [TestCategory("SDK_INTEGRATION")]
    public class PrivateDataIT
    {
        private static readonly TestConfig testConfig = TestConfig.Instance;
        private static readonly int DEPLOYWAITTIME = testConfig.GetDeployWaitTime();

        // ReSharper disable once UnusedMember.Local
        private static readonly bool IS_FABRIC_V10 = testConfig.IsRunningAgainstFabric10();
        private static readonly string TEST_ADMIN_NAME = "admin";
        private static readonly string TESTUSER_1_NAME = "user1";
        private static readonly string TEST_FIXTURES_PATH = "src/test/fixture";

        private static readonly string BAR_CHANNEL_NAME = "bar";

        //src/test/fixture/sdkintegration/gocc/samplePrivateData/src/github.com/private_data_cc/private_data_cc.go
        private readonly string CHAIN_CODE_FILEPATH = "sdkintegration/gocc/samplePrivateData";
        private readonly TransactionRequest.Type CHAIN_CODE_LANG = TransactionRequest.Type.GO_LANG;
        private readonly string CHAIN_CODE_NAME = "private_data_cc1_go";
        private readonly string CHAIN_CODE_PATH = "github.com/private_data_cc";

        private readonly string CHAIN_CODE_VERSION = "1";
        private readonly ChaincodeID chaincodeID;
        private readonly TestConfigHelper configHelper = new TestConfigHelper();

        private readonly string sampleStoreFile = Path.Combine(Path.GetTempPath(),"HFCSampletest.properties");

        private readonly string testName = "PrivateDataIT";
        private SampleStore sampleStore;
        private List<SampleOrg> testSampleOrgs;


        public PrivateDataIT()
        {
            chaincodeID = new ChaincodeID();
            chaincodeID.Name = CHAIN_CODE_NAME;
            chaincodeID.Version = CHAIN_CODE_VERSION;
            chaincodeID.Path = CHAIN_CODE_PATH;
        }


        [TestInitialize]
        public void CheckConfig()
        {
            Util.COut("\n\n\nRUNNING: %s.\n", testName);

            //      configHelper.clearConfig();
            TestUtils.TestUtils.ResetConfig();
            configHelper.CustomizeConfig();
            //      assertEquals(256, Config.getConfig().getSecurityLevel());

            testSampleOrgs = testConfig.GetIntegrationTestsSampleOrgs().ToList();
            //Set up hfca for each sample org

            foreach (SampleOrg sampleOrg in testSampleOrgs)
            {
                string caURL = sampleOrg.CALocation;
                sampleOrg.CAClient = HFCAClient.Create(caURL, null);
            }
        }

        [TestMethod]
        public void Setup()
        {
            try
            {
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
        public void SetupUsers(SampleStore sStore)
        {
            //SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface

            ////////////////////////////
            // get users for all orgs
            foreach (SampleOrg sampleOrg in testSampleOrgs)
            {
                string orgName = sampleOrg.Name;

                SampleUser admin = sStore.GetMember(TEST_ADMIN_NAME, orgName);
                sampleOrg.Admin = admin; // The admin of this org.

                // No need to enroll or register all done in End2endIt !
                SampleUser user = sStore.GetMember(TESTUSER_1_NAME, orgName);
                sampleOrg.AddUser(user); //Remember user belongs to this Org
                sampleOrg.PeerAdmin = sStore.GetMember(orgName + "Admin", orgName);
            }
        }

        public void RunFabricTest(SampleStore sStore)
        {
            ////////////////////////////
            // Setup client

            //Create instance of client.
            HFClient client = HFClient.Create();

            client.CryptoSuite = Factory.GetCryptoSuite();
            client.UserContext = sStore.GetMember(TEST_ADMIN_NAME, "peerOrg2");

            SampleOrg sampleOrg = testConfig.GetIntegrationTestsSampleOrg("peerOrg2");

            Channel barChannel = sStore.GetChannel(client, BAR_CHANNEL_NAME);

            barChannel.Initialize();
            RunChannel(client, barChannel, sampleOrg, 10);
            Assert.IsFalse(barChannel.IsShutdown);
            Assert.IsTrue(barChannel.IsInitialized);

            if (testConfig.IsFabricVersionAtOrAfter("1.3"))
            {
                HashSet<string> expect = new HashSet<string>(new [] { "COLLECTION_FOR_A", "COLLECTION_FOR_B"});
                HashSet<string> got = new HashSet<string>();

                CollectionConfigPackage queryCollectionsConfig = barChannel.QueryCollectionsConfig(CHAIN_CODE_NAME, barChannel.Peers.First(), sampleOrg.PeerAdmin);
                foreach (CollectionConfig collectionConfig in queryCollectionsConfig.CollectionConfigs)
                {
                    got.Add(collectionConfig.Name);

                }
                Assert.AreEqual(expect, got);
            }


            Util.COut("That's all folks!");
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private BlockEvent.TransactionEvent SetAmount(HFClient client, Channel channel, ChaincodeID chaincdeID, int delta, IUser user)
        {
            List<ProposalResponse> successful = new List<ProposalResponse>();
            List<ProposalResponse> failed = new List<ProposalResponse>();

            ///////////////
            // Send transaction proposal to all peers
            TransactionProposalRequest transactionProposalRequest = client.NewTransactionProposalRequest();
            transactionProposalRequest.SetChaincodeID(chaincdeID);
            transactionProposalRequest.SetFcn("set");


            Dictionary<string, byte[]> transientMap = new Dictionary<string, byte[]>();
            transientMap["A"]="a".ToBytes();   // test using bytes as args. End2end uses Strings.
            transientMap["AVal"]="500".ToBytes();
            transientMap["B"]="b".ToBytes();
            string arg3 = "" + (200 + delta);
            transientMap["BVal"]=arg3.ToBytes();
            transactionProposalRequest.TransientMap = transientMap;
            
            transactionProposalRequest.SetProposalWaitTime(testConfig.GetProposalWaitTime());
            if (user != null)
            {
                // specific user use that
                transactionProposalRequest.SetUserContext(user);
            }

            List<ProposalResponse> invokePropResp = channel.SendTransactionProposal(transactionProposalRequest);
            foreach (ProposalResponse response in invokePropResp)
            {
                if (response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                {
                    Util.COut("Successful transaction proposal response Txid: %s from peer %s", response.TransactionID, response.Peer.Name);
                    successful.Add(response);
                }
                else
                {
                    failed.Add(response);
                }
            }

            Util.COut("Received %d transaction proposal responses for setAmount. Successful+verified: %d . Failed: %d", invokePropResp.Count, successful.Count, failed.Count);
            if (failed.Count > 0)
            {
                ProposalResponse firstTransactionProposalResponse = failed.First();
                throw new ProposalException($"Not enough endorsers for set(move a,b,{0}):{firstTransactionProposalResponse.Status} endorser error:{firstTransactionProposalResponse.Message}. Was verified:{firstTransactionProposalResponse.IsVerified}");
            }

            Util.COut("Successfully received transaction proposal responses for setAmount. Now sending to orderer.");

            ////////////////////////////
            // Send transaction to orderer

            if (user != null)
            {
                return channel.SendTransaction(successful, user);
            }

            return channel.SendTransaction(successful);
        }

        // ReSharper disable once UnusedMethodReturnValue.Local
        private BlockEvent.TransactionEvent MoveAmount(HFClient client, Channel channel, ChaincodeID chaincdeID, string moveAmount, IUser user)
        {
            List<ProposalResponse> successful = new List<ProposalResponse>();
            List<ProposalResponse> failed = new List<ProposalResponse>();

            ///////////////
            // Send transaction proposal to all peers
            TransactionProposalRequest transactionProposalRequest = client.NewTransactionProposalRequest();
            transactionProposalRequest.SetChaincodeID(chaincdeID);
            transactionProposalRequest.SetFcn("move");

            // Private data needs to be sent via Transient field to prevent identifiable
            //information being sent to the orderer.
            Dictionary<string, byte[]> transientMap = new Dictionary<string, byte[]>();
            transientMap["A"]="a".ToBytes(); //test using bytes .. end2end uses Strings.
            transientMap["B"]="b".ToBytes();
            transientMap["moveAmount"]= moveAmount.ToBytes();
            transactionProposalRequest.TransientMap=transientMap;
                


            transactionProposalRequest.SetProposalWaitTime(testConfig.GetProposalWaitTime());
            if (user != null)
            {
                // specific user use that
                transactionProposalRequest.SetUserContext(user);
            }

            Util.COut("sending transaction proposal to all peers with arguments: move(a,b,%s)", moveAmount);

            List<ProposalResponse> invokePropResp = channel.SendTransactionProposal(transactionProposalRequest);
            foreach (ProposalResponse response in invokePropResp)
            {
                if (response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                {
                    Util.COut("Successful transaction proposal response Txid: %s from peer %s", response.TransactionID, response.Peer.Name);
                    successful.Add(response);
                }
                else
                {
                    failed.Add(response);
                }
            }

            Util.COut("Received %d transaction proposal responses for moveAmount. Successful+verified: %d . Failed: %d", invokePropResp.Count, successful.Count, failed.Count);
            if (failed.Count > 0)
            {
                ProposalResponse firstTransactionProposalResponse = failed.First();

                throw new ProposalException($"Not enough endorsers for invoke(move a,b,{moveAmount}):{firstTransactionProposalResponse.Status} endorser error:{firstTransactionProposalResponse.Message}. Was verified:{firstTransactionProposalResponse.IsVerified}");
            }

            Util.COut("Successfully received transaction proposal responses.");

            ////////////////////////////
            // Send transaction to orderer
            Util.COut("Sending chaincode transaction(move a,b,%s) to orderer.", moveAmount);
            if (user != null)
            {
                return channel.SendTransaction(successful, user);
            }

            return channel.SendTransaction(successful);
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

                Util.COut("Running Channel %s with a delta %d", channelName, delta);

                Util.COut("ChaincodeID: ", chaincodeID);

                client.UserContext = sampleOrg.PeerAdmin;
                ///////////////
                ////
                InstallProposalRequest installProposalRequest = client.NewInstallProposalRequest();
                installProposalRequest.SetChaincodeID(chaincodeID);
                ////For GO language and serving just a single user, chaincodeSource is mostly likely the users GOPATH
                installProposalRequest.SetChaincodeSourceLocation(Path.Combine(TEST_FIXTURES_PATH, CHAIN_CODE_FILEPATH).Locate());
                installProposalRequest.SetChaincodeVersion(CHAIN_CODE_VERSION);
                installProposalRequest.SetProposalWaitTime(testConfig.GetProposalWaitTime());
                installProposalRequest.SetChaincodeLanguage(CHAIN_CODE_LANG);

                Util.COut("Sending install proposal for channel: %s", channel.Name);

                ////////////////////////////
                // only a client from the same org as the peer can issue an install request
                int numInstallProposal = 0;

                List<ProposalResponse> responses;
                List<ProposalResponse> successful = new List<ProposalResponse>();
                List<ProposalResponse> failed = new List<ProposalResponse>();
                List<Peer> peersFromOrg = channel.Peers.ToList();
                numInstallProposal = numInstallProposal + peersFromOrg.Count;

                responses = client.SendInstallProposal(installProposalRequest, peersFromOrg);

                foreach (ProposalResponse response in responses)
                {
                    if (response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    {
                        Util.COut("Successful install proposal response Txid: %s from peer %s", response.TransactionID, response.Peer.Name);
                        successful.Add(response);
                    }
                    else
                    {
                        failed.Add(response);
                    }
                }

                Util.COut("Received %d install proposal responses. Successful+verified: %d . Failed: %d", numInstallProposal, successful.Count, failed.Count);

                if (failed.Count > 0)
                {
                    ProposalResponse first = failed.First();
                    Assert.Fail("Not enough endorsers for install :" + successful.Count + ".  " + first.Message);
                }

                InstantiateProposalRequest instantiateProposalRequest = client.NewInstantiationProposalRequest();
                instantiateProposalRequest.SetChaincodeID(chaincodeID);
                instantiateProposalRequest.SetProposalWaitTime(DEPLOYWAITTIME);
                instantiateProposalRequest.SetFcn("init");
                instantiateProposalRequest.SetArgs(new string[] { });
                instantiateProposalRequest.SetChaincodeCollectionConfiguration(ChaincodeCollectionConfiguration.FromYamlFile("fixture/collectionProperties/PrivateDataIT.yaml".Locate()));

                Util.COut("Sending instantiate proposal");

                List<ProposalResponse> responses2;

                responses2 = channel.SendInstantiationProposal(instantiateProposalRequest);

                successful.Clear();
                failed.Clear();
                foreach (ProposalResponse response in responses2)
                {
                    if (response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    {
                        Util.COut("Successful upgrade proposal response Txid: %s from peer %s", response.TransactionID, response.Peer.Name);
                        successful.Add(response);
                    }
                    else
                    {
                        failed.Add(response);
                    }
                }

                Util.COut("Received %d upgrade proposal responses. Successful+verified: %d . Failed: %d", channel.Peers.Count, successful.Count, failed.Count);

                if (failed.Count > 0)
                {
                    ProposalResponse first = failed.First();
                    Assert.Fail("Not enough endorsers for upgrade :" + successful.Count + ".  " + first.Message);
                }

                Util.COut("Sending instantiate proposal to orderer.");
                channel.SendTransaction(successful, sampleOrg.PeerAdmin);
                Util.COut("instantiate proposal completed.");

                //Now lets run the new chaincode which should *double* the results we asked to move.
                // return setAmount(client, channel, chaincodeID, "50", null).get(testConfig.getTransactionWaitTime(), TimeUnit.SECONDS);
                SetAmount(client, channel, chaincodeID, 50, null);
                Util.COut("Got back acknowledgement from setAmount from all peers.");
                WaitOnFabric(10000);

                Util.COut("delta is %s", delta);
                QueryChaincodeForExpectedValue(client, channel, "" + 250, chaincodeID);
                //Now lets run the new chaincode which should *double* the results we asked to move.
                MoveAmount(client, channel, chaincodeID, "50", null);
            }
            catch (TransactionEventException e)
            {
                BlockEvent.TransactionEvent te = e.TransactionEvent;
                Assert.Fail($"Transaction with txid %s failed. %s", te.TransactionID, e.Message);
            }

            catch (System.Exception e)
            {
                Assert.Fail($"Test failed with {e.Message} exception {e}");
            }

            QueryChaincodeForExpectedValue(client, channel, "" + 300, chaincodeID);

            Util.COut("Running for Channel %s done", channelName);
        }


        private void QueryChaincodeForExpectedValue(HFClient client, Channel channel, string expect, ChaincodeID chaincdeID)
        {
            Util.COut("Now query chaincode {0} on channel {1} for the value of b expecting to see: {2}", chaincdeID, channel.Name, expect);
            List<ProposalResponse> queryProposals;
            QueryByChaincodeRequest queryByChaincodeRequest = client.NewQueryProposalRequest();
            queryByChaincodeRequest.SetFcn("query");
            queryByChaincodeRequest.ChaincodeID = chaincdeID;
            Dictionary<string, byte[]> tmap = new Dictionary<string, byte[]>();
            tmap["B"] = "b".ToBytes(); // test using bytes as args. End2end uses Strings.
            queryByChaincodeRequest.TransientMap = tmap;
            queryProposals = channel.QueryByChaincode(queryByChaincodeRequest);
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
                    Assert.AreEqual(expect, payload, $"Failed compare on channel {channel.Name} chaincode id {chaincdeID} expected value:'{expect}', but got:'{payload}'");
                }
            }
        }

        // ReSharper disable once UnusedMember.Local
        private void WaitOnFabric()
        {
            WaitOnFabric(0);
        }

///// NO OP ... leave in case it's needed.
        // ReSharper disable once UnusedParameter.Local
        private void WaitOnFabric(int additional)
        {
        }
    }
}