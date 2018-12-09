/*
 *
 *  Copyright 2016,2017,2018 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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


using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Hyperledger.Fabric_CA.SDK.Logging;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Fabric_CA.SDK
{
    // Hyperledger Fabric CA Affiliation information
    public class HFCAAffiliation
    {
        public static string HFCA_AFFILIATION = HFCAClient.HFCA_CONTEXT_ROOT + "affiliations";
        private static readonly ILog logger = LogProvider.GetLogger(typeof(HFCAAffiliation));

        // Affiliations affected by this affiliation request
        private List<HFCAAffiliation> childHFCAAffiliations = new List<HFCAAffiliation>();

        private readonly HFCAClient client;

        private bool deleted;

        // Identities affected by this affiliation request
        private List<HFCAIdentity> identities = new List<HFCAIdentity>();

        private string updateName;

        public HFCAAffiliation(string name, HFCAClient client)
        {
            ValidateAffiliationNames(name);
            if (client.CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            Name = name;
            this.client = client;
        }

        public HFCAAffiliation(JObject result)
        {
            GenerateResponse(result);
        }

        /**
          * The name of the affiliation
          *
          * @return The affiliation name.
          */

        public string Name { get; private set; }

        /**
         * The name of the new affiliation
         *
         * @return The affiliation name.
         * @throws AffiliationException
         */

        /**
 * The name of the new affiliation
 * @throws AffiliationException
 *
 */

        public string UpdateName
        {
            get
            {
                if (deleted)
                    throw new AffiliationException("Affiliation has been deleted");
                return updateName;
            }

            set
            {
                if (deleted)
                    throw new AffiliationException("Affiliation has been deleted");
                updateName = value;
            }
        }

        /**
         * The names of all affiliations
         * affected by request
         *
         * @return The affiliation name.
         * @throws AffiliationException
         */

        public List<HFCAAffiliation> Children
        {
            get
            {
                if (deleted)
                    throw new AffiliationException("Affiliation has been deleted");
                return childHFCAAffiliations;
            }
        }

        /**
         * The identities affected during request. Identities are only returned
         * for update and delete requests. Read and Create do not return identities
         *
         * @return The identities affected.
         * @throws AffiliationException
         */

        public List<HFCAIdentity> Identities
        {
            get
            {
                if (deleted)
                    throw new AffiliationException("Affiliation has been deleted");
                return identities;
            }
        }

        /**
         * The identities affected during request
         * @param name Name of the child affiliation
         *
         * @return The requested child affiliation
         * @throws InvalidArgumentException
         * @throws AffiliationException
         */
        public HFCAAffiliation CreateDecendent(string name)
        {
            if (deleted)
                throw new AffiliationException("Affiliation has been deleted");
            ValidateAffiliationNames(name);
            return new HFCAAffiliation(Name + "." + name, client);
        }

        /**
         * Gets child affiliation by name
         * @param name Name of the child affiliation to get
         *
         * @return The requested child affiliation
         * @throws AffiliationException
         * @throws InvalidArgumentException
         */
        public HFCAAffiliation GetChild(string name)
        {
            if (deleted)
                throw new AffiliationException("Affiliation has been deleted");
            ValidateSingleAffiliationName(name);
            foreach (HFCAAffiliation childAff in childHFCAAffiliations)
            {
                if (childAff.Name.Equals(Name + "." + name))
                    return childAff;
            }
            return null;
        }

        /**
         * Returns true if the affiliation has been deleted
         *
         * @return Returns true if the affiliation has been deleted
         */
        public bool IsDeleted()
        {
            return deleted;
        }

        /**
         * gets a specific affiliation
         *
         * @param registrar The identity of the registrar
         * @return Returns response
         * @throws AffiliationException if getting an affiliation fails.
         * @throws InvalidArgumentException
         */
        public int Read(IUser registrar)
        {
            return ReadAsync(registrar).RunAndUnwarp();
        }
        public async Task<int> ReadAsync(IUser registrar, CancellationToken token=default(CancellationToken))
        {
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            string readAffURL = "";
            try
            {
                readAffURL = HFCA_AFFILIATION + "/" + Name;
                logger.Debug($"affiliation  url: {readAffURL}, registrar: {registrar.Name}");
                JObject result = await client.HttpGetAsync(readAffURL, registrar, token).ConfigureAwait(false);
                logger.Debug($"affiliation  url: {readAffURL}, registrar: {registrar} done.");
                HFCAAffiliationResp resp = GetResponse(result);
                childHFCAAffiliations = resp.Children;
                identities = resp.Identities;
                deleted = false;
                return resp.StatusCode;
            }
            catch (HTTPException e)
            {
                string msg = $"[Code: {e.StatusCode}] - Error while getting affiliation '{Name}' from url '{readAffURL}': {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.Error(msg);
                throw affiliationException;
            }
            catch (Exception e)
            {
                string msg = $"Error while getting affiliation {Name} url: {readAffURL}  {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.Error(msg);
                throw affiliationException;
            }
        }

        /**
         * create an affiliation
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return Response of request
         * @throws AffiliationException    if adding an affiliation fails.
         * @throws InvalidArgumentException
         */
        public HFCAAffiliationResp Create(IUser registrar)
        {
            return CreateAsync(registrar).RunAndUnwarp();
        }
        public Task<HFCAAffiliationResp> CreateAsync(IUser registrar, CancellationToken token=default(CancellationToken))
        {
            return CreateAsync(registrar, false,token);
        }

        /**
         * create an affiliation
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @param force Forces the creation of parent affiliations
         * @return Response of request
         * @throws AffiliationException    if adding an affiliation fails.
         * @throws InvalidArgumentException
         */
        public async Task<HFCAAffiliationResp> CreateAsync(IUser registrar, bool force, CancellationToken token = default(CancellationToken))
        {
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            string createURL = "";
            try
            {
                logger.Debug($"affiliation  url: {createURL}, registrar: {registrar.Name}");
                Dictionary<string, string> queryParm = new Dictionary<string, string>();
                queryParm.Add("force", force.ToString());
                createURL = client.GetURL(HFCA_AFFILIATION,queryParm); //TODO Report bug into the JAVA version, force was never sent.
                string body = client.ToJson(AffToJsonObject());
                JObject result = await client.HttpPostAsync(createURL, body, registrar, token).ConfigureAwait(false);
                logger.Debug($"identity  url: {createURL}, registrar: {registrar.Name} done.");
                deleted = false;
                return GetResponse(result);
            }
            catch (HTTPException e)
            {
                string msg = $"[Code: {e.StatusCode}] - Error while creating affiliation '{Name}' from url '{createURL}': {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.Error(msg);
                throw affiliationException;
            }
            catch (Exception e)
            {
                string msg = $"Error while creating affiliation {Name} url: {createURL}  {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.Error(msg);
                throw affiliationException;
            }
        }

        /**
         * update an affiliation
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return Response of request
         * @throws AffiliationException If updating an affiliation fails
         * @throws InvalidArgumentException
         */
        public HFCAAffiliationResp Update(IUser registrar)
        {
            return Update(registrar, false);
        }
        public Task<HFCAAffiliationResp> UpdateAsync(IUser registrar, CancellationToken token=default(CancellationToken))
        {
            return UpdateAsync(registrar, false, token);
        }

        /**
         * update an affiliation
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @param force Forces updating of child affiliations
         * @return Response of request
         * @throws AffiliationException If updating an affiliation fails
         * @throws InvalidArgumentException
         */
        public HFCAAffiliationResp Update(IUser registrar, bool force)
        {
            return UpdateAsync(registrar,force).RunAndUnwarp();
        }
        public async Task<HFCAAffiliationResp> UpdateAsync(IUser registrar, bool force, CancellationToken token = default(CancellationToken))
        {
            if (deleted)
                throw new AffiliationException("Affiliation has been deleted");
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            if (string.IsNullOrEmpty(Name))
                throw new ArgumentException("Affiliation name cannot be null or empty");
            string updateURL = "";
            try
            {
                Dictionary<string, string> queryParm = new Dictionary<string, string>();
                queryParm.Add("force", force.ToString());
                updateURL = client.GetURL(HFCA_AFFILIATION + "/" + Name, queryParm);
                logger.Debug($"affiliation  url: {updateURL}, registrar: {registrar.Name}");
                string body = client.ToJson(AffToJsonObject());
                JObject result = await client.HttpPutAsync(updateURL, body, registrar, token).ConfigureAwait(false);
                GenerateResponse(result);
                logger.Debug($"identity  url: {updateURL}, registrar: {registrar.Name} done.");
                HFCAAffiliationResp resp = GetResponse(result);
                childHFCAAffiliations = resp.Children;
                identities = resp.Identities;
                return GetResponse(result);
            }
            catch (HTTPException e)
            {
                string msg = $"[Code: {e.StatusCode}] - Error while updating affiliation '{Name}' from url '{updateURL}': {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.Error(msg);
                throw affiliationException;
            }
            catch (Exception e)
            {
                string msg = $"Error while updating affiliation {Name} url: {updateURL} {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.Error(msg);
                throw affiliationException;
            }
        }

        /**
         * delete an affiliation
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return Response of request
         * @throws AffiliationException    if deleting an affiliation fails.
         * @throws InvalidArgumentException
         */
        public HFCAAffiliationResp Delete(IUser registrar)
        {
            return Delete(registrar,false);
        }
        public Task<HFCAAffiliationResp> DeleteAsync(IUser registrar, CancellationToken token = default(CancellationToken))
        {
            return DeleteAsync(registrar, false,token);
        }

        /**
         * delete an affiliation
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @param force Forces the deletion of affiliation
         * @return Response of request
         * @throws AffiliationException    if deleting an affiliation fails.
         * @throws InvalidArgumentException
         */
        public HFCAAffiliationResp Delete(IUser registrar, bool force)
        {
            return DeleteAsync(registrar, force).RunAndUnwarp();
        }
        public async Task<HFCAAffiliationResp> DeleteAsync(IUser registrar, bool force, CancellationToken token = default(CancellationToken))
        {
            if (deleted)
                throw new AffiliationException("Affiliation has been deleted");
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            string deleteURL = "";
            try
            {
                Dictionary<string, string> queryParm = new Dictionary<string, string>();
                queryParm.Add("force", force.ToString());
                deleteURL = client.GetURL(HFCA_AFFILIATION + "/" + Name, queryParm);
                logger.Debug($"affiliation  url: {deleteURL}, registrar: {registrar.Name}");
                JObject result = await client.HttpDeleteAsync(deleteURL, registrar, token).ConfigureAwait(false);
                logger.Debug($"identity  url: {deleteURL}, registrar: {registrar.Name} done.");
                deleted = true;
                return GetResponse(result);
            }
            catch (HTTPException e)
            {
                string msg = $"[Code: {e.StatusCode}] - Error while deleting affiliation '{Name}' from url '{deleteURL}': {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.Error(msg);
                throw affiliationException;
            }
            catch (Exception e)
            {
                string msg = $"Error while deleting affiliation {Name} url: {deleteURL}  {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.Error(msg);
                throw affiliationException;
            }
        }

        public static List<HFCAAffiliation> FromJArray(JArray affiliations)
        {
            List<HFCAAffiliation> ret = new List<HFCAAffiliation>();
            if (affiliations != null && affiliations.Count > 0)
            {
                foreach (JToken aff in affiliations)
                    ret.Add(new HFCAAffiliation((JObject) aff));
            }
            return ret;
        }

        private HFCAAffiliationResp GetResponse(JObject result)
        {
            if (result.ContainsKey("name"))
                Name = result["name"].Value<string>();
            return new HFCAAffiliationResp(result);
        }

        private void GenerateResponse(JObject result)
        {
            if (result.ContainsKey("name"))
                Name = result["name"].Value<string>();
            if (result.ContainsKey("affiliations"))
                childHFCAAffiliations.AddRange(FromJArray(result["affiliations"] as JArray));
            if (result.ContainsKey("identities"))
                identities.AddRange(HFCAIdentity.FromJArray(result["identities"] as JArray));
        }

        // Convert the affiliation request to a JSON object
        private JObject AffToJsonObject()
        {
            JObject ob = new JObject();
            if (client.CAName != null)
                ob.Add(new JProperty(HFCAClient.FABRIC_CA_REQPROP, client.CAName));
            if (updateName != null)
            {
                ob.Add(new JProperty("name", updateName));
                updateName = null;
            }
            else
                ob.Add(new JProperty("name", Name));
            return ob;
        }

        /**
         * Validate affiliation name for proper formatting
         *
         * @param name the string to test.
         * @throws InvalidArgumentException
         */
        public void ValidateAffiliationNames(string name)
        {
            CheckFormat(name);
            if (name.StartsWith("."))
                throw new ArgumentException("Affiliation name cannot start with a dot '.'");
            if (name.EndsWith("."))
                throw new ArgumentException("Affiliation name cannot end with a dot '.'");
            for (int i = 0; i < name.Length; i++)
            {
                if (name[i] == '.' && name[i] == name[i - 1])
                {
                    throw new ArgumentException("Affiliation name cannot contain multiple consecutive dots '.'");
                }
            }
        }

        /**
         * Validate affiliation name for proper formatting
         *
         * @param name the string to test.
         * @throws InvalidArgumentException
         */
        public void ValidateSingleAffiliationName(string name)
        {
            CheckFormat(name);
            if (name.Contains("."))
                throw new ArgumentException("Single affiliation name cannot contain any dots '.'");
        }

        public static void CheckFormat(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Affiliation name cannot be null or empty");
            if (name.Contains(" ") || name.Contains("\t"))
                throw new ArgumentException("Affiliation name cannot contain an empty space or tab");
        }

        /**
         * Response of affiliation requests
         *
         */
        public class HFCAAffiliationResp
        {
            // Affiliations affected by this affiliation request
            // Identities affected by this affiliation request
            public HFCAAffiliationResp(JObject result)
            {
                if (result.ContainsKey("affiliations"))
                    Children.AddRange(FromJArray(result["affiliations"] as JArray));
                if (result.ContainsKey("identities"))
                    Identities.AddRange(HFCAIdentity.FromJArray(result["identities"] as JArray));
                if (result.ContainsKey("statusCode"))
                    StatusCode = result["statusCode"].Value<int>();
            }


            /**
             * The identities affected during request
             *
             * @return The identities affected.
             */

            public List<HFCAIdentity> Identities { get; } = new List<HFCAIdentity>();

            /**
             * The names of all affiliations
             * affected by request
             *
             * @return The affiliation name.
             */

            public List<HFCAAffiliation> Children { get; } = new List<HFCAAffiliation>();

            /**
             * @return HTTP status code
             */
            public int StatusCode { get; } = 200;
        }
    }
}