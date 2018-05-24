/*
 *  Copyright 2016, 2017 IBM, DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *  http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */
/*
package org.hyperledger.fabric.sdk.helper;

import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.util.Collections;
import java.util.HashMap;
import java.util.Map;
import java.util.Properties;

import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.apache.log4j.Level;
import org.bouncycastle.jce.provider.BouncyCastleProvider;

import static java.lang.String.format;
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
using System.Linq;
using System.Text.RegularExpressions;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.NetExtensions;
using IniParser;
using IniParser.Model;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class Config
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(Config));
        
        private static readonly string DEFAULT_CONFIG = "config.properties";
        public static readonly string ORG_HYPERLEDGER_FABRIC_SDK_CONFIGURATION = "org.hyperledger.fabric.sdk.configuration";

        /**
         * Timeout settings
         **/
        public static readonly string PROPOSAL_WAIT_TIME = "org.hyperledger.fabric.sdk.proposal.wait.time";
        public static readonly string CHANNEL_CONFIG_WAIT_TIME = "org.hyperledger.fabric.sdk.channelconfig.wait_time";
        public static readonly string TRANSACTION_CLEANUP_UP_TIMEOUT_WAIT_TIME = "org.hyperledger.fabric.sdk.client.transaction_cleanup_up_timeout_wait_time";
        public static readonly string ORDERER_RETRY_WAIT_TIME = "org.hyperledger.fabric.sdk.orderer_retry.wait_time";
        public static readonly string ORDERER_WAIT_TIME = "org.hyperledger.fabric.sdk.orderer.ordererWaitTimeMilliSecs";
        public static readonly string PEER_EVENT_REGISTRATION_WAIT_TIME = "org.hyperledger.fabric.sdk.peer.eventRegistration.wait_time";
        public static readonly string PEER_EVENT_RETRY_WAIT_TIME = "org.hyperledger.fabric.sdk.peer.retry_wait_time";
        public static readonly string EVENTHUB_CONNECTION_WAIT_TIME = "org.hyperledger.fabric.sdk.eventhub_connection.wait_time";
        public static readonly string EVENTHUB_RECONNECTION_WARNING_RATE = "org.hyperledger.fabric.sdk.eventhub.reconnection_warning_rate";
        public static readonly string PEER_EVENT_RECONNECTION_WARNING_RATE = "org.hyperledger.fabric.sdk.peer.reconnection_warning_rate";
        public static readonly string GENESISBLOCK_WAIT_TIME = "org.hyperledger.fabric.sdk.channel.genesisblock_wait_time";

        /**
         * Crypto configuration settings -- settings should not be changed.
         **/
        public static readonly string DEFAULT_CRYPTO_SUITE_FACTORY = "org.hyperledger.fabric.sdk.crypto.default_crypto_suite_factory";
        public static readonly string SECURITY_LEVEL = "org.hyperledger.fabric.sdk.security_level";
        public static readonly string SECURITY_PROVIDER_CLASS_NAME = "org.hyperledger.fabric.sdk.security_provider_class_name";
        public static readonly string SECURITY_CURVE_MAPPING = "org.hyperledger.fabric.sdk.security_curve_mapping";
        public static readonly string HASH_ALGORITHM = "org.hyperledger.fabric.sdk.hash_algorithm";
        public static readonly string ASYMMETRIC_KEY_TYPE = "org.hyperledger.fabric.sdk.crypto.asymmetric_key_type";
        public static readonly string CERTIFICATE_FORMAT = "org.hyperledger.fabric.sdk.crypto.certificate_format";
        public static readonly string SIGNATURE_ALGORITHM = "org.hyperledger.fabric.sdk.crypto.default_signature_algorithm";

        /**
         * Logging settings
         **/
        public static readonly string MAX_LOG_STRING_LENGTH = "org.hyperledger.fabric.sdk.log.stringlengthmax";
        public static readonly string EXTRALOGLEVEL = "org.hyperledger.fabric.sdk.log.extraloglevel"; // ORG_HYPERLEDGER_FABRIC_SDK_LOG_EXTRALOGLEVEL
        public static readonly string LOGGERLEVEL = "org.hyperledger.fabric.sdk.loglevel"; // ORG_HYPERLEDGER_FABRIC_SDK_LOGLEVEL=TRACE,DEBUG
        public static readonly string DIAGNOTISTIC_FILE_DIRECTORY = "org.hyperledger.fabric.sdk.diagnosticFileDir"; //ORG_HYPERLEDGER_FABRIC_SDK_DIAGNOSTICFILEDIR

        /**
         * Connections settings
         */

        public static readonly string CONN_SSL_PROVIDER = "org.hyperledger.fabric.sdk.connections.ssl.sslProvider";
        public static readonly string CONN_SSL_NEGTYPE = "org.hyperledger.fabric.sdk.connections.ssl.negotiationType";


        /**
         * Miscellaneous settings
         **/
        public static readonly string PROPOSAL_CONSISTENCY_VALIDATION = "org.hyperledger.fabric.sdk.proposal.consistency_validation";

        private static Config config;
        private static Dictionary<string,string> sdkProperties = new Dictionary<string, string>();
        private static void DefaultProperty(string key, string value)
        {
            string envvalue = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(envvalue))
            {
                value = envvalue;
            }
            if (!sdkProperties.ContainsKey(key))
                sdkProperties.Add(key, value);
        }
        private Config()
        {

            string fullpath = Path.Combine(Directory.GetCurrentDirectory(), DEFAULT_CONFIG);
            bool exists = File.Exists(fullpath);
            try
            {
                var parser = new FileIniDataParser();
                sdkProperties=new Dictionary<string, string>();
                logger.Debug($"Loading configuration from {fullpath} and it is present: {exists}");
                IniData data = parser.ReadFile(DEFAULT_CONFIG);
                KeyDataCollection sect = data.Global;
                foreach (KeyData kd in sect)
                {
                    sdkProperties.Add(kd.KeyName, kd.Value);
                }

            }
            catch (Exception e)
            {
                logger.Warn($"Failed to load any configuration from: {fullpath}. Using toolkit defaults");
            }
            finally
            {

                // Default values
                /**
                 * Timeout settings
                 **/
                DefaultProperty(PROPOSAL_WAIT_TIME, "20000");
                DefaultProperty(CHANNEL_CONFIG_WAIT_TIME, "15000");
                DefaultProperty(ORDERER_RETRY_WAIT_TIME, "200");
                DefaultProperty(ORDERER_WAIT_TIME, "10000");
                DefaultProperty(PEER_EVENT_REGISTRATION_WAIT_TIME, "5000");
                DefaultProperty(PEER_EVENT_RETRY_WAIT_TIME, "500");
                DefaultProperty(EVENTHUB_CONNECTION_WAIT_TIME, "5000");
                DefaultProperty(GENESISBLOCK_WAIT_TIME, "5000");
                /**
                 * This will NOT complete any transaction futures time out and must be kept WELL above any expected future timeout
                 * for transactions sent to the Orderer. For internal cleanup only.
                 */

                DefaultProperty(TRANSACTION_CLEANUP_UP_TIMEOUT_WAIT_TIME, "600000"); //10 min.

                /**
                 * Crypto configuration settings
                 **/
                DefaultProperty(DEFAULT_CRYPTO_SUITE_FACTORY, "org.hyperledger.fabric.sdk.security.HLSDKJCryptoSuiteFactory");
                DefaultProperty(SECURITY_LEVEL, "256");
                DefaultProperty(SECURITY_CURVE_MAPPING, "256=secp256r1:384=secp384r1");
                DefaultProperty(HASH_ALGORITHM, "SHA2");
                DefaultProperty(ASYMMETRIC_KEY_TYPE, "EC");

                DefaultProperty(CERTIFICATE_FORMAT, "X.509");
                DefaultProperty(SIGNATURE_ALGORITHM, "SHA256withECDSA");

                /**
                 * Connection defaults
                 */

                DefaultProperty(CONN_SSL_PROVIDER, "openSSL");
                DefaultProperty(CONN_SSL_NEGTYPE, "TLS");

                /**
                 * Logging settings
                 **/
                DefaultProperty(MAX_LOG_STRING_LENGTH, "64");
                DefaultProperty(EXTRALOGLEVEL, "0");
                DefaultProperty(LOGGERLEVEL, null);
                DefaultProperty(DIAGNOTISTIC_FILE_DIRECTORY, null);
                /**
                 * Miscellaneous settings
                 */
                DefaultProperty(PROPOSAL_CONSISTENCY_VALIDATION, "true");
                DefaultProperty(EVENTHUB_RECONNECTION_WARNING_RATE, "50");
                DefaultProperty(PEER_EVENT_RECONNECTION_WARNING_RATE, "50");
                //LOGGERLEVEL DO NOT WORK WITH Abstract LibLog
                string inLogLevel = sdkProperties.GetOrNull(LOGGERLEVEL);
                /*
                if (inLogLevel!=null)
                {
                    LogLevel setTo=LogLevel.Fatal;
                    switch (inLogLevel.ToUpperInvariant())
                    {

                        case "TRACE":
                            setTo = LogLevel.Trace;
                            break;

                        case "DEBUG":
                            setTo = LogLevel.Debug;
                            break;

                        case "INFO":
                            setTo = LogLevel.Info;
                            break;

                        case "WARN":
                            setTo = LogLevel.Warn;
                            break;

                        case "ERROR":
                            setTo = LogLevel.Error;
                            break;

                        default:
                            setTo = LogLevel.Info;
                            break;

                    }

                    if (setTo!=LogLevel.Fatal)
                    {

                    }

                }
                */
            }

        }

        /**
         * getConfig return back singleton for SDK configuration.
         *
         * @return Global configuration
         */
        public static Config GetConfig()
        {
            if (null == config)
            {
                config = new Config();
            }

            return config;

        }

        /**
         * getProperty return back property for the given value.
         *
         * @param property
         * @return String value for the property
         */
        private string GetProperty(string property)
        {

            string ret = sdkProperties.GetOrNull(property);

            if (null == ret)
            {
                logger.Warn($"No configuration value found for '{property}'");
            }

            return ret;
        }



        /**
         * Get the configured security level. The value determines the elliptic curve used to generate keys.
         *
         * @return the security level.
         */
        public int GetSecurityLevel()
        {
            if (int.TryParse(GetProperty(SECURITY_LEVEL),out int sec))
                return sec;
            return 0;

        }

        /**
         * Get the configured security provider.
         * This is the security provider used for the default SDK crypto suite factory.
         *
         * @return the security provider.
         */
        public string GetSecurityProviderClassName()
        {
            return GetProperty(SECURITY_PROVIDER_CLASS_NAME);
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

        /**
         * The default ssl provider for grpc connection
         *
         * @return The default ssl provider for grpc connection
         */
        public string GetDefaultSSLProvider()
        {
            return GetProperty(CONN_SSL_PROVIDER);

        }

        /**
         * The default ssl negotiation type
         *
         * @return The default ssl negotiation type
         */

        public string GetDefaultSSLNegotiationType()
        {
            return GetProperty(CONN_SSL_NEGTYPE);

        }


        private Dictionary<int, string> curveMapping = null;

        /**
         * Get a mapping from strength to curve desired.
         *
         * @return mapping from strength to curve name to use.
         */
        public Dictionary<int, string> GetSecurityCurveMapping()
        {
            return curveMapping ?? (curveMapping = ParseSecurityCurveMappings(GetProperty(SECURITY_CURVE_MAPPING)));
        }

        public static Dictionary<int, string> ParseSecurityCurveMappings(string property)
        {
            Dictionary<int, string> lcurveMapping = new Dictionary<int, string>(8);

            if (!string.IsNullOrEmpty(property))
            {
                //empty will be caught later.

                string[] cmaps = Regex.Split(property,"[ \t]*:[ \t]*");
                foreach(string mape in cmaps)
                { 
                    string[] ep = Regex.Split(mape,"[ \t]*=[ \t]*");
                    if (ep.Length != 2)
                    {
                        logger.Warn($"Bad curve mapping for {mape} in property {SECURITY_CURVE_MAPPING}");
                        continue;
                    }

                    try
                    {
                        int parseInt = int.Parse(ep[0]);
                        lcurveMapping.Add(parseInt, ep[1]);
                    }
                    catch (Exception e)
                    {
                        logger.Warn($"Bad curve mapping. Integer needed for strength {ep[0]} for {mape} in property {SECURITY_CURVE_MAPPING}");
                    }

                }

            }

            return lcurveMapping;
        }

        /**
         * Get the timeout for a single proposal request to endorser.
         *
         * @return the timeout in milliseconds.
         */
        public long GetProposalWaitTime()
        {
            if (long.TryParse(GetProperty(PROPOSAL_WAIT_TIME), out long p))
                return p;
            return 0;
        }

        /**
         * Get the configured time to wait for genesis block.
         *
         * @return time in milliseconds.
         */
        public long GetGenesisBlockWaitTime()
        {
            if (long.TryParse(GetProperty(GENESISBLOCK_WAIT_TIME), out long p))
                return p;
            return 0;
        }

        /**
         * Time to wait for channel to be configured.
         *
         * @return
         */
        public long GetChannelConfigWaitTime()
        {
            if (long.TryParse(GetProperty(CHANNEL_CONFIG_WAIT_TIME), out long p))
                return p;
            return 0;
        }

        /**
         * Time to wait before retrying an operation.
         *
         * @return
         */
        public long GetOrdererRetryWaitTime()
        {
            if (long.TryParse(GetProperty(ORDERER_RETRY_WAIT_TIME), out long p))
                return p;
            return 0;
        }

        public long GetOrdererWaitTime()
        {
            if (long.TryParse(GetProperty(ORDERER_WAIT_TIME), out long p))
                return p;
            return 0;
        }

        /**
         * getPeerEventRegistrationWaitTime
         *
         * @return time in milliseconds to wait for peer eventing service to wait for event registration
         */
        public long GetPeerEventRegistrationWaitTime()
        {
            if (long.TryParse(GetProperty(PEER_EVENT_REGISTRATION_WAIT_TIME), out long p))
                return p;
            return 0;
        }

        /**
         * getPeerEventRegistrationWaitTime
         *
         * @return time in milliseconds to wait for peer eventing service to wait for event registration
         */
        public long GetPeerRetryWaitTime()
        {
            if (long.TryParse(GetProperty(PEER_EVENT_RETRY_WAIT_TIME), out long p))
                return p;
            return 0;
        }

        /**
         * The number of failed  attempts to reissue a warning. Or -1 for none.
         *
         * @return The number of failed  attempts to reissue a warning.
         */
        public long GetEventHubReconnectionWarningRate()
        {
            if (long.TryParse(GetProperty(EVENTHUB_RECONNECTION_WARNING_RATE), out long p))
                return p;
            return 0;
        }

        public long GetPeerEventReconnectionWarningRate()
        {
            if (long.TryParse(GetProperty(PEER_EVENT_RECONNECTION_WARNING_RATE), out long p))
                return p;
            return 0;
        }

        public long GetEventHubConnectionWaitTime()
        {
            if (long.TryParse(GetProperty(EVENTHUB_CONNECTION_WAIT_TIME), out long p))
                return p;
            return 0;
        }

        public string GetAsymmetricKeyType()
        {
            return GetProperty(ASYMMETRIC_KEY_TYPE);
        }

        public string GetCertificateFormat()
        {
            return GetProperty(CERTIFICATE_FORMAT);
        }

        public string GetSignatureAlgorithm()
        {
            return GetProperty(SIGNATURE_ALGORITHM);
        }

        public string GetDefaultCryptoSuiteFactory()
        {
            return GetProperty(DEFAULT_CRYPTO_SUITE_FACTORY);
        }

        public int MaxLogStringLength()
        {
            if (int.TryParse(GetProperty(MAX_LOG_STRING_LENGTH), out int p))
                return p;
            return 0;
        }

        /**
         * getProposalConsistencyValidation determine if validation of the proposals should
         * be done before sending to the orderer.
         *
         * @return if true proposals will be checked they are consistent with each other before sending to the Orderer
         */

        public bool GetProposalConsistencyValidation()
        {
            if (bool.TryParse(GetProperty(PROPOSAL_CONSISTENCY_VALIDATION), out bool p))
                return p;
            return false;

        }

        private int extraLogLevel = -1;

        public bool ExtraLogLevel(int val)
        {
            if (extraLogLevel == -1)
            {
                if (int.TryParse(GetProperty(EXTRALOGLEVEL), out int p))
                    extraLogLevel = p;
            }

            return val <= extraLogLevel;

        }

        DiagnosticFileDumper diagnosticFileDumper = null;

        /**
         * The directory where diagnostic dumps are to be place, null if none should be done.
         *
         * @return The directory where diagnostic dumps are to be place, null if none should be done.
         */

        public DiagnosticFileDumper GetDiagnosticFileDumper()
        {

            if (diagnosticFileDumper != null)
            {
                return diagnosticFileDumper;
            }

            string dd = sdkProperties.GetOrNull(DIAGNOTISTIC_FILE_DIRECTORY);

            if (dd != null)
            {

                diagnosticFileDumper = DiagnosticFileDumper.ConfigInstance(new DirectoryInfo(dd));

            }

            return diagnosticFileDumper;
        }

        /**
         * This does NOT trigger futures time out and must be kept WELL above any expected future timeout
         * for transactions sent to the Orderer
         *
         * @return
         */
        public long GetTransactionListenerCleanUpTimeout()
        {
            if (long.TryParse(GetProperty(TRANSACTION_CLEANUP_UP_TIMEOUT_WAIT_TIME), out long p))
                return p;
            return 0;
        }
    }
}