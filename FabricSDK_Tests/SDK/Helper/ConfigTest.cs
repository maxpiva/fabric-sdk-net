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
using Hyperledger.Fabric.SDK.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Helper
{
    [TestClass]
    public class ConfigTest
    {
        private readonly TestConfigHelper configHelper = new TestConfigHelper();
        // private String originalHashAlgorithm;

        [TestInitialize]
        public void SetUp()
        {
            // reset Config before each test
            configHelper.ClearConfig();
        }

        [TestCleanup]
        public void TearDown()
        {
            // reset Config after each test. We do not want to interfere with the
            // next test or the next test suite
            configHelper.ClearConfig();
        }


        [ClassCleanup]
        public static void Tearclassdown()
        {
            TestConfigHelper sconfigHelper = new TestConfigHelper();
            sconfigHelper.ClearConfig();
            var _=Fabric.SDK.Helper.Config.Instance;
        }

        // Tests that Config.getConfig properly loads a value from a system property
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
                Assert.AreEqual(Config.Instance.GetHashAlgorithm(), newVal);
            }
            finally
            {
                // Restore the system property
                Environment.SetEnvironmentVariable(propName, originalValue);
            }
        }

        // Note: This unit test is of questionable value
        // It may be better to actually set the individual values (via a system property)
        // and make sure they come back again!
        [TestMethod]
        public void TestGetters()
        {


            // Numeric params
            Assert.IsTrue(Config.Instance.GetSecurityLevel() > 0);
            Assert.IsTrue(Config.Instance.GetProposalWaitTime() > 0);
            Assert.IsTrue(Config.Instance.GetGenesisBlockWaitTime() > 0);

            Assert.IsTrue(Config.Instance.MaxLogStringLength() > 0);

            // Boolean params
            // Not sure how best to deal with these, as they will always return either true or false
            // So, for coverage, let's simply call the method to ensure they don't throw exceptions...
            Config.Instance.GetProposalConsistencyValidation();

            // String params
            Assert.IsNotNull(Config.Instance.GetHashAlgorithm());
            Assert.IsNotNull(Config.Instance.GetAsymmetricKeyType());
            Assert.IsNotNull(Config.Instance.GetSignatureAlgorithm());
            Assert.IsNotNull(Config.Instance.GetCertificateFormat());
        }

        [TestMethod]
        public void TestExtraLogLevel()
        {
            Assert.IsTrue(Config.Instance.ExtraLogLevel(-99));
            Assert.IsFalse(Config.Instance.ExtraLogLevel(99));
        }

        //Not supported, since we use liblog, liblog abstracts the logging library. So user can use almost any.
        /*
        [TestMethod]
        public void TestLogLevelTrace()
        {
            testLogLevelAny("TRACE", org.apache.log4j.Level.TRACE);
    }

    @Test
    public void testLogLevelDebug() {
        testLogLevelAny("DEBUG", org.apache.log4j.Level.DEBUG);
    }

    @Test
    public void testLogLevelInfo() {
        testLogLevelAny("INFO", org.apache.log4j.Level.INFO);
    }

    @Test
    public void testLogLevelWarn() {
        testLogLevelAny("WARN", org.apache.log4j.Level.WARN);
    }

    @Test
    public void testLogLevelError() {
        testLogLevelAny("ERROR", org.apache.log4j.Level.ERROR);
    }

   */
    }
}