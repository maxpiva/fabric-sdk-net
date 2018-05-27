using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    [TestClass]
    [TestCategory("SDK_INTEGRATION_NODE")]
    /*
        This runs a version of end2end but with Node chaincode.
        It requires that End2endIT has been run already to do all enrollment and setting up of orgs,
        creation of the channels. None of that is specific to chaincode deployment language.
     */
    public class End2endNodeIT : End2endIT

    {
        internal override string testName { get; } = "End2endNodeIT"; //Just print out what test is really running.

        // This is what changes are needed to deploy and run Node code.

        // this is relative to src/test/fixture and is where the Node chaincode source is.
        internal override string CHAIN_CODE_FILEPATH { get; } = "sdkintegration/nodecc/sample1"; //override path to Node code
        internal override string CHAIN_CODE_PATH { get; } = null; //This is used only for GO.
        internal override string CHAIN_CODE_NAME { get; } = "example_cc_node"; // chaincode name.
        internal override TransactionRequest.Type CHAIN_CODE_LANG { get; } = TransactionRequest.Type.NODE; //language is Node.


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