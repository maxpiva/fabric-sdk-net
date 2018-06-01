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

using System;
using System.IO;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK
{
    [TestClass]
    [TestCategory("SDK")]
    public class OrdererTest
    {
        private static HFClient hfclient = null;
        private static Orderer orderer = null;
        private static string tempFile;

        private static readonly string DEFAULT_CHANNEL_NAME = "channel";
        private static readonly string ORDERER_NAME = "testorderer";


        [TestInitialize]
        public void SetupClient()
        {
            hfclient = TestHFClient.Create();
            orderer = hfclient.NewOrderer(ORDERER_NAME, "grpc://localhost:5151");
        }

        [ClassCleanup]
        public static void CleanUp()
        {
            if (tempFile != null)
            {
                File.Delete(tempFile);
                tempFile = null;
            }
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Can not add orderer testorderer to channel channel2 because it already belongs to channel channel.")]
        public void TestSetDuplicateChannnel()
        {
            Channel channel = hfclient.NewChannel(DEFAULT_CHANNEL_NAME);
            orderer.Channel = channel;
            Channel channel2 = hfclient.NewChannel("channel2");
            orderer.Channel = channel2;
            orderer.Channel = channel2;
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Channel can not be null")]
        public void TestSetNullChannel()
        {
            orderer.Channel = null;
        }

        [TestMethod]
        public void TestSetChannel()
        {
            try
            {
                Channel channel = hfclient.NewChannel(DEFAULT_CHANNEL_NAME);
                orderer.Channel = channel;
                Assert.IsTrue(channel == orderer.Channel);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception " + e.Message);
            }
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Invalid name")]
        public void TestNullOrdererName()
        {
            new Orderer(null, "url", null);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadAddress()
        {
            orderer = hfclient.NewOrderer("badorderer", "xxxxxx");
            Assert.Fail("Orderer did not allow setting bad URL.");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestMissingAddress()
        {
            orderer = hfclient.NewOrderer("badaddress", "");
            Assert.Fail("Orderer did not allow setting a missing address.");
        }

        [Ignore]
        [TestMethod]
        public void TestGetChannel()
        {
            try
            {
                Channel channel = hfclient.NewChannel(DEFAULT_CHANNEL_NAME);
                orderer = hfclient.NewOrderer("ordererName", "grpc://localhost:5151");
                channel.AddOrderer(orderer);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }

            Assert.IsTrue(orderer.Channel.Name.Equals(DEFAULT_CHANNEL_NAME, StringComparison.InvariantCultureIgnoreCase), "Test passed - ");
        }

        [TestMethod]
        [ExpectedException(typeof(IllegalArgumentException))]
        public void TestSendNullTransactionThrowsException()
        {
            try
            {
                orderer = hfclient.NewOrderer(ORDERER_NAME, "grpc://localhost:5151");
            }
            catch (InvalidArgumentException e)
            {
                Assert.Fail("Failed to create new orderer: " + e);
            }

            orderer.SendTransaction(null);
            Assert.Fail("Transaction should not be null.");
        }
    }
}