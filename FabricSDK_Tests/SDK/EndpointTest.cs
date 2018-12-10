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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Math;
// ReSharper disable ObjectCreationAsStatement

namespace Hyperledger.Fabric.Tests.SDK
{
    [TestClass]
    [TestCategory("SDK")]
    public class EndpointTest
    {
        [TestMethod]
        public void TestEndpointNonPEM()
        {
            Endpoint ep = new Endpoint("grpc://localhost:524", null);
            Assert.AreEqual("localhost", ep.Host);
            Assert.AreEqual(524, ep.Port);

            ep = new Endpoint("grpcs://localhost:524", null);
            Assert.AreEqual("localhost", ep.Host);

            try
            {
                new Endpoint("grpcs2://localhost:524", null);
                Assert.Fail("protocol grpcs2 should have been invalid");
            }
            catch (System.Exception rex)
            {
                Assert.AreEqual("Invalid protocol expected grpc or grpcs and found grpcs2.", rex.Message);
            }

            try
            {
                new Endpoint("grpcs://localhost", null);
                Assert.Fail("should have thrown error as there is no port in the url");
            }
            catch (System.Exception rex)
            {
                Assert.AreEqual("URL must be of the format protocol://host:port. Found: 'grpcs://localhost'", rex.Message);
            }

            try
            {
                new Endpoint("", null);
                Assert.Fail("should have thrown error as url is empty");
            }
            catch (System.Exception rex)
            {
                Assert.AreEqual("URL cannot be null or empty", rex.Message);
            }

            try
            {
                new Endpoint(null, null);
                Assert.Fail("should have thrown error as url is empty");
            }
            catch (System.Exception rex)
            {
                Assert.AreEqual("URL cannot be null or empty", rex.Message);
            }
        }

        [TestMethod]
        public void TestNullPropertySslProvider()
        {
            Properties testprops = new Properties();
            testprops.Set("hostnameOverride", "override");

            new Endpoint("grpcs://localhost:594", testprops);
        }

        /*
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "property of sslProvider has to be either openSSL or JDK")]
        public void TestEmptyPropertySslProvider() {

        Properties testprops = new Properties();
        testprops.Set("sslProvider", "closedSSL");
        testprops.Set("hostnameOverride", "override");

        new Endpoint("grpcs://localhost:594", testprops);
    }
    */
        [TestMethod]
        public void TestNullPropertyNegotiationType()
        {
            Properties testprops = new Properties();
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");

            new Endpoint("grpcs://localhost:594", testprops);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "property of negotiationType has to be either TLS or plainText")]
        public void TestEmptyPropertyNegotiationType()
        {
            Properties testprops = new Properties();
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "");

