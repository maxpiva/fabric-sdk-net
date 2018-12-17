using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Channels;
using Hyperledger.Fabric.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    [TestClass]
    [TestCategory("SDK_INTEGRATION")]
    public class End2endJavaIT : End2endIT
    {
        internal override string testName { get; } = "End2endJavaIT"; //Just print out what test is really running.
        // This is what changes are needed to deploy and run Node code.

        // this is relative to src/test/fixture and is where the Node chaincode source is.
        internal override string CHAIN_CODE_FILEPATH { get; } = "sdkintegration/javacc/sample1"; //override path to Node code
        internal override string CHAIN_CODE_PATH { get; } = null; //This is used only for GO.
        internal override string CHAIN_CODE_NAME { get; } = "example_cc_java"; // chaincode name.
        internal override TransactionRequest.Type CHAIN_CODE_LANG { get; } = TransactionRequest.Type.JAVA;
        
        internal override void BlockWalker(HFClient client, Channel channel)
        {
            // block walker depends on the state of the chain after go's end2end. Nothing here is language specific so
            // there is no loss in coverage for not doing this.
        }


        [TestMethod]
        public override void Setup()
        {
            sampleStore = new SampleStore(sampleStoreFile);
            EnrollUsersSetup(sampleStore);
            RunFabricTest(sampleStore); // just run fabric tests.
        }

        internal override Channel ConstructChannel(string name, HFClient client, SampleOrg sampleOrg)
        {
            // override this method since we don't want to construct the channel that's been done.
            // Just get it out of the samplestore!

            client.UserContext = sampleOrg.PeerAdmin;
            return sampleStore.GetChannel(client, name).Initialize();
        }
    }
}