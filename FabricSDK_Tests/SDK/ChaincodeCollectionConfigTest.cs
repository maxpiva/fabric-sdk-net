using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.SDK.Configuration;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK
{
    [TestClass]
    [TestCategory("SDK")]
    public class ChaincodeCollectionConfigTest
    {



        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "configStream must be specified")]
        public void TestLoadFromConfigNullStream()
        {
            // Should not be able to instantiate a new instance of "Client" without a valid path to the configuration');

            ChaincodeCollectionConfiguration.FromJsonStream(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "configFile must be specified")]
        public void TestLoadFromConfigNullYamlFile()
        {
            // Should not be able to instantiate a new instance of "Client" without a valid path to the configuration');

            ChaincodeCollectionConfiguration.FromYamlFile(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "configFile must be specified")]
        public void TestLoadFromConfigNullJsonFile()
        {
            // Should not be able to instantiate a new instance of "Client" without a valid path to the configuration');
            ChaincodeCollectionConfiguration.FromJsonFile(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(FileNotFoundException), "FileDoesNotExist.yaml")]
        public void TestLoadFromConfigYamlFileNotExists()
        {
            // Should not be able to instantiate a new instance of "Client" without an actual configuration file

            ChaincodeCollectionConfiguration.FromYamlFile("FileDoesNotExist.yaml");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(FileNotFoundException), "FileDoesNotExist.json")]
        public void TestLoadFromConfigJsonFileNotExists()
        {
            // Should not be able to instantiate a new instance of "Client" without an actual configuration file

            ChaincodeCollectionConfiguration.FromJsonFile("FileDoesNotExist.json");
        }

        [TestMethod]
        public void TestLoadFromConfigFileYamlBasic()
        {
            ChaincodeCollectionConfiguration config = ChaincodeCollectionConfiguration.FromYamlFile("Fixture/collectionProperties/testCollection.yaml".Locate());
            Assert.IsNotNull(config);
            byte[] configAsBytes = config.GetAsBytes();
            Assert.IsNotNull(configAsBytes);
            Assert.AreEqual(configAsBytes.Length, 137);
            CollectionConfigPackage collectionConfigPackage = CollectionConfigPackage.Parser.ParseFrom(configAsBytes);
            Assert.AreEqual(collectionConfigPackage.Config.Count, 1);
            CollectionConfig colConfig = collectionConfigPackage.Config.FirstOrDefault();
            Assert.IsNotNull(colConfig);
            StaticCollectionConfig staticCollectionConfig = colConfig.StaticCollectionConfig;
            Assert.IsNotNull(staticCollectionConfig);
            Assert.AreEqual(staticCollectionConfig.BlockToLive, 3);
            Assert.AreEqual(staticCollectionConfig.Name, "rick");
            Assert.AreEqual(staticCollectionConfig.MaximumPeerCount, 9);
            Assert.AreEqual(staticCollectionConfig.RequiredPeerCount, 7);
            CollectionPolicyConfig memberOrgsPolicy = staticCollectionConfig.MemberOrgsPolicy;
            Assert.IsNotNull(memberOrgsPolicy);
            SignaturePolicyEnvelope signaturePolicy = memberOrgsPolicy.SignaturePolicy;
            Assert.IsNotNull(signaturePolicy);
            Assert.AreEqual(signaturePolicy.Version, 0);
            SignaturePolicy rule = signaturePolicy.Rule;
            Assert.IsNotNull(rule);
            Assert.AreEqual(rule.TypeCase, SignaturePolicy.TypeOneofCase.NOutOf);
            SignaturePolicy.Types.NOutOf nOutOf = rule.NOutOf;
            Assert.IsNotNull(nOutOf);
            Assert.AreEqual(2, nOutOf.N);
            List<MSPPrincipal> identitiesList = signaturePolicy.Identities?.ToList();
            Assert.IsNotNull(identitiesList);
            Assert.AreEqual(3, identitiesList.Count);
        }

        [TestMethod]
        public void testLoadFromConfigFileJsonBasic()
        {
            ChaincodeCollectionConfiguration configYAML = ChaincodeCollectionConfiguration.FromYamlFile("fixture/collectionProperties/testCollection.yaml".Locate());
            Assert.IsNotNull(configYAML);
            byte[] configAsBytesYAML = configYAML.GetAsBytes();
            Assert.IsNotNull(configAsBytesYAML);
            Assert.AreEqual(configAsBytesYAML.Length, 137);
            ChaincodeCollectionConfiguration configJson = ChaincodeCollectionConfiguration.FromJsonFile("fixture/collectionProperties/testCollection.json".Locate());
            Assert.IsNotNull(configJson);
            byte[] configAsBytesJson = configYAML.GetAsBytes();
            Assert.IsNotNull(configAsBytesJson);
            Assert.AreEqual(configAsBytesJson.Length, 137);
            CollectionAssert.AreEqual(configAsBytesYAML, configAsBytesJson);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "collectionConfigPackage must be specified")]
        public void FromCollectionConfigPackageNULL()
        {
            ChaincodeCollectionConfiguration.FromCollectionConfigPackage(null);
        }

        [TestMethod]
        public void fromCollectionConfigPackageAndStream()
        {
            string yaml = "---\n" + "  - StaticCollectionConfig: \n" + "       name: rick \n" + "       blockToLive: 9999 \n" + "       maximumPeerCount: 0\n" + "       requiredPeerCount: 0\n" + "       SignaturePolicyEnvelope:\n" + "         identities:\n" + "             - user1: {\"role\": {\"name\": \"member\", \"mspId\": \"Org1MSP\"}}\n" + "         policy:\n" + "             1-of:\n" + "               - signed-by: \"user1\"";

            ChaincodeCollectionConfiguration chaincodeCollectionConfiguration = ChaincodeCollectionConfiguration.FromYamlStream(new MemoryStream(yaml.ToBytes()));
            ChaincodeCollectionConfiguration chaincodeCollectionConfigurationFromProto = ChaincodeCollectionConfiguration.FromCollectionConfigPackage(CollectionConfigPackage.Parser.ParseFrom(chaincodeCollectionConfiguration.GetAsBytes()));
            CollectionAssert.AreEqual(chaincodeCollectionConfiguration.GetAsBytes(), chaincodeCollectionConfigurationFromProto.GetAsBytes());
        }
    }
}