            new Endpoint("grpcs://localhost:594", testprops);
        }

        [TestMethod]
        public void TestExtractCommonName()
        {
            Properties testprops = new Properties();
            testprops.Set("trustServerCertificate", "true");
            testprops.Set("pemFile", "resources/keypair-signed.crt".Locate());
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "TLS");

            Assert.AreSame(new Endpoint("grpcs://localhost:594", testprops).GetType(), typeof(Endpoint));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Properties \"clientKeyFile\" and \"clientCertFile\" must both be set or both be null")]
        public void TestNullPropertyClientKeyFile()
        {
            Properties testprops = new Properties();
            testprops.Set("trustServerCertificate", "true");
            testprops.Set("pemFile", "resources/keypair-signed.crt".Locate());
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "TLS");
            testprops.Set("clientCertFile", "clientCertFile");

            new Endpoint("grpcs://localhost:594", testprops);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Properties \"clientKeyBytes\" and \"clientCertBytes\" must both be set or both be null")]
        public void TestNullPropertyClientKeyBytes()
        {
            Properties testprops = new Properties();
            testprops.Set("trustServerCertificate", "true");
            testprops.Set("pemFile", "resources/keypair-signed.crt".Locate());
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "TLS");
            testprops.Set("clientCertBytes", new string('\0', 100));

            new Endpoint("grpcs://localhost:594", testprops);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Properties \"clientKeyFile\" and \"clientCertFile\" must both be set or both be null")]
        public void TestNullPropertyClientCertFile()
        {
            Properties testprops = new Properties();
            testprops.Set("trustServerCertificate", "true");
            testprops.Set("pemFile", "resources/keypair-signed.crt".Locate());
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "TLS");
            testprops.Set("clientKeyFile", "clientKeyFile");

            new Endpoint("grpcs://localhost:594", testprops);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Properties \"clientKeyBytes\" and \"clientCertBytes\" must both be set or both be null")]
        public void TestNullPropertyClientCertBytes()
        {
            Properties testprops = new Properties();
            testprops.Set("trustServerCertificate", "true");
            testprops.Set("pemFile", "resources/keypair-signed.crt".Locate());
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "TLS");
            testprops.Set("clientKeyBytes", new string('\0', 100));

            new Endpoint("grpcs://localhost:594", testprops);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Failed to parse TLS client key and/or cert")]
        public void TestBadClientKeyFile()
        {
            Properties testprops = new Properties();
            testprops.Set("trustServerCertificate", "true");
            testprops.Set("pemFile", "resources/keypair-signed.crt".Locate());
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "TLS");
            testprops.Set("clientKeyFile", "resources/bad-ca.crt".Locate());
            testprops.Set("clientCertFile", "resources/tls-client.crt".Locate());

            new Endpoint("grpcs://localhost:594", testprops);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Failed to parse TLS client key and/or cert")]
        public void TestBadClientCertFile()
        {
            Properties testprops = new Properties();
            testprops.Set("trustServerCertificate", "true");
            testprops.Set("pemFile", "resources/keypair-signed.crt".Locate());
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "TLS");
            testprops.Set("clientKeyFile", "/resources/tls-client.key".Locate());
            testprops.Set("clientCertFile", "resources/bad-ca.crt".Locate());

            new Endpoint("grpcs://localhost:594", testprops);
        }

        [TestMethod]
        public void TestClientTLSInvalidProperties()
        {
            Properties testprops = new Properties();
            testprops.Set("trustServerCertificate", "true");
            testprops.Set("pemFile", "resources/keypair-signed.crt".Locate());
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "TLS");

            testprops.Set("clientKeyFile", "resources/tls-client.key".Locate());
            testprops.Set("clientKeyBytes", new string('\0', 100));
            try
            {
                new Endpoint("grpcs://localhost:594", testprops);
            }
            catch (System.Exception e)
            {
                Assert.AreEqual("Properties \"clientKeyFile\" and \"clientKeyBytes\" must cannot both be set", e.Message);
            }

            testprops.GetAndRemove("clientKeyFile");
            testprops.GetAndRemove("clientKeyBytes");
            testprops.Set("clientCertFile", "resources/tls-client.crt".Locate());
            testprops.Set("clientCertBytes", new string('\0', 100));
            try
            {
                new Endpoint("grpcs://localhost:594", testprops);
            }
            catch (System.Exception e)
            {
                Assert.AreEqual("Properties \"clientCertFile\" and \"clientCertBytes\" must cannot both be set", e.Message);
            }

            testprops.GetAndRemove("clientCertFile");
            testprops.Set("clientKeyBytes", new string('\0', 100));
            testprops.Set("clientCertBytes", new string('\0', 100));
            try
            {
                new Endpoint("grpcs://localhost:594", testprops);
            }
            catch (System.Exception e)
            {
                Assert.AreEqual("Failed to parse TLS client certificate", e.Message);
            }
        }

        [TestMethod]
        public void TestClientTLSProperties()
        {
            Properties testprops = new Properties();

            testprops.Set("trustServerCertificate", "true");
            testprops.Set("pemFile", "resources/keypair-signed.crt".Locate());
            testprops.Set("sslProvider", "openSSL");
            testprops.Set("hostnameOverride", "override");
            testprops.Set("negotiationType", "TLS");
            testprops.Set("clientKeyFile", "resources/tls-client.key".Locate());
            testprops.Set("clientCertFile", "resources/tls-client.crt".Locate());

            new Endpoint("grpcs://localhost:594", testprops);

            byte[] ckb = null, ccb = null;
            try
            {
                ckb = File.ReadAllBytes("resources/tls-client.key".Locate());
                ccb = File.ReadAllBytes("resources/tls-client.crt".Locate());
            }
            catch (System.Exception e)
            {
                Assert.Fail("failed to read tls client key or cert: " + e.Message);
            }

            testprops.GetAndRemove("clientKeyFile");
            testprops.GetAndRemove("clientCertFile");
            testprops.Set("clientKeyBytes", ckb.ToUTF8String());
            testprops.Set("clientCertBytes", ccb.ToUTF8String());
            new Endpoint("grpcs://localhost:594", testprops);
        }

        [TestMethod]
        public void TestClientTLSCACertProperties()
        {
            Properties testprops = new Properties();

            testprops.Set("pemFile", "fixture/testPems/caBundled.pems".Locate() + ", " + // has 3 certs
                                     "fixture/testPems/Org1MSP_CA.pem".Locate()); // has 1

            testprops.Set("pemBytes", File.ReadAllText("fixture/testPems/Org2MSP_CA.pem".Locate(), Encoding.UTF8)); //Can have pem bytes too. 1 cert


            Endpoint endpoint = new Endpoint("grpcs://localhost:594", testprops);
            CryptoPrimitives cp = new CryptoPrimitives();
            cp.Store.AddCertificate(endpoint.creds.RootCertificates);

            List<X509Certificate2> certs = cp.Store.Certificates.Select(a => a.X509Certificate2).ToList();

            HashSet<BigInteger> expected = new HashSet<BigInteger>()
            {
                new BigInteger("4804555946196630157804911090140692961"),
                new BigInteger("127556113420528788056877188419421545986539833585"),
                new BigInteger("704500179517916368023344392810322275871763581896"),
                new BigInteger("70307443136265237483967001545015671922421894552"),
                new BigInteger("276393268186007733552859577416965113792")
            };

            foreach (X509Certificate2 cert in certs)
            {
                BigInteger serialNumber = new BigInteger(cert.SerialNumber.FromHexString());
                Assert.IsTrue(expected.Contains(serialNumber), $"Missing certificate {serialNumber}");
            }

            Assert.AreEqual(expected.Count, certs.Count, "Didn't find the expected number of certs"); // should have same number.
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "Failed to read certificate file")]
        public void TestClientTLSCACertPropertiesBadFile()
        {
            Properties testprops = new Properties();

            testprops.Set("pemFile", "fixture/testPems/caBundled.pems".Locate() + "," + // has 3 certs
                                     "fixture/testPems/IMBAD" + "," + "fixture/testPems/Org1MSP_CA.pem".Locate()); // has 1

            new Endpoint("grpcs://localhost:594", testprops);
        }
    }
}