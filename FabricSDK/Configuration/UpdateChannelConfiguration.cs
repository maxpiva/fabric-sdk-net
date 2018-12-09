/*
 *
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */

using System.IO;

namespace Hyperledger.Fabric.SDK.Configuration
{
    /**
     * A wrapper for the Hyperledger Channel update configuration
     */
    public class UpdateChannelConfiguration
    {
        /**
         * The  constructor for the UpdateChannelConfiguration wrapper. You will
         * need to use the {@link Channel#updateChannelConfiguration(UpdateChannelConfiguration, byte[]...)} method to
         * populate the update channel configuration
         */
        public UpdateChannelConfiguration()
        {
        }

        /**
         * constructs a UpdateChannelConfiguration object with the actual configuration gotten from the file system
         *
         * @param configFile The file containing the channel configuration.
         * @throws IOException
         */
        public UpdateChannelConfiguration(string configFile)
        {
            UpdateChannelConfigurationBytes = File.ReadAllBytes(configFile);
        }

        /**
         * constructs a UpdateChannelConfiguration object
         *
         * @param configAsBytes the byte array containing the serialized channel configuration
         */
        public UpdateChannelConfiguration(byte[] configAsBytes)
        {
            UpdateChannelConfigurationBytes = configAsBytes;
        }

        /**
         * sets the UpdateChannelConfiguration from a byte array
         *
         * @param updateChannelConfigurationAsBytes the byte array containing the serialized channel configuration
         */
        /**
            * @return the channel configuration serialized per protobuf and ready for inclusion into channel configuration
        */
        public byte[] UpdateChannelConfigurationBytes { get; set; }
    }
}