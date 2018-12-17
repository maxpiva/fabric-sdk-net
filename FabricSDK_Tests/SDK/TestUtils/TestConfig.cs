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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utils = Hyperledger.Fabric.SDK.Helper.Utils;

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
        private static readonly string RUNIDEMIXMTTEST = PROPBASE + "RunIdemixMTTest";  // org.hyperledger.fabric.sdktest.RunIdemixMTTest ORG_HYPERLEDGER_FABRIC_SDKTEST_RUNIDEMIXMTTEST

        private static readonly string INTEGRATIONTESTS_ORG = PROPBASE + "integrationTests.org.";
        private static readonly Regex orgPat = new Regex("^" + Regex.Escape(INTEGRATIONTESTS_ORG) + "([^\\.]+)\\.mspid$", RegexOptions.Compiled);

        private static readonly string INTEGRATIONTESTSTLS = PROPBASE + "integrationtests.tls";

        // location switching between fabric cryptogen and configtxgen artifacts for v1.0 and v1.1 in src/test/fixture/sdkintegration/e2e-2Orgs
        public string FAB_CONFIG_GEN_VERS;
            
            
            

        internal static TestConfig config;

        private static Properties sdkProperties = new Properties();
        private Dictionary<string, SampleOrg> sampleOrgs = new Dictionary<string, SampleOrg>();

        private static readonly string ORG_HYPERLEDGER_FABRIC_SDKTEST_VERSION = Environment.GetEnvironmentVariable("ORG_HYPERLEDGER_FABRIC_SDKTEST_VERSION") == null ? "1.3.0" : Environment.GetEnvironmentVariable("ORG_HYPERLEDGER_FABRIC_SDKTEST_VERSION");

        int[] fabricVersion = new int[3];


        private readonly bool runningFabricCATLS;

        private readonly bool runningFabricTLS;
        private readonly bool runningTLS;

        private TestConfig()
        {
            string[] fvs = ORG_HYPERLEDGER_FABRIC_SDKTEST_VERSION.Split('.');
            if (fvs.Length != 3)
                throw new AssertFailedException("Expected environment variable 'ORG_HYPERLEDGER_FABRIC_SDKTEST_VERSION' to be three numbers sperated by dots (1.0.0)  but got: " + ORG_HYPERLEDGER_FABRIC_SDKTEST_VERSION);
            fabricVersion[0] = int.Parse(fvs[0].Trim());
            fabricVersion[1] = int.Parse(fvs[1].Trim());
            fabricVersion[2] = int.Parse(fvs[2].Trim());

            FAB_CONFIG_GEN_VERS = "v" + fabricVersion[0] + "." + fabricVersion[1];


            string fullpath = Environment.GetEnvironmentVariable(ORG_HYPERLEDGER_FABRIC_SDK_CONFIGURATION);
            if (string.IsNullOrEmpty(fullpath))
                fullpath = Path.Combine(Directory.GetCurrentDirectory(), DEFAULT_CONFIG);
            bool exists = File.Exists(fullpath);
            try
            {
                sdkProperties = new Properties();
                logger.Debug($"Loading configuration from {fullpath} and it is present: {exists}");
                sdkProperties.Load(fullpath);
            }
            catch (System.Exception)
            {
                logger.Warn($"Failed to load any configuration from: {fullpath}. Using toolkit defaults");
            }
            finally
            {
                // Default values

                DefaultProperty(INVOKEWAITTIME, "32000");
                DefaultProperty(DEPLOYWAITTIME, "120000");
                DefaultProperty(PROPOSALWAITTIME, "120000");
                DefaultProperty(RUNIDEMIXMTTEST, "false");
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
                runningTLS = sdkProperties.Contains(INTEGRATIONTESTSTLS);
                runningFabricCATLS = runningTLS;
                runningFabricTLS = runningTLS;

                foreach (string key in sdkProperties.Keys)
                {
                    string val = sdkProperties[key] + string.Empty;

                    if (key.StartsWith(INTEGRATIONTESTS_ORG))
                    {
                        Match match = orgPat.Match(key);

                        if (match.Success && match.Groups.Count >= 1)
                        {
                            string orgName = match.Groups[1].Value.Trim();
                            sampleOrgs[orgName] = new SampleOrg(orgName, val.Trim());
                        }
                    }
                }

                foreach (string orgName in sampleOrgs.Keys)
                {
                    SampleOrg sampleOrg = sampleOrgs[orgName];


                    string peerNames = sdkProperties.Get(INTEGRATIONTESTS_ORG + orgName + ".peer_locations");
                    string[] ps = new Regex("[ \t]*,[ \t]*").Split(peerNames);
                    foreach (string peer in ps)
                    {
                        string[] nl = new Regex("[ \t]*@[ \t]*").Split(peer);
                        sampleOrg.AddPeerLocation(nl[0], GrpcTLSify(nl[1]));
                    }

                    string domainName = sdkProperties.Get(INTEGRATIONTESTS_ORG + orgName + ".domname");

                    sampleOrg.DomainName = domainName;

                    string ordererNames = sdkProperties.Get(INTEGRATIONTESTS_ORG + orgName + ".orderer_locations");
                    ps = new Regex("[ \t]*,[ \t]*").Split(ordererNames);
                    foreach (string peer in ps)
                    {
                        string[] nl = new Regex("[ \t]*@[ \t]*").Split(peer);
                        sampleOrg.AddOrdererLocation(nl[0], GrpcTLSify(nl[1]));
                    }

                    if (IsFabricVersionBefore("1.3"))
                    {
                        // Eventhubs supported.

                        string eventHubNames = sdkProperties.Get(INTEGRATIONTESTS_ORG + orgName + ".eventhub_locations");
                        ps = new Regex("[ \t]*,[ \t]*").Split(eventHubNames);
                        foreach (string peer in ps)
                        {
                            string[] nl = new Regex("[ \t]*@[ \t]*").Split(peer);
                            sampleOrg.AddEventHubLocation(nl[0], GrpcTLSify(nl[1]));
                        }
                    }

                    sampleOrg.CALocation = HttpTLSify(sdkProperties.Get(INTEGRATIONTESTS_ORG + orgName + ".ca_location"));

                    sampleOrg.CAName = sdkProperties.Get(INTEGRATIONTESTS_ORG + orgName + ".caName");

                    if (runningFabricCATLS)
                    {
                        string cert = "fixture/sdkintegration/e2e-2Orgs/FAB_CONFIG_GEN_VERS/crypto-config/peerOrganizations/DNAME/ca/ca.DNAME-cert.pem".Replace("DNAME", domainName).Replace("FAB_CONFIG_GEN_VERS", FAB_CONFIG_GEN_VERS).Locate();
                        if (!File.Exists(cert))
                        {
                            throw new System.Exception($"TEST is missing cert file {cert}");
                        }

                        Properties properties = new Properties();
                        properties.Set("pemFile", cert);

                        properties.Set("allowAllHostNames", "true"); //testing environment only NOT FOR PRODUCTION!

                        sampleOrg.CAProperties = properties;
                    }
                }
            }
        }
        public string FabricConfigGenVers => FAB_CONFIG_GEN_VERS;

        public bool IsFabricVersionAtOrAfter(string version)
        {

            int[] vers = ParseVersion(version);
            for (int i = 0; i < 3; ++i)
            {
                if (vers[i] > fabricVersion[i])
                {
                    return false;
                }
            }
            return true;
        }
        public bool IsFabricVersionBefore(string version)
        {
            return !IsFabricVersionAtOrAfter(version);
        }

        private static int[] ParseVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                throw new AssertFailedException("Version is bad :" + version);
            string[] split = new Regex("[ \\t]*\\.[ \\t]*").Split(version);
            if (split.Length < 1 || split.Length > 3)
            {
                throw new AssertFailedException("Version is bad :" + version);
            }
            int[] ret = new int[3];
            int i = 0;
            for (; i < split.Length; ++i)
            {
                ret[i] = int.Parse(split[i].Trim());
            }
            for (; i < 3; ++i)
            {
                ret[i] = 0;
            }
            return ret;

        }
        public static TestConfig Instance => config ?? (config = new TestConfig());

        public bool IsRunningFabricTLS()
        {
            return runningFabricTLS;
        }

        private string GrpcTLSify(string location)
        {
            location = location.Trim();
            System.Exception e = Utils.CheckGrpcUrl(location);
            if (e != null)
            {
                throw new System.Exception($"Bad TEST parameters for grpc url {location}");
            }

            return runningFabricTLS ? Regex.Replace(location, "^grpc://", "grpcs://") : location;
        }

        private string HttpTLSify(string location)
        {
            location = location.Trim();

            return runningFabricCATLS ? Regex.Replace(location, "^http://", "https://") : location;
        }
        public void Destroy()
        {
            // config.sampleOrgs = null;
            config = null;

        }


        /**
         * getProperty return back property for the given value.
         *
         * @param property
         * @return string value for the property
         */
        private string GetProperty(string property)
        {
            string ret = sdkProperties[property];

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
                sdkProperties[key] = ret;
            }
            else
            {
                string envKey = key.ToUpperInvariant().Replace("\\.", "_");
                ret = Environment.GetEnvironmentVariable(envKey);
                if (null != ret)
                {
                    sdkProperties[key] = ret;
                }
                else
                {
                    if (!sdkProperties.Contains(key) && value != null)
                    {
                        sdkProperties[key] = value;
                    }
                }
            }
        }
        public bool GetRunIdemixMTTest()
        {
            return bool.Parse(GetProperty(RUNIDEMIXMTTEST));
        }

        public int GetTransactionWaitTime()
        {
            return int.Parse(GetProperty(INVOKEWAITTIME));
        }

        public int GetDeployWaitTime()
        {
            return int.Parse(GetProperty(DEPLOYWAITTIME));
        }

        public long GetProposalWaitTime()
        {
            return int.Parse(GetProperty(PROPOSALWAITTIME));
        }

        public IReadOnlyList<SampleOrg> GetIntegrationTestsSampleOrgs()
        {
            return sampleOrgs.Values.ToList();
        }

        public SampleOrg GetIntegrationTestsSampleOrg(string name)
        {
            return sampleOrgs.GetOrNull(name);
        }

        public Properties GetPeerProperties(string name)
        {
            return GetEndPointProperties("peer", name);
        }

        public Properties GetOrdererProperties(string name)
        {
            return GetEndPointProperties("orderer", name);
        }

        public Properties GetEndPointProperties(string type, string name)
        {
            Properties ret = new Properties();

            string domainName = GetDomainName(name);

            string cert = Path.Combine(GetTestChannelPath(), "crypto-config/ordererOrganizations".Replace("orderer", type), domainName, type + "s", name, "tls/server.crt");
            if (!File.Exists(cert))
            {
                throw new System.Exception($"Missing cert file for: {name}. Could not find at location: {cert}");
            }

            if (!IsRunningAgainstFabric10())
            {
                string clientCert;
                string clientKey;
                if ("orderer".Equals(type))
                {
                    clientCert = Path.Combine(GetTestChannelPath(), "crypto-config/ordererOrganizations/example.com/users/Admin@example.com/tls/client.crt");
                    clientKey = Path.Combine(GetTestChannelPath(), "crypto-config/ordererOrganizations/example.com/users/Admin@example.com/tls/client.key");
                }
                else
                {
                    clientCert = Path.Combine(GetTestChannelPath(), "crypto-config/peerOrganizations/", domainName, "users/User1@" + domainName, "tls/client.crt");
                    clientKey = Path.Combine(GetTestChannelPath(), "crypto-config/peerOrganizations/", domainName, "users/User1@" + domainName, "tls/client.key");
                }

                if (!File.Exists(clientCert))
                {
                    throw new System.Exception($"Missing  client cert file for: {name}. Could not find at location: {clientCert}");
                }

                if (!File.Exists(clientKey))
                {
                    throw new System.Exception($"Missing  client key file for: {name}. Could not find at location: {clientKey}");
                }

                ret.Set("clientCertFile", clientCert);
                ret.Set("clientKeyFile", clientKey);
            }

            ret.Set("pemFile", cert);
            ret.Set("hostnameOverride", name);
            ret.Set("sslProvider", "openSSL");
            ret.Set("negotiationType", "TLS");

            return ret;
        }

        public Properties GetEventHubProperties(string name)
        {
            return GetEndPointProperties("peer", name); //uses same as named peer
        }

        public string GetTestChannelPath()
        {
            return ("fixture/sdkintegration/e2e-2Orgs/" + FAB_CONFIG_GEN_VERS).Locate();
        }

        public bool IsRunningAgainstFabric10()
        {
            return IsFabricVersionBefore("1.1");
        }

        /**
         * url location of configtxlator
         *
         * @return
         */

        public string GetFabricConfigTxLaterLocation()
        {
            return "http://" + LOCALHOST + ":7059";
        }

        /**
         * Returns the appropriate Network Config YAML file based on whether TLS is currently
         * enabled or not
         *
         * @return The appropriate Network Config YAML file
         */
        public string GetTestNetworkConfigFileYAML()
        {
            string fname = runningTLS ? "network-config-tls.yaml" : "network-config.yaml";
            string pname = "fixture/sdkintegration/network_configs/";

            string ret = Path.Combine(pname.Locate(), fname);

            if (!"localhost".Equals(LOCALHOST) || IsFabricVersionAtOrAfter("1.3"))
            {
                // change on the fly ...
                string temp;


                //create a temp file
                string dir = Path.GetTempPath();
                Directory.CreateDirectory(dir);
                temp = Path.Combine(dir, fname + "-FixedUp.yaml");
                if (File.Exists(temp))
                {
                    //For testing start fresh
                    File.Delete(temp);
                }

                string sourceText = File.ReadAllText(ret, Encoding.UTF8);

                sourceText = sourceText.Replace("https://localhost", "https://" + LOCALHOST);
                sourceText = sourceText.Replace("http://localhost", "http://" + LOCALHOST);
                sourceText = sourceText.Replace("grpcs://localhost", "grpcs://" + LOCALHOST);
                sourceText = sourceText.Replace("grpc://localhost", "grpc://" + LOCALHOST);
                if (IsFabricVersionAtOrAfter("1.3"))
                {
                    //eventUrl: grpc://localhost:8053
                    sourceText = new Regex("(?m)^[ \\t]*eventUrl:").Replace(sourceText, "# eventUrl:");
                }



                File.WriteAllText(temp, sourceText);
                logger.Info($"produced new network-config.yaml file at: {temp}");


                ret = temp;
            }

            return TestUtils.RelocateFilePathsYAML(ret);
        }

        private string GetDomainName(string name)
        {
            int dot = name.IndexOf(".",StringComparison.InvariantCulture);
            if (-1 == dot)
            {
                return null;
            }

            return name.Substring(dot + 1);
        }
    }
}