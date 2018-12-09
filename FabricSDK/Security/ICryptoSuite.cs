/*
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *   http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Security
{
    /**
     * All packages for PKI key creation/signing/verification implement this interface
     */
    public interface ICryptoSuite
    {
        KeyStore Store { get; set; }

        /**
         * Get Crypto Suite Factory for this implementation.
         *
         * @return MUST return the one and only one instance of a factory that produced this crypto suite.
         */

        ICryptoSuiteFactory GetCryptoSuiteFactory();

        /**
         * @return the {@link Properties} object containing implementation specific key generation properties
         */
        Properties GetProperties();


        /**
         * Set the Certificate Authority certificates to be used when validating a certificate chain of trust
         *
         * @param certificates A collection of {@link java.security.cert.Certificate}s
         * @throws CryptoException
         */
        //void LoadCACertificates(List<X509Certificate2> certificates);

        /**
         * Set the Certificate Authority certificates to be used when validating a certificate chain of trust.
         *
         * @param certificates a collection of certificates in PEM format
         * @throws CryptoException
         */
        //void LoadCACertificatesAsBytes(List<byte[]> certificates);

        /**
         * Generate a key.
         *
         * @return the generated key.
         * @throws CryptoException
         */
        KeyPair KeyGen();

        /**
         * Sign the specified byte string.
         *
         * @param key       the {@link java.security.PrivateKey} to be used for signing
         * @param plainText the byte string to sign
         * @return the signed data.
         * @throws CryptoException
         */
        byte[] Sign(KeyPair key, byte[] plainText);

        /**
         * Verify the specified signature
         *
         * @param certificate        the certificate of the signer as the contents of the PEM file
         * @param signatureAlgorithm the algorithm used to create the signature.
         * @param signature          the signature to verify
         * @param plainText          the original text that is to be verified
         * @return {@code true} if the signature is successfully verified; otherwise {@code false}.
         * @throws CryptoException
         */
        bool Verify(byte[] certificate, string signatureAlgorithm, byte[] signature, byte[] plainText);

        /**
         * Hash the specified text byte data.
         *
         * @param plainText the text to hash
         * @return the hashed data.
         */
        byte[] Hash(byte[] plainText);

        /**
         * Generates a CertificationRequest
         *
         * @param user
         * @param keypair
         * @return String in PEM format for certificate request.
         * @throws InvalidIllegalArgumentException
         */
        string GenerateCertificationRequest(string user, KeyPair keypair);

        /**
         * Convert bytes in PEM format to Certificate.
         *
         * @param certBytes
         * @return Certificate
         * @throws CryptoException
         */
        //X509Certificate2 BytesToCertificate(byte[] certBytes);

        /**
         * The CryptoSuite factory. Currently {@link #getCryptoSuite} will always
         * give you a {@link CryptoPrimitives} object
         */
    }

    public class Factory
    {
        private static ICryptoSuiteFactory _instance;

        private Factory()
        {
        }

        public static ICryptoSuiteFactory Instance => _instance ?? (_instance = new HLSDKJCryptoSuiteFactory());

        /**
         * Get a crypto suite with the default factory with default settings.
         * Settings which can define such parameters such as curve strength, are specific to the crypto factory.
         *
         * @return Default crypto suite.
         * @throws IllegalAccessException
         * @throws InstantiationException
         * @throws ClassNotFoundException
         * @throws CryptoException
         * @throws InvalidIllegalArgumentException
         * @throws NoSuchMethodException
         * @throws InvocationTargetException
         */

        public static ICryptoSuite GetCryptoSuite()
        {
            return Instance.GetCryptoSuite();
        }

        /**
         * Get a crypto suite with the default factory with settings defined by properties
         * Properties are uniquely defined by the specific crypto factory.
         *
         * @param properties properties that define suite characteristics such as strength, curve, hashing .
         * @return
         * @throws IllegalAccessException
         * @throws InstantiationException
         * @throws ClassNotFoundException
         * @throws CryptoException
         * @throws InvalidIllegalArgumentException
         * @throws NoSuchMethodException
         * @throws InvocationTargetException
         */
        public static ICryptoSuite GetCryptoSuite(Properties properties)
        {
            return Instance.GetCryptoSuite(properties);
        }
    }
}