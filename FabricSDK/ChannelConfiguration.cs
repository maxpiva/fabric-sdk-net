/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

using System.IO;

namespace Hyperledger.Fabric.SDK
{
    /**
     * A wrapper for the Hyperledger Channel configuration
     */
    public class ChannelConfiguration
    {

        /**
         * The null constructor for the ChannelConfiguration wrapper. You will
         * need to use the {@link #setChannelConfiguration(byte[])} method to
         * populate the channel configuration
         */
        public ChannelConfiguration()
        {
        }

        /**
         * constructs a ChannelConfiguration object with the actual configuration gotten from the file system
         *
         * @param configFile The file containing the channel configuration.
         * @throws IOException
         */
        public ChannelConfiguration(FileInfo configFile)
        {
            ChannelConfigurationBytes = File.ReadAllBytes(configFile.FullName);
        }

        /**
         * constructs a ChannelConfiguration object
         *
         * @param configAsBytes the byte array containing the serialized channel configuration
         */
        public ChannelConfiguration(byte[] configAsBytes)
        {
            ChannelConfigurationBytes = configAsBytes;
        }

        /**
         * sets the ChannelConfiguration from a byte array
         *
         * @param channelConfigurationAsBytes the byte array containing the serialized channel configuration
         */
        /**
         * @return the channel configuration serialized per protobuf and ready for inclusion into channel configuration
         */
        public byte[] ChannelConfigurationBytes { get; set; }
    }
}
