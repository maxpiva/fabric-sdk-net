/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Channels;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Logging;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    /**
 * A local file-based key value store.
 */
    public class SampleStore
    {
        private readonly string file;
        private readonly ILog logger = LogProvider.GetLogger(typeof(SampleStore));
        private ICryptoSuite cryptoSuite;
        private readonly Dictionary<string, SampleUser> members = new Dictionary<string, SampleUser>();

        public SampleStore(string file)
        {
            this.file = file;
        }

        /**
         * Get the value associated with name.
         *
         * @param name
         * @return value associated with the name
         */
        public string GetValue(string name)
        {
            Properties properties = LoadProperties();
            return properties[name];
        }

        /**
         * Has the value present.
         *
         * @param name
         * @return true if it's present.
         */
        public bool HasValue(string name)
        {
            Properties properties = LoadProperties();
            return properties.Contains(name);
        }

        private Properties LoadProperties()
        {
            Properties properties = new Properties();
            try
            {
                properties.Load(file);
            }
            catch (FileNotFoundException)
            {
                logger.Info($"Could not find the file \"{file}\"");
            }
            catch (IOException e)
            {
                logger.Warn($"Could not load keyvalue store from file \"{file}\", reason:{e.Message}");
            }

            return properties;
        }

        /**
         * Set the value associated with name.
         *
         * @param name  The name of the parameter
         * @param value Value for the parameter
         */
        public void SetValue(string name, string value)
        {
            Properties properties = LoadProperties();
            try
            {
                properties.Set(name, value);
                properties.Save();
            }
            catch (IOException e)
            {
                logger.Warn($"Could not save the keyvalue store, reason:{e.Message}%s");
            }
        }

        /**
         * Get the user with a given name
         *
         * @param name
         * @param org
         * @return user
         */
        public SampleUser GetMember(string name, string org)
        {
            // Try to get the SampleUser state from the cache

            SampleUser sampleUser = members.GetOrNull(SampleUser.ToKeyValStoreName(name, org));
            if (null != sampleUser)
            {
                return sampleUser;
            }

            // Create the SampleUser and try to restore it's state from the key value store (if found).
            sampleUser = new SampleUser(name, org, this,cryptoSuite);

            return sampleUser;
        }

        /**
         * Check if store has user.
         *
         * @param name
         * @param org
         * @return true if the user exists.
         */
        public bool HasMember(string name, string org)
        {
            // Try to get the SampleUser state from the cache

            if (members.ContainsKey(SampleUser.ToKeyValStoreName(name, org)))
            {
                return true;
            }

            return SampleUser.IsStored(name, org, this);
        }

        /**
         * Get the user with a given name
         *
         * @param name
         * @param org
         * @param mspId
         * @param privateKeyFile
         * @param certificateFile
         * @return user
         * @throws IOException
         * @throws NoSuchAlgorithmException
         * @throws NoSuchProviderException
         * @throws InvalidKeySpecException
         */
        public SampleUser GetMember(string name, string org, string mspId, string privateKeyFile, string certificateFile)
        {
            try
            {
                // Try to get the SampleUser state from the cache
                SampleUser sampleUser = members.GetOrNull(SampleUser.ToKeyValStoreName(name, org));
                if (null != sampleUser)
                {
                    return sampleUser;
                }

                // Create the SampleUser and try to restore it's state from the key value store (if found).
                sampleUser = new SampleUser(name, org, this,cryptoSuite);
                sampleUser.MspId = mspId;
                string certificate = File.ReadAllText(certificateFile, Encoding.UTF8);

                KeyPair pair = KeyPair.Create(File.ReadAllText(privateKeyFile));

                sampleUser.Enrollment = new SampleStoreEnrollement(pair.Pem, certificate);

                sampleUser.SaveState();

                return sampleUser;
            }
            catch (IOException e)
            {
                logger.ErrorException(e.Message, e);
                throw;
            }
            catch (System.Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw;
            }
        }




        public void SaveChannel(Channel channel)
        {
            SetValue("channel." + channel.Name, channel.Serialize().ToBytes().ToHexString());
        }

        public Channel GetChannel(HFClient client, string name)
        {
            Channel ret = null;

            string channelHex = GetValue("channel." + name);
            if (channelHex != null)
            {
                ret = client.DeSerializeChannel(channelHex.FromHexString().ToUTF8String());
            }

            return ret;
        }

        public void StoreClientPEMTLSKey(SampleOrg sampleOrg, string key)
        {
            SetValue("clientPEMTLSKey." + sampleOrg.Name, key);
        }

        public string GetClientPEMTLSKey(SampleOrg sampleOrg)
        {
            return GetValue("clientPEMTLSKey." + sampleOrg.Name);
        }

        public void StoreClientPEMTLCertificate(SampleOrg sampleOrg, string certificate)
        {
            SetValue("clientPEMTLSCertificate." + sampleOrg.Name, certificate);
        }

        public string GetClientPEMTLSCertificate(SampleOrg sampleOrg)
        {
            return GetValue("clientPEMTLSCertificate." + sampleOrg.Name);
        }
        // Use this to make sure SDK is not dependent on HFCA enrollment for non-Idemix
        [Serializable]
        public class SampleStoreEnrollement : IEnrollment
        {
            public SampleStoreEnrollement(string key, string certificate)
            {
                Key = key;
                Cert = certificate;
            }

            public SampleStoreEnrollement()
            {
            }

            public string Key { get; set; }
            public string Cert { get; set; }
        }
    }
}