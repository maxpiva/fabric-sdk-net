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
/*
package org.hyperledger.fabric.sdkintegration;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileNotFoundException;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.Reader;
import java.io.Serializable;
import java.io.StringReader;
import java.security.NoSuchAlgorithmException;
import java.security.NoSuchProviderException;
import java.security.PrivateKey;
import java.security.Security;
import java.security.spec.InvalidKeySpecException;
import java.util.HashMap;
import java.util.Map;
import java.util.Properties;

import org.apache.commons.io.IOUtils;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.bouncycastle.asn1.pkcs.PrivateKeyInfo;
import org.bouncycastle.jce.provider.BouncyCastleProvider;
import org.bouncycastle.openssl.PEMParser;
import org.bouncycastle.openssl.jcajce.JcaPEMKeyConverter;
import org.bouncycastle.util.encoders.Hex;
import org.hyperledger.fabric.sdk.Channel;
import org.hyperledger.fabric.sdk.Enrollment;
import org.hyperledger.fabric.sdk.HFClient;
import org.hyperledger.fabric.sdk.exception.InvalidArgumentException;
*/


using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    /**
 * A local file-based key value store.
 */
    public class SampleStore
    {
        private readonly string file;
        private readonly ILog logger = LogProvider.GetLogger(typeof(SampleStore));

        private readonly Dictionary<string, SampleUser> members = new Dictionary<string, SampleUser>();

        public SampleStore(FileInfo file)
        {
            this.file = file.FullName;
        }

        /**
         * Get the value associated with name.
         *
         * @param name
         * @return value associated with the name
         */
        public string GetValue(string name)
        {
            Properties properties = loadProperties();
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
            Properties properties = loadProperties();
            return properties.Contains(name);
        }

        private Properties loadProperties()
        {
            Properties properties = new Properties();
            try
            {
                properties.Load(file);
            }
            catch (FileNotFoundException e)
            {
                logger.Warn($"Could not find the file \"{file}\"");
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
            Properties properties = loadProperties();
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

            SampleUser sampleUser = members.GetOrNull(SampleUser.toKeyValStoreName(name, org));
            if (null != sampleUser)
            {
                return sampleUser;
            }

            // Create the SampleUser and try to restore it's state from the key value store (if found).
            sampleUser = new SampleUser(name, org, this);

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

            if (members.ContainsKey(SampleUser.toKeyValStoreName(name, org)))
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
        public SampleUser GetMember(string name, string org, string mspId, FileInfo privateKeyFile, FileInfo certificateFile)
        {
            try
            {
                // Try to get the SampleUser state from the cache
                SampleUser sampleUser = members.GetOrNull(SampleUser.toKeyValStoreName(name, org));
                if (null != sampleUser)
                {
                    return sampleUser;
                }

                // Create the SampleUser and try to restore it's state from the key value store (if found).
                sampleUser = new SampleUser(name, org, this);
                sampleUser.MspId = mspId;
                string certificate = File.ReadAllText(certificateFile.FullName, Encoding.UTF8);

                AsymmetricAlgorithm privateKey = GetPrivateKeyFromBytes(File.ReadAllBytes(privateKeyFile.FullName));

                sampleUser.Enrollment = new SampleStoreEnrollement(privateKey, certificate);

                sampleUser.SaveState();

                return sampleUser;
            }
            catch (IOException e)
            {
                logger.ErrorException(e.Message, e);
                throw e;
            }
            catch (System.Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw e;
            }
        }


        private static AsymmetricAlgorithm GetPrivateKeyFromBytes(byte[] data)
        {
            CryptoPrimitives prim = new CryptoPrimitives();
            return prim.BytesToPrivateKey(data);
        }


        private void SaveChannel(Channel channel)
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
            SetValue("clientPEMTLSKey." + sampleOrg.GetName(), key);
        }

        public string GetClientPEMTLSKey(SampleOrg sampleOrg)
        {
            return GetValue("clientPEMTLSKey." + sampleOrg.GetName());
        }

        public void StoreClientPEMTLCertificate(SampleOrg sampleOrg, string certificate)
        {
            SetValue("clientPEMTLSCertificate." + sampleOrg.GetName(), certificate);
        }

        public string getClientPEMTLSCertificate(SampleOrg sampleOrg)
        {
            return GetValue("clientPEMTLSCertificate." + sampleOrg.GetName());
        }

        [Serializable]
        private class SampleStoreEnrollement : IEnrollment
        {
            public SampleStoreEnrollement(AsymmetricAlgorithm privateKey, string certificate)
            {
                Key = privateKey;
                Cert = certificate;
            }

            public AsymmetricAlgorithm Key { get; }
            public string Cert { get; }
        }
    }
}