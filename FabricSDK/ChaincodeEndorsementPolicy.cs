/*
 *  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text.RegularExpressions;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;


using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Hyperledger.Fabric.SDK
{
    /**
     * A wrapper for the Hyperledger Fabric Policy object
     */
    public class ChaincodeEndorsementPolicy
    {
        private static readonly Regex noofPattern = new Regex("^(\\d+)-of$", RegexOptions.Compiled);
        private byte[] policyBytes = null;

        /**
         * The null constructor for the ChaincodeEndorsementPolicy wrapper. You will
         * need to use the {@link #fromBytes(byte[])} method to
         * populate the policy
         */
        public ChaincodeEndorsementPolicy()
        {
        }

        private static SignaturePolicy ParsePolicy(IndexedHashMap<string, MSPPrincipal> identities, Dictionary<string, object> mp)
        {
            if (mp == null)
            {
                throw new ChaincodeEndorsementPolicyParseException("No policy section was found in the document.");
            }

            foreach (KeyValuePair<string, object> ks in mp)
            {
                string key = ks.Key;
                object vo = ks.Value;
                if ("signed-by".Equals(key, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!(vo is string))
                        throw new ChaincodeEndorsementPolicyParseException("signed-by expecting a string value");
                    MSPPrincipal mspPrincipal = identities[(string) vo];
                    if (null == mspPrincipal)
                    {
                        throw new ChaincodeEndorsementPolicyParseException($"No identity found by name {(string) vo} in signed-by.");
                    }

                    return new SignaturePolicy {SignedBy = identities.Index((string) vo)};
                }

                Match match = noofPattern.Match(key);

                if (match.Success && match.Groups.Count == 1)
                {
                    string matchStingNo = match.Groups[0].Value.Trim();
                    int.TryParse(matchStingNo, out int matchNo);
                    List<Dictionary<string, object>> voList = vo as List<Dictionary<string, object>>;
                    if (voList == null)
                        throw new ChaincodeEndorsementPolicyParseException($"{key} expected to have list but found {vo}x.");
                    if (voList.Count < matchNo)
                        throw new ChaincodeEndorsementPolicyParseException($"{key} expected to have at least {matchNo} items to match but only found {voList.Count}.");
                    SignaturePolicy.Types.NOutOf spB = new SignaturePolicy.Types.NOutOf { N = matchNo};
                    foreach (Dictionary<string, object> nlo in voList)
                    {

                        SignaturePolicy sp = ParsePolicy(identities, nlo);
                        spB.Rules.Add(sp);
                    }

                    return new SignaturePolicy {NOutOf = spB};
                }

                throw new ChaincodeEndorsementPolicyParseException($"Unsupported policy type {key}");
            }

            throw new ChaincodeEndorsementPolicyParseException("No values found for policy");
        }




        private static IndexedHashMap<string, MSPPrincipal> ParseIdentities(Dictionary<string, object> identities)
        {
            //Only Role types are excepted at this time.

            IndexedHashMap<string, MSPPrincipal> ret = new IndexedHashMap<string, MSPPrincipal>();

            foreach (KeyValuePair<string, object> kp in identities)
            {
                string key = kp.Key;
                object val = kp.Value;
                /*
                if (!(key instanceof String)) {
                    throw new ChaincodeEndorsementPolicyParseException(format("In identities key expected String got %s ", key == null ? "null" : key.getClass().getName()));
                }
                */
                if (ret.ContainsKey(key))
                {
                    throw new ChaincodeEndorsementPolicyParseException($"In identities with key {key} is listed more than once ");
                }

                Dictionary<string, object> dictval = val as Dictionary<string, object>;
                if (dictval == null)
                {
                    string str = (val == null) ? "null" : val.GetType().Name;
                    throw new ChaincodeEndorsementPolicyParseException($"In identities with key {key} value expected Map got {str}");
                }

                object role = dictval.ContainsKey("role") ? dictval["role"] : null;
                Dictionary<string, object> roleMap = role as Dictionary<string, object>;
                if (roleMap == null)
                {
                    string str = (role == null) ? "null" : role.GetType().Name;
                    throw new ChaincodeEndorsementPolicyParseException("In identities with key {key} value expected Map for role got {str}");
                }

                object nameObj = roleMap.ContainsKey("name") ? roleMap["name"] : null;
                string name = nameObj as string;
                if (name == null)
                {
                    string str = (nameObj == null) ? "null" : nameObj.GetType().Name;
                    throw new ChaincodeEndorsementPolicyParseException($"In identities with key {key} name expected String in role got {str}");
                }

                name = name.Trim();
                object mspId = roleMap.ContainsKey("mspId") ? roleMap["mspId"] : null;

                if (!(mspId is string))
                {
                    string str = (mspId == null) ? "null" : mspId.GetType().Name;
                    throw new ChaincodeEndorsementPolicyParseException($"In identities with key {key} mspId expected String in role got {str}");
                }

                if (string.IsNullOrEmpty((string) mspId))
                {

                    throw new ChaincodeEndorsementPolicyParseException($"In identities with key {key} mspId must not be null or empty String in role");
                }

                MSPRole.Types.MSPRoleType mspRoleType;

                switch (name)
                {
                    case "member":
                        mspRoleType = MSPRole.Types.MSPRoleType.Member;
                        break;
                    case "admin":
                        mspRoleType = MSPRole.Types.MSPRoleType.Admin;
                        break;
                    case "client":
                        mspRoleType = MSPRole.Types.MSPRoleType.Client;
                        break;
                    case "peer":
                        mspRoleType = MSPRole.Types.MSPRoleType.Peer;
                        break;
                    default:
                        throw new ChaincodeEndorsementPolicyParseException($"In identities with key {key} name expected member, admin, client, or peer in role got {name}");
                }

                MSPRole mspRole = new MSPRole {MspIdentifier = (string) mspId, Role = mspRoleType};
                MSPPrincipal principal = new MSPPrincipal {Principal = mspRole.ToByteString(), PrincipalClassification = MSPPrincipal.Types.Classification.Role};
                ret.Add(key, principal);

            }

            if (ret.Count == 0)
            {
                throw new ChaincodeEndorsementPolicyParseException("No identities were found in the policy specification");
            }

            return ret;

        }

        /**
         * constructs a ChaincodeEndorsementPolicy object with the actual policy gotten from the file system
         *
         * @param policyFile The file containing the policy
         * @throws IOException
         */
        public void FromFile(FileInfo policyFile)
        {
            policyBytes = File.ReadAllBytes(policyFile.FullName);
        }

        private Dictionary<string, object> RecursiveGenerateDictionaries(YamlMappingNode node)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            foreach (YamlNode k in node.Children.Keys)
            {
                YamlScalarNode key = k as YamlScalarNode;
                if (key != null)
                {
                    YamlScalarNode scnode = node.Children[key] as YamlScalarNode;
                    YamlMappingNode snode = node.Children[key] as YamlMappingNode;
                    if (snode != null)
                        ret.Add(key.Value, RecursiveGenerateDictionaries(snode));
                    else if (scnode != null)
                        ret.Add(key.Value, scnode.Value);
                }
            }

            return ret;
        }

        /**
         * From a yaml file
         *
         * @param yamlPolicyFile File location for the chaincode endorsement policy specification.
         * @throws IOException
         * @throws ChaincodeEndorsementPolicyParseException
         */

        public void FromYamlFile(FileInfo yamlPolicyFile)
        {

            YamlStream yaml = new YamlStream();
            yaml.Load(new StreamReader(yamlPolicyFile.OpenRead()));







            var mapping = (YamlMappingNode) yaml.Documents[0].RootNode;
            var mpy = (YamlMappingNode) mapping.Children[new YamlScalarNode("policy")];
            if (null == mpy)
            {
                throw new ChaincodeEndorsementPolicyParseException("The policy file has no policy section");
            }

            Dictionary<string, object> mp = RecursiveGenerateDictionaries(mpy);
            Dictionary<string, object> idsp = (Dictionary<string, object>) mp["identities"];
            IndexedHashMap<String, MSPPrincipal> identities = ParseIdentities(idsp);
            SignaturePolicy sp = ParsePolicy(identities, mp);
            SignaturePolicyEnvelope env = new SignaturePolicyEnvelope {Rule = sp, Version = 0};
            env.Identities.AddRange(identities.Values);
            policyBytes = env.ToByteArray();
        }

        /**
         * Construct a chaincode endorsement policy from a stream.
         *
         * @param inputStream
         * @throws IOException
         */

        public void FromStream(Stream inputStream)
        {
            policyBytes = inputStream.ToByteArray();
        }

        /**
         * sets the ChaincodeEndorsementPolicy from a byte array
         *
         * @param policyAsBytes the byte array containing the serialized policy
         */
        public void FromBytes(byte[] policyAsBytes)
        {
            this.policyBytes = policyAsBytes;
        }

        /**
         * @return the policy serialized per protobuf and ready for inclusion into the various Block/Envelope/ChaincodeInputSpec structures
         */
        public byte[] ChaincodeEndorsementPolicyAsBytes => policyBytes;
    }
}
