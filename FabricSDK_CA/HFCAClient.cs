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

/**
 * HFCAClient Hyperledger Fabric Certificate Authority Client.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.AMCL;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Idemix;
using Hyperledger.Fabric.SDK.Identity;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Hyperledger.Fabric_CA.SDK.Logging;
using Hyperledger.Fabric_CA.SDK.Requests;
using Hyperledger.Fabric_CA.SDK.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Security.Certificates;
using Config = Hyperledger.Fabric_CA.SDK.Helper.Config;

// ReSharper disable UnusedMember.Local


namespace Hyperledger.Fabric_CA.SDK
{
    public class HFCAClient
    {
        /**
     * Default profile name.
     */
        public static readonly string DEFAULT_PROFILE_NAME = "";

        /**
     * HFCA_TYPE_PEER indicates that an identity is acting as a peer
     */
        public static readonly string HFCA_TYPE_PEER = "peer";

        /**
     * HFCA_TYPE_ORDERER indicates that an identity is acting as an orderer
     */
        public static readonly string HFCA_TYPE_ORDERER = "orderer";

        /**
     * HFCA_TYPE_CLIENT indicates that an identity is acting as a client
     */
        public static readonly string HFCA_TYPE_CLIENT = "client";

        /**
     * HFCA_TYPE_USER indicates that an identity is acting as a user
     */
        public static readonly string HFCA_TYPE_USER = "user";

        /**
     * HFCA_ATTRIBUTE_HFREGISTRARROLES is an attribute that allows a registrar to manage identities of the specified roles
     */
        public static readonly string HFCA_ATTRIBUTE_HFREGISTRARROLES = "hf.Registrar.Roles";

        /**
     * HFCA_ATTRIBUTE_HFREGISTRARDELEGATEROLES is an attribute that allows a registrar to give the roles specified
     * to a registree for its 'hf.Registrar.Roles' attribute
     */
        public static readonly string HFCA_ATTRIBUTE_HFREGISTRARDELEGATEROLES = "hf.Registrar.DelegateRoles";

        /**
     * HFCA_ATTRIBUTE_HFREGISTRARATTRIBUTES is an attribute that has a list of attributes that the registrar is allowed to register
     * for an identity
     */
        public static readonly string HFCA_ATTRIBUTE_HFREGISTRARATTRIBUTES = "hf.Registrar.Attributes";

        /**
     * HFCA_ATTRIBUTE_HFINTERMEDIATECA is a boolean attribute that allows an identity to enroll as an intermediate CA
     */
        public static readonly string HFCA_ATTRIBUTE_HFINTERMEDIATECA = "hf.IntermediateCA";

        /**
     * HFCA_ATTRIBUTE_HFREVOKER is a boolean attribute that allows an identity to revoker a user and/or certificates
     */
        public static readonly string HFCA_ATTRIBUTE_HFREVOKER = "hf.Revoker";

        /**
     * HFCA_ATTRIBUTE_HFAFFILIATIONMGR is a boolean attribute that allows an identity to manage affiliations
     */
        public static readonly string HFCA_ATTRIBUTE_HFAFFILIATIONMGR = "hf.AffiliationMgr";

        /**
     * HFCA_ATTRIBUTE_HFGENCRL is an attribute that allows an identity to generate a CRL
     */
        public static readonly string HFCA_ATTRIBUTE_HFGENCRL = "hf.GenCRL";

        private static readonly Config config = Config.Instance; // DO NOT REMOVE THIS IS NEEDED TO MAKE SURE WE FIRST LOAD CONFIG!!!


        private static readonly int CONNECTION_REQUEST_TIMEOUT = config.GetConnectionRequestTimeout();
        private static readonly int CONNECT_TIMEOUT = config.GetConnectTimeout();
        private static readonly int SOCKET_TIMEOUT = config.GetSocketTimeout();


        private static readonly ILog logger = LogProvider.GetLogger(typeof(HFCAClient));

        public static readonly string FABRIC_CA_REQPROP = "caname";
        public static readonly string HFCA_CONTEXT_ROOT = "/api/v1/";

        private static readonly string HFCA_ENROLL = HFCA_CONTEXT_ROOT + "enroll";
        private static readonly string HFCA_REGISTER = HFCA_CONTEXT_ROOT + "register";
        private static readonly string HFCA_REENROLL = HFCA_CONTEXT_ROOT + "reenroll";
        private static readonly string HFCA_REVOKE = HFCA_CONTEXT_ROOT + "revoke";
        private static readonly string HFCA_INFO = HFCA_CONTEXT_ROOT + "cainfo";
        private static readonly string HFCA_GENCRL = HFCA_CONTEXT_ROOT + "gencrl";
        private static readonly string HFCA_CERTIFICATE = HFCA_CONTEXT_ROOT + "certificates";
        private static readonly string HFCA_IDEMIXCRED = HFCA_CONTEXT_ROOT + "idemix/credential";

        private readonly bool isSSL;


        private readonly Properties properties;

        private readonly string url;

        private KeyStore caStore;

        // Cache the payload type, so don't need to make get cainfo call everytime
        private bool? newPayloadType;

        /**
         * HFCAClient constructor
         *
         * @param url        Http URL for the Fabric's certificate authority services endpoint
         * @param properties PEM used for SSL .. not implemented.
         *                   <p>
         *                   Supported properties
         *                   <ul>
         *                   <li>pemFile - File location for x509 pem certificate for SSL.</li>
         *                   <li>allowAllHostNames - boolen(true/false) override certificates CN Host matching -- for development only.</li>
         *                   </ul>
         * @throws MalformedURLException
         */
        public HFCAClient(string caName, string url, Properties properties)
        {
            logger.Debug($"new HFCAClient {url}");
            this.url = url;
            CAName = caName; //name may be null
            Uri purl;
            try
            {
                purl = new Uri(url);
            }
            catch (UriFormatException e)
            {
                if (e.Message.Contains("hostname could not be parsed"))
                    throw new ArgumentException("HFCAClient url needs host");
                throw;
            }

            string proto = purl.Scheme;
            if (!"http".Equals(proto) && !"https".Equals(proto))
                throw new ArgumentException("HFCAClient only supports http or https not " + proto);
            string host = purl.Host;
            if (string.IsNullOrEmpty(host))
                throw new ArgumentException("HFCAClient url needs host");
            string path = purl.LocalPath;
            if (!string.IsNullOrEmpty(path) && path != "/")
                throw new ArgumentException("HFCAClient url does not support path portion in url remove path: '" + path + "'.");
            string query = purl.Query;
            if (!string.IsNullOrEmpty(query))
                throw new ArgumentException("HFCAClient url does not support query portion in url remove query: '" + query + "'.");
            isSSL = "https".Equals(proto);
            this.properties = properties?.Clone();
        }

        /**
         * The Certificate Authority name.
         *
         * @return May return null or empty string for default certificate authority.
         */
        public string CAName { get; }

        /**
         * The Status Code level of client, HTTP status codes above this value will return in a
         * exception, otherwise, the status code will be return the status code and appropriate error
         * will be logged.
         *
         * @return statusCode
         */
        public int StatusCode { get; internal set; } = 400;

        public ICryptoSuite CryptoSuite { get; set; }

        public static HFCAClient Create(string url, Properties properties)
        {
            return new HFCAClient(null, url, properties);
        }

        public static HFCAClient Create(string name, string url, Properties properties)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("name must not be null or an empty string.");
            return new HFCAClient(name, url, properties);
        }

        /**
         * Create HFCAClient from a NetworkConfig.CAInfo using default crypto suite.
         *
         * @param caInfo created from NetworkConfig.getOrganizationInfo("org_name").getCertificateAuthorities()
         * @return HFCAClient
         * @throws MalformedURLException
         * @throws InvalidArgumentException
         */

        public static HFCAClient Create(NetworkConfig.CAInfo caInfo)
        {
            try
            {
                return Create(caInfo, Factory.Instance.GetCryptoSuite());
            }
            catch (Exception e)
            {
                throw new ArgumentException(e.Message, e);
            }
        }

        /**
         * Create HFCAClient from a NetworkConfig.CAInfo
         *
         * @param caInfo      created from NetworkConfig.getOrganizationInfo("org_name").getCertificateAuthorities()
         * @param cryptoSuite the specific cryptosuite to use.
         * @return HFCAClient
         * @throws MalformedURLException
         * @throws InvalidArgumentException
         */

        public static HFCAClient Create(NetworkConfig.CAInfo caInfo, ICryptoSuite cryptoSuite)
        {
            if (null == caInfo)
                throw new ArgumentException("The caInfo parameter can not be null.");
            if (null == cryptoSuite)
                throw new ArgumentException("The cryptoSuite parameter can not be null.");
            HFCAClient ret = new HFCAClient(caInfo.CAName, caInfo.Url, caInfo.Properties);
            ret.CryptoSuite = cryptoSuite;
            return ret;
        }

        /**
         * Register a user.
         *
         * @param request   Registration request with the following fields: name, role.
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return the enrollment secret.
         * @throws RegistrationException    if registration fails.
         * @throws InvalidArgumentException
         */
        public string Register(RegistrationRequest request, IUser registrar)
        {
            return RegisterAsync(request, registrar).RunAndUnwrap();
        }

        public async Task<string> RegisterAsync(RegistrationRequest request, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            if (string.IsNullOrEmpty(request.EnrollmentID))
                throw new ArgumentException("EntrollmentID cannot be null or empty");
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            logger.Debug($"register  url: {url}, registrar: {registrar.Name}");
            SetUpSSL();
            try
            {
                string body = request.ToJson();
                JObject resp = await HttpPostAsync(url + HFCA_REGISTER, body, registrar, token).ConfigureAwait(false);
                string secret = resp["secret"]?.Value<string>();
                if (secret == null)
                {
                    throw new Exception("secret was not found in response");
                }

                logger.Debug($"register  url: {url}, registrar: {registrar.Name} done.");
                return secret;
            }
            catch (Exception e)
            {
                RegistrationException registrationException = new RegistrationException($"Error while registering the user {registrar.Name} url: {url}  {e.Message}", e);
                logger.Error(registrationException.Message, registrationException);
                throw registrationException;
            }
        }

        /**
         * Enroll the user with member service
         *
         * @param user   Identity name to enroll
         * @param secret Secret returned via registration
         * @return enrollment
         * @throws EnrollmentException
         * @throws InvalidArgumentException
         */
        public IEnrollment Enroll(string user, string secret)
        {
            return Enroll(user, secret, new EnrollmentRequest());
        }

        public Task<IEnrollment> EnrollAsync(string user, string secret, CancellationToken token = default(CancellationToken))
        {
            return EnrollAsync(user, secret, new EnrollmentRequest(), token);
        }

        /**
         * Enroll the user with member service
         *
         * @param user   Identity name to enroll
         * @param secret Secret returned via registration
         * @param req    Enrollment request with the following fields: hosts, profile, csr, label, keypair
         * @return enrollment
         * @throws EnrollmentException
         * @throws InvalidArgumentException
         */
        public IEnrollment Enroll(string user, string secret, EnrollmentRequest req)
        {
            return EnrollAsync(user, secret, req).RunAndUnwrap();
        }

        public async Task<IEnrollment> EnrollAsync(string user, string secret, EnrollmentRequest req, CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"url: {url} enroll user: {user}");
            if (string.IsNullOrEmpty(user))
                throw new ArgumentException("enrollment user is not set");
            if (string.IsNullOrEmpty(secret))
                throw new ArgumentException("enrollment secret is not set");
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            SetUpSSL();
            try
            {
                string pem = req.CSR;
                KeyPair keypair = req.KeyPair;
                if (null != pem && keypair == null)
                    throw new ArgumentException("If certificate signing request is supplied the key pair needs to be supplied too.");
                if (keypair == null)
                {
                    logger.Debug("[HFCAClient.enroll] Generating keys...");
                    // generate ECDSA keys: signing and encryption keys
                    keypair = CryptoSuite.KeyGen();
                    logger.Debug("[HFCAClient.enroll] Generating keys...done!");
                }

                if (pem == null)
                    req.CSR = CryptoSuite.GenerateCertificationRequest(user, keypair);
                if (!string.IsNullOrEmpty(CAName))
                    req.CAName = CAName;
                string body = req.ToJson();
                string responseBody = await HttpPostAsync(url + HFCA_ENROLL, body, new NetworkCredential(user, secret), token).ConfigureAwait(false);
                logger.Debug("response:" + responseBody);
                JObject jsonst = JObject.Parse(responseBody);
                bool success = jsonst["success"]?.Value<bool>() ?? false;
                logger.Debug($"[HFCAClient] enroll success:[{success}]");
                if (!success)
                    throw new EnrollmentException($"FabricCA failed enrollment for user {user} response success is false.");
                JObject result = jsonst["result"] as JObject;
                if (result == null)
                    throw new EnrollmentException($"FabricCA failed enrollment for user {user} - response did not contain a result");
                string signedPem = Convert.FromBase64String(result["Cert"]?.Value<string>() ?? "").ToUTF8String();
                logger.Debug($"[HFCAClient] enroll returned pem:[{signedPem}]");
                JArray messages = jsonst["messages"] as JArray;
                if (messages != null && messages.Count > 0)
                {
                    JToken jo = messages[0];
                    string message = $"Enroll request response message [code {jo["code"]?.Value<int>() ?? 0}]: {jo["message"]?.Value<string>() ?? ""}";
                    logger.Info(message);
                }

                logger.Debug("Enrollment done.");
                return new X509Enrollment(keypair, signedPem);
            }
            catch (EnrollmentException ee)
            {
                logger.ErrorException($"url:{url}, user:{user}  error:{ee.Message}", ee);
                throw;
            }
            catch (Exception e)
            {
                EnrollmentException ee = new EnrollmentException($"Url:{url}, Failed to enroll user {user}", e);
                logger.ErrorException(e.Message, e);
                throw ee;
            }
        }

        /**
         * Return information on the Fabric Certificate Authority.
         * No credentials are needed for this API.
         *
         * @return {@link HFCAInfo}
         * @throws InfoException
         * @throws InvalidArgumentException
         */
        public HFCAInfo Info()
        {
            return InfoAsync().RunAndUnwrap();
        }

        public async Task<HFCAInfo> InfoAsync(CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"info url:{url}");
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            SetUpSSL();
            try
            {
                JObject body = new JObject();
                if (CAName != null)
                    body.Add(new JProperty(FABRIC_CA_REQPROP, CAName));
                string responseBody = await HttpPostAsync(url + HFCA_INFO, body.ToString(), (NetworkCredential) null, token).ConfigureAwait(false);
                logger.Debug("response:" + responseBody);
                JObject jsonst = JObject.Parse(responseBody);
                bool success = jsonst["success"]?.Value<bool>() ?? false;
                logger.Debug($"[HFCAClient] enroll success:[{success}]");
                if (!success)
                    throw new EnrollmentException($"FabricCA failed info {url}");
                JObject result = jsonst["result"] as JObject;
                if (result == null)
                    throw new InfoException($"FabricCA info error  - response did not contain a result url {url}");
                string caName = result["CAName"]?.Value<string>();
                string caChain = result["CAChain"]?.Value<string>();
                string version = null;
                if (result.ContainsKey("Version"))
                    version = result["Version"].Value<string>();
                string issuerPublicKey = result["IssuerPublicKey"]?.Value<string>();
                string issuerRevocationPublicKey = result["IssuerRevocationPublicKey"]?.Value<string>();
                logger.Info($"CA Name: {caName}, Version: {caChain}, issuerPublicKey: {issuerPublicKey}, issuerRevocationPublicKey: {issuerRevocationPublicKey}");
                return new HFCAInfo(caName, caChain, version, issuerPublicKey, issuerRevocationPublicKey);
            }
            catch (Exception e)
            {
                InfoException ee = new InfoException($"Url:{url}, Failed to get info", e);
                logger.ErrorException(e.Message, e);
                throw ee;
            }
        }

        /**
         * Re-Enroll the user with member service
         *
         * @param user User to be re-enrolled
         * @return enrollment
         * @throws EnrollmentException
         * @throws InvalidArgumentException
         */
        public IEnrollment Reenroll(IUser user)
        {
            return Reenroll(user, new EnrollmentRequest());
        }

        public Task<IEnrollment> ReenrollAsync(IUser user, CancellationToken token = default(CancellationToken))
        {
            return ReenrollAsync(user, new EnrollmentRequest(), token);
        }

        /**
         * Re-Enroll the user with member service
         *
         * @param user User to be re-enrolled
         * @param req  Enrollment request with the following fields: hosts, profile, csr, label
         * @return enrollment
         * @throws EnrollmentException
         * @throws InvalidArgumentException
         */
        public IEnrollment Reenroll(IUser user, EnrollmentRequest req)
        {
            return ReenrollAsync(user, req).RunAndUnwrap();
        }

        public async Task<IEnrollment> ReenrollAsync(IUser user, EnrollmentRequest req, CancellationToken token = default(CancellationToken))
        {
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            if (user == null)
                throw new ArgumentException("reenrollment user is missing");
            if (user.Enrollment == null)
                throw new ArgumentException("reenrollment user is not a valid user object");
            logger.Debug($"re-enroll user: {user.Name}, url: {url}");
            try
            {
                SetUpSSL();
                // generate CSR

                string pem = CryptoSuite.GenerateCertificationRequest(user.Name, user.Enrollment.GetKeyPair());
                // build request body
                req.CSR = pem;
                if (!string.IsNullOrEmpty(CAName))
                {
                    req.CAName = CAName;
                }

                string body = req.ToJson();

                // build authentication header
                JObject result = await HttpPostAsync(url + HFCA_REENROLL, body, user, token).ConfigureAwait(false);

                // get new cert from response
                string signedPem = Convert.FromBase64String(result["Cert"]?.Value<string>() ?? "").ToUTF8String();
                logger.Debug($"[HFCAClient] re-enroll returned pem:[{signedPem}]");

                logger.Debug($"reenroll user {user.Name} done.");
                return new X509Enrollment(user.Enrollment.GetKeyPair(), signedPem);
            }
            catch (EnrollmentException ee)
            {
                logger.ErrorException(ee.Message, ee);
                throw;
            }
            catch (Exception e)
            {
                EnrollmentException ee = new EnrollmentException($"Failed to re-enroll user {user}", e);
                logger.ErrorException(e.Message, e);
                throw ee;
            }
        }

        /**
         * revoke one enrollment of user
         *
         * @param revoker    admin user who has revoker attribute configured in CA-server
         * @param enrollment the user enrollment to be revoked
         * @param reason     revoke reason, see RFC 5280
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public void Revoke(IUser revoker, IEnrollment enrollment, string reason)
        {
            RevokeAsync(revoker, enrollment, reason).RunAndUnwrap();
        }

        public Task RevokeAsync(IUser revoker, IEnrollment enrollment, string reason, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, enrollment, reason, false, token);
        }

        /**
         * revoke one enrollment of user
         *
         * @param revoker    admin user who has revoker attribute configured in CA-server
         * @param enrollment the user enrollment to be revoked
         * @param reason     revoke reason, see RFC 5280
         * @param genCRL     generate CRL list
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public string Revoke(IUser revoker, IEnrollment enrollment, string reason, bool genCRL)
        {
            return RevokeAsync(revoker, enrollment, reason, genCRL).RunAndUnwrap();
        }

        public Task<string> RevokeAsync(IUser revoker, IEnrollment enrollment, string reason, bool genCRL, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, enrollment, reason, genCRL, token);
        }

        private async Task<string> RevokeInternalAsync(IUser revoker, IEnrollment enrollment, string reason, bool genCRL, CancellationToken token)
        {
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            if (enrollment == null)
                throw new ArgumentException("revokee enrollment is not set");
            if (revoker == null)
                throw new ArgumentException("revoker is not set");
            logger.Debug($"revoke revoker: {revoker.Name}, reason: {reason}, url: {url}x");
            try
            {
                SetUpSSL();
                // get cert from to-be-revoked enrollment
                Org.BouncyCastle.X509.X509Certificate ncert = Certificate.PEMToX509Certificate(enrollment.Cert);
                // get its serial number
                string serial = ncert.SerialNumber.ToByteArray().ToHexString();
                // get its aki
                // 2.5.29.35 : AuthorityKeyIdentifier
                Asn1OctetString akiOc = ncert.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier);
                string aki = AuthorityKeyIdentifier.GetInstance(Asn1Sequence.GetInstance(akiOc.GetOctets())).GetKeyIdentifier().ToHexString();
                // build request body
                RevocationRequest req = new RevocationRequest(CAName, null, serial, aki, reason, genCRL);
                string body = req.ToJson();
                // send revoke request
                JObject resp = await HttpPostAsync(url + HFCA_REVOKE, body, revoker, token).ConfigureAwait(false);
                logger.Debug("revoke done");
                if (genCRL)
                {
                    if (!resp.HasValues)
                        throw new RevocationException("Failed to return CRL, revoke response is empty");
                    if (!resp.ContainsKey("CRL"))
                        throw new RevocationException("Failed to return CRL");
                    return resp["CRL"]?.Value<string>();
                }

                return null;
            }
            catch (NullReferenceException e)
            {
                logger.Error($"Cannot validate certificate. Error is: {e.Message}");
                throw new RevocationException($"Error while revoking cert. {e.Message}", e);
            }
            catch (CertificateException e)
            {
                logger.Error($"Cannot validate certificate. Error is: {e.Message}");
                throw new RevocationException($"Error while revoking cert. {e.Message}", e);
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new RevocationException($"Error while revoking the user. {e.Message}", e);
            }
        }


        /**
         * revoke one user (including his all enrollments)
         *
         * @param revoker admin user who has revoker attribute configured in CA-server
         * @param revokee user who is to be revoked
         * @param reason  revoke reason, see RFC 5280
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public void Revoke(IUser revoker, string revokee, string reason)
        {
            RevokeAsync(revoker, revokee, reason).RunAndUnwrap();
        }

        public Task RevokeAsync(IUser revoker, string revokee, string reason, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, revokee, reason, false, token);
        }

        /**
         * revoke one user (including his all enrollments)
         *
         * @param revoker admin user who has revoker attribute configured in CA-server
         * @param revokee user who is to be revoked
         * @param reason  revoke reason, see RFC 5280
         * @param genCRL  generate CRL
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public string Revoke(IUser revoker, string revokee, string reason, bool genCRL)
        {
            return RevokeAsync(revoker, revokee, reason, genCRL).RunAndUnwrap();
        }

        public Task<string> RevokeAsync(IUser revoker, string revokee, string reason, bool genCRL, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, revokee, reason, genCRL, token);
        }

        private async Task<string> RevokeInternalAsync(IUser revoker, string revokee, string reason, bool genCRL, CancellationToken token)
        {
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            logger.Debug($"revoke revoker: {revoker}, revokee: {revokee}, reason: {reason}");
            if (string.IsNullOrEmpty(revokee))
                throw new ArgumentException("revokee user is not set");
            if (revoker == null)
                throw new ArgumentException("revoker is not set");
            try
            {
                SetUpSSL();
                // build request body
                RevocationRequest req = new RevocationRequest(CAName, revokee, null, null, reason, genCRL);
                string body = req.ToJson();
                // send revoke request
                JObject resp = await HttpPostAsync(url + HFCA_REVOKE, body, revoker, token).ConfigureAwait(false);
                logger.Debug($"revoke revokee: {revokee} done.");
                if (genCRL)
                {
                    if (!resp.HasValues)
                        throw new RevocationException("Failed to return CRL, revoke response is empty");
                    if (!resp.ContainsKey("CRL"))
                        throw new RevocationException("Failed to return CRL");
                    return resp["CRL"]?.Value<string>();
                }

                return null;
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new RevocationException($"Error while revoking the user. {e.Message}", e);
            }
        }

        /**
         * revoke one certificate
         *
         * @param revoker admin user who has revoker attribute configured in CA-server
         * @param serial  serial number of the certificate to be revoked
         * @param aki     aki of the certificate to be revoke
         * @param reason  revoke reason, see RFC 5280
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public void Revoke(IUser revoker, string serial, string aki, string reason)
        {
            RevokeAsync(revoker, serial, aki, reason).RunAndUnwrap();
        }

        public Task RevokeAsync(IUser revoker, string serial, string aki, string reason, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, serial, aki, reason, false, token);
        }

        /**
         * revoke one enrollment of user
         *
         * @param revoker admin user who has revoker attribute configured in CA-server
         * @param serial  serial number of the certificate to be revoked
         * @param aki     aki of the certificate to be revoke
         * @param reason  revoke reason, see RFC 5280
         * @param genCRL  generate CRL list
         * @throws RevocationException
         * @throws InvalidArgumentException
         */
        public string Revoke(IUser revoker, string serial, string aki, string reason, bool genCRL)
        {
            return RevokeAsync(revoker, serial, aki, reason, genCRL).RunAndUnwrap();
        }

        public Task<string> RevokeAsync(IUser revoker, string serial, string aki, string reason, bool genCRL, CancellationToken token = default(CancellationToken))
        {
            return RevokeInternalAsync(revoker, serial, aki, reason, genCRL, token);
        }

        private async Task<string> RevokeInternalAsync(IUser revoker, string serial, string aki, string reason, bool genCRL, CancellationToken token)
        {
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            if (string.IsNullOrEmpty(serial))
                throw new ArgumentException("Serial number id required to revoke ceritificate");
            if (string.IsNullOrEmpty(aki))
                throw new ArgumentException("AKI is required to revoke certificate");
            if (revoker == null)
                throw new ArgumentException("revoker is not set");
            logger.Debug($"revoke revoker: {revoker.Name}, reason: {reason}, url: {url}");

            try
            {
                SetUpSSL();
                // build request body
                RevocationRequest req = new RevocationRequest(CAName, null, serial, aki, reason, genCRL);
                string body = req.ToJson();
                // send revoke request
                JObject resp = await HttpPostAsync(url + HFCA_REVOKE, body, revoker, token).ConfigureAwait(false);
                logger.Debug("revoke done");
                if (genCRL)
                {
                    if (!resp.HasValues)
                        throw new RevocationException("Failed to return CRL, revoke response is empty");
                    if (!resp.ContainsKey("CRL"))
                        throw new RevocationException("Failed to return CRL");
                    return resp["CRL"]?.Value<string>();
                }

                return null;
            }
            catch (CertificateException e)
            {
                logger.ErrorException($"Cannot validate certificate. Error is: {e.Message}", e);
                throw new RevocationException($"Error while revoking cert. {e.Message}", e);
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new RevocationException($"Error while revoking the user. {e.Message}", e);
            }
        }

        /**
         * Generate certificate revocation list.
         *
         * @param registrar     admin user configured in CA-server
         * @param revokedBefore Restrict certificates returned to revoked before this date if not null.
         * @param revokedAfter  Restrict certificates returned to revoked after this date if not null.
         * @param expireBefore  Restrict certificates returned to expired before this date if not null.
         * @param expireAfter   Restrict certificates returned to expired after this date if not null.
         * @throws InvalidArgumentException
         */

        public string GenerateCRL(IUser registrar, DateTime? revokedBefore, DateTime? revokedAfter, DateTime? expireBefore, DateTime? expireAfter)
        {
            return GenerateCRLAsync(registrar, revokedBefore, revokedAfter, expireBefore, expireAfter).RunAndUnwrap();
        }

        public async Task<string> GenerateCRLAsync(IUser registrar, DateTime? revokedBefore, DateTime? revokedAfter, DateTime? expireBefore, DateTime? expireAfter, CancellationToken token = default(CancellationToken))
        {
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            if (registrar == null)
                throw new ArgumentException("registrar is not set");
            try
            {
                SetUpSSL();
                //---------------------------------------
                JObject o = new JObject();
                if (revokedBefore != null)
                    o.Add(new JProperty("revokedBefore", revokedBefore.Value.ToHyperDate()));
                if (revokedAfter != null)
                    o.Add(new JProperty("revokedAfter", revokedAfter.Value.ToHyperDate()));
                if (expireBefore != null)
                    o.Add(new JProperty("expireBefore", expireBefore.Value.ToHyperDate()));
                if (expireAfter != null)
                    o.Add(new JProperty("expireAfter", expireAfter.Value.ToHyperDate()));
                if (CAName != null)
                    o.Add(new JProperty(FABRIC_CA_REQPROP, CAName));
                string body = o.ToString();
                //---------------------------------------
                // send revoke request
                JObject ret = await HttpPostAsync(url + HFCA_GENCRL, body, registrar, token).ConfigureAwait(false);
                return ret["CRL"]?.Value<string>();
            }
            catch (Exception e)
            {
                logger.ErrorException(e.Message, e);
                throw new GenerateCRLException(e.Message, e);
            }
        }

        /**
         * Creates a new HFCA Identity object
         *
         * @param enrollmentID The enrollment ID associated for this identity
         * @return HFCAIdentity object
         * @throws InvalidArgumentException Invalid (null) argument specified
         */

        public HFCAIdentity NewHFCAIdentity(string enrollmentID)
        {
            return new HFCAIdentity(enrollmentID, this);
        }

        /**
         * gets all identities that the registrar is allowed to see
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return the identity that was requested
         * @throws IdentityException        if adding an identity fails.
         * @throws InvalidArgumentException Invalid (null) argument specified
         */
        public List<HFCAIdentity> GetHFCAIdentities(IUser registrar)
        {
            return GetHFCAIdentitiesAsync(registrar).RunAndUnwrap();
        }

        public async Task<List<HFCAIdentity>> GetHFCAIdentitiesAsync(IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            logger.Debug($"identity  url: {url}, registrar: {registrar.Name}");
            try
            {
                JObject result = await HttpGetAsync(HFCAIdentity.HFCA_IDENTITY, registrar, token).ConfigureAwait(false);
                List<HFCAIdentity> allIdentities = HFCAIdentity.FromJArray(result["identities"] as JArray);
                logger.Debug($"identity  url: {url}, registrar: {registrar.Name} done.");
                return allIdentities;
            }
            catch (HTTPException e)
            {
                string msg = $"[HTTP Status Code: {e.StatusCode}] - Error while getting all users from url '{url}': {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.ErrorException(msg, e);
                throw identityException;
            }
            catch (Exception e)
            {
                string msg = $"Error while getting all users from url '{url}': {e.Message}";
                IdentityException identityException = new IdentityException(msg, e);
                logger.ErrorException(msg, e);
                throw identityException;
            }
        }

        /**
         * @param name Name of the affiliation
         * @return HFCAAffiliation object
         * @throws InvalidArgumentException Invalid (null) argument specified
         */
        public HFCAAffiliation NewHFCAAffiliation(string name)
        {
            return new HFCAAffiliation(name, this);
        }

        /**
         * gets all affiliations that the registrar is allowed to see
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @return The affiliations that were requested
         * @throws AffiliationException     if getting all affiliations fails
         * @throws InvalidArgumentException
         */
        public HFCAAffiliation GetHFCAAffiliations(IUser registrar)
        {
            return GetHFCAAffiliationsAsync(registrar).RunAndUnwrap();
        }

        public async Task<HFCAAffiliation> GetHFCAAffiliationsAsync(IUser registrar, CancellationToken token = default(CancellationToken))
        {
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set.");
            if (registrar == null)
                throw new ArgumentException("Registrar should be a valid member");
            logger.Debug($"affiliations  url: {url}, registrar: {registrar.Name}");
            try
            {
                JObject result = await HttpGetAsync(HFCAAffiliation.HFCA_AFFILIATION, registrar, token).ConfigureAwait(false);
                HFCAAffiliation affiliations = new HFCAAffiliation(result);
                logger.Debug($"affiliations  url: {url}, registrar: {registrar.Name} done.");
                return affiliations;
            }
            catch (HTTPException e)
            {
                string msg = $"[HTTP Status Code: {e.StatusCode}] - Error while getting all affiliations from url '{url}': {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.ErrorException(msg, e);
                throw affiliationException;
            }
            catch (Exception e)
            {
                string msg = $"Error while getting all affiliations from url '{url}': {e.Message}";
                AffiliationException affiliationException = new AffiliationException(msg, e);
                logger.ErrorException(msg, e);
                throw affiliationException;
            }
        }

        /** idemixEnroll returns an Identity Mixer Enrollment, which supports anonymity and unlinkability
         *
         * @param enrollment a x509 enrollment credential
         * @return IdemixEnrollment
         * @throws EnrollmentException
         * @throws InvalidArgumentException
         */
        public IEnrollment IdemixEnroll(IEnrollment enrollment, string mspID)
        {
            return IdemixEnrollAsync(enrollment, mspID).RunAndUnwrap();
        }
        public async Task<IEnrollment> IdemixEnrollAsync(IEnrollment enrollment, string mspID, CancellationToken token=default(CancellationToken))
        {
            if (CryptoSuite == null)
                throw new ArgumentException("Crypto primitives not set");
            if (enrollment == null)
                throw new ArgumentException("enrollment is missing");
            if (string.IsNullOrEmpty(mspID))
                throw new ArgumentException("mspID cannot be null or empty");
            if (enrollment is IdemixEnrollment)
                throw new ArgumentException("enrollment type must be x509");

            RAND rng = IdemixUtils.GetRand();
            try
            {
                SetUpSSL();

                // Get nonce
                IdemixEnrollmentRequest idemixEnrollReq = new IdemixEnrollmentRequest();
                string body = idemixEnrollReq.ToJson();
                JObject result = await HttpPostAsync(url + HFCA_IDEMIXCRED, body, enrollment, token).ConfigureAwait(false);
                if (result == null)
                    throw new EnrollmentException("No response received for idemix enrollment request");
                string nonceString = result["Nonce"]?.Value<string>();
                if (string.IsNullOrEmpty(nonceString))
                    throw new ArgumentException("fabric-ca-server did not return a nonce in the response from " + HFCA_IDEMIXCRED);

                byte[] nonceBytes = Convert.FromBase64String(nonceString);

                BIG nonce = BIG.FromBytes(nonceBytes);

                // Get issuer public key and revocation key from the cainfo section of response
                JObject info = result["CAInfo"] as JObject;
                if (info == null)
                    throw new Exception("fabric-ca-server did not return 'cainfo' in the response from " + HFCA_IDEMIXCRED);

                IdemixIssuerPublicKey ipk = GetIssuerPublicKey(info["IssuerPublicKey"]?.Value<string>());
                KeyPair rpk = GetRevocationPublicKey(info["IssuerRevocationPublicKey"]?.Value<string>());

                // Create and send idemix credential request
                BIG sk = new BIG(rng.RandModOrder());
                IdemixCredRequest idemixCredRequest = new IdemixCredRequest(sk, nonce, ipk);
                idemixEnrollReq.IdemixCredReq = idemixCredRequest;
                body = idemixEnrollReq.ToJson();
                result = await HttpPostAsync(url + HFCA_IDEMIXCRED, body, enrollment, token).ConfigureAwait(false);
                if (result == null)
                    throw new EnrollmentException("No response received for idemix enrollment request");


                // Deserialize idemix credential
                string credential = result["Credential"]?.Value<string>();
                if (string.IsNullOrEmpty(credential))
                    throw new ArgumentException("fabric-ca-server did not return a 'credential' in the response from " + HFCA_IDEMIXCRED);

                byte[] credBytes = Convert.FromBase64String(credential);
                Credential credProto = Credential.Parser.ParseFrom(credBytes);
                IdemixCredential cred = new IdemixCredential(credProto);

                // Deserialize idemix cri (Credential Revocation Information)
                string criStr = result["CRI"]?.Value<string>();
                if (string.IsNullOrEmpty(criStr))
                    throw new ArgumentException("fabric-ca-server did not return a 'CRI' in the response from " + HFCA_IDEMIXCRED);

                byte[] criBytes = Convert.FromBase64String(criStr);
                CredentialRevocationInformation cri = CredentialRevocationInformation.Parser.ParseFrom(criBytes);

                JObject attrs = result["Attrs"] as JObject;
                if (attrs == null)
                    throw new EnrollmentException("fabric-ca-server did not return 'attrs' in the response from " + HFCA_IDEMIXCRED);

                string ou = attrs["OU"]?.Value<string>();
                if (string.IsNullOrEmpty(ou))
                    throw new ArgumentException("fabric-ca-server did not return a 'ou' attribute in the response from " + HFCA_IDEMIXCRED);
                int? role = attrs["Role"]?.Value<int>(); // Encoded IdemixRole from Fabric-Ca
                if (!role.HasValue)
                    throw new ArgumentException("fabric-ca-server did not return a 'role' attribute in the response from " + HFCA_IDEMIXCRED);

                // Return the idemix enrollment
                return new IdemixEnrollment(ipk, rpk, mspID, sk, cred, cri, ou, (IdemixRoles) role.Value);
            }
            catch (EnrollmentException ee)
            {
                logger.Error(ee.Message, ee);
                throw ee;
            }
            catch (Exception e)
            {
                EnrollmentException ee = new EnrollmentException("Failed to get Idemix credential", e);
                logger.Error(ee.Message, e);
                throw ee;
            }
        }

        private IdemixIssuerPublicKey GetIssuerPublicKey(string str)
        {
            if (string.IsNullOrEmpty(str))
                throw new EnrollmentException("fabric-ca-server did not return 'issuerPublicKey' in the response from " + HFCA_IDEMIXCRED);

            byte[] ipkBytes = Convert.FromBase64String(str);
            IssuerPublicKey ipkProto = IssuerPublicKey.Parser.ParseFrom(ipkBytes);
            return new IdemixIssuerPublicKey(ipkProto);
        }

        private KeyPair GetRevocationPublicKey(string str)
        {
            if (string.IsNullOrEmpty(str))
                throw new EnrollmentException("fabric-ca-server did not return 'issuerPublicKey' in the response from " + HFCA_IDEMIXCRED);
            AsymmetricKeyParameter pub = PublicKeyFactory.CreateKey(KeyPair.PemToDer(Convert.FromBase64String(str).ToUTF8String()));
            return KeyPair.Create(pub, null);
        }
        /* 
        private byte[] ConvertPemToDer(string pem) 
        {
            PemReader pemReader = new PemReader(new StringReader(pem));
            return pemReader.ReadPemObject().Content;
        }*/


        /**
         * @return HFCACertificateRequest object
         */
        public HFCACertificateRequest NewHFCACertificateRequest()
        {
            return new HFCACertificateRequest();
        }

        /**
         * Gets all certificates that the registrar is allowed to see and based on filter parameters that
         * are part of the certificate request.
         *
         * @param registrar The identity of the registrar (i.e. who is performing the registration).
         * @param req The certificate request that contains filter parameters
         * @return HFCACertificateResponse object
         * @throws HFCACertificateException Failed to process get certificate request
         */
        public HFCACertificateResponse GetHFCACertificates(IUser registrar, HFCACertificateRequest req)
        {
            return GetHFCACertificatesAsync(registrar, req).RunAndUnwrap();
        }

        public async Task<HFCACertificateResponse> GetHFCACertificatesAsync(IUser registrar, HFCACertificateRequest req, CancellationToken token = default(CancellationToken))
        {
            try
            {
                logger.Debug($"certificate url: {HFCA_CERTIFICATE}, registrar: {registrar.Name}");

                JObject result = await HttpGetAsync(HFCA_CERTIFICATE, registrar, req.QueryParameters, token).ConfigureAwait(false);
                int statusCode = result["statusCode"].Value<int>();
                List<HFCACredential> certs = new List<HFCACredential>();
                if (statusCode < 400)
                {
                    JArray certificates = result["certs"] as JArray;
                    if (certificates != null && certificates.Count > 0)
                    {
                        foreach (JToken j in certificates)
                        {
                            string certPEM = j["PEM"].Value<string>();
                            certs.Add(new HFCAX509Certificate(certPEM));
                        }
                    }

                    logger.Debug($"certificate url: {HFCA_CERTIFICATE}, registrar: {registrar} done.");
                }

                return new HFCACertificateResponse(statusCode, certs);
            }
            catch (HTTPException e)
            {
                string msg = $"[Code: {e.StatusCode}] - Error while getting certificates from url '{HFCA_CERTIFICATE}': {e.Message}";
                HFCACertificateException certificateException = new HFCACertificateException(msg, e);
                logger.Error(msg);
                throw certificateException;
            }
            catch (Exception e)
            {
                string msg = $"Error while getting certificates from url '{HFCA_CERTIFICATE}': {e.Message}";
                HFCACertificateException certificateException = new HFCACertificateException(msg, e);
                logger.Error(msg);
                throw certificateException;
            }
        }

        internal void SetUpSSL()
        {
            if (CryptoSuite == null)
            {
                try
                {
                    CryptoSuite = Factory.GetCryptoSuite();
                }
                catch (Exception e)
                {
                    throw new ArgumentException(e.Message, e);
                }
            }

            if (isSSL && null == caStore)
            {
                if (!properties.Contains("pemBytes") && !properties.Contains("pemFile"))
                    logger.Warn("SSL with no CA certficates in either pemBytes or pemFile");
                try
                {
                    if (properties.Contains("pemBytes"))
                    {
                        CryptoSuite.Store.AddCertificate(properties["pemBytes"]);
                    }

                    if (properties.Contains("pemFile"))
                    {
                        string pemFile = properties["pemFile"];
                        if (!string.IsNullOrEmpty(pemFile))
                        {
                            Regex pattern = new Regex("[ \t]*,[ \t]*");
                            string[] pems = pattern.Split(pemFile);
                            foreach (string pem in pems)
                            {
                                if (!string.IsNullOrEmpty(pem))
                                {
                                    string fname = Path.GetFullPath(pem);
                                    try
                                    {
                                        CryptoSuite.Store.AddCertificate(File.ReadAllText(fname));
                                    }
                                    catch (IOException)
                                    {
                                        throw new ArgumentException($"Unable to add CA certificate, can't open certificate file {pem}");
                                    }
                                }
                            }
                        }
                    }

                    caStore = CryptoSuite.Store;
                }
                catch (Exception e)
                {
                    logger.ErrorException(e.Message, e);
                    throw new ArgumentException(e.Message, e);
                }
            }
        }

        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
            if (caStore == null)
                return false;
            return caStore.Validate(Certificate.Create(new X509Certificate2(certificate)));
        }

        /**
         * Http Post Request.
         *
         * @param url         Target URL to POST to.
         * @param body        Body to be sent with the post.
         * @param credentials Credentials to use for basic auth.
         * @return Body of post returned.
         * @throws Exception
         */

        public string HttpPost(string murl, string body, NetworkCredential credentials)
        {
            return HttpPostAsync(murl, body, credentials).RunAndUnwrap();
        }


        public virtual async Task<string> HttpPostAsync(string murl, string body, NetworkCredential credentials, CancellationToken token = default(CancellationToken))
        {
            logger.Debug($"httpPost {murl}, body:{body}");
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback += ValidateServerCertificate;
            handler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            using (HttpClient client = new HttpClient(handler, true))
            {
                long timeout = CONNECTION_REQUEST_TIMEOUT;
                if (timeout > 0)
                    client.Timeout = TimeSpan.FromMilliseconds(timeout);
                //url = url.Replace("localhost", "localhost.fiddler");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, murl);
                if (credentials != null)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials.UserName + ":" + credentials.Password)));
                if (!string.IsNullOrEmpty(body))
                {
                    request.Content = new StringContent(body, Encoding.UTF8);
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                }

                logger.Trace($"httpPost {murl}  sending...");
                HttpResponseMessage msg = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
                string result = await msg.Content.ReadAsStringAsync().ConfigureAwait(false);
                logger.Trace($"httpPost {murl}  responseBody {result}");
                int status = (int) msg.StatusCode;
                if (status >= 400)
                {
                    Exception e = new Exception($"POST request to {murl}  with request body: {body ?? ""}, failed with status code: {status}. Response: {result ?? msg.ReasonPhrase}");
                    logger.ErrorException(e.Message, e);
                    throw e;
                }

                logger.Debug($"httpPost Status: {status} returning: {result}");
                return result;
            }
        }

        public JObject HttpPost(string murl, string body, IUser registrar)
        {
            return HttpPostAsync(murl, body, registrar).RunAndUnwrap();
        }

        public virtual Task<JObject> HttpPostAsync(string murl, string body, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            return HttpVerbAsync(murl, "POST", body, registrar.Enrollment, token);
        }

        public JObject HttpPost(string murl, string body, IEnrollment enrollment)
        {
            return HttpPostAsync(murl, body, enrollment).RunAndUnwrap();
        }

        public virtual Task<JObject> HttpPostAsync(string murl, string body, IEnrollment enrollment, CancellationToken token = default(CancellationToken))
        {
            return HttpVerbAsync(murl, "POST", body, enrollment, token);
        }

        private async Task<JObject> HttpVerbAsync(string murl, string verb, string body, IEnrollment enrollment, CancellationToken token)
        {
            string authHTTPCert = GetHTTPAuthCertificate(enrollment, verb, murl, body);
            murl = AddCAToURL(murl);
            logger.Debug($"http{verb} {murl}, body:{body}, authHTTPCert: {authHTTPCert}");
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback += ValidateServerCertificate;
            handler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            using (HttpClient client = new HttpClient(handler, true))
            {
                long timeout = CONNECTION_REQUEST_TIMEOUT;
                if (timeout > 0)
                    client.Timeout = TimeSpan.FromMilliseconds(timeout);
                HttpMethod method;
                switch (verb)
                {
                    case "GET":
                        method = HttpMethod.Get;
                        break;
                    case "POST":
                        method = HttpMethod.Post;
                        break;
                    case "PUT":
                        method = HttpMethod.Put;
                        break;
                    case "DELETE":
                        method = HttpMethod.Delete;
                        break;
                    default:
                        method = new HttpMethod(verb);
                        break;
                }
                //url = url.Replace("localhost", "localhost.fiddler");

                HttpRequestMessage request = new HttpRequestMessage(method, murl);
                request.Headers.TryAddWithoutValidation("Authorization", authHTTPCert);
                if (!string.IsNullOrEmpty(body))
                {
                    request.Content = new StringContent(body, Encoding.UTF8);
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                }

                HttpResponseMessage msg = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false);
                return await GetResultAsync(msg, body, verb).ConfigureAwait(false);
            }
        }

        public JObject HttpGet(string murl, IUser registrar)
        {
            return HttpGetAsync(murl, registrar).RunAndUnwrap();
        }

        public Task<JObject> HttpGetAsync(string murl, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            murl = GetURL(murl);
            return HttpVerbAsync(murl, "GET", "", registrar.Enrollment, token);
        }

        public JObject HttpGet(string murl, IUser registrar, IDictionary<string, string> querymap)
        {
            return HttpGetAsync(murl, registrar, querymap).RunAndUnwrap();
        }

        public Task<JObject> HttpGetAsync(string murl, IUser registrar, IDictionary<string, string> querymap, CancellationToken token = default(CancellationToken))
        {
            murl = GetURL(murl, querymap);
            return HttpVerbAsync(murl, "GET", "", registrar.Enrollment, token);
        }


        public JObject HttpPut(string murl, string body, IUser registrar)
        {
            return HttpPutAsync(murl, body, registrar).RunAndUnwrap();
        }

        public Task<JObject> HttpPutAsync(string murl, string body, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            return HttpVerbAsync(murl, "PUT", body, registrar.Enrollment, token);
        }

        public JObject HttpDelete(string murl, IUser registrar)
        {
            return HttpDeleteAsync(murl, registrar).RunAndUnwrap();
        }

        public Task<JObject> HttpDeleteAsync(string murl, IUser registrar, CancellationToken token = default(CancellationToken))
        {
            return HttpVerbAsync(murl, "DELETE", "", registrar.Enrollment, token);
        }

        private async Task<JObject> GetResultAsync(HttpResponseMessage response, string body, string type)
        {
            int respStatusCode = (int) response.StatusCode;
            logger.Trace($"response status {respStatusCode}, Phrase {response.ReasonPhrase ?? ""}");
            string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            logger.Trace($"responseBody: {responseBody}");

            // If the status code in the response is greater or equal to the status code set in the client object then an exception will
            // be thrown, otherwise, we continue to read the response and return any error code that is less than 'statusCode'
            if (respStatusCode >= StatusCode)
            {
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body}. Response: {responseBody}", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }

            if (responseBody == null)
            {
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body} with null response body returned.", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }

            logger.Debug("Status: " + respStatusCode);
            JObject jobj = JObject.Parse(responseBody);
            JObject job = new JObject();
            job.Add(new JProperty("statusCode", respStatusCode));
            JArray errors = jobj["errors"] as JArray;
            // If the status code is greater than or equal to 400 but less than or equal to the client status code setting,
            // then encountered an error and we return back the status code, and log the error rather than throwing an exception.
            if (respStatusCode < StatusCode && respStatusCode >= 400)
            {
                if (errors != null && errors.Count > 0)
                {
                    JObject jo = (JObject) errors.First;
                    string errorMsg = $"[HTTP Status Code: {respStatusCode}] - {type} request to {url} failed request body {body} error message: [Error Code {jo["code"]?.Value<int>()}] - {jo["message"].Value<string>()}";
                    logger.Error(errorMsg);
                }

                return job;
            }

            if (errors != null && errors.Count > 0)
            {
                JObject jo = (JObject) errors.First;
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body} error message: [Error Code {jo["code"]?.Value<int>() ?? 0}] - {jo["message"]?.Value<string>() ?? ""}", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }

            bool success = jobj["success"]?.Value<bool>() ?? false;
            if (!success)
            {
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body} Body of response did not contain success", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }

            JObject result = jobj["result"] as JObject;
            if (result == null)
            {
                HTTPException e = new HTTPException($"{type} request to {url} failed request body {body} Body of response did not contain result", respStatusCode);
                logger.ErrorException(e.Message, e);
                throw e;
            }

            JArray messages = jobj["messages"] as JArray;
            if (messages != null && messages.Count > 0)
            {
                JObject jo = (JObject) messages.First;
                string message = $"{type} request to {url} failed request body {body} response message: [Error Code {jo["code"]?.Value<int>() ?? 0}] - {jo["message"]?.Value<string>() ?? ""}";
                logger.Info(message);
            }

            // Construct JSON object that contains the result and HTTP status code
            foreach (JToken prop in result.Children())
            {
                job.Add(prop);
            }

            logger.Debug($"{type} {url}, body:{body} result: {job}");
            return job;
        }

        private string GetHTTPAuthCertificate(IEnrollment enrollment, string method, string uurl, string body)
        {
            string cert = Convert.ToBase64String(enrollment.Cert.ToBytes());
            body = Convert.ToBase64String(body.ToBytes());
            string signString;
            // Cache the version, so don't need to make info call everytime the same client is used
            if (newPayloadType == null)
            {
                newPayloadType = true;

                // If CA version is less than 1.4.0, use old payload
                string caVersion = Info().Version;
                logger.Info($"CA Version: {caVersion ?? ""}");

                if (string.IsNullOrEmpty(caVersion))
                    newPayloadType = false;

                string version = caVersion + ".";
                if (version.StartsWith("1.1.") || version.StartsWith("1.2.") || version.StartsWith("1.3."))
                    newPayloadType = false;
            }

            // ReSharper disable once PossibleInvalidOperationException
            if (newPayloadType.Value)
            {
                uurl = AddCAToURL(uurl);
                string file = Convert.ToBase64String(new Uri(uurl).PathAndQuery.ToBytes());
                signString = method + "." + file + "." + body + "." + cert;
            }
            else
            {
                signString = body + "." + cert;
            }

            byte[] signature = CryptoSuite.Sign(enrollment.GetKeyPair(), signString.ToBytes());
            return cert + "." + Convert.ToBase64String(signature);
        }

        public string AddCAToURL(string uurl)
        {
            if (CAName != null && !uurl.Contains("?ca=") && !uurl.Contains("&ca="))
                return AddQueryValue(uurl, "ca", CAName);
            return uurl;
        }


        public string GetURL(string endpoint)
        {
            return GetURL(endpoint, null);
        }

        private string AddQueryValue(string urll, string name, string value)
        {
            if (urll.Contains("?"))
                urll += "&" + name + "=" + HttpUtility.UrlEncode(value);
            else
                urll += "?" + name + "=" + HttpUtility.UrlEncode(value);
            return urll;
        }

        public string GetURL(string endpoint, IDictionary<string, string> queryMap)
        {
            SetUpSSL();
            string murl = AddCAToURL(url + endpoint);
            if (queryMap != null)
            {
                foreach (string key in queryMap.Keys)
                {
                    if (!string.IsNullOrEmpty(queryMap[key]))
                        murl = AddQueryValue(murl, key, queryMap[key]);
                }
            }

            return murl;
        }

        // Convert the identity request to a JSON string
        public string ToJson(JObject toJsonFunc)
        {
            return toJsonFunc.ToString(Formatting.None);
        }
    }
}