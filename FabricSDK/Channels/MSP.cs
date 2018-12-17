using Google.Protobuf;
using Hyperledger.Fabric.Protos.Msp.MspConfig;

namespace Hyperledger.Fabric.SDK.Channels
{
    /**
      * MSPs
      */
    public class MSP
    {
        private readonly FabricMSPConfig fabricMSPConfig;
        private byte[][] adminCerts;
        private byte[][] intermediateCerts;
        private byte[][] rootCerts;

        public MSP(string orgName, FabricMSPConfig fabricMSPConfig)
        {
            OrgName = orgName;
            this.fabricMSPConfig = fabricMSPConfig;
        }

        public string OrgName { get; }

        /**
         * Known as the MSPID internally
         *
         * @return
         */

        public string ID => fabricMSPConfig.Name;


        /**
         * AdminCerts
         *
         * @return array of admin certs in PEM bytes format.
         */
        public byte[][] AdminCerts
        {
            get
            {
                if (null == adminCerts)
                {
                    adminCerts = new byte[fabricMSPConfig.Admins.Count][];
                    int i = 0;
                    foreach (ByteString cert in fabricMSPConfig.Admins)
                    {
                        adminCerts[i++] = cert.ToByteArray();
                    }
                }

                return adminCerts;
            }
        }

        /**
         * RootCerts
         *
         * @return array of admin certs in PEM bytes format.
         */
        public byte[][] RootCerts
        {
            get
            {
                if (null == rootCerts)
                {
                    rootCerts = new byte[fabricMSPConfig.RootCerts.Count][];
                    int i = 0;
                    foreach (ByteString cert in fabricMSPConfig.RootCerts)
                    {
                        rootCerts[i++] = cert.ToByteArray();
                    }
                }

                return rootCerts;
            }
        }

        /**
         * IntermediateCerts
         *
         * @return array of intermediate certs in PEM bytes format.
         */
        public byte[][] IntermediateCerts
        {
            get
            {
                if (null == intermediateCerts)
                {
                    intermediateCerts = new byte[fabricMSPConfig.IntermediateCerts.Count][];
                    int i = 0;
                    foreach (ByteString cert in fabricMSPConfig.IntermediateCerts)
                    {
                        intermediateCerts[i++] = cert.ToByteArray();
                    }
                }

                return intermediateCerts;
            }
        }
    }
}