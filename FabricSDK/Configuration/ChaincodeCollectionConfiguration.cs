using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

namespace Hyperledger.Fabric.SDK.Configuration
{
    public class ChaincodeCollectionConfiguration
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(ChaincodeCollectionConfiguration));
        private static readonly Regex noofPattern = new Regex("^(\\d+)-of$", RegexOptions.Compiled);

        private readonly Protos.Common.CollectionConfigPackage collectionConfigPackage;

        public ChaincodeCollectionConfiguration(JArray jsonConfig)
        {
            collectionConfigPackage = Parse(jsonConfig);
            if (collectionConfigPackage == null)
            {
                throw new ChaincodeCollectionConfigurationException("Parsing collection configuration produce null configuration.");
            }
        }

        public ChaincodeCollectionConfiguration(Protos.Common.CollectionConfigPackage collectionConfigPackage)
        {
            this.collectionConfigPackage = collectionConfigPackage;
        }

        public byte[] GetAsBytes()
        {
            if (collectionConfigPackage == null)
            {
                throw new ChaincodeCollectionConfigurationException("Collection configuration was null.");
            }
            return collectionConfigPackage.ToByteArray();
        }

        /**
         * Creates a new ChaincodeCollectionConfiguration instance configured with details supplied in a YAML file.
         *
         * @param configFile The file containing the network configuration
         * @return A new ChaincodeCollectionConfiguration instance
         * @throws InvalidArgumentException
         * @throws IOException
         */
        public static ChaincodeCollectionConfiguration FromYamlFile(string configFile)
        {
            return FromFile(configFile, false);
        }

        /**
         * Creates a new ChaincodeCollectionConfiguration instance configured with details supplied in a JSON file.
         *
         * @param configFile The file containing the network configuration
         * @return A new ChaincodeCollectionConfiguration instance
         * @throws InvalidArgumentException
         * @throws IOException
         */
        public static ChaincodeCollectionConfiguration FromJsonFile(string configFile)
        {
            return FromFile(configFile, true);
        }

        /**
         * Creates a new ChaincodeCollectionConfiguration instance configured with details supplied in YAML format
         *
         * @param configStream A stream opened on a YAML document containing network configuration details
         * @return A new ChaincodeCollectionConfiguration instance
         * @throws InvalidArgumentException
         */
        public static ChaincodeCollectionConfiguration FromYamlStream(Stream configStream)
        {
            logger.Trace("ChaincodeCollectionConfiguration.fromYamlStream...");

            // Sanity check
            if (configStream == null)
            {
                throw new ArgumentException("ConfigStream must be specified");
            }

            var r = new StreamReader(configStream);
            var deserializer = new Deserializer();
            var yamlObject = deserializer.Deserialize(r);
            var serializer = new JsonSerializer();
            var w = new StringWriter();
            serializer.Serialize(w, yamlObject);
            return FromJsonObject(JArray.Parse(w.ToString()));
        }

        /**
         * Creates a new ChaincodeCollectionConfiguration instance configured with details supplied in JSON format
         *
         * @param configStream A stream opened on a JSON document containing network configuration details
         * @return A new ChaincodeCollectionConfiguration instance
         * @throws InvalidArgumentException
         */
        public static ChaincodeCollectionConfiguration FromJsonStream(Stream configStream)
        {
            logger.Trace("ChaincodeCollectionConfiguration.fromJsonStream...");

            // Sanity check
            if (configStream == null)
            {
                throw new ArgumentException("configStream must be specified");
            }

            using (var r = new StreamReader(configStream))
            {
                string str = r.ReadToEnd();
                return FromJsonObject(JArray.Parse(str));
            }
        }

        /**
         * Creates a new ChaincodeCollectionConfiguration instance configured with details supplied in a JSON object
         *
         * @param jsonConfig JSON object containing network configuration details
         * @return A new ChaincodeCollectionConfiguration instance
         * @throws InvalidArgumentException
         */
        public static ChaincodeCollectionConfiguration FromJsonObject(JArray jsonConfig)
        {
            // Sanity check
            if (jsonConfig == null)
            {
                throw new ArgumentException("jsonConfig must be specified");
            }

            if (logger.IsTraceEnabled())
            {
                logger.Trace($"ChaincodeCollectionConfiguration.fromJsonObject: {jsonConfig}");
            }

            return Load(jsonConfig);
        }
        /*
            public void setCollectionConfigPackage(Collection.CollectionConfigPackage collectionConfigPackage) {
            this.collectionConfigPackage = collectionConfigPackage;
        }
         */

        public static ChaincodeCollectionConfiguration FromCollectionConfigPackage(Protos.Common.CollectionConfigPackage collectionConfigPackage)
        {
            // Sanity check
            if (collectionConfigPackage == null)
            {
                throw new ArgumentException("collectionConfigPackage must be specified");
            }

            return new ChaincodeCollectionConfiguration(collectionConfigPackage);
        }

        // Loads a ChaincodeCollectionConfiguration object from a Json or Yaml file
        private static ChaincodeCollectionConfiguration FromFile(string configFile, bool isJson)
        {
            // Sanity check
            if (configFile == null)
            {
                throw new ArgumentException("configFile must be specified");
            }

            if (logger.IsTraceEnabled())
            {
                logger.Trace($"ChaincodeCollectionConfiguration.fromFile: {configFile}  isJson = {isJson}");
            }

            // Json file
            using (Stream stream = File.OpenRead(configFile))
            {
                return isJson ? FromJsonStream(stream) : FromYamlStream(stream);
            }
        }

        /**
         * Returns a new ChaincodeCollectionConfiguration instance and populates it from the specified JSON object
         *
         * @param jsonConfig The JSON object containing the config details
         * @return A populated ChaincodeCollectionConfiguration instance
         * @throws InvalidArgumentException
         */
        private static ChaincodeCollectionConfiguration Load(JArray jsonConfig)
        {
            // Sanity check
            if (jsonConfig == null)
            {
                throw new ArgumentException("jsonConfig must be specified");
            }

            return new ChaincodeCollectionConfiguration(jsonConfig);
        }

        private Protos.Common.CollectionConfigPackage Parse(JArray jsonConfig)
        {
            Protos.Common.CollectionConfigPackage ls = new Protos.Common.CollectionConfigPackage();
            foreach (JToken j in jsonConfig)
            {
                JObject scf = j["StaticCollectionConfig"] as JObject;
                if (scf == null)
                {
                    throw new ChaincodeCollectionConfigurationException($"Expected StaticCollectionConfig to be Object type but got: {j.Type.ToString()}");
                }
                StaticCollectionConfig ssc = new StaticCollectionConfig();
                ssc.Name = scf["name"].Value<string>();
                ssc.BlockToLive = scf["blockToLive"].Value<ulong>();
                ssc.MaximumPeerCount = scf["maximumPeerCount"].Value<int>();
                CollectionPolicyConfig cpc = new CollectionPolicyConfig();
                cpc.SignaturePolicy = ParseSignaturePolicyEnvelope(scf);
                ssc.MemberOrgsPolicy = cpc;
                ssc.RequiredPeerCount = scf["requiredPeerCount"].Value<int>();
                Protos.Common.CollectionConfig cf = new Protos.Common.CollectionConfig();
                cf.StaticCollectionConfig = ssc;
                ls.Config.Add(cf);
            }

            return ls;
        }

        private SignaturePolicyEnvelope ParseSignaturePolicyEnvelope(JObject scf)
        {
            JObject signaturePolicyEnvelope = scf["SignaturePolicyEnvelope"] as JObject;
            if (signaturePolicyEnvelope==null)
                throw new ChaincodeCollectionConfigurationException($"Expected SignaturePolicyEnvelope to be Object type but got: {scf.Type.ToString()}");
            JArray ids = signaturePolicyEnvelope["identities"] as JArray;
            Dictionary<string, MSPPrincipal> identities = ParseIdentities(ids);
            SignaturePolicy sp = ParsePolicy(identities, signaturePolicyEnvelope["policy"] as JObject);
            SignaturePolicyEnvelope env = new SignaturePolicyEnvelope();
            env.Identities.Add(identities.Values);
            env.Rule = sp;
//        env.Version = signaturePolicyEnvelope["identities"].ToObject<int>();
            return env;
        }

        private SignaturePolicy ParsePolicy(Dictionary<string, MSPPrincipal> identities, JObject policy)
        {
            SignaturePolicy sp = new SignaturePolicy();
            if (policy.Count != 1)
            {
                throw new ChaincodeCollectionConfigurationException($"Expected policy size of 1 but got {policy.Count}");
            }

            JToken jo = policy["signed-by"];
            if (jo != null)
            {
                string vo = jo.ToObject<string>();
                if (!identities.ContainsKey(vo))
                    throw new ChaincodeCollectionConfigurationException($"No identity found by name {vo} in signed-by.");
                sp.SignedBy = identities.Values.ToList().IndexOf(identities[vo]);
            }
            else
            {
                string key = policy.Properties().Select(p => p.Name).First();
                Match match = noofPattern.Match(key);
                JArray vo = policy[key] as JArray;
                if (vo==null)
                    throw new ChaincodeCollectionConfigurationException($"{key} expected to have at least an array of items");
                if (match.Success && match.Groups.Count >= 1)
                {
                    string matchStingNo = match.Groups[1].Value.Trim();
                    int matchNo = int.Parse(matchStingNo);

                    if (vo.Count < matchNo)
                    {
                        throw new ChaincodeCollectionConfigurationException($"{key} expected to have at least {matchNo} items to match but only found {vo.Count}.");
                    }

                    SignaturePolicy.Types.NOutOf nsp = new SignaturePolicy.Types.NOutOf();
                    nsp.N = matchNo;
                    for (int i = vo.Count - 1; i >= 0; --i)
                    {
                        JToken jsonValue = vo[i];
                        if (jsonValue.Type != JTokenType.Object)
                        {
                            throw new ChaincodeCollectionConfigurationException($"Expected object type in Nof but got {jsonValue.Type.ToString()}");
                        }

                        SignaturePolicy spp = ParsePolicy(identities, jsonValue as JObject);
                        nsp.Rules.Add(spp);
                    }

                    sp.NOutOf = nsp;
                }
                else
                {
                    throw new ChaincodeCollectionConfigurationException($"Unsupported policy type {key}");
                }
            }

            return sp;
        }

        private Dictionary<string, MSPPrincipal> ParseIdentities(JArray identities)
        {
            Dictionary<string, MSPPrincipal> ret = new Dictionary<string, MSPPrincipal>();
            foreach (JToken jsonValue in identities)
            {
                if (jsonValue.Type != JTokenType.Object)
                {
                    throw new ChaincodeCollectionConfigurationException($"Expected in identities user to be Object type but got: {jsonValue.Type.ToString()}");
                }

                JObject user = jsonValue as JObject;
                if (user?.Count != 1)
                {
                    throw new ChaincodeCollectionConfigurationException("Only expected on property for user entry in identities.");
                }

                string key = user.Properties().Select(p => p.Name).First();
                JToken vv = user[key];
                if (vv.Type != JTokenType.Object)
                {
                    throw new ChaincodeCollectionConfigurationException($"Expected in identities role to be Object type but got: {vv.Type.ToString()}");
                }

                JObject role = vv as JObject;
                if (role == null)
                    throw new ChaincodeCollectionConfigurationException($"Expected a valid role");
                JObject roleObj = role["role"] as JObject;
                if (roleObj==null)
                    throw new ChaincodeCollectionConfigurationException($"Expected a valid role");
                string roleName = roleObj["name"].Value<string>();
                string mspId = roleObj["mspId"].Value<string>();

                MSPRole.Types.MSPRoleType mspRoleType;

                switch (roleName.ToLowerInvariant())
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
                        throw new ChaincodeCollectionConfigurationException($"In identities with key {key} name expected member, admin, client, or peer in role got {roleName}");
                }

                MSPRole mspRole = new MSPRole();
                mspRole.Role = mspRoleType;
                mspRole.MspIdentifier = mspId;
                MSPPrincipal principal = new MSPPrincipal();
                principal.PrincipalClassification = MSPPrincipal.Types.Classification.Role;
                principal.Principal = mspRole.ToByteString();
                ret.Add(key, principal);
            }

            return ret;
        }
    }
}