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
/**
 * Update channel scenario
 * See http://hyperledger-fabric.readthedocs.io/en/master/configtxlator.html
 * for details.
 */

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    [TestClass]
    [TestCategory("SDK_INTEGRATION")]
    [TestCategory("SDK_INTEGRATION_NODE")]
    public class UpdateChannelIT
    {
        private static readonly TestConfig testConfig = TestConfig.Instance;
        private static readonly string CONFIGTXLATOR_LOCATION = testConfig.GetFabricConfigTxLaterLocation();

        private static readonly string ORIGINAL_BATCH_TIMEOUT = "\"timeout\": \"2s\""; // Batch time out in configtx.yaml
        private static readonly string UPDATED_BATCH_TIMEOUT = "\"timeout\": \"5s\""; // What we want to change it to.

        private static readonly string FOO_CHANNEL_NAME = "foo";

        private readonly TestConfigHelper configHelper = new TestConfigHelper();
        private int eventCountBlock = 0;
        private int eventCountFilteredBlock = 0;

        private IReadOnlyList<SampleOrg> testSampleOrgs;

        [TestInitialize]
        public void CheckConfig()
        {
            Util.COut("\n\n\nRUNNING: UpdateChannelIT\n");
            TestUtils.TestUtils.ResetConfig();
            configHelper.CustomizeConfig();
//        Assert.AreEqual(256, Config.GetConfig().GetSecurityLevel());

            testSampleOrgs = testConfig.GetIntegrationTestsSampleOrgs();
        }

        [TestMethod]
        public void Setup()
        {
            try
            {
                ////////////////////////////
                // Setup client

                //Create instance of client.
                HFClient client = HFClient.Create();

                client.CryptoSuite = Factory.Instance.GetCryptoSuite();

                ////////////////////////////
                //Set up USERS

                //Persistence is not part of SDK. Sample file store is for demonstration purposes only!
                //   MUST be replaced with more robust application implementation  (Database, LDAP)
                string sampleStoreFile = Path.Combine(Path.GetTempPath(), "HFCSampletest.properties");

                SampleStore sampleStore = new SampleStore(sampleStoreFile);

                //SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface

                ////////////////////////////
                // get users for all orgs

                foreach (SampleOrg sampleOrgs in testSampleOrgs)
                {
                    string orgName = sampleOrgs.Name;
                    sampleOrgs.PeerAdmin = sampleStore.GetMember(orgName + "Admin", orgName);
                }

                ////////////////////////////
                //Reconstruct and run the channels
                SampleOrg sampleOrg = testConfig.GetIntegrationTestsSampleOrg("peerOrg1");
                Channel fooChannel = ReconstructChannel(FOO_CHANNEL_NAME, client, sampleOrg);

                // Getting foo channels current configuration bytes.
                byte[] channelConfigurationBytes = fooChannel.GetChannelConfigurationBytes();
                (int statuscode, byte[] data) = HttpPost(CONFIGTXLATOR_LOCATION + "/protolator/decode/common.Config", channelConfigurationBytes);
                Util.COut("Got {0} status for decoding current channel config bytes", statuscode);
                Assert.AreEqual(200, statuscode);

                string responseAsString = data.ToUTF8String();

                //responseAsString is JSON but use just string operations for this test.

                if (!responseAsString.Contains(ORIGINAL_BATCH_TIMEOUT))
                {
                    Assert.Fail($"Did not find expected batch timeout '{ORIGINAL_BATCH_TIMEOUT}', in:{responseAsString}");
                }

                //Now modify the batch timeout
                string updateString = responseAsString.Replace(ORIGINAL_BATCH_TIMEOUT, UPDATED_BATCH_TIMEOUT);
                (statuscode, data) = HttpPost(CONFIGTXLATOR_LOCATION + "/protolator/decode/common.Config", updateString.ToBytes());
                Util.COut("Got {0} status for encoding the new desired channel config bytes", statuscode);
                Assert.AreEqual(200, statuscode);
                byte[] newConfigBytes = data;

                // Now send to configtxlator multipart form post with original config bytes, updated config bytes and channel name.
                List<(string Name, byte[] Body, string Mime, string FName)> parts = new List<(string Name, byte[] Body, string Mime, string FName)>();
                parts.Add(("original", channelConfigurationBytes, "application/octet-stream", "originalFakeFilename"));
                parts.Add(("updated", newConfigBytes, "application/octet-stream", "updatedFakeFilename"));
                parts.Add(("channel", fooChannel.Name.ToBytes(), null, null));
                (statuscode, data) = HttpPostMultiPart(CONFIGTXLATOR_LOCATION + "/configtxlator/compute/update-from-configs", parts);
                Util.COut("Got {0} status for updated config bytes needed for updateChannelConfiguration ", statuscode);
                Assert.AreEqual(200, statuscode);
                byte[] updateBytes = data;

                UpdateChannelConfiguration updateChannelConfiguration = new UpdateChannelConfiguration(updateBytes);

                //To change the channel we need to sign with orderer admin certs which crypto gen stores:

                // private key: src/test/fixture/sdkintegration/e2e-2Orgs/channel/crypto-config/ordererOrganizations/example.com/users/Admin@example.com/msp/keystore/f1a9a940f57419a18a83a852884790d59b378281347dd3d4a88c2b820a0f70c9_sk
                //certificate:  src/test/fixture/sdkintegration/e2e-2Orgs/channel/crypto-config/ordererOrganizations/example.com/users/Admin@example.com/msp/signcerts/Admin@example.com-cert.pem

                string sampleOrgName = sampleOrg.Name;
                SampleUser ordererAdmin = sampleStore.GetMember(sampleOrgName + "OrderAdmin", sampleOrgName, "OrdererMSP", Util.FindFileSk("fixture/sdkintegration/e2e-2Orgs/" + TestConfig.FAB_CONFIG_GEN_VERS + "/crypto-config/ordererOrganizations/example.com/users/Admin@example.com/msp/keystore/"), ("fixture/sdkintegration/e2e-2Orgs/" + TestConfig.FAB_CONFIG_GEN_VERS + "/crypto-config/ordererOrganizations/example.com/users/Admin@example.com/msp/signcerts/Admin@example.com-cert.pem").Locate());

                client.UserContext = ordererAdmin;

                //Ok now do actual channel update.
                fooChannel.UpdateChannelConfiguration(updateChannelConfiguration, client.GetUpdateChannelConfigurationSignature(updateChannelConfiguration, ordererAdmin));

                //Let's add some additional verification...

                client.UserContext = sampleOrg.PeerAdmin;

                byte[] modChannelBytes = fooChannel.GetChannelConfigurationBytes();

                //Now decode the new channel config bytes to json...
                (statuscode, data) = HttpPost(CONFIGTXLATOR_LOCATION + "/protolator/decode/common.Config", modChannelBytes);
                Assert.AreEqual(200, statuscode);

                responseAsString = data.ToUTF8String();

                if (!responseAsString.Contains(UPDATED_BATCH_TIMEOUT))
                {
                    //If it doesn't have the updated time out it failed.
                    Assert.Fail($"Did not find updated expected batch timeout '{UPDATED_BATCH_TIMEOUT}', in:{responseAsString}");
                }

                if (responseAsString.Contains(ORIGINAL_BATCH_TIMEOUT))
                {
                    //Should not have been there anymore!

                    Assert.Fail($"Found original batch timeout '{ORIGINAL_BATCH_TIMEOUT}', when it was not expected in:{responseAsString}");
                }

                Util.COut("\n");

                Thread.Sleep(3000); // give time for events to happen

                Assert.IsTrue(eventCountFilteredBlock > 0); // make sure we got blockevent that were tested.
                Assert.IsTrue(eventCountBlock > 0); // make sure we got blockevent that were tested.

                Util.COut("That's all folks!");
            }
            catch (System.Exception e)
            {
                Assert.Fail(e.Message);
            }
        }

        private (int, byte[]) HttpPost(string url, byte[] body)
        {
            HttpClientHandler handler = new HttpClientHandler();
            using (HttpClient client = new HttpClient(handler, true))
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new ByteArrayContent(body);
                HttpResponseMessage msg = client.SendAsync(request, HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult();
                int respStatusCode = (int) msg.StatusCode;
                byte[] responseBodt = msg.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return (respStatusCode, responseBodt);
            }
        }

        private (int, byte[]) HttpPostMultiPart(string url, List<(string Name, byte[] Body, string Mime, string FName)> parts)
        {
            HttpClientHandler handler = new HttpClientHandler();
            using (HttpClient client = new HttpClient(handler, true))
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                var requestContent = new MultipartFormDataContent();
                foreach ((string Name, byte[] Body, string Mime, string FName) part in parts)
                {
                    ByteArrayContent content = new ByteArrayContent(part.Body);
                    if (part.Mime != null)
                        content.Headers.ContentType = MediaTypeHeaderValue.Parse(part.Mime);
                    if (part.FName != null)
                        requestContent.Add(content, part.Name, part.FName);
                    else
                        requestContent.Add(content, part.Name);
                }

                request.Content = requestContent;
                HttpResponseMessage msg = client.SendAsync(request, HttpCompletionOption.ResponseContentRead).GetAwaiter().GetResult();
                int respStatusCode = (int) msg.StatusCode;
                byte[] responseBodt = msg.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return (respStatusCode, responseBodt);
            }
        }

        private Channel ReconstructChannel(string name, HFClient client, SampleOrg sampleOrg)
        {
            client.UserContext = sampleOrg.PeerAdmin;
            Channel newChannel = client.NewChannel(name);

            foreach (string orderName in sampleOrg.GetOrdererNames())
            {
                newChannel.AddOrderer(client.NewOrderer(orderName, sampleOrg.GetOrdererLocation(orderName), testConfig.GetOrdererProperties(orderName)));
            }

            Assert.IsTrue(sampleOrg.GetPeerNames().Count > 1); // need at least two for testing.

            int i = 0;
            foreach (string peerName in sampleOrg.GetPeerNames())
            {
                string peerLocation = sampleOrg.GetPeerLocation(peerName);
                Peer peer = client.NewPeer(peerName, peerLocation, testConfig.GetPeerProperties(peerName));

                //Query the actual peer for which channels it belongs to and check it belongs to this channel
                HashSet<string> channels = client.QueryChannels(peer);
                if (!channels.Contains(name))
                {
                    Assert.Fail($"Peer {peerName} does not appear to belong to channel {name}");
                }

                Channel.PeerOptions peerOptions = Channel.PeerOptions.CreatePeerOptions().SetPeerRoles(PeerRole.CHAINCODE_QUERY, PeerRole.ENDORSING_PEER, PeerRole.LEDGER_QUERY, PeerRole.EVENT_SOURCE);

                if (i % 2 == 0)
                {
                    peerOptions.RegisterEventsForFilteredBlocks(); // we need a mix of each type for testing.
                }
                else
                {
                    peerOptions.RegisterEventsForBlocks();
                }

                ++i;

                newChannel.AddPeer(peer, peerOptions);
            }

            foreach (string eventHubName in sampleOrg.GetEventHubNames())
            {
                EventHub eventHub = client.NewEventHub(eventHubName, sampleOrg.GetEventHubLocation(eventHubName), testConfig.GetEventHubProperties(eventHubName));
                newChannel.AddEventHub(eventHub);
            }

            //For testing of blocks which are not transactions.
            newChannel.RegisterBlockListener(blockEvent =>
            {
                // Note peer eventing will always start with sending the last block so this will get the last endorser block
                int transactions = 0;
                int nonTransactions = 0;
                foreach (BlockInfo.EnvelopeInfo envelopeInfo in blockEvent.EnvelopeInfos)
                {
                    if (BlockInfo.EnvelopeType.TRANSACTION_ENVELOPE == envelopeInfo.EnvelopeType)
                    {
                        ++transactions;
                    }
                    else
                    {
                        Assert.AreEqual(BlockInfo.EnvelopeType.ENVELOPE, envelopeInfo.GetType());
                        ++nonTransactions;
                    }
                }

                Assert.IsTrue(nonTransactions < 2, $"nontransactions {nonTransactions}, transactions {transactions}"); // non transaction blocks only have one envelope
                Assert.IsTrue(nonTransactions + transactions > 0, $"nontransactions {nonTransactions}, transactions {transactions}"); // has to be one.
                Assert.IsFalse(nonTransactions > 0 && transactions > 0, $"nontransactions {nonTransactions}, transactions {transactions}"); // can't have both.

                if (nonTransactions > 0)
                {
                    // this is an update block -- don't care about others here.

                    if (blockEvent.IsFiltered)
                    {
                        ++eventCountFilteredBlock; // make sure we're seeing non transaction events.
                    }
                    else
                    {
                        ++eventCountBlock;
                    }

                    Assert.AreEqual(0, blockEvent.TransactionCount);
                    Assert.AreEqual(1, blockEvent.EnvelopeCount);
                    foreach (BlockEvent.TransactionEvent transactionEvent in blockEvent.TransactionEvents)
                    {
                        Assert.Fail("Got transaction event in a block update"); // only events for update should not have transactions.
                    }
                }
            });

            newChannel.Initialize();

            return newChannel;
        }
    }
}