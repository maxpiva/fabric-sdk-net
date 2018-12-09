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

using System;
using System.IO;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;

namespace Hyperledger.Fabric.SDK.Configuration
{
    /**
     * A wrapper for the Hyperledger Channel configuration
     */
    public class ChannelConfiguration
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(ChannelConfiguration));
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

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
        public ChannelConfiguration(string configFile)
        {
            if (string.IsNullOrEmpty(configFile))
            {
                throw new ArgumentException("ChannelConfiguration configFile must be non-null");
            }

            logger.Trace($"Creating ChannelConfiguration from file {Path.GetFullPath(configFile)}");
            try
            {
                _channelConfigurationBytes = File.ReadAllBytes(configFile);
            }
            catch (Exception e)
            {
                logger.Error($"Error Creating ChannelConfiguration from file {Path.GetFullPath(configFile)}",e);
                throw;
            }
        }

        /**
         * constructs a ChannelConfiguration object
         *
         * @param configAsBytes the byte array containing the serialized channel configuration
         */
        public ChannelConfiguration(byte[] configAsBytes)
        {
            if (configAsBytes == null)
            {
                throw new ArgumentException("ChannelConfiguration configAsBytes must be non-null");
            }
            logger.Trace("Creating ChannelConfiguration from bytes");
            _channelConfigurationBytes = configAsBytes;
        }

        /**
         * sets the ChannelConfiguration from a byte array
         *
         * @param channelConfigurationAsBytes the byte array containing the serialized channel configuration
         */
        /**
         * @return the channel configuration serialized per protobuf and ready for inclusion into channel configuration
         */
        private byte[] _channelConfigurationBytes;

        public byte[] ChannelConfigurationBytes
        {
            get
            {
                if (_channelConfigurationBytes == null)
                    logger.Error("ChannelConfiguration configBytes is null!");
                else if (IS_TRACE_LEVEL)
                    logger.Trace($"getChannelConfigurationAsBytes: %s", _channelConfigurationBytes.ToHexString());
                return _channelConfigurationBytes;
            }
            set
            {
                if (_channelConfigurationBytes == null)
                {
                    throw new ArgumentException("ChannelConfiguration channelConfigurationAsBytes must be non-null");
                }
                logger.Trace("Creating setChannelConfiguration from bytes");
            }
        }
    }
}
