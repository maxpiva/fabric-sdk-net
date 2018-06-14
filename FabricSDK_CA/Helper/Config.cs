/*
 *  Copyright 2016, 2017 IBM, DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

/*
package org.hyperledger.fabric_ca.sdk.helper;

import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.util.Properties;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.apache.log4j.Level;
*/
/**
 * Config allows for a global config of the toolkit. Central location for all
 * toolkit configuration defaults. Has a local config file that can override any
 * property defaults. Config file can be relocated via a system property
 * "org.hyperledger.fabric.sdk.configuration". Any property can be overridden
 * with environment variable and then overridden
 * with a java system property. Property hierarchy goes System property
 * overrides environment variable which overrides config file for default values specified here.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Hyperledger.Fabric.SDK.Helper;

using Hyperledger.Fabric_CA.SDK.Logging;



[assembly: InternalsVisibleTo("Hyperledger.Fabric.Tests")]
namespace Hyperledger.Fabric_CA.SDK.Helper
{
    public class Config
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Config));

        private static readonly string DEFAULT_CONFIG = "config.properties";
        public static readonly string ORG_HYPERLEDGER_FABRIC_SDK_CONFIGURATION = "org.hyperledger.fabric.sdk.configuration";
        public static readonly string SECURITY_LEVEL = "org.hyperledger.fabric.sdk.security_level";
        public static readonly string HASH_ALGORITHM = "org.hyperledger.fabric.sdk.hash_algorithm";
        public static readonly string CACERTS = "org.hyperledger.fabric.sdk.cacerts";
        public static readonly string PROPOSAL_WAIT_TIME = "org.hyperledger.fabric.sdk.proposal.wait.time";

        public static readonly string ASYMMETRIC_KEY_TYPE = "org.hyperledger.fabric.sdk.crypto.asymmetric_key_type";
        public static readonly string KEY_AGREEMENT_ALGORITHM = "org.hyperledger.fabric.sdk.crypto.key_agreement_algorithm";
        public static readonly string SYMMETRIC_KEY_TYPE = "org.hyperledger.fabric.sdk.crypto.symmetric_key_type";
        public static readonly string SYMMETRIC_KEY_BYTE_COUNT = "org.hyperledger.fabric.sdk.crypto.symmetric_key_byte_count";
        public static readonly string SYMMETRIC_ALGORITHM = "org.hyperledger.fabric.sdk.crypto.symmetric_algorithm";
        public static readonly string MAC_KEY_BYTE_COUNT = "org.hyperledger.fabric.sdk.crypto.mac_key_byte_count";
        public static readonly string CERTIFICATE_FORMAT = "org.hyperledger.fabric.sdk.crypto.certificate_format";
        public static readonly string SIGNATURE_ALGORITHM = "org.hyperledger.fabric.sdk.crypto.default_signature_algorithm";
        public static readonly string MAX_LOG_STRING_LENGTH = "org.hyperledger.fabric.sdk.log.stringlengthmax";
        public static readonly string LOGGERLEVEL = "org.hyperledger.fabric_ca.sdk.loglevel"; // ORG_HYPERLEDGER_FABRIC_CA_SDK_LOGLEVEL=TRACE,DEBUG

        internal static Config config;

        /**
         * getConfig return back singleton for SDK configuration.
         *
         * @return Global configuration
         */
        public static Config Instance => config ?? (config = new Config());
        internal static Properties sdkProperties = new Properties();

        private Config()
        {
            string fullpath = Environment.GetEnvironmentVariable(ORG_HYPERLEDGER_FABRIC_SDK_CONFIGURATION);
            if (string.IsNullOrEmpty(fullpath))
                fullpath = Path.Combine(Directory.GetCurrentDirectory(), DEFAULT_CONFIG);
            bool exists = File.Exists(fullpath);
            try
            {
                sdkProperties = new Properties();
                logger.Debug($"Loading configuration from {fullpath} and it is present: {exists}");
                sdkProperties.Load(fullpath);
            }
            catch (Exception)
            {
                logger.Warn($"Failed to load any configuration from: {fullpath}. Using toolkit defaults");
            }
            finally
            {

                // Default values
                DefaultProperty(ASYMMETRIC_KEY_TYPE, "EC");
                DefaultProperty(KEY_AGREEMENT_ALGORITHM, "ECDH");
                DefaultProperty(SYMMETRIC_KEY_TYPE, "AES");
                DefaultProperty(SYMMETRIC_KEY_BYTE_COUNT, "32");
                DefaultProperty(SYMMETRIC_ALGORITHM, "AES/CFB/NoPadding");
                DefaultProperty(MAC_KEY_BYTE_COUNT, "32");
                DefaultProperty(CERTIFICATE_FORMAT, "X.509");
                DefaultProperty(SIGNATURE_ALGORITHM, "SHA256withECDSA");
                DefaultProperty(SECURITY_LEVEL, "256");
                DefaultProperty(HASH_ALGORITHM, "SHA2");
                // TODO remove this once we have implemented MSP and get the peer certs from the channel
                DefaultProperty(CACERTS, "/genesisblock/peercacert.pem");

                DefaultProperty(PROPOSAL_WAIT_TIME, "12000");

                DefaultProperty(MAX_LOG_STRING_LENGTH, "64");

                DefaultProperty(LOGGERLEVEL, null);
                /*
                string inLogLevel = sdkProperties.GetOrNull(LOGGERLEVEL);
    
                if (null != inLogLevel) {
    
                    org.apache.log4j.Level setTo = null;
    
                    switch (inLogLevel) {
    
                        case "TRACE":
                            setTo = org.apache.log4j.Level.TRACE;
                            break;
    
                        case "DEBUG":
                            setTo = org.apache.log4j.Level.DEBUG;
                            break;
    
                        case "INFO":
                            setTo = Level.INFO;
                            break;
    
                        case "WARN":
                            setTo = Level.WARN;
                            break;
    
                        case "ERROR":
                            setTo = Level.ERROR;
                            break;
    
                        default:
                            setTo = Level.INFO;
                            break;
    
                    }
    
                    if (null != setTo) {
                        org.apache.log4j.Logger.getLogger("org.hyperledger.fabric_ca").setLevel(setTo);
                    }
                    */
            }

        }





        /**
         * getProperty return back property for the given value.
         *
         * @param property
         * @return String value for the property
         */
        private string GetProperty(string property)
        {

            string ret = sdkProperties.Get(property);

            if (null == ret)
            {
                logger.Warn($"No configuration value found for '{property}'");
            }

            return ret;
        }

        private static void DefaultProperty(string key, string value)
        {
            string envvalue = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(envvalue))
            {
                value = envvalue;
            }

            if (!sdkProperties.Contains(key))
                sdkProperties.Set(key, value);
        }

        /**
         * Get the configured security level. The value determines the elliptic curve used to generate keys.
         *
         * @return the security level.
         */
        public int GetSecurityLevel()
        {

            if (int.TryParse(GetProperty(SECURITY_LEVEL), out int sec))
                return sec;
            return 0;

        }

        /**
         * Get the name of the configured hash algorithm, used for digital signatures.
         *
         * @return the hash algorithm name.
         */
        public string GetHashAlgorithm()
        {
            return GetProperty(HASH_ALGORITHM);

        }

        public string[] GetPeerCACerts()
        {
            return GetProperty(CACERTS).Split(new char[] {'\''}, StringSplitOptions.RemoveEmptyEntries);
        }

        /**
         * Returns the timeout for a single proposal request to endorser in milliseconds.
         *
         * @return the timeout for a single proposal request to endorser in milliseconds
         */
        public long GetProposalWaitTime()
        {
            if (long.TryParse(GetProperty(PROPOSAL_WAIT_TIME), out long p))
                return p;
            return 0;
        }

        public string GetAsymmetricKeyType()
        {
            return GetProperty(ASYMMETRIC_KEY_TYPE);
        }

        public string GetKeyAgreementAlgorithm()
        {
            return GetProperty(KEY_AGREEMENT_ALGORITHM);
        }

        public string GetSymmetricKeyType()
        {
            return GetProperty(SYMMETRIC_KEY_TYPE);
        }

        public int GetSymmetricKeyByteCount()
        {
            if (int.TryParse(GetProperty(SYMMETRIC_KEY_BYTE_COUNT), out int p))
                return p;
            return 0;
        }

        public string GetSymmetricAlgorithm()
        {
            return GetProperty(SYMMETRIC_ALGORITHM);
        }

        public int GetMACKeyByteCount()
        {
            if (int.TryParse(GetProperty(MAC_KEY_BYTE_COUNT), out int p))
                return p;
            return 0;
        }

        public string GetCertificateFormat()
        {
            return GetProperty(CERTIFICATE_FORMAT);
        }

        public string GetSignatureAlgorithm()
        {
            return GetProperty(SIGNATURE_ALGORITHM);
        }

        public int GetMaxLogStringLength()
        {
            if (int.TryParse(GetProperty(MAX_LOG_STRING_LENGTH), out int p))
                return p;
            return 0;
        }

    }
}
