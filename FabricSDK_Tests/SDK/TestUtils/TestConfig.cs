/*
 *  Copyright 2016, 2017 IBM, DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

/*
package org.hyperledger.fabric.sdk.testutils;

import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.nio.file.StandardOpenOption;
import java.util.Collection;
import java.util.Collections;
import java.util.HashMap;
import java.util.Map;
import java.util.Objects;
import java.util.Properties;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.hyperledger.fabric.sdk.helper.Utils;
import org.hyperledger.fabric.sdkintegration.SampleOrg;
*/

/**
 * Config allows for a global config of the toolkit. Central location for all
 * toolkit configuration defaults. Has a local config file that can override any
 * property defaults. Config file can be relocated via a system property
 * "org.hyperledger.fabric.sdk.configuration". Any property can be overridden
 * with environment variable and then overridden
 * with a java system property. Property hierarchy goes System property
 * overrides environment variable which overrides config file for default values specified here.
 */

/**
 * Test Configuration
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using Hyperledger.Fabric.SDK.Logging;




namespace Hyperledger.Fabric.Tests.SDK.TestUtils
{
    public class TestConfig
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(TestConfig));
        
        private static readonly string DEFAULT_CONFIG = "src/test/java/org/hyperledger/fabric/sdk/testutils.properties";
        private static readonly string ORG_HYPERLEDGER_FABRIC_SDK_CONFIGURATION = "org.hyperledger.fabric.sdktest.configuration";
        private static readonly string ORG_HYPERLEDGER_FABRIC_SDK_TEST_FABRIC_HOST = "ORG_HYPERLEDGER_FABRIC_SDK_TEST_FABRIC_HOST";

        private static readonly string LOCALHOST = //Change test to reference another host .. easier config for my testing on Windows !
            Environment.GetEnvironmentVariable(ORG_HYPERLEDGER_FABRIC_SDK_TEST_FABRIC_HOST) == null ? "localhost" : Environment.GetEnvironmentVariable(ORG_HYPERLEDGER_FABRIC_SDK_TEST_FABRIC_HOST);


        private static readonly string PROPBASE = "org.hyperledger.fabric.sdktest.";

        private static readonly string INVOKEWAITTIME = PROPBASE + "InvokeWaitTime";
        private static readonly string DEPLOYWAITTIME = PROPBASE + "DeployWaitTime";
        private static readonly string PROPOSALWAITTIME = PROPBASE + "ProposalWaitTime";

        private static readonly string INTEGRATIONTESTS_ORG = PROPBASE + "integrationTests.org.";
        private static readonly Regex orgPat = new Regex("^\"" + INTEGRATIONTESTS_ORG + "\"([^\\.]+)\\.mspid$",RegexOptions.Compiled);

        private static readonly string INTEGRATIONTESTSTLS = PROPBASE + "integrationtests.tls";

        // location switching between fabric cryptogen and configtxgen artifacts for v1.0 and v1.1 in src/test/fixture/sdkintegration/e2e-2Orgs
        public static readonly string FAB_CONFIG_GEN_VERS =
        (Environment.GetEnvironmentVariable("ORG_HYPERLEDGER_FABRIC_SDKTEST_VERSION") ?? "").Equals("1.0.0") ? "v1.0" : "v1.1";

        private static TestConfig config;
        private static Dictionary<string, string> sdkProperties = new Dictionary<string, string>();
        private readonly bool runningTLS;
        private readonly bool runningFabricCATLS;

        public bool IsRunningFabricTLS()
        {
            return runningFabricTLS;
        }

        private readonly bool runningFabricTLS;
        private static readonly Dictionary<string, SampleOrg> sampleOrgs = new Dictionary<string, SampleOrg>();

        private TestConfig()
        {
            string fullpath = Environment.GetEnvironmentVariable(ORG_HYPERLEDGER_FABRIC_SDK_CONFIGURATION);
            if (string.IsNullOrEmpty(fullpath))
                fullpath = Path.Combine(Directory.GetCurrentDirectory(), DEFAULT_CONFIG);
            bool exists = File.Exists(fullpath);
            try
            {

                var parser = new FileIniDataParser();
                sdkProperties = new Dictionary<string, string>();
                logger.Debug($"Loading configuration from {fullpath} and it is present: {exists}");
                IniData data = parser.ReadFile(DEFAULT_CONFIG);
                KeyDataCollection sect = data.Global;
                foreach (KeyData kd in sect)
                {
                    sdkProperties.Add(kd.KeyName, kd.Value);
                }

            }
            catch (System.Exception e)
            {
                logger.Warn($"Failed to load any configuration from: {fullpath}. Using toolkit defaults");
            }
            finally
            {

                // Default values

                DefaultProperty(INVOKEWAITTIME, "120");
                DefaultProperty(DEPLOYWAITTIME, "120000");
                DefaultProperty(PROPOSALWAITTIME, "120000");

                //////
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg1.mspid", "Org1MSP");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg1.domname", "org1.example.com");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg1.ca_location", "http://" + LOCALHOST + ":7054");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg1.caName", "ca0");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg1.peer_locations", "peer0.org1.example.com@grpc://" + LOCALHOST + ":7051, peer1.org1.example.com@grpc://" + LOCALHOST + ":7056");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg1.orderer_locations", "orderer.example.com@grpc://" + LOCALHOST + ":7050");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg1.eventhub_locations", "peer0.org1.example.com@grpc://" + LOCALHOST + ":7053,peer1.org1.example.com@grpc://" + LOCALHOST + ":7058");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg2.mspid", "Org2MSP");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg2.domname", "org2.example.com");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg2.ca_location", "http://" + LOCALHOST + ":8054");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg2.peer_locations", "peer0.org2.example.com@grpc://" + LOCALHOST + ":8051,peer1.org2.example.com@grpc://" + LOCALHOST + ":8056");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg2.orderer_locations", "orderer.example.com@grpc://" + LOCALHOST + ":7050");
                DefaultProperty(INTEGRATIONTESTS_ORG + "peerOrg2.eventhub_locations", "peer0.org2.example.com@grpc://" + LOCALHOST + ":8053, peer1.org2.example.com@grpc://" + LOCALHOST + ":8058");

                DefaultProperty(INTEGRATIONTESTSTLS, null);
                runningTLS = sdkProperties.ContainsKey(INTEGRATIONTESTSTLS);
                runningFabricCATLS = runningTLS;
                runningFabricTLS = runningTLS;

                foreach (string key in sdkProperties.Keys)
                {
                    string val = sdkProperties[key] + string.Empty;
                    
                    if (key.StartsWith(INTEGRATIONTESTS_ORG))
                    {
                        Match match = orgPat.Match(key);

                        if (match.Success && match.Groups.Count == 1)
                        {
                            string orgName = match.Groups[1].Value.Trim();
                            sampleOrgs[orgName]=new SampleOrg(orgName, val.Trim()));

                        }
                    }
                }

                for (Map.Entry < String, SampleOrg > org : sampleOrgs.entrySet())
                {
                    final SampleOrg sampleOrg = org.getValue();
                    final string orgName = org.getKey();

                    string peerNames = sdkProperties.getProperty(INTEGRATIONTESTS_ORG + orgName + ".peer_locations");
                    String[] ps = peerNames.split("[ \t]*,[ \t]*");
                    for (string peer :
                    ps) {
                        String[] nl = peer.split("[ \t]*@[ \t]*");
                        sampleOrg.addPeerLocation(nl[0], grpcTLSify(nl[1]));
                    }

                    final string domainName = sdkProperties.getProperty(INTEGRATIONTESTS_ORG + orgName + ".domname");

                    sampleOrg.setDomainName(domainName);

                    string ordererNames = sdkProperties.getProperty(INTEGRATIONTESTS_ORG + orgName + ".orderer_locations");
                    ps = ordererNames.split("[ \t]*,[ \t]*");
                    for (string peer :
                    ps) {
                        String[] nl = peer.split("[ \t]*@[ \t]*");
                        sampleOrg.addOrdererLocation(nl[0], grpcTLSify(nl[1]));
                    }

                    string eventHubNames = sdkProperties.getProperty(INTEGRATIONTESTS_ORG + orgName + ".eventhub_locations");
                    ps = eventHubNames.split("[ \t]*,[ \t]*");
                    for (string peer :
                    ps) {
                        String[] nl = peer.split("[ \t]*@[ \t]*");
                        sampleOrg.addEventHubLocation(nl[0], grpcTLSify(nl[1]));
                    }

                    sampleOrg.setCALocation(httpTLSify(sdkProperties.getProperty((INTEGRATIONTESTS_ORG + org.getKey() + ".ca_location"))));

                    sampleOrg.setCAName(sdkProperties.getProperty((INTEGRATIONTESTS_ORG + org.getKey() + ".caName")));

                    if (runningFabricCATLS)
                    {
                        string cert = "src/test/fixture/sdkintegration/e2e-2Orgs/FAB_CONFIG_GEN_VERS/crypto-config/peerOrganizations/DNAME/ca/ca.DNAME-cert.pem".replaceAll("DNAME", domainName).replaceAll("FAB_CONFIG_GEN_VERS", FAB_CONFIG_GEN_VERS);
                        File cf = new File(cert);
                        if (!cf.exists() || !cf.isFile())
                        {
                            throw new RuntimeException("TEST is missing cert file " + cf.getAbsolutePath());
                        }

                        Properties properties = new Properties();
                        properties.setProperty("pemFile", cf.getAbsolutePath());

                        properties.setProperty("allowAllHostNames", "true"); //testing environment only NOT FOR PRODUCTION!

                        sampleOrg.setCAProperties(properties);
                    }
                }

            }

        }

        private string grpcTLSify(string location)
        {
            location = location.trim();
            Exception e = Utils.checkGrpcUrl(location);
            if (e != null)
            {
                throw new RuntimeException(String.format("Bad TEST parameters for grpc url %s", location), e);
            }

            return runningFabricTLS ? location.replaceFirst("^grpc://", "grpcs://") : location;

        }

        private string httpTLSify(string location)
        {
            location = location.trim();

            return runningFabricCATLS ? location.replaceFirst("^http://", "https://") : location;
        }

        /**
         * getConfig return back singleton for SDK configuration.
         *
         * @return Global configuration
         */
        public static TestConfig getConfig()
        {
            if (null == config)
            {
                config = new TestConfig();
            }

            return config;

        }

        /**
         * getProperty return back property for the given value.
         *
         * @param property
         * @return string value for the property
         */
        private string GetProperty(string property)
        {

            string ret = sdkProperties.GetOrNull(property);

            if (null == ret)
            {
                logger.Warn($"No configuration value found for '{property}'");
            }

            return ret;
        }


        private static void DefaultProperty(string key, string value)
        {

            string ret = Environment.GetEnvironmentVariable(key);
            if (ret != null)
            {
                sdkProperties[key]=ret;
            }
            else
            {
                string envKey = key.ToUpperInvariant().Replace("\\.", "_");
                ret = Environment.GetEnvironmentVariable(key);
                if (null != ret)
                {
                    sdkProperties[key] = ret;
                }
                else
                {
                    if (!sdkProperties.ContainsKey(key) && value != null)
                    {
                        sdkProperties[key] = value;
                    }

                }

            }
        }

        public int getTransactionWaitTime()
        {
            return Integer.parseInt(getProperty(INVOKEWAITTIME));
        }

        public int getDeployWaitTime()
        {
            return Integer.parseInt(getProperty(DEPLOYWAITTIME));
        }

        public long getProposalWaitTime()
        {
            return Integer.parseInt(getProperty(PROPOSALWAITTIME));
        }

        public Collection<SampleOrg> getIntegrationTestsSampleOrgs()
        {
            return Collections.unmodifiableCollection(sampleOrgs.values());
        }

        public SampleOrg getIntegrationTestsSampleOrg(string name)
        {
            return sampleOrgs.get(name);

        }

        public Properties getPeerProperties(string name)
        {

            return getEndPointProperties("peer", name);

        }

        public Properties getOrdererProperties(string name)
        {

            return getEndPointProperties("orderer", name);

        }

        public Properties getEndPointProperties(final string type, final string name) {
            Properties ret = new Properties();

            final string domainName = getDomainName(name);

            File cert = Paths.get(getTestChannelPath(), "crypto-config/ordererOrganizations".replace("orderer", type), domainName, type + "s", name, "tls/server.crt").toFile();
            if (!cert.exists())
            {
                throw new RuntimeException(String.format("Missing cert file for: %s. Could not find at location: %s", name, cert.getAbsolutePath()));
            }

            if (!isRunningAgainstFabric10())
            {
                File clientCert;
                File clientKey;
                if ("orderer".equals(type))
                {
                    clientCert = Paths.get(getTestChannelPath(), "crypto-config/ordererOrganizations/example.com/users/Admin@example.com/tls/client.crt").toFile();

                    clientKey = Paths.get(getTestChannelPath(), "crypto-config/ordererOrganizations/example.com/users/Admin@example.com/tls/client.key").toFile();
                }
                else
                {
                    clientCert = Paths.get(getTestChannelPath(), "crypto-config/peerOrganizations/", domainName, "users/User1@" + domainName, "tls/client.crt").toFile();
                    clientKey = Paths.get(getTestChannelPath(), "crypto-config/peerOrganizations/", domainName, "users/User1@" + domainName, "tls/client.key").toFile();
                }

                if (!clientCert.exists())
                {
                    throw new RuntimeException(String.format("Missing  client cert file for: %s. Could not find at location: %s", name, clientCert.getAbsolutePath()));
                }

                if (!clientKey.exists())
                {
                    throw new RuntimeException(String.format("Missing  client key file for: %s. Could not find at location: %s", name, clientKey.getAbsolutePath()));
                }

                ret.setProperty("clientCertFile", clientCert.getAbsolutePath());
                ret.setProperty("clientKeyFile", clientKey.getAbsolutePath());
            }

            ret.setProperty("pemFile", cert.getAbsolutePath());

            ret.setProperty("hostnameOverride", name);
            ret.setProperty("sslProvider", "openSSL");
            ret.setProperty("negotiationType", "TLS");

            return ret;
        }

        public Properties getEventHubProperties(string name)
        {

            return getEndPointProperties("peer", name); //uses same as named peer

        }

        public string getTestChannelPath()
        {

            return "src/test/fixture/sdkintegration/e2e-2Orgs/" + FAB_CONFIG_GEN_VERS;

        }

        public boolean isRunningAgainstFabric10()
        {

            return "IntegrationSuiteV1.java".equals(System.getProperty("org.hyperledger.fabric.sdktest.ITSuite"));

        }

        /**
         * url location of configtxlator
         *
         * @return
         */

        public string getFabricConfigTxLaterLocation()
        {
            return "http://" + LOCALHOST + ":7059";
        }

        /**
         * Returns the appropriate Network Config YAML file based on whether TLS is currently
         * enabled or not
         *
         * @return The appropriate Network Config YAML file
         */
        public File getTestNetworkConfigFileYAML()
        {
            string fname = runningTLS ? "network-config-tls.yaml" : "network-config.yaml";
            string pname = "src/test/fixture/sdkintegration/network_configs/";
            File ret = new File(pname, fname);

            if (!"localhost".equals(LOCALHOST))
            {
                // change on the fly ...
                File temp = null;

                try
                {
                    //create a temp file
                    temp = File.createTempFile(fname, "-FixedUp.yaml");

                    if (temp.exists())
                    {
                        //For testing start fresh
                        temp.delete();
                    }

                    byte[] data = Files.readAllBytes(Paths.get(ret.getAbsolutePath()));

                    string sourceText = new String(data, StandardCharsets.UTF_8);

                    sourceText = sourceText.replaceAll("https://localhost", "https://" + LOCALHOST);
                    sourceText = sourceText.replaceAll("http://localhost", "http://" + LOCALHOST);
                    sourceText = sourceText.replaceAll("grpcs://localhost", "grpcs://" + LOCALHOST);
                    sourceText = sourceText.replaceAll("grpc://localhost", "grpc://" + LOCALHOST);

                    Files.write(Paths.get(temp.getAbsolutePath()), sourceText.getBytes(StandardCharsets.UTF_8), StandardOpenOption.CREATE_NEW, StandardOpenOption.TRUNCATE_EXISTING, StandardOpenOption.WRITE);

                    if (!Objects.equals("true", System.getenv(ORG_HYPERLEDGER_FABRIC_SDK_TEST_FABRIC_HOST + "_KEEP")))
                    {
                        temp.deleteOnExit();
                    }
                    else
                    {
                        System.err.println("produced new network-config.yaml file at:" + temp.getAbsolutePath());
                    }

                }
                catch (Exception e)
                {
                    throw new RuntimeException(e);
                }

                ret = temp;
            }

            return ret;
        }

        private string getDomainName(final string name)
        {
            int dot = name.indexOf(".");
            if (-1 == dot)
            {
                return null;
            }
            else
            {
                return name.substring(dot + 1);
            }

        }

    }
}