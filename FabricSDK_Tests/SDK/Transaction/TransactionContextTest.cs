/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

using Google.Protobuf;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Channels;
using Hyperledger.Fabric.SDK.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Transaction
{
    [TestClass]
    [TestCategory("SDK")]
    public class TransactionContextTest
    {
        private static HFClient hfclient;

        [ClassInitialize]
        public static void SetupClient(TestContext context)
        {
            try
            {
                hfclient = TestHFClient.Create();
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestGetters()
        {
            Channel channel = CreateTestChannel("channel1");

            IUser user = hfclient.UserContext;
            ICryptoSuite cryptoSuite = hfclient.CryptoSuite;

            TransactionContext context = new TransactionContext(channel, user, cryptoSuite);

            // ensure getCryptoPrimitives returns what we passed in to the constructor
            ICryptoSuite cryptoPrimitives = context.CryptoPrimitives;
            Assert.AreEqual(cryptoSuite, cryptoPrimitives);
        }

        [TestMethod]
        public void TestSignByteStrings()
        {
            TransactionContext context = CreateTestContext();

            Assert.IsNull(context.SignByteStrings((ByteString) null));
            Assert.IsNull(context.SignByteStrings((ByteString[]) null));
            Assert.IsNull(context.SignByteStrings(new ByteString[0]));

            IUser[] users = new IUser[0];
            Assert.IsNull(context.SignByteStrings(users, (ByteString) null));
            // ReSharper disable once RedundantCast
            Assert.IsNull(context.SignByteStrings(users, (ByteString[]) null));
            Assert.IsNull(context.SignByteStrings(users, new ByteString[0]));
        }

        // ==========================================================================================
        // Helper methods
        // ==========================================================================================

        private TransactionContext CreateTestContext()
        {
            Channel channel = CreateTestChannel("channel1");

            IUser user = hfclient.UserContext;
            ICryptoSuite cryptoSuite = hfclient.CryptoSuite;

            return new TransactionContext(channel, user, cryptoSuite);
        }

        private Channel CreateTestChannel(string channelName)
        {
            Channel channel = null;

            try
            {
                channel = Channel.Create(channelName, hfclient);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }

            return channel;
        }
    }
}