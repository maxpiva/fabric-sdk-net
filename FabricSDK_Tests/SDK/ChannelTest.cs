/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *        http://www.apache.org/licenses/LICENSE-2.0
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
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Channels;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK
{
    [TestClass]
    [TestCategory("SDK")]
    public class ChannelTest
    {
        private static HFClient hfclient;
        private static Channel shutdownChannel;
        private static readonly string BAD_STUFF = "this is bad!";
        private static Orderer throwOrderer;
        private static Channel throwChannel;
        private static readonly string CHANNEL_NAME = "channel3";

        private static readonly string CHANNEL_NAME2 = "channel";

        private static readonly string SAMPLE_GO_CC = "fixture/sdkintegration/gocc/sample1";


        [ClassInitialize]
        public static void SetupClient(TestContext context)
        {
            try
            {
                hfclient = TestHFClient.Create();

                shutdownChannel = new Channel("shutdown", hfclient);
                shutdownChannel.AddOrderer(hfclient.NewOrderer("shutdow_orderer", "grpc://localhost:99"));
                shutdownChannel.IsShutdown = true;
                throwOrderer = new ThrowOrderer("foo", "grpc://localhost:8", null);
                throwChannel = new Channel("throw", hfclient);
                throwChannel.AddOrderer(throwOrderer);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestChannelCreation()
        {
            try
            {
                string channelName = "channel3";
                Channel testchannel = new Channel(channelName, hfclient);
                Assert.AreEqual(channelName, testchannel.Name);
                Assert.AreEqual(testchannel.client, hfclient);
                Assert.AreEqual(testchannel.Orderers.Count, 0);
                Assert.AreEqual(testchannel.Peers.Count, 0);
                Assert.AreEqual(testchannel.IsInitialized, false);
            }
            catch (ArgumentException e)
            {
                Assert.Fail($"Unexpected exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestChannelAddPeer()
        {
            string channelName = "channel3";
            Channel testchannel = new Channel(channelName, hfclient);
            Peer peer = hfclient.NewPeer("peer_", "grpc://localhost:7051");

            testchannel.AddPeer(peer);

            Assert.AreEqual(testchannel.Peers.Count, 1);
            Assert.AreEqual(testchannel.Peers.First(), peer);
        }

        [TestMethod]
        public void TestChannelAddOrder()
        {
            Channel testChannel = new Channel(CHANNEL_NAME, hfclient);
            Orderer orderer = hfclient.NewOrderer("testorder", "grpc://localhost:7051");
            testChannel.AddOrderer(orderer);

            Assert.AreEqual(testChannel.Orderers.Count, 1);
            Assert.AreEqual(testChannel.Orderers.First(), orderer);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel client is invalid can not be null.")]
        public void TestChannelNullClient()
        {
            // ReSharper disable once ObjectCreationAsStatement
            new Channel(CHANNEL_NAME, null);
        }

        [TestMethod]
        public void TestChannelAddNullPeer()
        {
            Channel testChannel = null;

            try
            {
                testChannel = new Channel(CHANNEL_NAME, hfclient);

                testChannel.AddPeer(null);

                Assert.Fail("Expected set null peer to throw exception.");
            }
            catch (ArgumentException)
            {
                Assert.AreEqual(testChannel?.Peers.Count ?? -1, 0);
            }
        }


        [TestMethod]
        public void TestChannelAddNoNamePeer()
        {
            Channel testChannel = null;

            try
            {
                testChannel = new Channel(CHANNEL_NAME, hfclient);
                Peer peer = hfclient.NewPeer(null, "grpc://localhost:7051");

                testChannel.AddPeer(peer);
                Assert.Fail("Expected no named peer to throw exception.");
            }
            catch (ArgumentException)
            {
                Assert.AreEqual(testChannel?.Peers.Count ?? -1, 0);
            }
        }

        [TestMethod]
        public void TestChannelAddNullOrder()
        {
            Channel testChannel = null;

            try
            {
                testChannel = new Channel(CHANNEL_NAME, hfclient);

                testChannel.AddOrderer(null);

                Assert.Fail("Expected set null order to throw exception.");
            }
            catch (ArgumentException)
            {
                Assert.AreEqual(testChannel?.Orderers.Count ?? -1, 0);
            }
        }

        [TestMethod]
        public void TestChannelAddNullEventhub()
        {
            Channel testChannel = null;

            try
            {
                testChannel = new Channel(CHANNEL_NAME, hfclient);

                testChannel.AddEventHub(null);

                Assert.Fail("Expected set null peer to throw exception.");
            }
            catch (ArgumentException)
            {
                Assert.AreEqual(testChannel?.EventHubs.Count ?? -1, 0);
            }
        }

        [TestMethod]
        public void TestChannelInitialize()
        {
            //test may not be doable once initialize is done


            Channel testChannel = new MockChannel(CHANNEL_NAME, hfclient);
            Peer peer = hfclient.NewPeer("peer_", "grpc://localhost:7051");

            testChannel.AddPeer(peer, PeerOptions.CreatePeerOptions().SetPeerRoles(PeerRole.ENDORSING_PEER));
            Assert.IsFalse(testChannel.IsInitialized);
            testChannel.Initialize();
            Assert.IsTrue(testChannel.IsInitialized);
        }
        //     Allow no peers
        //    [TestMethod]
        //    public void TestChannelInitializeNoPeer() {
        //        Channel testChannel = null;
        //
        //        try {
        //
        //            testChannel = new Channel(CHANNEL_NAME, hfclient);
        //
        //            Assert.AreEqual(testChannel.isInitialized(), false);
        //            testChannel.initialize();
        //            Assert.fail("Expected initialize to throw exception with no peers.");
        //
        //        } catch (Exception e) {
        //
        //            Assert.IsTrue(e.getClass() == InvalidArgumentException.class);
        //            Assert.IsFalse(testChannel.isInitialized());
        //        }
        //
        //    }

        //Shutdown channel tests

        [TestMethod]
        public void TestChannelShutdown()
        {
            try
            {
                Assert.IsTrue(shutdownChannel.IsShutdown);
            }
            catch (ArgumentException)
            {
                Assert.IsTrue(shutdownChannel.IsInitialized);
            }
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel shutdown has been shutdown.")]
        public void TestChannelShutdownAddPeer()
        {
            Assert.IsTrue(shutdownChannel.IsShutdown);
            shutdownChannel.AddPeer(hfclient.NewPeer("name", "grpc://myurl:90"));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel shutdown has been shutdown.")]
        public void TestChannelShutdownAddOrderer()
        {
            Assert.IsTrue(shutdownChannel.IsShutdown);
            shutdownChannel.AddOrderer(hfclient.NewOrderer("name", "grpc://myurl:90"));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel shutdown has been shutdown.")]
        public void TestChannelShutdownAddEventHub()
        {
            Assert.IsTrue(shutdownChannel.IsShutdown);
            shutdownChannel.AddEventHub(hfclient.NewEventHub("name", "grpc://myurl:90"));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "Channel shutdown has been shutdown.")]
        public void TestChannelShutdownJoinPeer()
        {
            Assert.IsTrue(shutdownChannel.IsShutdown);
            shutdownChannel.JoinPeer(hfclient.NewPeer("name", "grpc://myurl:90"));
        }


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel shutdown has been shutdown.")]
        public void TestChannelShutdownInitialize()
        {
            Assert.IsTrue(shutdownChannel.IsShutdown);
            shutdownChannel.Initialize();
        }


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel shutdown has been shutdown.")]
        public void TestChannelShutdownInstiateProposal()
        {
            Assert.IsTrue(shutdownChannel.IsShutdown);
            shutdownChannel.SendInstantiationProposal(hfclient.NewInstantiationProposalRequest());
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel shutdown has been shutdown.")]
        public void TestChannelShutdownQueryTransactionByIDl()
        {
            Assert.IsTrue(shutdownChannel.IsShutdown);
            shutdownChannel.QueryBlockByHash(new byte[] { });
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel shutdown has been shutdown.")]
        public void TestChannelBadOrderer()
        {
            shutdownChannel.SendTransaction(null,  null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Peer value is null.")]
        public void TestChannelBadPeerNull()
        {
            Channel channel = CreateRunningChannel(null);
            channel.QueryBlockByHash((Peer) null, "rick".ToBytes());
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel channel does not have peer peer2")]
        public void TestChannelBadPeerDoesNotBelong()
        {
            Channel channel = CreateRunningChannel(null);

            Peer[] peers = new Peer[] {hfclient.NewPeer("peer2", "grpc://localhost:22")};
            CreateRunningChannel("testChannelBadPeerDoesNotBelong", peers);

            channel.SendInstantiationProposal(hfclient.NewInstantiationProposalRequest(), peers);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Peer peer1 not set for channel channel")]
        public void TestChannelBadPeerDoesNotBelong2()
        {
            Channel channel = CreateRunningChannel(null);

            Peer peer = channel.Peers.First();

            Channel channel2 = CreateRunningChannel("testChannelBadPeerDoesNotBelong2", null);
            peer.intChannel = channel2;

            channel.SendInstantiationProposal(hfclient.NewInstantiationProposalRequest());
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Peer value is null.")]
        public void TestChannelBadPeerCollection()
        {
            Channel channel = CreateRunningChannel(null);

            channel.QueryByChaincode(hfclient.NewQueryProposalRequest(), new Peer[] {null});
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Collection of peers is empty.")]
        public void TestChannelBadPeerCollectionEmpty()
        {
            Channel channel = CreateRunningChannel(null);

            channel.SendUpgradeProposal(hfclient.NewUpgradeProposalRequest(), new Peer[] { });
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Collection of peers is null.")]
        public void TestChannelBadPeerCollectionNull()
        {
            Channel channel = CreateRunningChannel(null);

            channel.SendTransactionProposal(hfclient.NewTransactionProposalRequest(), null);
        }


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel by the name testTwoChannelsSameName already exists")]
        public void TestTwoChannelsSameName()
        {
            CreateRunningChannel("testTwoChannelsSameName", null);
            CreateRunningChannel("testTwoChannelsSameName", null);
        }

        public static Channel CreateRunningChannel(IEnumerable<Peer> peers)
        {
            Channel prevChannel = hfclient.GetChannel(CHANNEL_NAME2);
            if (null != prevChannel)
            {
                //cleanup remove default channel.
                prevChannel.Shutdown(false);
            }

            return CreateRunningChannel(CHANNEL_NAME2, peers);
        }

        public static Channel CreateRunningChannel(string channelName, IEnumerable<Peer> peers)
        {
            Channel channel = hfclient.NewChannel(channelName);
            if (peers == null)
            {
                Peer peer = hfclient.NewPeer("peer1", "grpc://localhost:22");
                channel.AddPeer(peer);
                channel.AddOrderer(hfclient.NewOrderer("order1", "grpc://localhost:22"));
            }
            else
            {
                foreach (Peer peer in peers)
                {
                    channel.AddPeer(peer);
                }
            }

            channel.initialized = true;

            return channel;
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "Can not add peer peer2 to channel testChannelBadPeerDoesNotBelongJoin because it already belongs to channel testChannelBadPeerDoesNotBelongJoin2")]
        public void TestChannelBadPeerDoesNotBelongJoin()
        {
            Channel channel = CreateRunningChannel("testChannelBadPeerDoesNotBelongJoin", null);

            Peer[] peers = new Peer[] {hfclient.NewPeer("peer2", "grpc://localhost:22")};

            CreateRunningChannel("testChannelBadPeerDoesNotBelongJoin2", peers);

            //Peer joining channel when it belongs to another channel.

            channel.JoinPeer(peers.First());
        }


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "Channel channel does not have any orderers associated with it.")]
        public void TestChannelPeerJoinNoOrderer()
        {
            Channel channel = CreateRunningChannel(null);
            channel.orderers = new ConcurrentHashSet<Orderer>();

            channel.JoinPeer(hfclient.NewPeer("peerJoiningNOT", "grpc://localhost:22"));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Can not initialize channel without a valid name.")]
        public void TestChannelInitNoname()
        {
            Channel channel = hfclient.NewChannel("del");
            channel.Name = null;
            channel.Initialize();
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Can not initialize channel without a client object.")]
        public void TestChannelInitNullClient()
        {
            Channel channel = hfclient.NewChannel("testChannelInitNullClient");
            channel.client = null;
            channel.Initialize();
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "InstantiateProposalRequest is null")]
        public void TestChannelsendInstantiationProposalNull()
        {
            Channel channel = CreateRunningChannel(null);

            channel.SendInstantiationProposal(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "InstallProposalRequest is null")]
        public void TestChannelsendInstallProposalNull()
        {
            Channel channel = CreateRunningChannel(null);

            channel.SendInstallProposal(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Upgradeproposal is null")]
        public void TestChannelsendUpgradeProposalNull()
        {
            Channel channel = CreateRunningChannel(null);

            channel.SendUpgradeProposal(null);
        }

        //queryBlockByHash

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "blockHash parameter is null.")]
        public void TestChannelQueryBlockByHashNull()
        {
            Channel channel = CreateRunningChannel(null);

            channel.QueryBlockByHash(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Channel channel has not been initialized.")]
        public void TestChannelQueryBlockByHashNotInitialized()
        {
            Channel channel = CreateRunningChannel(null);
            channel.initialized = false;

            channel.QueryBlockByHash("hyper this hyper that".ToBytes());
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "TxID parameter is null.")]
        public void TestChannelQueryBlockByTransactionIDNull()
        {
            Channel channel = CreateRunningChannel(null);

            channel.QueryBlockByTransactionID(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "TxID parameter is null.")]
        public void TestChannelQueryTransactionByIDNull()
        {
            Channel channel = CreateRunningChannel(null);

            channel.QueryTransactionByID(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "You interrupting me?")]
        public void TestQueryInstalledChaincodesThrowInterrupted()
        {
            Channel channel = CreateRunningChannel(null);
            Peer peer = channel.Peers.First();
            peer.endorserClent = new MockEndorserClient(new ProposalException("You interrupting me?"));

            hfclient.QueryChannels(peer);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "rick did this:)")]
        public void TestQueryInstalledChaincodesThrowPeerException()
        {
            Channel channel = CreateRunningChannel(null);
            Peer peer = channel.Peers.First();
            peer.endorserClent = new MockEndorserClient(new ProposalException("rick did this:)"));

            hfclient.QueryChannels(peer);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "What time is it?")]
        public void TestQueryInstalledChaincodesThrowTimeoutException()
        {
            Channel channel = CreateRunningChannel(null);
            Peer peer = channel.Peers.First();
            peer.endorserClent = new MockEndorserClient(new ProposalException("What time is it?"));

            hfclient.QueryChannels(peer);
            //????
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "Error bad bad bad")]
        public void TestQueryInstalledChaincodesERROR()
        {
            Channel channel = CreateRunningChannel(null);
            Peer peer = channel.Peers.First();
            peer.endorserClent = new MockEndorserClient(new ProposalException("Error bad bad bad"));
            hfclient.QueryChannels(peer);
        }

        /*
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "ABORTED")]
        public void TestQueryInstalledChaincodesStatusRuntimeException()
        {
            Channel channel = CreateRunningChannel(null);
            Peer peer = channel.Peers.First();
            peer.endorserClent = new MockEndorserClient(future);
            hfclient.QueryChannels(peer);
        }
        */
        [TestMethod]
        public void TestProposalBuilderWithMetaInf()
        {
            InstallProposalBuilder installProposalBuilder = InstallProposalBuilder.Create();

            installProposalBuilder.ChaincodeLanguage(TransactionRequest.Type.GO_LANG);
            installProposalBuilder.ChaincodePath("github.com/example_cc");
            installProposalBuilder.ChaincodeSource(SAMPLE_GO_CC.Locate());
            installProposalBuilder.ChaincodeName("example_cc.go");
            installProposalBuilder.ChaincodeMetaInfLocation("fixture/meta-infs/test1".Locate());
            installProposalBuilder.ChaincodeVersion("1");

            Channel channel = hfclient.NewChannel("testProposalBuilderWithMetaInf");

            TestUtils.TestUtils.MockUser mockUser = TestUtils.TestUtils.GetMockUser("rick", "rickORG");
            TransactionContext transactionContext = new TransactionContext(channel, mockUser, Factory.Instance.GetCryptoSuite());
            installProposalBuilder.Context(transactionContext);

            Proposal proposal = installProposalBuilder.Build(); // Build it get the proposal. Then unpack it to see if it's what we expect.

            ChaincodeProposalPayload chaincodeProposalPayload = ChaincodeProposalPayload.Parser.ParseFrom(proposal.Payload);
            ChaincodeInvocationSpec chaincodeInvocationSpec = ChaincodeInvocationSpec.Parser.ParseFrom(chaincodeProposalPayload.Input);
            ChaincodeSpec chaincodeSpec = chaincodeInvocationSpec.ChaincodeSpec;
            ChaincodeInput input = chaincodeSpec.Input;

            ChaincodeDeploymentSpec chaincodeDeploymentSpec = ChaincodeDeploymentSpec.Parser.ParseFrom(input.Args[1]);
            ByteString codePackage = chaincodeDeploymentSpec.CodePackage;

            List<string> tarBytesToEntryArrayList = TestUtils.TestUtils.TarBytesToEntryArrayList(codePackage.ToByteArray());
            List<string> expect = new List<string> {"META-INF/statedb/couchdb/indexes/MockFakeIndex.json", "src/github.com/example_cc/example_cc.go"};
            CollectionAssert.AreEquivalent(expect, tarBytesToEntryArrayList, "Tar in Install Proposal's codePackage does not have expected entries. ");
        }

        [TestMethod]
        public void TestProposalBuilderWithOutMetaInf()
        {
            InstallProposalBuilder installProposalBuilder = InstallProposalBuilder.Create();

            installProposalBuilder.ChaincodeLanguage(TransactionRequest.Type.GO_LANG);
            installProposalBuilder.ChaincodePath("github.com/example_cc");
            installProposalBuilder.ChaincodeSource(SAMPLE_GO_CC.Locate());
            installProposalBuilder.ChaincodeName("example_cc.go");
            installProposalBuilder.ChaincodeVersion("1");

            Channel channel = hfclient.NewChannel("testProposalBuilderWithOutMetaInf");
            TransactionContext transactionContext = new TransactionContext(channel, TestUtils.TestUtils.GetMockUser("rick", "rickORG"), Factory.Instance.GetCryptoSuite());

            installProposalBuilder.Context(transactionContext);

            Proposal proposal = installProposalBuilder.Build(); // Build it get the proposal. Then unpack it to see if it's what we expect.
            ChaincodeProposalPayload chaincodeProposalPayload = ChaincodeProposalPayload.Parser.ParseFrom(proposal.Payload);
            ChaincodeInvocationSpec chaincodeInvocationSpec = ChaincodeInvocationSpec.Parser.ParseFrom(chaincodeProposalPayload.Input);
            ChaincodeSpec chaincodeSpec = chaincodeInvocationSpec.ChaincodeSpec;
            ChaincodeInput input = chaincodeSpec.Input;

            ChaincodeDeploymentSpec chaincodeDeploymentSpec = ChaincodeDeploymentSpec.Parser.ParseFrom(input.Args[1]);
            ByteString codePackage = chaincodeDeploymentSpec.CodePackage;
            List<string> tarBytesToEntryArrayList = TestUtils.TestUtils.TarBytesToEntryArrayList(codePackage.ToByteArray());

            List<string> expect = new List<string>() {"src/github.com/example_cc/example_cc.go"};
            CollectionAssert.AreEquivalent(expect, tarBytesToEntryArrayList, "Tar in Install Proposal's codePackage does not have expected entries. ");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "The META-INF directory does not exist in")]
        public void TestProposalBuilderWithNoMetaInfDir()
        {
            InstallProposalBuilder installProposalBuilder = InstallProposalBuilder.Create();

            installProposalBuilder.ChaincodeLanguage(TransactionRequest.Type.GO_LANG);
            installProposalBuilder.ChaincodePath("github.com/example_cc");
            installProposalBuilder.ChaincodeSource(SAMPLE_GO_CC.Locate());
            installProposalBuilder.ChaincodeName("example_cc.go");
            installProposalBuilder.ChaincodeVersion("1");
            installProposalBuilder.ChaincodeMetaInfLocation("fixture/meta-infs/test1/META-INF".Locate()); // points into which is not what's expected.

            Channel channel = hfclient.NewChannel("testProposalBuilderWithNoMetaInfDir");
            TransactionContext transactionContext = new TransactionContext(channel, TestUtils.TestUtils.GetMockUser("rick", "rickORG"), Factory.Instance.GetCryptoSuite());

            installProposalBuilder.Context(transactionContext);

            installProposalBuilder.Build(); // Build it get the proposal. Then unpack it to see if it's what we epect.
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Directory to find chaincode META-INF")]
        public void TestProposalBuilderWithMetaInfExistsNOT()
        {
            InstallProposalBuilder installProposalBuilder = InstallProposalBuilder.Create();

            installProposalBuilder.ChaincodeLanguage(TransactionRequest.Type.GO_LANG);
            installProposalBuilder.ChaincodePath("github.com/example_cc");
            installProposalBuilder.ChaincodeSource(SAMPLE_GO_CC.Locate());
            installProposalBuilder.ChaincodeName("example_cc.go");
            installProposalBuilder.ChaincodeVersion("1");
            installProposalBuilder.ChaincodeMetaInfLocation("/tmp/fdsjfksfj/fjksfjskd/fjskfjdsk/should never exist"); // points into which is not what's expected.
            Channel channel = hfclient.NewChannel("testProposalBuilderWithMetaInfExistsNOT");
            TransactionContext transactionContext = new TransactionContext(channel, TestUtils.TestUtils.GetMockUser("rick", "rickORG"), Factory.Instance.GetCryptoSuite());

            installProposalBuilder.Context(transactionContext);

            installProposalBuilder.Build(); // Build it get the proposal. Then unpack it to see if it's what we epect.
        }

        [TestMethod]
        public void TestNOf()
        {
            Peer peer1Org1 = new Peer("peer1Org1", "grpc://localhost:9", null);
            Peer peer1Org12nd = new Peer("org12nd", "grpc://localhost:9", null);
            Peer peer2Org2 = new Peer("peer2Org2", "grpc://localhost:9", null);
            Peer peer2Org22nd = new Peer("peer2Org22nd", "grpc://localhost:9", null);

            //One from each set.
            NOfEvents nOfEvents = NOfEvents.CreateNofEvents().AddNOfs(NOfEvents.CreateNofEvents().SetN(1).AddPeers(peer1Org1, peer1Org12nd), NOfEvents.CreateNofEvents().SetN(1).AddPeers(peer2Org2, peer2Org22nd));

            NOfEvents nOfEvents1 = new NOfEvents(nOfEvents);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer1Org1);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer1Org12nd);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer2Org22nd);
            Assert.IsTrue(nOfEvents1.Ready);
            Assert.IsFalse(nOfEvents.Ready);

            nOfEvents = NOfEvents.CreateNofEvents().AddNOfs(NOfEvents.CreateNofEvents().AddPeers(peer1Org1, peer1Org12nd), NOfEvents.CreateNofEvents().AddPeers(peer2Org2, peer2Org22nd));
            nOfEvents1 = new NOfEvents(nOfEvents);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer1Org1);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer2Org2);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer1Org12nd);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer2Org22nd);
            Assert.IsTrue(nOfEvents1.Ready);
            Assert.IsFalse(nOfEvents.Ready);

            EventHub peer2Org2eh = new EventHub("peer2Org2", "grpc://localhost:9", null);
            EventHub peer2Org22ndeh = new EventHub("peer2Org22nd", "grpc://localhost:9", null);

            nOfEvents = NOfEvents.CreateNofEvents().SetN(1).AddNOfs(NOfEvents.CreateNofEvents().AddPeers(peer1Org1, peer1Org12nd), NOfEvents.CreateNofEvents().AddEventHubs(peer2Org2eh, peer2Org22ndeh));

            nOfEvents1 = new NOfEvents(nOfEvents);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer1Org1);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer2Org2eh);
            Assert.IsFalse(nOfEvents1.Ready);
            nOfEvents1.Seen(peer2Org22ndeh);
            Assert.IsTrue(nOfEvents1.Ready);
            Assert.IsFalse(nOfEvents.Ready);

            nOfEvents = NOfEvents.CreateNoEvents();
            Assert.IsTrue(nOfEvents.Ready);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "The META-INF directory")]
        public void TestProposalBuilderWithMetaInfEmpty()
        {
            string emptyINF = "fixture/meta-infs/emptyMetaInf/META-INF".Locate(); // make it cause git won't check in empty directory
            if (!Directory.Exists(emptyINF))
            {
                Directory.CreateDirectory(emptyINF);
            }

            InstallProposalBuilder installProposalBuilder = InstallProposalBuilder.Create();

            installProposalBuilder.ChaincodeLanguage(TransactionRequest.Type.GO_LANG);
            installProposalBuilder.ChaincodePath("github.com/example_cc");
            installProposalBuilder.ChaincodeSource(SAMPLE_GO_CC.Locate());
            installProposalBuilder.ChaincodeName("example_cc.go");
            installProposalBuilder.ChaincodeVersion("1");
            installProposalBuilder.ChaincodeMetaInfLocation("fixture/meta-infs/emptyMetaInf".Locate()); // points into which is not what's expected.

            Channel channel = hfclient.NewChannel("testProposalBuilderWithMetaInfEmpty");
            TransactionContext transactionContext = new TransactionContext(channel, TestUtils.TestUtils.GetMockUser("rick", "rickORG"), Factory.Instance.GetCryptoSuite());

            installProposalBuilder.Context(transactionContext);

            installProposalBuilder.Build(); // Build it get the proposal. Then unpack it to see if it's what we epect.
        }

        public class ThrowOrderer : Orderer
        {
            public ThrowOrderer(string name, string url, Properties properties) : base(name, url, properties)
            {
            }

            public new DeliverResponse[] SendDeliver(Envelope transaction)
            {
                throw new TransactionException(BAD_STUFF);
            }

            public new BroadcastResponse SendTransaction(Envelope transaction)
            {
                throw new System.Exception(BAD_STUFF);
            }
        }

        public class MockChannel : Channel
        {
            public MockChannel(string name, HFClient client) : base(name, client)
            {
            }

            protected override Task<Dictionary<string, MSP>> ParseConfigBlockAsync(bool force, CancellationToken token)
            {
                return Task.FromResult((Dictionary<string, MSP>) null);
            }

            protected override Task LoadCACertificatesAsync(bool force, CancellationToken token)
            {
                return Task.FromResult(0);
            }
        }

        private class MockEndorserClient : EndorserClient
        {
            private readonly ProposalResponse returned;
            private readonly System.Exception throwThis;

            public MockEndorserClient(System.Exception throwThis) : base("blahchannlname", "blahpeerName",new Endpoint("grpc://loclhost:99", null))
            {
                this.throwThis = throwThis ?? throw new ArgumentException("Can't throw a null!");
                returned = null;
            }

            // ReSharper disable once UnusedMember.Local
            public MockEndorserClient(ProposalResponse returned) : base("blahchannlname", "blahpeerName",new Endpoint("grpc://loclhost:99", null))
            {
                throwThis = null;
                this.returned = returned;
            }


            public override bool IsChannelActive => true;


            public override Task<ProposalResponse> SendProposalAsync(SignedProposal proposal, CancellationToken token = default(CancellationToken))
            {
                if (throwThis != null)
                    throw throwThis;
                return Task.FromResult(returned);
            }
        }
    }
}