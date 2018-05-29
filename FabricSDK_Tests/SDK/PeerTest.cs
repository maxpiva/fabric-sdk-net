/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK
{
    [TestClass]
    [TestCategory("SDK")]
    public class PeerTest
    {
        private static HFClient hfclient;
        private static Peer peer;

        private static readonly string PEER_NAME = "peertest";


        [ClassInitialize]
        public static void SetupClient(TestContext context)
        {
            try
            {
                hfclient = TestHFClient.Create();
                peer = hfclient.NewPeer(PEER_NAME, "grpc://localhost:7051");
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestGetName()
        {
            Assert.IsTrue(peer != null);
            try
            {
                peer = hfclient.NewPeer(PEER_NAME, "grpc://localhost:4");
                Assert.AreEqual(PEER_NAME, peer.Name);
            }
            catch (InvalidArgumentException e)
            {
                Assert.Fail($"Unexpected Exeception {e.Message}");
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestSetNullName()
        {
            peer = hfclient.NewPeer(null, "grpc://localhost:4");
            Assert.Fail("expected set null name to throw exception.");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestSetEmptyName()
        {
            peer = hfclient.NewPeer("", "grpc://localhost:4");
            Assert.Fail("expected set empty name to throw exception.");
        }

        [TestMethod]
        [ExpectedException(typeof(PeerException))]
        public void TestSendAsyncNullProposal()
        {
            peer.SendProposalAsync(null).RunAndUnwarp();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadURL()
        {
            hfclient.NewPeer(PEER_NAME, " ");
            Assert.Fail("Expected peer with no channel throw exception");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Can not add peer peertest to channel duplicate because it already belongs to channel duplicate.")]
        public void TestDuplicateChannel()
        {
            Channel duplicate = hfclient.NewChannel("duplicate");
            peer.Channel = duplicate;
            peer.Channel = duplicate;
        }
    }
}