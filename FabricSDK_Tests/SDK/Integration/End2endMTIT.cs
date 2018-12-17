using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.Protos.Ledger.Rwset.Kvrwset;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Blocks;
using Hyperledger.Fabric.SDK.Channels;
using Hyperledger.Fabric.SDK.Configuration;
using Hyperledger.Fabric.SDK.Deserializers;
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
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

/**
 * Test end to end with multiple threads
 */
namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    [TestClass]
    [TestCategory("SDK_INTEGRATION")]
    public class End2endMTIT
    {
        private static readonly TestConfig testConfig = TestConfig.Instance;
        private static readonly string TEST_ADMIN_NAME = "admin";
        private static readonly string TESTUSER_1_NAME = "user1";
        private static readonly string TEST_FIXTURES_PATH = "fixture";

        private static readonly string FOO_CHANNEL_NAME = "foo";
        private static readonly string BAR_CHANNEL_NAME = "bar";
        private static readonly int DEPLOYWAITTIME = testConfig.GetDeployWaitTime();

        private static readonly byte[] EXPECTED_EVENT_DATA = "!".ToBytes();
        private static readonly string EXPECTED_EVENT_NAME = "event";
        private static readonly Dictionary<string, string> TX_EXPECTED;
        private static readonly int WORKER_COUNT = 399;

        // ReSharper disable once CollectionNeverQueried.Local
        private readonly Dictionary<string, Properties> clientTLSProperties = new Dictionary<string, Properties>();

        private readonly TestConfigHelper configHelper = new TestConfigHelper();

        internal readonly string sampleStoreFile = Path.Combine(Path.GetTempPath(), "HFCSampletest.properties");
        private readonly CancellationTokenSource memorySource = new CancellationTokenSource();
        private Task memoryThread ;
        private readonly Random random = new Random();
        internal SampleStore sampleStore;
        private IReadOnlyList<SampleOrg> testSampleOrgs;
        private string testTxID; // save the CC invoke TxID and use in queries


        static End2endMTIT()
        {
            TX_EXPECTED = new Dictionary<string, string>();
            TX_EXPECTED.Add("readset1", "Missing readset for channel bar block 1");
            TX_EXPECTED.Add("writeset1", "Missing writeset for channel bar block 1");
        }

        internal virtual string testName { get; } = "End2endMTIT";

        internal virtual string CHAIN_CODE_FILEPATH { get; } = "sdkintegration/gocc/sample_mv";
        internal virtual string CHAIN_CODE_NAME { get; } = "example_cc_mv_go";
        internal virtual string CHAIN_CODE_PATH { get; } = "github.com/example_cc";
        internal virtual string CHAIN_CODE_VERSION { get; } = "1";
        internal virtual TransactionRequest.Type CHAIN_CODE_LANG { get; } = TransactionRequest.Type.GO_LANG;

        //CHECKSTYLE.ON: Method length is 320 lines (max allowed is 150).

        private static string PrintableString(string str)
        {
            int maxLogStringLength = 64;
            if (string.IsNullOrEmpty(str))
                return str;
            string ret = str; //Regex.Replace(str, "[^\\p{Print}]", "?");

            ret = ret.Substring(0, Math.Min(ret.Length, maxLogStringLength)) + (ret.Length > maxLogStringLength ? "..." : "");

            return ret;
        }

        private void MemoryAllocator()
        {
            memoryThread = Task.Run(async () =>
            {
                int loopSleep = 1000 * 60;
                // ReSharper disable once NotAccessedVariable
                byte[] junk = null;
                do
                {
                    try
                    {
                        if (memorySource.Token.IsCancellationRequested)
                            return;
                        await Task.Delay(loopSleep, memorySource.Token).ConfigureAwait(false);
                        Util.COut("ALLOCATING MEMORY.");
                        // ReSharper disable once RedundantAssignment
                        junk = new byte[1000000 * 90];
                        await Task.Delay(1000 * 60 * 2, memorySource.Token).ConfigureAwait(false);
                        Util.COut("DEALLOCATING MEMORY.");
                        // ReSharper disable once RedundantAssignment
                        junk = null;
                        GC.Collect();
                        loopSleep = 1000 * 60 * random.Next(3) + 1;
                        if (memorySource.Token.IsCancellationRequested)
                            return;
                    }
                    catch (System.Exception e)
                    {
                        Util.COut(e.ToString());
                    }
                } while (true);
            });
        }

        [TestInitialize]
        public virtual void CheckConfig()
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

        [TestCleanup]
        public void CleanUp()
        {
            if (memoryThread != null)
                memorySource.Cancel();
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
            MemoryAllocator();
            EnrollUsersSetup(sampleStore); //This enrolls users with fabric ca and setups sample store to get users later.
            RunFabricTest(sampleStore); //Runs Fabric tests with constructing channels, joining peers, exercising chaincode
        }

        public async Task<TransactionEvent> InstallInstantiateAsync(Channel channel, HFClient client, SampleOrg sampleOrg, CancellationToken token=default(CancellationToken))
        {
            client.UserContext = sampleOrg.PeerAdmin;
            Util.COut("Creating install proposal");
            ChaincodeID chaincodeID = new ChaincodeID();
            chaincodeID.Name = CHAIN_CODE_NAME;
            chaincodeID.Version = CHAIN_CODE_VERSION;
            if (null != CHAIN_CODE_PATH)
                chaincodeID.Path = CHAIN_CODE_PATH;

            InstallProposalRequest installProposalRequest = client.NewInstallProposalRequest();
            installProposalRequest.ChaincodeID = chaincodeID;

            ////For GO language and serving just a single user, chaincodeSource is mostly likely the users GOPATH
            installProposalRequest.ChaincodeSourceLocation = Path.Combine(TEST_FIXTURES_PATH, CHAIN_CODE_FILEPATH).Locate();
            installProposalRequest.ChaincodeVersion = CHAIN_CODE_VERSION;
            installProposalRequest.ChaincodeLanguage = CHAIN_CODE_LANG;
            Util.COut("Sending install proposal");

            ////////////////////////////
            // only a client from the same org as the peer can issue an install request
            int numInstallProposal = 0;
            List<Peer> peers = channel.Peers.ToList();
            numInstallProposal = numInstallProposal + peers.Count;
            List<ProposalResponse> responses = await client.SendInstallProposalAsync(installProposalRequest, peers, token).ConfigureAwait(false);

            List<ProposalResponse> successful = new List<ProposalResponse>();
            List<ProposalResponse> failed = new List<ProposalResponse>();

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

            ///////////////
            //// Instantiate chaincode.
            InstantiateProposalRequest instantiateProposalRequest = client.NewInstantiationProposalRequest();
            instantiateProposalRequest.SetProposalWaitTime(DEPLOYWAITTIME);
            instantiateProposalRequest.SetChaincodeID(chaincodeID);
            instantiateProposalRequest.SetChaincodeLanguage(CHAIN_CODE_LANG);
            instantiateProposalRequest.SetFcn("init");
            instantiateProposalRequest.SetArgs(new string[] {"a", "500000000", "b", "" + 200});
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
            Util.COut("Sending instantiateProposalRequest to all peers with arguments: a and b set to 100 and %s respectively", "" + 200);
            successful.Clear();
            failed.Clear();

            responses = await channel.SendInstantiationProposalAsync(instantiateProposalRequest, token).ConfigureAwait(false);

            foreach (ProposalResponse response in responses)
            {
                if (response.IsVerified && response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                {
                    successful.Add(response);
                    Util.COut("Succesful instantiate proposal response Txid: %s from peer %s", response.TransactionID, response.Peer.Name);
                }
                else
                {
                    failed.Add(response);
                }
            }

            Util.COut("Received %d instantiate proposal responses. Successful+verified: %d . Failed: %d", responses.Count, successful.Count, failed.Count);
            if (failed.Count > 0)
            {
                foreach (ProposalResponse fail in failed)
                {
                    Util.COut("Not enough endorsers for instantiate :" + successful.Count + "endorser failed with " + fail.Message + ", on peer" + fail.Peer);
                }

                ProposalResponse first = failed.First();
                Util.COut("Not enough endorsers for instantiate :" + successful.Count + "endorser failed with " + first.Message + ". Was verified:" + first.IsVerified);
            }

            ///////////////
            // Send instantiate transaction to orderer
            Util.COut("Sending instantiateTransaction to orderer with a and b set to 100 and %s respectively", "" + 200);

            //Specify what events should complete the interest in this transaction. This is the default
            // for all to complete. It's possible to specify many different combinations like
            //any from a group, all from one group and just one from another or even None(NOfEvents.createNoEvents).
            // See. Channel.NOfEvents
            NOfEvents nOfEvents = NOfEvents.CreateNofEvents();
            if (channel.GetPeers(PeerRole.EVENT_SOURCE).Count > 0)
            {
                nOfEvents.AddPeers(channel.GetPeers(PeerRole.EVENT_SOURCE));
            }

            if (channel.EventHubs.Count > 0)
            {
                nOfEvents.AddEventHubs(channel.EventHubs);
            }

            return await channel.SendTransactionAsync(successful, TransactionOptions.Create() //Basically the default options but shows it's usage.
                    .SetUserContext(client.UserContext) //could be a different user context. this is the default.
                    .SetShuffleOrders(false) // don't shuffle any orderers the default is true.
                    .SetOrderers(channel.Orderers) // specify the orderers we want to try this transaction. Fails once all Orderers are tried.
                    .SetNOfEvents(nOfEvents) // The events to signal the completion of the interest in the transaction
            ,10000, token).ConfigureAwait(false);
        }

        public void Worker(int id, HFClient client, CancellationToken token, params TestPair[] testPairs)
        {
            int[] start = new int[testPairs.Length];
            for (int i = 0; i < start.Length; i++)
            {
                start[i] = 200;
            }

            for (int i = 0; i < 200000000; ++i)
            {
                Util.COut("Worker %d doing run: %d", id, i);
                int moveAmount = random.Next(9) + 1;
                int whichChannel = random.Next(testPairs.Length);
                if (token.IsCancellationRequested)
                    return;
                RunChannel(client, testPairs[whichChannel].Channel, id, i, testPairs[whichChannel].SampleOrg, moveAmount, start[whichChannel]);
                start[whichChannel] += moveAmount;
                if (token.IsCancellationRequested)
                    return;
            }
        }

        public void RunFabricTest(SampleStore sStore)
        {
            ////////////////////////////
            // Setup client

            //Create instance of client.
            HFClient client = HFClient.Create();
            client.CryptoSuite = Factory.Instance.GetCryptoSuite();

            ////////////////////////////
            //Construct and run the channels
            List<Task<TransactionEvent>> futures = new List<Task<TransactionEvent>>(2);
            //  CompletableFuture<BlockEvent.TransactionEvent>[] ii = new CompletableFuture[2];
            SampleOrg sampleOrg1 = testConfig.GetIntegrationTestsSampleOrg("peerOrg1");
            Channel fooChannel = ConstructChannel(FOO_CHANNEL_NAME, client, sampleOrg1);
            futures.Add(InstallInstantiateAsync(fooChannel, client, sampleOrg1));

            SampleOrg sampleOrg2 = testConfig.GetIntegrationTestsSampleOrg("peerOrg2");
            Channel barChannel = ConstructChannel(BAR_CHANNEL_NAME, client, sampleOrg2);
            futures.Add(InstallInstantiateAsync(barChannel, client, sampleOrg2));
            Task.WhenAll(futures).RunAndUnwrap();

            TestPair[] testPairs = {new TestPair(fooChannel, sampleOrg1), new TestPair(barChannel, sampleOrg2)};
            CancellationTokenSource src = new CancellationTokenSource();
            CancellationToken token = src.Token;
            for (int i = 0; i < WORKER_COUNT; ++i)
            {
                int k = i;
                Task.Run(() => Worker(k, client, token, testPairs));
                try
                {
                    Task.Delay(random.Next(3000)).RunAndUnwrap();
                }
                catch (ThreadInterruptedException e)
                {
                    Util.COut(e.ToString());
                }
            }

            src.Cancel();
            Util.COut("That's all folks!");
        }

        /**
     * Will register and enroll users persisting them to samplestore.
     *
     * @param sampleStore
     * @throws Exception
     */
        public void EnrollUsersSetup(SampleStore sStore)
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
                    sStore.StoreClientPEMTLCertificate(sampleOrg, tlsCertPEM);
                    sStore.StoreClientPEMTLSKey(sampleOrg, tlsKeyPEM);
                }

                HFCAInfo info = ca.Info(); //just check if we connect at all.
                Assert.IsNotNull(info);
                string infoName = info.CAName;
                if (!string.IsNullOrEmpty(infoName))
                    Assert.AreEqual(ca.CAName, infoName);

                SampleUser admin = sStore.GetMember(TEST_ADMIN_NAME, orgName);
                if (!admin.IsEnrolled)
                {
                    //Preregistered admin only needs to be enrolled with Fabric caClient.
                    admin.Enrollment = ca.Enroll(admin.Name, "adminpw");
                    admin.MspId = mspid;
                }

                sampleOrg.Admin = admin; // The admin of this org --

                SampleUser user = sStore.GetMember(TESTUSER_1_NAME, sampleOrg.Name);
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

                SampleUser peerOrgAdmin = sStore.GetMember(sampleOrgName + "Admin", sampleOrgName, sampleOrg.MSPID, Util.FindFileSk(Path.Combine(testConfig.GetTestChannelPath(), "crypto-config/peerOrganizations/", sampleOrgDomainName, $"users/Admin@{sampleOrgDomainName}/msp/keystore")), Path.Combine(testConfig.GetTestChannelPath(), "crypto-config/peerOrganizations", sampleOrgDomainName, $"users/Admin@{sampleOrgDomainName}/msp/signcerts/Admin@{sampleOrgDomainName}-cert.pem"));

                sampleOrg.PeerAdmin = peerOrgAdmin; //A special user that can create channels, join peers and install chaincode
            }
        }


        //CHECKSTYLE.OFF: Method length is 320 lines (max allowed is 150).
        private void RunChannel(HFClient client, Channel channel, int workerId, int runId, SampleOrg sampleOrg, int delta, int start)
        {
    


            //List<End2endIT.ChaincodeEventCapture> chaincodeEvents = new List<End2endIT.ChaincodeEventCapture>(); // Test list to capture chaincode events.

            try
            {
                string channelName = channel.Name;
                //bool isFooChain = FOO_CHANNEL_NAME.Equals(channelName);
                Util.COut("Running channel {0}", channelName);

                //IReadOnlyList<Orderer> orderers = channel.Orderers;
                ChaincodeID chaincodeID;
                //List<ProposalResponse> responses;
                List<ProposalResponse> successful = new List<ProposalResponse>();
                List<ProposalResponse> failed = new List<ProposalResponse>();


                ChaincodeID chaincodeIDBuilder = new ChaincodeID().SetName(CHAIN_CODE_NAME).SetVersion(CHAIN_CODE_VERSION);
                if (null != CHAIN_CODE_PATH)
                {
                    chaincodeIDBuilder.SetPath(CHAIN_CODE_PATH);
                }

                chaincodeID = chaincodeIDBuilder;


                successful.Clear();
                failed.Clear();
                IUser user = sampleOrg.GetUser(TESTUSER_1_NAME);


                ///////////////
                // Send transaction proposal to all peers
                TransactionProposalRequest transactionProposalRequest = client.NewTransactionProposalRequest();
                transactionProposalRequest.ChaincodeID = chaincodeID;
                transactionProposalRequest.ChaincodeLanguage = CHAIN_CODE_LANG;
                transactionProposalRequest.UserContext = user;
                //transactionProposalRequest.SetFcn("invoke");
                transactionProposalRequest.Fcn = "move";
                transactionProposalRequest.ProposalWaitTime = testConfig.GetProposalWaitTime();
                transactionProposalRequest.SetArgs("a" + workerId, "b" + workerId, delta + "");

                Dictionary<string, byte[]> tm2 = new Dictionary<string, byte[]>();
                tm2.Add("HyperLedgerFabric", "TransactionProposalRequest:JavaSDK".ToBytes()); //Just some extra junk in transient map
                tm2.Add("method", "TransactionProposalRequest".ToBytes()); // ditto
                tm2.Add("result", ":)".ToBytes()); // This should be returned see chaincode why.
                tm2.Add(EXPECTED_EVENT_NAME, EXPECTED_EVENT_DATA); //This should trigger an event see chaincode why.

                transactionProposalRequest.SetTransientMap(tm2);

                Util.COut("Sending transactionProposal to all peers with arguments: move(a%d,b%d,%d) with b at %d", workerId, workerId, delta, start);


                List<ProposalResponse> transactionPropResp = channel.SendTransactionProposal(transactionProposalRequest, channel.Peers);
                foreach (ProposalResponse response in transactionPropResp)
                {
                    if (response.Status == ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                    {
                        Util.COut("Successful channel%s worker id %d transaction proposal response Txid: %s from peer %s", channelName, workerId, response.TransactionID, response.Peer.Name);
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

                Util.COut("Channel %s worker id %d, received %d transaction proposal responses. Successful+verified: %d . Failed: %d", channelName, workerId, transactionPropResp.Count, successful.Count, failed.Count);
                if (failed.Count > 0)
                {
                    ProposalResponse firstTransactionProposalResponse = failed.First();
                    Assert.Fail($"Not enough endorsers for invoke(move a,b,100): {failed.Count} endorser error: {firstTransactionProposalResponse.Message}. Was verified: {firstTransactionProposalResponse.IsVerified}");
                }

                Util.COut("Channel %s, worker id %d successfully received transaction proposal responses.", channelName, workerId);


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
                Util.COut("Sending chaincode transaction(move a%d,b%d,%d) to orderer. with b value %d", workerId, workerId, delta, start);

                TransactionEvent transactionEvent = channel.SendTransaction(successful, user);

                try
                {
                    WaitOnFabric(0);

                    Assert.IsTrue(transactionEvent.IsValid); // must be valid to be here.
                    Util.COut("Channel %s worker id %d Finished transaction with transaction id %s", channelName, workerId, transactionEvent.TransactionID);

                    testTxID = transactionEvent.TransactionID; // used in the channel queries later

                    ////////////////////////////
                    // Send Query Proposal to all peers
                    //
                    string expect = start + delta + "";
                    Util.COut("Channel %s Now query chaincode for the value of b%d.", channelName, workerId);

                    QueryByChaincodeRequest queryByChaincodeRequest = client.NewQueryProposalRequest();
                    queryByChaincodeRequest.SetArgs(new string[] {"b" + workerId});
                    queryByChaincodeRequest.SetFcn("query");
                    queryByChaincodeRequest.SetChaincodeID(chaincodeID);
                    tm2.Clear();
                    tm2.Add("HyperLedgerFabric", "QueryByChaincodeRequest:JavaSDK".ToBytes());
                    tm2.Add("method", "QueryByChaincodeRequest".ToBytes());
                    queryByChaincodeRequest.SetTransientMap(tm2);

                    List<ProposalResponse> queryProposals = channel.QueryByChaincode(queryByChaincodeRequest, channel.Peers);
                    foreach (ProposalResponse proposalResponse in queryProposals)
                    {
                        if (!proposalResponse.IsVerified || proposalResponse.Status != ChaincodeResponse.ChaincodeResponseStatus.SUCCESS)
                        {
                            Assert.Fail($"Failed query proposal from peer {proposalResponse.Peer.Name} status: {proposalResponse.Status}. Messages: {proposalResponse.Message}. Was verified : {proposalResponse.IsVerified}");
                        }
                        else
                        {
                            string payload = proposalResponse.ProtoProposalResponse.Response.Payload.ToStringUtf8();
                            Util.COut("Channel %s worker id %d, query payload of b%d from peer %s returned %s and was expecting: %d", channelName, workerId, workerId, proposalResponse.Peer.Name, payload, delta + start);
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

                long atomicHeight = long.MaxValue;
                BlockchainInfo[] bcInfoA = new BlockchainInfo[1];

                channel.Peers.ToList().ForEach(peer =>
                {
                    try
                    {
                        BlockchainInfo channelInfo2 = channel.QueryBlockchainInfo(peer, user);
                        long height = channelInfo2.Height;
                        if (height < atomicHeight)
                        {
                            Interlocked.Exchange(ref atomicHeight, height);
                            bcInfoA[0] = channelInfo2;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Util.COut(e.ToString());
                        Assert.Fail(e.Message);
                    }
                });

                BlockchainInfo channelInfo = bcInfoA[0];

                Util.COut("Channel info for : " + channelName);
                Util.COut("Channel height: " + channelInfo.Height);
                string chainCurrentHash = channelInfo.CurrentBlockHash.ToHexString();
                string chainPreviousHash = channelInfo.PreviousBlockHash.ToHexString();
                Util.COut("Chain current block hash: {0}", chainCurrentHash);
                Util.COut("Chainl previous block hash: {0}", chainPreviousHash);
                long getBlockNumber = atomicHeight - 1;

                // Query by block number. Should return latest block, i.e. block number 2
                BlockInfo returnedBlock = channel.QueryBlockByNumber(getBlockNumber, user);
                string previousHash = returnedBlock.PreviousHash.ToHexString();
                Util.COut("queryBlockByNumber returned correct block with blockNumber " + returnedBlock.BlockNumber + " \n previous_hash " + previousHash);
                Assert.AreEqual(getBlockNumber, returnedBlock.BlockNumber);
                Assert.AreEqual(chainPreviousHash, previousHash);
                int cnt = returnedBlock.EnvelopeCount;
                Util.COut("Worker: %d, run: %d, channel: %s block transaction count: %d", workerId, runId, channelName, cnt);

                // Query by block hash. Using latest block's previous hash so should return block number 1
                byte[] hashQuery = returnedBlock.PreviousHash;
                returnedBlock = channel.QueryBlockByHash(hashQuery);
                Util.COut("queryBlockByHash returned block with blockNumber " + returnedBlock.BlockNumber);
                Assert.AreEqual(getBlockNumber - 1, returnedBlock.BlockNumber, $"query by hash expected block number {getBlockNumber - 1} but was {returnedBlock.BlockNumber}%d");

                // Query block by TxID. Since it's the last TxID, should be block 2

                //TODO RICK returnedBlock = channel.QueryBlockByTransactionID(testTxID);
                //Util.COut("queryBlockByTxID returned block with blockNumber " + returnedBlock.BlockNumber);
                //Assert.AreEqual(channelInfo.Height - 1, returnedBlock.BlockNumber);

                // query transaction by ID
                //TransactionInfo txInfo = channel.QueryTransactionByID(testTxID);
                //Util.COut("QueryTransactionByID returned TransactionInfo: txID " + txInfo.TransactionID + "\n     validation code " + txInfo.ValidationCode);

                Util.COut("Running for Channel {0} done", channelName);
            }
            catch (TransactionEventException t)
            {
                TransactionEvent te = t.TransactionEvent;
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

            ChannelConfiguration channelConfiguration = new ChannelConfiguration(Path.Combine(TEST_FIXTURES_PATH.Locate(), "sdkintegration", "e2e-2Orgs", TestConfig.Instance.FAB_CONFIG_GEN_VERS, name + ".tx"));

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
                if (testConfig.IsFabricVersionAtOrAfter("1.3"))
                {
                    newChannel.JoinPeer(peer, PeerOptions.CreatePeerOptions().SetPeerRoles(PeerRole.ENDORSING_PEER, PeerRole.LEDGER_QUERY, PeerRole.CHAINCODE_QUERY, PeerRole.EVENT_SOURCE)); //Default is all roles.
                }
                else
                {
                    if (doPeerEventing && everyother)
                    {
                        newChannel.JoinPeer(peer, PeerOptions.CreatePeerOptions()); //Default is all roles.
                    }
                    else
                    {
                        // Set peer to not be all roles but eventing.
                        newChannel.JoinPeer(peer, PeerOptions.CreatePeerOptions().SetPeerRoles(PeerRoleExtensions.NoEventSource()));
                    }
                }

                Util.COut("Peer %s joined channel %s", peerName, name);
                everyother = !everyother;
            }

            //just for testing ...
            if (doPeerEventing || testConfig.IsFabricVersionAtOrAfter("1.3"))
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
            newChannel.Serialize();
            newChannel.Initialize();
            return newChannel;
        }

        // ReSharper disable once UnusedParameter.Local
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
                    foreach (EnvelopeInfo envelopeInfo in returnedBlock.EnvelopeInfos)
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

                        if (envelopeInfo.EnvelopeType == EnvelopeType.TRANSACTION_ENVELOPE)
                        {
                            ++transactionCount;
                            TransactionEnvelopeInfo transactionEnvelopeInfo = (TransactionEnvelopeInfo) envelopeInfo;

                            Util.COut("  Transaction number {0} has {1} actions", i, transactionEnvelopeInfo.TransactionActionInfoCount);
                            Assert.AreEqual(1, transactionEnvelopeInfo.TransactionActionInfoCount); // for now there is only 1 action per transaction.
                            Util.COut("  Transaction number {0} isValid {1}", i, transactionEnvelopeInfo.IsValid);
                            Assert.AreEqual(transactionEnvelopeInfo.IsValid, true);
                            Util.COut("  Transaction number {0} validation code {1}", i, transactionEnvelopeInfo.ValidationCode);
                            Assert.AreEqual(0, transactionEnvelopeInfo.ValidationCode);

                            int j = 0;
                            foreach (TransactionActionInfo transactionActionInfo in transactionEnvelopeInfo.TransactionActionInfos)
                            {
                                ++j;
                                Util.COut("   Transaction action {0} has response status {1}", j, transactionActionInfo.ResponseStatus);
                                Assert.AreEqual(200, transactionActionInfo.ResponseStatus);
                                Util.COut("   Transaction action {0} has response message bytes as string: {1}", j, PrintableString(transactionActionInfo.ResponseMessageBytes.ToUTF8String()));
                                Util.COut("   Transaction action {0} has {1} endorsements", j, transactionActionInfo.EndorsementsCount);
                                Assert.AreEqual(2, transactionActionInfo.EndorsementsCount);

                                for (int n = 0; n < transactionActionInfo.EndorsementsCount; ++n)
                                {
                                    EndorserInfo endorserInfo = transactionActionInfo.GetEndorsementInfo(n);
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

                                string chaincodeIDName = transactionActionInfo.ChaincodeIDName;
                                string chaincodeIDVersion = transactionActionInfo.ChaincodeIDVersion;
                                string chaincodeIDPath = transactionActionInfo.ChaincodeIDPath;
                                Util.COut("   Transaction action %d proposal chaincodeIDName: %s, chaincodeIDVersion: %s,  chaincodeIDPath: %s ", j, chaincodeIDName, chaincodeIDVersion, chaincodeIDPath);


                                // Check to see if we have our expected event.
                                if (blockNumber == 2)
                                {
                                    ChaincodeEventDeserializer chaincodeEvent = transactionActionInfo.Event;
                                    Assert.IsNotNull(chaincodeEvent);

                                    CollectionAssert.AreEqual(EXPECTED_EVENT_DATA, chaincodeEvent.Payload.ToByteArray());
                                    Assert.AreEqual(testTxID, chaincodeEvent.TxId);
                                    Assert.AreEqual(CHAIN_CODE_NAME, chaincodeEvent.ChaincodeId);
                                    Assert.AreEqual(EXPECTED_EVENT_NAME, chaincodeEvent.EventName);
                                    Assert.AreEqual(CHAIN_CODE_NAME, chaincodeIDName);
                                    Assert.AreEqual("github.com/example_cc", chaincodeIDPath);
                                    Assert.AreEqual("1", chaincodeIDVersion);
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

                                            Util.COut("     Namespace {0} read set {1} key {2}  version [{3}:{4}]", nspace, rs, readList.Key, readList.Version?.BlockNum ?? 0, readList.Version?.TxNum ?? 0);

                                            if ("bar".Equals(channelId) && blockNumber == 2)
                                            {
                                                if ("example_cc_go".Equals(nspace))
                                                {
                                                    if (rs == 0)
                                                    {
                                                        Assert.AreEqual("a", readList.Key);
                                                        // ReSharper disable once PossibleNullReferenceException
                                                        Assert.AreEqual((ulong) 1, readList.Version.BlockNum);
                                                        Assert.AreEqual((ulong) 0, readList.Version.TxNum);
                                                    }
                                                    else if (rs == 1)
                                                    {
                                                        Assert.AreEqual("b", readList.Key);
                                                        // ReSharper disable once PossibleNullReferenceException
                                                        Assert.AreEqual((ulong) 1, readList.Version.BlockNum);
                                                        Assert.AreEqual((ulong) 0, readList.Version.TxNum);
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

        public class TestPair
        {
            public TestPair(Channel channel, SampleOrg sampleOrg)
            {
                Channel = channel;
                SampleOrg = sampleOrg;
            }

            public Channel Channel { get; set; }
            public SampleOrg SampleOrg { get; set; }
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