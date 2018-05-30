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

/**
 * Test end to end scenario
 */


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.Protos.Ledger.Rwset.Kvrwset;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Deserializers;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Responses;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.Integration;
using Hyperledger.Fabric.Tests.SDK.TestUtils;
using Hyperledger.Fabric_CA.SDK;
using Hyperledger.Fabric_CA.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    [TestClass]
    [TestCategory("SDK_INTEGRATION")]
    public class End2endIT
    {
        private static readonly TestConfig testConfig = TestConfig.Instance;
        private static readonly string TEST_ADMIN_NAME = "admin";
        private static readonly string TESTUSER_1_NAME = "user1";
        private static readonly string TEST_FIXTURES_PATH = "fixture";

        private static readonly string FOO_CHANNEL_NAME = "foo";
        private static readonly string BAR_CHANNEL_NAME = "bar";

        private static readonly byte[] EXPECTED_EVENT_DATA = "!".ToBytes();
        private static readonly string EXPECTED_EVENT_NAME = "event";
        private static readonly Dictionary<string, string> TX_EXPECTED;

        private readonly TestConfigHelper configHelper = new TestConfigHelper();

        private readonly Dictionary<string, Properties> clientTLSProperties = new Dictionary<string, Properties>();
        internal SampleStore sampleStore = null;

        internal readonly string sampleStoreFile = Path.Combine(Path.GetTempPath(), "HFCSampletest.properties");
        private IReadOnlyList<SampleOrg> testSampleOrgs;
        private string testTxID = null; // save the CC invoke TxID and use in queries


        static End2endIT()
        {
            TX_EXPECTED = new Dictionary<string, string>();
            TX_EXPECTED.Add("readset1", "Missing readset for channel bar block 1");
            TX_EXPECTED.Add("writeset1", "Missing writeset for channel bar block 1");
        }

        internal virtual string testName { get; } = "End2endIT";

        internal virtual string CHAIN_CODE_FILEPATH { get; } = "sdkintegration/gocc/sample1";
        internal virtual string CHAIN_CODE_NAME { get; } = "example_cc_go";
        internal virtual string CHAIN_CODE_PATH { get; } = "github.com/example_cc";
        internal virtual string CHAIN_CODE_VERSION { get; } = "1";
        internal virtual TransactionRequest.Type CHAIN_CODE_LANG { get; } = TransactionRequest.Type.GO_LANG;

        //CHECKSTYLE.ON: Method length is 320 lines (max allowed is 150).

        private static string PrintableString(string str)
        {
            int maxLogStringLength = 64;
            if (string.IsNullOrEmpty(str))
                return str;
            string ret = Regex.Replace(str, "[^\\p{Print}]", "?");

            ret = ret.Substring(0, Math.Min(ret.Length, maxLogStringLength)) + (ret.Length > maxLogStringLength ? "..." : "");

            return ret;
        }

        [TestInitialize]
        public void CheckConfig()
        {
            Util.COut("\n\n\nRUNNING: %s.\n", testName);
            //   configHelper.clearConfig();
            //   Assert.AreEqual(256, Config.GetConfig().GetSecurityLevel());
            TestUtils.TestUtils.ResetConfig();
            configHelper.CustomizeConfig();

            testSampleOrgs = testConfig.GetIntegrationTestsSampleOrgs();
            //Set up hfca for each sample org

            foreach (SampleOrg sampleOrg in testSampleOrgs)
            {
                string caName = sampleOrg.CAName; //Try one of each name and no name.
                if (!string.IsNullOrEmpty(caName))
                {
                    sampleOrg.CAClient = HFCAClient.Create(caName, sampleOrg.CALocation, sampleOrg.CAProperties);
                }
                else
                {
                    sampleOrg.CAClient = HFCAClient.Create(sampleOrg.CALocation, sampleOrg.CAProperties);
                }
            }
        }

        [TestMethod]
        public virtual void Setup()
        {
            //Persistence is not part of SDK. Sample file store is for demonstration purposes only!
            //   MUST be replaced with more robust application implementation  (Database, LDAP)

            if (File.Exists(sampleStoreFile))
            {
                //For testing start fresh
                File.Delete(sampleStoreFile);
            }

            sampleStore = new SampleStore(sampleStoreFile);

            EnrollUsersSetup(sampleStore); //This enrolls users with fabric ca and setups sample store to get users later.
            RunFabricTest(sampleStore); //Runs Fabric tests with constructing channels, joining peers, exercising chaincode
        }

        public void RunFabricTest(SampleStore sampleStore)
        {
            ////////////////////////////
            // Setup client

            //Create instance of client.
            HFClient client = HFClient.Create();

            client.CryptoSuite = Factory.Instance.GetCryptoSuite();

            ////////////////////////////
            //Construct and run the channels
            SampleOrg sampleOrg = testConfig.GetIntegrationTestsSampleOrg("peerOrg1");
            Channel fooChannel = ConstructChannel(FOO_CHANNEL_NAME, client, sampleOrg);
            sampleStore.SaveChannel(fooChannel);
            RunChannel(client, fooChannel, true, sampleOrg, 0);

            Assert.IsFalse(fooChannel.IsShutdown);
            fooChannel.Shutdown(true); // Force foo channel to shutdown clean up resources.
            Assert.IsTrue(fooChannel.IsShutdown);

            Assert.IsNull(client.GetChannel(FOO_CHANNEL_NAME));
            Util.COut("\n");

            sampleOrg = testConfig.GetIntegrationTestsSampleOrg("peerOrg2");
            Channel barChannel = ConstructChannel(BAR_CHANNEL_NAME, client, sampleOrg);
            Assert.IsTrue(barChannel.IsInitialized);
            /**
             * sampleStore.saveChannel uses {@link Channel#serializeChannel()}
             */
            sampleStore.SaveChannel(barChannel);
            Assert.IsFalse(barChannel.IsShutdown);
            RunChannel(client, barChannel, true, sampleOrg, 100); //run a newly constructed bar channel with different b value!
            //let bar channel just shutdown so we have both scenarios.

            Util.COut("\nTraverse the blocks for chain {0} ", barChannel.Name);

            BlockWalker(client, barChannel);

            Assert.IsFalse(barChannel.IsShutdown);
            Assert.IsTrue(barChannel.IsInitialized);
            Util.COut("That's all folks!");
        }

        /**
     * Will register and enroll users persisting them to samplestore.
     *
     * @param sampleStore
     * @throws Exception
     */
        public void EnrollUsersSetup(SampleStore sampleStore)
        {
            ////////////////////////////
            //Set up USERS

            //SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface

            ////////////////////////////
            // get users for all orgs

            foreach (SampleOrg sampleOrg in testSampleOrgs)
            {
                HFCAClient ca = sampleOrg.CAClient;

                string orgName = sampleOrg.Name;
                string mspid = sampleOrg.MSPID;
                ca.CryptoSuite = Factory.Instance.GetCryptoSuite();

                if (testConfig.IsRunningFabricTLS())
                {
                    //This shows how to get a client TLS certificate from Fabric CA
                    // we will use one client TLS certificate for orderer peers etc.
                    EnrollmentRequest enrollmentRequestTLS = new EnrollmentRequest();
                    enrollmentRequestTLS.AddHost("localhost");
                    enrollmentRequestTLS.Profile = "tls";
                    IEnrollment enroll = ca.Enroll("admin", "adminpw", enrollmentRequestTLS);
                    string tlsCertPEM = enroll.Cert;
                    string tlsKeyPEM = enroll.Key;
                    
                    Properties tlsProperties = new Properties();

                    tlsProperties["clientKeyBytes"] = tlsKeyPEM;
                    tlsProperties["clientCertBytes"] = tlsCertPEM;
                    clientTLSProperties[sampleOrg.Name] = tlsProperties;
                    //Save in samplestore for follow on tests.
                    sampleStore.StoreClientPEMTLCertificate(sampleOrg, tlsCertPEM);
                    sampleStore.StoreClientPEMTLSKey(sampleOrg, tlsKeyPEM);
                }

                HFCAInfo info = ca.Info(); //just check if we connect at all.
                Assert.IsNotNull(info);
                string infoName = info.CAName;
                if (!string.IsNullOrEmpty(infoName))
                    Assert.AreEqual(ca.CAName, infoName);

                SampleUser admin = sampleStore.GetMember(TEST_ADMIN_NAME, orgName);
                if (!admin.IsEnrolled)
                {
                    //Preregistered admin only needs to be enrolled with Fabric caClient.
                    admin.Enrollment = ca.Enroll(admin.Name, "adminpw");
                    admin.MspId = mspid;
                }

                sampleOrg.Admin = admin; // The admin of this org --

                SampleUser user = sampleStore.GetMember(TESTUSER_1_NAME, sampleOrg.Name);
                if (!user.IsRegistered)
                {
                    // users need to be registered AND enrolled
                    RegistrationRequest rr = new RegistrationRequest(user.Name, "org1.department1");
                    user.EnrollmentSecret = ca.Register(rr, admin);
                }

                if (!user.IsEnrolled)
                {
                    user.Enrollment = ca.Enroll(user.Name, user.EnrollmentSecret);
                    user.MspId = mspid;
                }

                sampleOrg.AddUser(user); //Remember user belongs to this Org

                string sampleOrgName = sampleOrg.Name;
                string sampleOrgDomainName = sampleOrg.DomainName;

                // src/test/fixture/sdkintegration/e2e-2Orgs/channel/crypto-config/peerOrganizations/org1.example.com/users/Admin@org1.example.com/msp/keystore/

                SampleUser peerOrgAdmin = sampleStore.GetMember(sampleOrgName + "Admin", sampleOrgName, sampleOrg.MSPID, Util.FindFileSk(Path.Combine(testConfig.GetTestChannelPath(), "crypto-config/peerOrganizations/", sampleOrgDomainName, $"/users/Admin@{sampleOrgDomainName}/msp/keystore")), Path.Combine(testConfig.GetTestChannelPath(), "crypto-config/peerOrganizations/", sampleOrgDomainName, $"/users/Admin@{sampleOrgDomainName}/msp/signcerts/Admin@{sampleOrgDomainName}-cert.pem"));

                sampleOrg.PeerAdmin = peerOrgAdmin; //A special user that can create channels, join peers and install chaincode
            }
        }

        public static byte[] GetPEMStringFromPrivateKey(AsymmetricAlgorithm priv)
        {
            AsymmetricCipherKeyPair keyPair = DotNetUtilities.GetRsaKeyPair((RSA) priv);
            using (StringWriter str = new StringWriter())
            {
                PemWriter pw = new PemWriter(str);
                pw.WriteObject(keyPair.Private);
                str.Flush();
                return str.ToString().ToBytes();
            }
        }

        //CHECKSTYLE.OFF: Method length is 320 lines (max allowed is 150).
        private void RunChannel(HFClient client, Channel channel, bool installChaincode, SampleOrg sampleOrg, int delta)
        {
            List<ChaincodeEventCapture> chaincodeEvents = new List<ChaincodeEventCapture>(); // Test list to capture chaincode events.

            try
            {
                string channelName = channel.Name;
                bool isFooChain = FOO_CHANNEL_NAME.Equals(channelName);
                Util.COut("Running channel {0}", channelName);

                IReadOnlyList<Orderer> orderers = channel.Orderers;
                ChaincodeID chaincodeID;
                List<ProposalResponse> responses;
                List<ProposalResponse> successful = new List<ProposalResponse>();
                List<ProposalResponse> failed = new List<ProposalResponse>();

                // Register a chaincode event listener that will trigger for any chaincode id and only for EXPECTED_EVENT_NAME event.

                string chaincodeEventListenerHandle = channel.RegisterChaincodeEventListener(new Regex(".*", RegexOptions.Compiled), new Regex(Regex.Escape(EXPECTED_EVENT_NAME)), (handle, blockEventx, chaincodeEvent) =>
                {
                    chaincodeEvents.Add(new ChaincodeEventCapture(handle, blockEventx, chaincodeEvent));

                    string es = blockEventx.Peer != null ? blockEventx.Peer.Name : blockEventx.EventHub.Name;
                    Util.COut("RECEIVED Chaincode event with handle: {0}, chaincode Id: {1}, chaincode event name: {2}, " + "transaction id: {3}, event payload: \"{4}\", from eventhub: {5}", handle, chaincodeEvent.ChaincodeId, chaincodeEvent.EventName, chaincodeEvent.TxId, chaincodeEvent.Payload.ToStringUtf8(), es);
                });

                //For non foo channel unregister event listener to test events are not called.
                if (!isFooChain)
                {
                    channel.UnregisterChaincodeEventListener(chaincodeEventListenerHandle);
                    chaincodeEventListenerHandle = null;
                }

                ChaincodeID chaincodeIDBuilder = new ChaincodeID().SetName(CHAIN_CODE_NAME).SetVersion(CHAIN_CODE_VERSION);
                if (null != CHAIN_CODE_PATH)
                {
                    chaincodeIDBuilder.SetPath(CHAIN_CODE_PATH);
                }

                chaincodeID = chaincodeIDBuilder;

                if (installChaincode)
                {
                    ////////////////////////////
                    // Install Proposal Request
                    //

                    client.UserContext = sampleOrg.PeerAdmin;

                    Util.COut("Creating install proposal");

                    InstallProposalRequest installProposalRequest = client.NewInstallProposalRequest();
                    installProposalRequest.ChaincodeID = chaincodeID;

                    if (isFooChain)
                    {
                        // on foo chain install from directory.

                        ////For GO language and serving just a single user, chaincodeSource is mostly likely the users GOPATH
                        installProposalRequest.ChaincodeSourceLocation = Path.Combine(TEST_FIXTURES_PATH, CHAIN_CODE_FILEPATH).Locate();
                    }
                    else
                    {
                        // On bar chain install from an input stream.

                        if (CHAIN_CODE_LANG == TransactionRequest.Type.GO_LANG)
                        {
                            installProposalRequest.ChaincodeInputStream = Util.GenerateTarGzInputStream(Path.Combine(TEST_FIXTURES_PATH, CHAIN_CODE_FILEPATH, "src", CHAIN_CODE_PATH).Locate(), Path.Combine("src", CHAIN_CODE_PATH));
                        }
                        else
                        {
                            installProposalRequest.ChaincodeInputStream = Util.GenerateTarGzInputStream(Path.Combine(TEST_FIXTURES_PATH, CHAIN_CODE_FILEPATH).Locate(), "src");
                        }
                    }

                    installProposalRequest.SetChaincodeVersion(CHAIN_CODE_VERSION);
                    installProposalRequest.ChaincodeLanguage = CHAIN_CODE_LANG;

                    Util.COut("Sending install proposal");

                    ////////////////////////////
                    // only a client from the same org as the peer can issue an install request
                    int numInstallProposal = 0;
                    //    Set<String> orgs = orgPeers.keySet();
                    //   for (SampleOrg org : testSampleOrgs) {

                    IReadOnlyList<Peer> peers = channel.Peers;
                    numInstallProposal = numInstallProposal + peers.Count;
                    responses = client.SendInstallProposal(installProposalRequest, peers);

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

                    //   }
                    Util.COut("Received {0} install proposal responses. Successful+verified: {1} . Failed: {2}", numInstallProposal, successful.Count, failed.Count);

                    if (failed.Count > 0)
                    {
                        ProposalResponse first = failed.First();
                        Assert.Fail($"Not enough endorsers for install :{successful.Count}. {first.Message}");
                    }
                }

                //   client.SetUserContext(sampleOrg.GetUser(TEST_ADMIN_NAME));
                //  readonly ChaincodeID chaincodeID = firstInstallProposalResponse.GetChaincodeID();
                // Note installing chaincode does not require transaction no need to
                // send to Orderers

                ///////////////
                //// Instantiate chaincode.
                InstantiateProposalRequest instantiateProposalRequest = client.NewInstantiationProposalRequest();
                instantiateProposalRequest.ProposalWaitTime = testConfig.GetProposalWaitTime();
                instantiateProposalRequest.ChaincodeID = chaincodeID;
                instantiateProposalRequest.ChaincodeLanguage = CHAIN_CODE_LANG;
                instantiateProposalRequest.Fcn = "init";
                instantiateProposalRequest.SetArgs(new string[] {"a", "500", "b", "" + (200 + delta)});
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
                instantiateProposalRequest.ChaincodeEndorsementPolicy = chaincodeEndorsementPolicy;

                Util.COut("Sending instantiateProposalRequest to all peers with arguments: a and b set to 100 and {0} respectively", "" + (200 + delta));
                successful.Clear();
                failed.Clear();

                if (isFooChain)
                {
                    //Send responses both ways with specifying peers and by using those on the channel.
                    responses = channel.SendInstantiationProposal(instantiateProposalRequest, channel.Peers);
                }
                else
                {
                    responses = channel.SendInstantiationProposal(instantiateProposalRequest);
                }

                foreach (ProposalResponse response in responses)
                {
                    if (response.IsVerified && response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    {
                        successful.Add(response);
                        Util.COut("Succesful instantiate proposal response Txid: {0} from peer {1}", response.TransactionID, response.Peer.Name);
                    }
                    else
                    {
                        failed.Add(response);
                    }
                }

                Util.COut("Received {0} instantiate proposal responses. Successful+verified: {1} . Failed: {2}", responses.Count, successful.Count, failed.Count);
                if (failed.Count > 0)
                {
                    foreach (ProposalResponse fail in failed)
                    {
                        Util.COut("Not enough endorsers for instantiate :" + successful.Count + "endorser failed with " + fail.Message + ", on peer" + fail.Peer);
                    }

                    ProposalResponse first = failed.First();
                    Assert.Fail($"Not enough endorsers for instantiate : {successful.Count} endorser failed with {first.Message}. Was verified: {first.IsVerified}");
                }

                ///////////////
                /// Send instantiate transaction to orderer
                Util.COut("Sending instantiateTransaction to orderer with a and b set to 100 and {0} respectively", "" + (200 + delta));

                //Specify what events should complete the interest in this transaction. This is the default
                // for all to complete. It's possible to specify many different combinations like
                //any from a group, all from one group and just one from another or even None(NOfEvents.createNoEvents).
                // See. Channel.NOfEvents
                Channel.NOfEvents nOfEvents = Channel.NOfEvents.CreateNofEvents();
                if (channel.GetPeers(PeerRole.EVENT_SOURCE).Count > 0)
                {
                    nOfEvents.AddPeers(channel.GetPeers(PeerRole.EVENT_SOURCE));
                }

                if (channel.EventHubs.Count > 0)
                {
                    nOfEvents.AddEventHubs(channel.EventHubs);
                }

                BlockEvent.TransactionEvent transactionEvent = channel.SendTransaction(successful, Channel.TransactionOptions.Create() //Basically the default options but shows it's usage.
                        .SetUserContext(client.UserContext) //could be a different user context. this is the default.
                        .SetShuffleOrders(false) // don't shuffle any orderers the default is true.
                        .SetOrderers(channel.Orderers) // specify the orderers we want to try this transaction. Fails once all Orderers are tried.
                        .SetNOfEvents(nOfEvents) // The events to signal the completion of the interest in the transaction
                    , testConfig.GetTransactionWaitTime() * 1000);

                WaitOnFabric(0);

                Assert.IsTrue(transactionEvent.IsValid); // must be valid to be here.
                Assert.IsNotNull(transactionEvent.Signature); //musth have a signature.
                BlockEvent blockEvent = transactionEvent.BlockEvent; // This is the blockevent that has this transaction.
                Assert.IsNotNull(blockEvent.Block); // Make sure the RAW Fabric block is returned.

                Util.COut("Finished instantiate transaction with transaction id {0}", transactionEvent.TransactionID);
                try
                {
                    Assert.AreEqual(blockEvent.ChannelId, channel.Name);
                    successful.Clear();
                    failed.Clear();

                    client.UserContext = sampleOrg.GetUser(TESTUSER_1_NAME);

                    ///////////////
                    /// Send transaction proposal to all peers
                    TransactionProposalRequest transactionProposalRequest = client.NewTransactionProposalRequest();
                    transactionProposalRequest.ChaincodeID = chaincodeID;
                    transactionProposalRequest.ChaincodeLanguage = CHAIN_CODE_LANG;
                    //transactionProposalRequest.SetFcn("invoke");
                    transactionProposalRequest.Fcn = "move";
                    transactionProposalRequest.ProposalWaitTime = testConfig.GetProposalWaitTime();
                    transactionProposalRequest.SetArgs("a", "b", "100");

                    Dictionary<string, byte[]> tm2 = new Dictionary<string, byte[]>();
                    tm2.Add("HyperLedgerFabric", "TransactionProposalRequest:JavaSDK".ToBytes()); //Just some extra junk in transient map
                    tm2.Add("method", "TransactionProposalRequest".ToBytes()); // ditto
                    tm2.Add("result", ":)".ToBytes()); // This should be returned see chaincode why.
                    tm2.Add(EXPECTED_EVENT_NAME, EXPECTED_EVENT_DATA); //This should trigger an event see chaincode why.

                    transactionProposalRequest.SetTransientMap(tm2);

                    Util.COut("sending transactionProposal to all peers with arguments: move(a,b,100)");

                    List<ProposalResponse> transactionPropResp = channel.SendTransactionProposal(transactionProposalRequest, channel.GetPeers());
                    foreach (ProposalResponse response in transactionPropResp)
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
                    // where all the proposals above are consistent. Note the when sending to Orderer this is done automatically.
                    //  Shown here as an example that applications can invoke and select.
                    // See org.hyperledger.fabric.sdk.proposal.consistency_validation config property.
                    List<HashSet<ProposalResponse>> proposalConsistencySets = SDKUtils.GetProposalConsistencySets(transactionPropResp);
                    if (proposalConsistencySets.Count != 1)
                        Assert.Fail($"Expected only one set of consistent proposal responses but got {proposalConsistencySets.Count}");

                    Util.COut("Received {0} transaction proposal responses. Successful+verified: {1} . Failed: {2}", transactionPropResp.Count, successful.Count, failed.Count);
                    if (failed.Count > 0)
                    {
                        ProposalResponse firstTransactionProposalResponse = failed.First();
                        Assert.Fail($"Not enough endorsers for invoke(move a,b,100): {failed.Count} endorser error: {firstTransactionProposalResponse.Message}. Was verified: {firstTransactionProposalResponse.IsVerified}");
                    }

                    Util.COut("Successfully received transaction proposal responses.");

                    ProposalResponse resp = successful.First();
                    byte[] x = resp.ChaincodeActionResponsePayload; // This is the data returned by the chaincode.
                    string resultAsString = null;
                    if (x != null)
                    {
                        resultAsString = x.ToUTF8String();
                    }

                    Assert.AreEqual(":)", resultAsString);

                    Assert.AreEqual(200, resp.ChaincodeActionResponseStatus); //Chaincode's status.

                    TxReadWriteSetInfo readWriteSetInfo = resp.ChaincodeActionResponseReadWriteSetInfo;
                    //See blockwalker below how to transverse this
                    Assert.IsNotNull(readWriteSetInfo);
                    Assert.IsTrue(readWriteSetInfo.NsRwsetCount > 0);

                    ChaincodeID cid = resp.ChaincodeID;
                    Assert.IsNotNull(cid);
                    string path = cid.Path;
                    if (null == CHAIN_CODE_PATH)
                    {
                        Assert.IsTrue(path == null || "".Equals(path));
                    }
                    else
                    {
                        Assert.AreEqual(CHAIN_CODE_PATH, path);
                    }

                    Assert.AreEqual(CHAIN_CODE_NAME, cid.Name);
                    Assert.AreEqual(CHAIN_CODE_VERSION, cid.Version);

                    ////////////////////////////
                    // Send Transaction Transaction to orderer
                    Util.COut("Sending chaincode transaction(move a,b,100) to orderer.");
                    transactionEvent=channel.SendTransaction(successful, testConfig.GetTransactionWaitTime() * 1000);
                }
                catch (System.Exception e)
                {
                    Util.COut("Caught an exception while invoking chaincode");
                    Assert.Fail($"Failed invoking chaincode with error : {e.Message}");
                }
                try
                {
                    WaitOnFabric(0);

                    Assert.IsTrue(transactionEvent.IsValid); // must be valid to be here.
                    Util.COut("Finished transaction with transaction id {0}", transactionEvent.TransactionID);
                    testTxID = transactionEvent.TransactionID; // used in the channel queries later

                    ////////////////////////////
                    // Send Query Proposal to all peers
                    //
                    string expect = "" + (300 + delta);
                    Util.COut("Now query chaincode for the value of b.");
                    QueryByChaincodeRequest queryByChaincodeRequest = client.NewQueryProposalRequest();
                    queryByChaincodeRequest.SetArgs(new string[] { "b" });
                    queryByChaincodeRequest.SetFcn("query");
                    queryByChaincodeRequest.SetChaincodeID(chaincodeID);

                    Dictionary<string, byte[]> tm2 = new Dictionary<string, byte[]>();
                    tm2.Add("HyperLedgerFabric", "QueryByChaincodeRequest:JavaSDK".ToBytes());
                    tm2.Add("method", "QueryByChaincodeRequest".ToBytes());
                    queryByChaincodeRequest.SetTransientMap(tm2);

                    List<ProposalResponse> queryProposals = channel.QueryByChaincode(queryByChaincodeRequest, channel.GetPeers());
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
                            Assert.AreEqual(payload, expect);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Util.COut("Caught exception while running query");
                    Assert.Fail($"Failed during chaincode query with error : {e.Message}");
                }


                // Channel queries

                // We can only send channel queries to peers that are in the same org as the SDK user context
                // Get the peers from the current org being used and pick one randomly to send the queries to.
                //  Set<Peer> peerSet = sampleOrg.GetPeers();
                //  Peer queryPeer = peerSet.iterator().next();
                //   Util.COut("Using peer %s for channel queries", queryPeer.GetName());

                BlockchainInfo channelInfo = channel.QueryBlockchainInfo();
                Util.COut("Channel info for : " + channelName);
                Util.COut("Channel height: " + channelInfo.Height);
                string chainCurrentHash = channelInfo.CurrentBlockHash.ToHexString();
                string chainPreviousHash = channelInfo.PreviousBlockHash.ToHexString();
                Util.COut("Chain current block hash: {0}", chainCurrentHash);
                Util.COut("Chainl previous block hash: {0}", chainPreviousHash);

                // Query by block number. Should return latest block, i.e. block number 2
                BlockInfo returnedBlock = channel.QueryBlockByNumber(channelInfo.Height - 1);
                string previousHash = returnedBlock.PreviousHash.ToHexString();
                Util.COut("queryBlockByNumber returned correct block with blockNumber " + returnedBlock.BlockNumber + " \n previous_hash " + previousHash);
                Assert.AreEqual(channelInfo.Height - 1, returnedBlock.BlockNumber);
                Assert.AreEqual(chainPreviousHash, previousHash);

                // Query by block hash. Using latest block's previous hash so should return block number 1
                byte[] hashQuery = returnedBlock.PreviousHash;
                returnedBlock = channel.QueryBlockByHash(hashQuery);
                Util.COut("queryBlockByHash returned block with blockNumber " + returnedBlock.BlockNumber);
                Assert.AreEqual(channelInfo.Height - 2, returnedBlock.BlockNumber);

                // Query block by TxID. Since it's the last TxID, should be block 2
                returnedBlock = channel.QueryBlockByTransactionID(testTxID);
                Util.COut("queryBlockByTxID returned block with blockNumber " + returnedBlock.BlockNumber);
                Assert.AreEqual(channelInfo.Height - 1, returnedBlock.BlockNumber);

                // query transaction by ID
                TransactionInfo txInfo = channel.QueryTransactionByID(testTxID);
                Util.COut("QueryTransactionByID returned TransactionInfo: txID " + txInfo.TransactionID + "\n     validation code " + txInfo.ValidationCode);

                if (chaincodeEventListenerHandle != null)
                {
                    channel.UnregisterChaincodeEventListener(chaincodeEventListenerHandle);
                    //Should be two. One event in chaincode and two notification for each of the two event hubs

                    int numberEventsExpected = channel.EventHubs.Count + channel.GetPeers(PeerRole.EVENT_SOURCE).Count;
                    //just make sure we get the notifications.
                    for (int i = 15; i > 0; --i)
                    {
                        if (chaincodeEvents.Count == numberEventsExpected)
                        {
                            break;
                        }
                        else
                        {
                            Thread.Sleep(90); // wait for the events.
                        }
                    }

                    Assert.AreEqual(numberEventsExpected, chaincodeEvents.Count);

                    foreach (ChaincodeEventCapture chaincodeEventCapture in chaincodeEvents)
                    {
                        Assert.AreEqual(chaincodeEventListenerHandle, chaincodeEventCapture.handle);
                        Assert.AreEqual(testTxID, chaincodeEventCapture.chaincodeEvent.TxId);
                        Assert.AreEqual(EXPECTED_EVENT_NAME, chaincodeEventCapture.chaincodeEvent.EventName);
                        CollectionAssert.AreEqual(EXPECTED_EVENT_DATA, chaincodeEventCapture.chaincodeEvent.Payload.ToByteArray());
                        Assert.AreEqual(CHAIN_CODE_NAME, chaincodeEventCapture.chaincodeEvent.ChaincodeId);

                        blockEvent = chaincodeEventCapture.blockEvent;
                        Assert.AreEqual(channelName, blockEvent.ChannelId);
                        //   Assert.IsTrue(channel.GetEventHubs().contains(blockEvent.GetEventHub()));
                    }
                }
                else
                {
                    Assert.IsTrue(chaincodeEvents.Count == 0);
                }

                Util.COut("Running for Channel {0} done", channelName);
            }
            catch (TransactionEventException t)
            {
                BlockEvent.TransactionEvent te = t.TransactionEvent;
                if (te != null)
                    Assert.Fail($"Transaction with txid {te.TransactionID} failed. {t.Message}");
                Assert.Fail($"Test failed with exception message {t.Message}");

            }
            catch (System.Exception e)
            {
                Assert.Fail($"Test failed with {e.GetType().Name} exception {e.Message}");
            }

        }

        internal virtual Channel ConstructChannel(string name, HFClient client, SampleOrg sampleOrg)
        {
            ////////////////////////////
            //Construct the channel
            //

            Util.COut("Constructing channel {0}", name);

            //boolean doPeerEventing = false;
            bool doPeerEventing = !testConfig.IsRunningAgainstFabric10() && BAR_CHANNEL_NAME.Equals(name);
//        boolean doPeerEventing = !testConfig.isRunningAgainstFabric10() && FOO_CHANNEL_NAME.equals(name);
            //Only peer Admin org
            client.UserContext = sampleOrg.PeerAdmin;

            List<Orderer> orderers = new List<Orderer>();

            foreach (string orderName in sampleOrg.GetOrdererNames())
            {
                Properties ordererProperties = testConfig.GetOrdererProperties(orderName);

                //example of setting keepAlive to avoid timeouts on inactive http2 connections.
                // Under 5 minutes would require changes to server side to accept faster ping rates.
                ordererProperties.Set("grpc.keepalive_time_ms", 5 * 60 * 1000);
                ordererProperties.Set("grpc.keepalive_timeout_ms", 8 * 1000);
                //ordererProperties.put("grpc.NettyChannelBuilderOption.keepAliveWithoutCalls", new Object[] {true});


                orderers.Add(client.NewOrderer(orderName, sampleOrg.GetOrdererLocation(orderName), ordererProperties));
            }

            //Just pick the first orderer in the list to create the channel.

            Orderer anOrderer = orderers.First();
            orderers.Remove(anOrderer);

            ChannelConfiguration channelConfiguration = new ChannelConfiguration(Path.Combine(TEST_FIXTURES_PATH + "sdkintegration/e2e-2Orgs/" + TestConfig.FAB_CONFIG_GEN_VERS + "/" + name + ".tx").Locate());

            //Create channel that has only one signer that is this orgs peer admin. If channel creation policy needed more signature they would need to be added too.
            Channel newChannel = client.NewChannel(name, anOrderer, channelConfiguration, client.GetChannelConfigurationSignature(channelConfiguration, sampleOrg.PeerAdmin));

            Util.COut("Created channel %s", name);

            bool everyother = true; //test with both cases when doing peer eventing.
            foreach (string peerName in sampleOrg.GetPeerNames())
            {
                string peerLocation = sampleOrg.GetPeerLocation(peerName);

                Properties peerProperties = testConfig.GetPeerProperties(peerName); //test properties for peer.. if any.
                if (peerProperties == null)
                {
                    peerProperties = new Properties();
                }


                //Example of setting specific options on grpc's NettyChannelBuilder
                peerProperties.Set("grpc.max_receive_message_length", 9000000);

                Peer peer = client.NewPeer(peerName, peerLocation, peerProperties);
                if (doPeerEventing && everyother)
                {
                    newChannel.JoinPeer(peer, Channel.PeerOptions.CreatePeerOptions()); //Default is all roles.
                }
                else
                {
                    // Set peer to not be all roles but eventing.
                    newChannel.JoinPeer(peer, Channel.PeerOptions.CreatePeerOptions().SetPeerRoles(PeerRoleExtensions.NoEventSource()));
                }

                Util.COut("Peer %s joined channel %s", peerName, name);
                everyother = !everyother;
            }

            //just for testing ...
            if (doPeerEventing)
            {
                // Make sure there is one of each type peer at the very least.
                Assert.IsFalse(newChannel.GetPeers(PeerRole.EVENT_SOURCE).Count == 0);
                Assert.IsFalse(newChannel.GetPeers(PeerRoleExtensions.NoEventSource()).Count == 0);
            }

            foreach (Orderer orderer in orderers)
            {
                //add remaining orderers if any.
                newChannel.AddOrderer(orderer);
            }

            foreach (string eventHubName in sampleOrg.GetEventHubNames())
            {
                Properties eventHubProperties = testConfig.GetEventHubProperties(eventHubName);

                eventHubProperties.Set("grpc.keepalive_time_ms", 5 * 60 * 1000);
                eventHubProperties.Set("grpc.keepalive_timeout_ms", 8 * 1000);


                EventHub eventHub = client.NewEventHub(eventHubName, sampleOrg.GetEventHubLocation(eventHubName), eventHubProperties);
                newChannel.AddEventHub(eventHub);
            }

            newChannel.Initialize();

            Util.COut("Finished initialization channel {0}", name);

            //Just checks if channel can be serialized and deserialized .. otherwise this is just a waste :)
            string serializedChannelBytes = newChannel.Serialize();
            newChannel.Shutdown(true);

            return client.DeSerializeChannel(serializedChannelBytes).Initialize();
        }

        private void WaitOnFabric(int additional)
        {
            //NOOP today
        }

        internal virtual void BlockWalker(HFClient client, Channel channel)
        {
            try
            {
                BlockchainInfo channelInfo = channel.QueryBlockchainInfo();

                for (long current = channelInfo.Height - 1; current > -1; --current)
                {
                    BlockInfo returnedBlock = channel.QueryBlockByNumber(current);
                    long blockNumber = returnedBlock.BlockNumber;

                    Util.COut("current block number {0} has data hash: {1}", blockNumber, returnedBlock.DataHash.ToHexString());
                    Util.COut("current block number {0} has previous hash id: {1}", blockNumber, returnedBlock.PreviousHash.ToHexString());
                    Util.COut("current block number {0} has calculated block hash is {1}", blockNumber, SDKUtils.CalculateBlockHash(client, blockNumber, returnedBlock.PreviousHash, returnedBlock.DataHash));

                    int envelopeCount = returnedBlock.EnvelopeCount;
                    Assert.AreEqual(1, envelopeCount);
                    Util.COut("current block number {0} has {1} envelope count:", blockNumber, returnedBlock.EnvelopeCount);
                    int i = 0;
                    int transactionCount = 0;
                    foreach (BlockInfo.EnvelopeInfo envelopeInfo in returnedBlock.EnvelopeInfos)
                    {
                        ++i;

                        Util.COut("  Transaction number {0} has transaction id: {1}", i, envelopeInfo.TransactionID);
                        string channelId = envelopeInfo.ChannelId;
                        Assert.IsTrue("foo".Equals(channelId) || "bar".Equals(channelId));

                        Util.COut("  Transaction number {0} has channel id: {1}", i, channelId);
                        Util.COut("  Transaction number {0} has epoch: {1}", i, envelopeInfo.Epoch);
                        Util.COut("  Transaction number {0} has transaction timestamp: {1}", i, envelopeInfo.Timestamp?.ToString() ?? string.Empty);
                        Util.COut("  Transaction number {0} has type id: {1}", i, "" + envelopeInfo.GetType());
                        Util.COut("  Transaction number {0} has nonce : {1}", i, "" + envelopeInfo.Nonce.ToHexString());
                        Util.COut("  Transaction number {0} has submitter mspid: {1},  certificate: {2}", i, envelopeInfo.Creator.Mspid, envelopeInfo.Creator.Id);

                        if (envelopeInfo.EnvelopeType == BlockInfo.EnvelopeType.TRANSACTION_ENVELOPE)
                        {
                            ++transactionCount;
                            BlockInfo.TransactionEnvelopeInfo transactionEnvelopeInfo = (BlockInfo.TransactionEnvelopeInfo) envelopeInfo;

                            Util.COut("  Transaction number {0} has {1} actions", i, transactionEnvelopeInfo.TransactionActionInfoCount);
                            Assert.AreEqual(1, transactionEnvelopeInfo.TransactionActionInfoCount); // for now there is only 1 action per transaction.
                            Util.COut("  Transaction number {0} isValid {1}", i, transactionEnvelopeInfo.IsValid);
                            Assert.AreEqual(transactionEnvelopeInfo.IsValid, true);
                            Util.COut("  Transaction number {0} validation code {1}", i, transactionEnvelopeInfo.ValidationCode);
                            Assert.AreEqual(0, transactionEnvelopeInfo.ValidationCode);

                            int j = 0;
                            foreach (BlockInfo.TransactionEnvelopeInfo.TransactionActionInfo transactionActionInfo in transactionEnvelopeInfo.TransactionActionInfos)
                            {
                                ++j;
                                Util.COut("   Transaction action {0} has response status {1}", j, transactionActionInfo.ResponseStatus);
                                Assert.AreEqual(200, transactionActionInfo.ResponseStatus);
                                Util.COut("   Transaction action {0} has response message bytes as string: {1}", j, PrintableString(transactionActionInfo.ResponseMessageBytes.ToUTF8String()));
                                Util.COut("   Transaction action {0} has {1} endorsements", j, transactionActionInfo.EndorsementsCount);
                                Assert.AreEqual(2, transactionActionInfo.EndorsementsCount);

                                for (int n = 0; n < transactionActionInfo.EndorsementsCount; ++n)
                                {
                                    BlockInfo.EndorserInfo endorserInfo = transactionActionInfo.GetEndorsementInfo(n);
                                    Util.COut("Endorser {0} signature: {1}", n, endorserInfo.Signature.ToHexString());
                                    Util.COut("Endorser {0} endorser: mspid {1} \n certificate {2}", n, endorserInfo.Mspid, endorserInfo.Id);
                                }

                                Util.COut("   Transaction action {0} has {1} chaincode input arguments", j, transactionActionInfo.ChaincodeInputArgsCount);
                                for (int z = 0; z < transactionActionInfo.ChaincodeInputArgsCount; ++z)
                                {
                                    Util.COut("     Transaction action {0} has chaincode input argument {1} is: {2}", j, z, PrintableString(transactionActionInfo.GetChaincodeInputArgs(z).ToUTF8String()));
                                }

                                Util.COut("   Transaction action {0} proposal response status: {1}", j, transactionActionInfo.ProposalResponseStatus);
                                Util.COut("   Transaction action {0} proposal response payload: {1}", j, PrintableString(transactionActionInfo.ProposalResponsePayload.ToUTF8String()));

                                // Check to see if we have our expected event.
                                if (blockNumber == 2)
                                {
                                    ChaincodeEventDeserializer chaincodeEvent = transactionActionInfo.Event;
                                    Assert.IsNotNull(chaincodeEvent);

                                    CollectionAssert.AreEqual(EXPECTED_EVENT_DATA, chaincodeEvent.Payload.ToByteArray());
                                    Assert.AreEqual(testTxID, chaincodeEvent.TxId);
                                    Assert.AreEqual(CHAIN_CODE_NAME, chaincodeEvent.ChaincodeId);
                                    Assert.AreEqual(EXPECTED_EVENT_NAME, chaincodeEvent.EventName);
                                }

                                TxReadWriteSetInfo rwsetInfo = transactionActionInfo.TxReadWriteSet;
                                if (null != rwsetInfo)
                                {
                                    Util.COut("   Transaction action {0} has {1} name space read write sets", j, rwsetInfo.NsRwsetCount);

                                    foreach (TxReadWriteSetInfo.NsRwsetInfo nsRwsetInfo in rwsetInfo.NsRwsetInfos)
                                    {
                                        string nspace = nsRwsetInfo.Namespace;
                                        KVRWSet rws = nsRwsetInfo.Rwset;

                                        int rs = -1;
                                        foreach (KVRead readList in rws.Reads)
                                        {
                                            rs++;

                                            Util.COut("     Namespace {0} read set {1} key {2}  version [{3}:{4}]", nspace, rs, readList.Key, readList.Version.BlockNum, readList.Version.TxNum);

                                            if ("bar".Equals(channelId) && blockNumber == 2)
                                            {
                                                if ("example_cc_go".Equals(nspace))
                                                {
                                                    if (rs == 0)
                                                    {
                                                        Assert.AreEqual("a", readList.Key);
                                                        Assert.AreEqual(1, readList.Version.BlockNum);
                                                        Assert.AreEqual(0, readList.Version.TxNum);
                                                    }
                                                    else if (rs == 1)
                                                    {
                                                        Assert.AreEqual("b", readList.Key);
                                                        Assert.AreEqual(1, readList.Version.BlockNum);
                                                        Assert.AreEqual(0, readList.Version.TxNum);
                                                    }
                                                    else
                                                    {
                                                        Assert.Fail($"unexpected readset {rs}");
                                                    }

                                                    TX_EXPECTED.Remove("readset1");
                                                }
                                            }
                                        }

                                        rs = -1;
                                        foreach (KVWrite writeList in rws.Writes)
                                        {
                                            rs++;
                                            string valAsString = PrintableString(writeList.Value.ToStringUtf8());

                                            Util.COut("     Namespace {0} write set {1} key {2} has value '{3}' ", nspace, rs, writeList.Key, valAsString);

                                            if ("bar".Equals(channelId) && blockNumber == 2)
                                            {
                                                if (rs == 0)
                                                {
                                                    Assert.AreEqual("a", writeList.Key);
                                                    Assert.AreEqual("400", valAsString);
                                                }
                                                else if (rs == 1)
                                                {
                                                    Assert.AreEqual("b", writeList.Key);
                                                    Assert.AreEqual("400", valAsString);
                                                }
                                                else
                                                {
                                                    Assert.Fail($"unexpected writeset {rs}x");
                                                }

                                                TX_EXPECTED.Remove("writeset1");
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        Assert.AreEqual(transactionCount, returnedBlock.TransactionCount);
                    }
                }

                if (TX_EXPECTED.Count > 0)
                {
                    Assert.Fail(TX_EXPECTED.Values.First());
                }
            }
            catch (InvalidProtocolBufferRuntimeException e)
            {
                Assert.Fail($"Error {e.Message}");
            }
        }

        internal class ChaincodeEventCapture
        {
            public readonly BlockEvent blockEvent;
            public readonly ChaincodeEventDeserializer chaincodeEvent; //A test class to capture chaincode events
            public readonly string handle;

            public ChaincodeEventCapture(string handle, BlockEvent blockEvent, ChaincodeEventDeserializer chaincodeEvent)
            {
                this.handle = handle;
                this.blockEvent = blockEvent;
                this.chaincodeEvent = chaincodeEvent;
            }
        }
    }
}