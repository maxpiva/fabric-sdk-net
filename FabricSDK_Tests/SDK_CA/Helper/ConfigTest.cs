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
using Hyperledger.Fabric.Tests.SDK;
using Hyperledger.Fabric_CA.SDK.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK_CA.Helper
{
    [TestClass]
    [TestCategory("SDK_CA")]
    public class ConfigTest
    {
        private readonly TestConfigHelper configHelper = new TestConfigHelper();

        [TestInitialize]
        public void SetUp()
        {
            // reset Config before each test
            configHelper.ClearCaConfig();
        }

        [TestCleanup]
        public void TearDown()
        {
            // reset Config after each test. We do not want to interfere with the
            // next test or the next test suite
            configHelper.ClearCaConfig();
        }

        // Tests that Config.GetConfig properly loads a value from a system property
        [TestMethod]
        public void TestGetConfigSystemProperty()
        {
            string propName = Config.HASH_ALGORITHM;

            // Get the original value of the property so that we can restore it later
            string originalValue = Environment.GetEnvironmentVariable(propName);

            try
            {
                // Configure the system property with the value to be tested
                string newVal = "XXX";
                Environment.SetEnvironmentVariable(propName, newVal);

                // getConfig should load the property from System
                Config config = Config.Instance;
                Assert.AreEqual(config.GetHashAlgorithm(), newVal);
            }
            finally
            {
                // Restore the system property
                SetSystemProperty(propName, originalValue);
            }
        }

        // Note: This unit test is of questionable value
        // It may be better to actually set the individual values (via a system property)
        // and make sure they come back again!
        [TestMethod]
        public void TestGetters()
        {
            Config config = Config.Instance;

            // Numeric params
            Assert.IsTrue(config.GetSecurityLevel() > 0);
            Assert.IsTrue(config.GetProposalWaitTime() > 0);
            Assert.IsTrue(config.GetSymmetricKeyByteCount() > 0);
            Assert.IsTrue(config.GetMACKeyByteCount() > 0);

            // TODO: This Config property should be renamed getMaxLogStringLength
            Assert.IsTrue(config.GetMaxLogStringLength() > 0);

            // String params
            Assert.IsNotNull(config.GetHashAlgorithm());
            Assert.IsNotNull(config.GetAsymmetricKeyType());
            Assert.IsNotNull(config.GetSymmetricKeyType());
            Assert.IsNotNull(config.GetKeyAgreementAlgorithm());
            Assert.IsNotNull(config.GetSymmetricAlgorithm());
            Assert.IsNotNull(config.GetSignatureAlgorithm());
            Assert.IsNotNull(config.GetCertificateFormat());

            // Arrays
            Assert.IsNotNull(config.GetPeerCACerts());
        }
        /*
        [TestMethod]
        public void TestLogLevelTrace()
        {
            testLogLevelAny("TRACE", org.apache.log4j.Level.TRACE);
        }

        [TestMethod]
        public void TestLogLevelDebug()
        {
            testLogLevelAny("DEBUG", org.apache.log4j.Level.DEBUG);
        }

        [TestMethod]
        public void TestLogLevelInfo()
        {
            testLogLevelAny("INFO", org.apache.log4j.Level.INFO);
        }

        [TestMethod]
        public void TestLogLevelWarn()
        {
            testLogLevelAny("WARN", org.apache.log4j.Level.WARN);
        }

        [TestMethod]
        public void TestLogLevelError()
        {
            testLogLevelAny("ERROR", org.apache.log4j.Level.ERROR);
        }
        */
        // ==========================================================================================
        // Helper methods
        // ==========================================================================================

        // Helper method to set the value of a system property
        private void SetSystemProperty(string propName, string propValue)
        {
            Environment.SetEnvironmentVariable(propName, propValue);
        }

        /*
        // Helper function to test one of the possible log levels
        private void TestLogLevelAny(String levelString, Level level)
        {
            String originalValue = System.setProperty(Config.LOGGERLEVEL, levelString);
            try
            {
                // Dummy call to ensure that a config instance is created and the
                // underlying logging level is set...
                Config.GetConfig();
                Assert.AreEqual(level, org.apache.log4j.Logger.GetLogger("org.hyperledger.fabric_ca").GetLevel());
            }
            finally
            {
                // Restore the original value so that other tests run consistently
                setSystemProperty(Config.LOGGERLEVEL, originalValue);
            }
        }
        */
    }
}