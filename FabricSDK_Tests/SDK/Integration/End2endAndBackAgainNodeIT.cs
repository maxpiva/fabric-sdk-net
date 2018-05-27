using Hyperledger.Fabric.SDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    /**
     * Test end to end scenario
     */
    [TestClass]
    [TestCategory("SDK_INTEGRATION_NODE")]
    //    This runs a version of end2endAndBackAgainIT but with Node chaincode.s
    public class End2endAndBackAgainNodeIT : End2endAndBackAgainIT
    {


        // This is what changes are needed to deploy and run Node code.
        internal override string testName { get; } = "End2endAndBackAgainNodeIT";

        // this is relative to src/test/fixture and is where the Node chaincode source is.
        internal override string CHAIN_CODE_FILEPATH { get; } = "sdkintegration/nodecc/sample_11"; //override path to Node code
        internal override string CHAIN_CODE_PATH { get; } = null; //This is used only for GO.
        internal override string CHAIN_CODE_NAME { get; } = "example_cc_node"; // chaincode name.
        internal override TransactionRequest.Type CHAIN_CODE_LANG { get; } = TransactionRequest.Type.NODE; //language is Node.
        internal override ChaincodeID chaincodeID => new ChaincodeID().SetName(CHAIN_CODE_NAME).SetVersion(CHAIN_CODE_VERSION);
        internal override ChaincodeID chaincodeID_11 => new ChaincodeID().SetName(CHAIN_CODE_NAME).SetVersion(CHAIN_CODE_VERSION_11);


        [TestInitialize]
        public override void Setup()
        {

            sampleStore = new SampleStore(sampleStoreFile);

            SetupUsers(sampleStore);
            RunFabricTest(sampleStore);
        }
    }
}
