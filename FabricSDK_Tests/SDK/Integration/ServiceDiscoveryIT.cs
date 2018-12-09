using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Discovery;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Responses;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    [TestClass]
    [TestCategory("SDK_INTEGRATION")]
    public class ServiceDiscoveryIT
    {
        private static readonly TestConfig testConfig = TestConfig.Instance;
        private readonly TransactionRequest.Type CHAIN_CODE_LANG = TransactionRequest.Type.GO_LANG;

        private readonly string CHAIN_CODE_NAME = "example_cc_go";

        private readonly string sampleStoreFile = Path.Combine(Path.GetTempPath(),"HFCSampletest.properties");

        [TestMethod]
        [Ignore] //Hostnames reported by service discovery won't work unless you edit hostfile
        public void Setup()
        {
            //Persistence is not part of SDK. Sample file store is for demonstration purposes only!
            //   MUST be replaced with more robust application implementation  (Database, LDAP)
            Util.COut("\n\n\nRUNNING: %s.\n", "ServiceDiscoveryIT");

            SampleStore sampleStore = new SampleStore(sampleStoreFile);

            //  SampleUser peerAdmin = sampleStore.getMember("admin", "peerOrg1");
            SampleUser user1 = sampleStore.GetMember("user1", "peerOrg1");
            HFClient client = HFClient.Create();
            testConfig.GetIntegrationTestsSampleOrg("peerOrg1");
            client.CryptoSuite = Factory.GetCryptoSuite();
            client.UserContext = user1;
            Properties properties = testConfig.GetPeerProperties("peer0.org1.example.com");

            string protocol = testConfig.IsRunningFabricTLS() ? "grpcs:" : "grpc:";

            Properties sdprops = new Properties();

            //Create initial discovery peer.

            Peer discoveryPeer = client.NewPeer("peer0.org1.example.com", protocol + "//localhost:7051", properties);
            Channel foo = client.NewChannel("foo"); //create channel that will be discovered.

            foo.AddPeer(discoveryPeer, Channel.PeerOptions.CreatePeerOptions().SetPeerRoles(PeerRole.SERVICE_DISCOVERY, PeerRole.LEDGER_QUERY, PeerRole.EVENT_SOURCE, PeerRole.CHAINCODE_QUERY));

            // Need to provide client TLS certificate and key files when running mutual tls.
            if (testConfig.IsRunningFabricTLS())
            {
                sdprops.Add("org.hyperledger.fabric.sdk.discovery.default.clientCertFile", "fixture/sdkintegration/e2e-2Orgs/v1.2/crypto-config/peerOrganizations/org1.example.com/users/User1@org1.example.com/tls/client.crt".Locate());
                sdprops.Add("org.hyperledger.fabric.sdk.discovery.default.clientKeyFile", "fixture/sdkintegration/e2e-2Orgs/v1.2/crypto-config/peerOrganizations/org1.example.com/users/User1@org1.example.com/tls/client.key".Locate());

                // Need to do host name override for true tls in testing environment
                sdprops.Add("org.hyperledger.fabric.sdk.discovery.endpoint.hostnameOverride.localhost:7050", "orderer.example.com");
                sdprops.Add("org.hyperledger.fabric.sdk.discovery.endpoint.hostnameOverride.localhost:7051", "peer0.org1.example.com");
                sdprops.Add("org.hyperledger.fabric.sdk.discovery.endpoint.hostnameOverride.localhost:7056", "peer1.org1.example.com");
            }
            else
            {
                sdprops.Add("org.hyperledger.fabric.sdk.discovery.default.protocol", "grpc:");
            }

            foo.ServiceDiscoveryProperties = sdprops;

            string channel = foo.Serialize();
            // Next 3 lines are for testing purposes only!
            foo.Shutdown(false);
            foo = client.DeSerializeChannel(channel);
            foo.Initialize(); // initialize the channel.
            List<string> expect = new List<string>() {protocol + "//orderer.example.com:7050"}; //discovered orderer
            foreach (Orderer orderer in foo.Orderers)
            {
                expect.Remove(orderer.Url);
            }

            Assert.IsTrue(expect.Count == 0);

            List<string> discoveredChaincodeNames = foo.GetDiscoveredChaincodeNames();

            Assert.IsTrue(discoveredChaincodeNames.Contains(CHAIN_CODE_NAME));

            ChaincodeID chaincodeID = new ChaincodeID();
            chaincodeID.Name = CHAIN_CODE_NAME;


            ///////////////
            // Send transaction proposal to all peers
            TransactionProposalRequest transactionProposalRequest = client.NewTransactionProposalRequest();
            transactionProposalRequest.SetChaincodeID(chaincodeID);
            transactionProposalRequest.SetChaincodeLanguage(CHAIN_CODE_LANG);
            transactionProposalRequest.SetFcn("move");
            transactionProposalRequest.SetProposalWaitTime(testConfig.GetProposalWaitTime());
            transactionProposalRequest.SetArgs("a", "b", "1");

            //Send proposal request discovering the what endorsers (peers) are needed.

            List<ProposalResponse> transactionPropResp = foo.SendTransactionProposalToEndorsers(transactionProposalRequest, Channel.DiscoveryOptions.CreateDiscoveryOptions().SetEndorsementSelector(ServiceDiscovery.ENDORSEMENT_SELECTION_RANDOM).SetForceDiscovery(true));
            Assert.IsFalse(transactionPropResp.Count == 0);

            transactionProposalRequest = client.NewTransactionProposalRequest();
            transactionProposalRequest.SetChaincodeID(chaincodeID);
            transactionProposalRequest.SetChaincodeLanguage(CHAIN_CODE_LANG);
            transactionProposalRequest.SetFcn("move");
            transactionProposalRequest.SetProposalWaitTime(testConfig.GetProposalWaitTime());
            transactionProposalRequest.SetArgs("a", "b", "1");

            //Send proposal request discovering the what endorsers (peers) are needed.
            transactionPropResp = foo.SendTransactionProposalToEndorsers(transactionProposalRequest, Channel.DiscoveryOptions.CreateDiscoveryOptions().IgnoreEndpoints("blah.blah.blah.com:90", "blah.com:80",
                    // aka peer0.org1.example.com our discovery peer. Lets ignore it in endorsers selection and see if other discovered peer endorses.
                    "peer0.org1.example.com:7051")
                // if chaincode makes additional chaincode calls or uses collections you should add them with setServiceDiscoveryChaincodeInterests
                //         .setServiceDiscoveryChaincodeInterests(Channel.ServiceDiscoveryChaincodeCalls.createServiceDiscoveryChaincodeCalls("someOtherChaincodeName").addCollections("collection1", "collection2"))
            );
            Assert.AreEqual(transactionPropResp.Count, 1);
            ProposalResponse proposalResponse = transactionPropResp.First();
            Peer peer = proposalResponse.Peer;
            Assert.AreEqual(protocol + "//peer1.org1.example.com:7056", peer.Url); // not our discovery peer but the discovered one.

            string expectedTransactionId = null;

            StringBuilder evenTransactionId = new StringBuilder();

            foreach (ProposalResponse response in transactionPropResp)
            {
                expectedTransactionId = response.TransactionID;
                if (response.Status != ChaincodeResponse.ChaincodeResponseStatus.SUCCESS || !response.IsVerified)
                {
                    Assert.Fail("Failed status bad endorsement");
                }
            }

            //Send it to the orderer that was discovered.
            try
            {
                BlockEvent.TransactionEvent transactionEvent = foo.SendTransaction(transactionPropResp);

                evenTransactionId.Length = 0;

                evenTransactionId.Append(transactionEvent.TransactionID);
            }
            catch (TransactionEventException e)
            {
                BlockEvent.TransactionEvent te = e.TransactionEvent;
                if (te != null)
                    throw new System.Exception($"Transaction with txid {te.TransactionID} failed. {e.Message}");
            }
            catch (System.Exception e)
            {
                throw new System.Exception($"Test failed with {e.Message} exception {e}");
            }


            Assert.AreEqual(expectedTransactionId, evenTransactionId.ToString());
            Util.COut("That's all folks!");
        }
    }
}