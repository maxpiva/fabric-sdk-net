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
    // Hyperledger Fabric CA Identity information

    public class HFCAIdentity
    {
        public static readonly string HFCA_IDENTITY = HFCAClient.HFCA_CONTEXT_ROOT + "identities";
        private static readonly ILog logger = LogProvider.GetLogger(typeof(HFCAIdentity));

        // The enrollment ID of the user
        // Type of identity
        // Optional secret
        // Maximum number of enrollments with the secret
        // Affiliation for a user
        // Array of attribute names and values

        private readonly HFCAClient client;

        private int statusCode;

        public HFCAIdentity(string enrollmentID, HFCAClient client)
        {
            if (string.IsNullOrEmpty(enrollmentID))
                throw new ArgumentException("EnrollmentID cannot be null or empty");
            if (client.CryptoSuite == null)
                throw new ArgumentException("Client's crypto primitives not set");
            EnrollmentId = enrollmentID;
            this.client = client;
        }

        public HFCAIdentity(JObject result)
        {
            EnrollmentId = result["id"]?.Value<string>();
            GetHFCAIdentity(result);
        }

        /**
         * The name of the identity
         *
         * @return The identity name.
         */

        public string EnrollmentId { get; }

        /**
         * The type of the identity
         *
         * @return The identity type.
         */

        public string Type { get; set; } = "user";

        /**
         * The secret of the identity
         *
         * @return The identity secret.
         */

        public string Secret { get; set; }

        /**
         * The max enrollment value of the identity
         *
         * @return The identity max enrollment.
         */

        public int? MaxEnrollments { get; set; }

        /**
         * The affiliation of the identity
         *
         * @return The identity affiliation.
         */

        /**
 * Set affiliation of the identity
 * @param affiliation Affiliation name
 *
 */
        public string Affiliation { get; set; }
        /**
         * The attributes of the identity
         *
         * @return The identity attributes.
         */

        public List<Attribute> Attributes { get; set; } = new List<Attribute>();

        /**
         * Returns true if the identity has been deleted
         *
         * @return Returns true if the identity has been deleted
         */
        public bool IsDeleted { get; private set; }

        /**
         * Set affiliation of the identity
         * @param affiliation Affiliation name
         *
         */
        public void SetAffiliation(HFCAAffiliation affiliation)
        {
            Affiliation = affiliation.Name;
        }

        /**
         * read retrieves a specific identity
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return statusCode The HTTP status code in the response
         * @throws IdentityException    if retrieving an identity fails.
         * @throws InvalidArgumentException Invalid (null) argument specified
         */
        public int Read(IUser registrar)
        {
            return ReadAsync(registrar).RunAndUnwrap();
        }

        public async Task<int> ReadAsync(IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            string readIdURL = "";
            try
            {
                readIdURL = HFCA_IDENTITY + "/" + EnrollmentId;
                logger.Debug($"identity  url: {readIdURL}, registrar: {registrar.Name}");
                JObject result = await client.HttpGetAsync(readIdURL, registrar, token).ConfigureAwait(false);
                statusCode = result["statusCode"]?.Value<int>() ?? 500;
                if (statusCode < 400)
                {
                    Type = result["type"]?.Value<string>();
                    MaxEnrollments = result["max_enrollments"]?.Value<int>() ?? 0;
                    Affiliation = result["affiliation"]?.Value<string>();
                    JArray attributes = result["attrs"] as JArray;
                    List<Attribute> attrs = new List<Attribute>();
                    if (attributes != null && attributes.Count > 0)
                    {
                        foreach (JToken attribute in attributes)
                        {
                            Attribute attr = new Attribute(attribute["name"]?.Value<string>(), attribute["value"]?.Value<string>(), attribute["cert"]?.Value<bool>() ?? false);
                            attrs.Add(attr);
                        }
                    }

                    Attributes = attrs;
                    logger.Debug($"identity  url: {readIdURL}, registrar: {registrar} done.");
                }

                IsDeleted = false;
                return statusCode;
            }
            catch (HTTPException e)
            {
                string msg = $"[Code: {e.StatusCode}] - Error while getting user '{EnrollmentId}' from url '{readIdURL}': {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.Error(msg);
                throw identityException;
            }
            catch (Exception e)
            {
                string msg = $"Error while getting user '{EnrollmentId}' from url '{readIdURL}': {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.Error(msg);
                throw identityException;
            }
        }

        /**
         * create an identity
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return statusCode The HTTP status code in the response
         * @throws IdentityException    if creating an identity fails.
         * @throws InvalidArgumentException Invalid (null) argument specified
         */
        public int Create(IUser registrar)
        {
            return CreateAsync(registrar).RunAndUnwrap();
        }

        public async Task<int> CreateAsync(IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (IsDeleted)
                throw new IdentityException("Identity has been deleted");
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            string createURL = "";
            try
            {
                createURL = client.GetURL(HFCA_IDENTITY);
                logger.Debug($"identity  url: {createURL}, registrar: {registrar.Name}");
                string body = client.ToJson(IdToJsonObject());
                JObject result = await client.HttpPostAsync(createURL, body, registrar, token).ConfigureAwait(false);
                statusCode = result["statusCode"]?.Value<int>() ?? 500;
                if (statusCode >= 400)
                {
                    GetHFCAIdentity(result);
                    logger.Debug($"identity  url: {createURL}, registrar: {registrar} done.");
                }

                IsDeleted = false;
                return statusCode;
            }
            catch (HTTPException e)
            {
                string msg = $"[Code: {e.StatusCode}] - Error while creating user '{EnrollmentId}' from url '{createURL}': {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.Error(msg);
                throw identityException;
            }
            catch (Exception e)
            {
                string msg = $"Error while creating user '{EnrollmentId}' from url '{createURL}':  {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.Error(msg);
                throw identityException;
            }
        }

        /**
        * update an identity
        *
        * @param registrar The identity of the registrar (i.e. who is performing the registration).
        * @return statusCode The HTTP status code in the response
        * @throws IdentityException    if adding an identity fails.
        * @throws InvalidArgumentException Invalid (null) argument specified
        */
        public int Update(IUser registrar)
        {
            return UpdateAsync(registrar).RunAndUnwrap();
        }

        public async Task<int> UpdateAsync(IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (IsDeleted)
                throw new IdentityException("Identity has been deleted");
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            string updateURL = "";
            try
            {
                updateURL = client.GetURL(HFCA_IDENTITY + "/" + EnrollmentId);
                logger.Debug($"identity  url: {updateURL}, registrar: {registrar.Name}");
                string body = client.ToJson(IdToJsonObject());
                JObject result = await client.HttpPutAsync(updateURL, body, registrar, token).ConfigureAwait(false);
                statusCode = result["statusCode"]?.Value<int>() ?? 500;
                if (statusCode < 400)
                {
                    GetHFCAIdentity(result);
                    logger.Debug($"identity  url: {updateURL}, registrar: {registrar} done.");
                }

                return statusCode;
            }
            catch (HTTPException e)
            {
                string msg = $"[Code: {e.StatusCode}] - Error while updating user '{EnrollmentId}' from url '{updateURL}': {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.Error(msg);
                throw identityException;
            }
            catch (Exception e)
            {
                string msg = $"Error while updating user '{EnrollmentId}' from url '{updateURL}':  {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.Error(msg);
                throw identityException;
            }
        }

        /**
         * delete an identity
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return statusCode The HTTP status code in the response
         * @throws IdentityException    if adding an identity fails.
         * @throws InvalidArgumentException Invalid (null) argument specified
         */
        public int Delete(IUser registrar)
        {
            return DeleteAsync(registrar).RunAndUnwrap();
        }

        public async Task<int> DeleteAsync(IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (IsDeleted)
                throw new IdentityException("Identity has been deleted");
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            string deleteURL = "";
            try
            {
                deleteURL = client.GetURL(HFCA_IDENTITY + "/" + EnrollmentId);
                logger.Debug($"identity  url: {deleteURL}, registrar: {registrar.Name}");
                JObject result = await client.HttpDeleteAsync(deleteURL, registrar, token).ConfigureAwait(false);
                statusCode = result["statusCode"]?.Value<int>() ?? 500;
                if (statusCode < 400)
                {
                    GetHFCAIdentity(result);
                    logger.Debug($"identity  url: {deleteURL}, registrar: {registrar} done.");
                }

                IsDeleted = true;
                return statusCode;
            }
            catch (HTTPException e)
            {
                string msg = $"[Code: {e.StatusCode}] - Error while deleting user '{EnrollmentId}' from url '{deleteURL}': {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.Error(msg);
                throw identityException;
            }
            catch (Exception e)
            {
                string msg = $"Error while deleting user '{EnrollmentId}' from url '{deleteURL}':  {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.Error(msg);
                throw identityException;
            }
        }

        private void GetHFCAIdentity(JObject result)
        {
            Type = result["type"]?.Value<string>();
            if (result.ContainsKey("secret"))
                Secret = result["secret"].Value<string>();
            MaxEnrollments = result["max_enrollments"]?.Value<int>() ?? 0;
            Affiliation = result["affiliation"]?.Value<string>();
            JArray attributes = result["attrs"] as JArray;
            List<Attribute> attrs = new List<Attribute>();
            if (attributes != null && attributes.Count > 0)
            {
                foreach (JToken attribute in attributes)
                {
                    Attribute attr = new Attribute(attribute["name"]?.Value<string>(), attribute["value"]?.Value<string>(), attribute["cert"]?.Value<bool>() ?? false);
                    attrs.Add(attr);
                }
            }

            Attributes = attrs;
        }

        // Convert the identity request to a JSON object
        private JObject IdToJsonObject()
        {
            JObject ob = new JObject();
            ob.Add(new JProperty("id", EnrollmentId));
            ob.Add(new JProperty("type", Type));
            if (null != MaxEnrollments)
                ob.Add(new JProperty("max_enrollments", MaxEnrollments.Value));
            if (Affiliation != null)
                ob.Add(new JProperty("affiliation", Affiliation));
            JArray ab = new JArray();
            foreach (Attribute attr in Attributes)
                ab.Add(attr.ToJsonObject());
            ob.Add(new JProperty("attrs", ab));
            if (Secret != null)
                ob.Add(new JProperty("secret", Secret));
            if (client.CAName != null)
                ob.Add(new JProperty(HFCAClient.FABRIC_CA_REQPROP, client.CAName));
            return ob;
        }

        public static List<HFCAIdentity> FromJArray(JArray ids)
        {
            List<HFCAIdentity> ret = new List<HFCAIdentity>();
            if (ids != null && ids.Count > 0)
            {
                foreach (JToken id in ids)
                    ret.Add(new HFCAIdentity((JObject) id));
            }

            return ret;
        }
    }
}