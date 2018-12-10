using System;
using System.Collections.Generic;
using System.Linq;
using Hyperledger.Fabric.SDK.Helper;

// ReSharper disable EmptyConstructor

namespace Hyperledger.Fabric_CA.SDK.Requests
{
    /**
     * Request to the Fabric CA server to get certificates
     * based on filter parameters
     */
    public class HFCACertificateRequest
    {
        private readonly Dictionary<string, string> queryParms = new Dictionary<string, string>();

        /**
         * Get certificate request from Fabric CA server
         */
        public HFCACertificateRequest()
        {
        }

        /**
         * Get certificates for this enrollment ID
         *
         * @param enrollmentID Enrollment ID associated with the certificate(s)
         */
        public string EnrollementID
        {
            get => queryParms.GetOrNull("id");
            set => queryParms["id"] = value;
        }


        /**
         * Get certificates for this serial number
         *
         * @param serial Serial Number of the certificate
         */
        public string Serial
        {
            get => queryParms.GetOrNull("serial");
            set => queryParms["serial"] = value;
        }

        /**
         * Get certificates for this aki
         *
         * @param aki AKI of the certificate(s)
         */
        public string Aki
        {
            get => queryParms.GetOrNull("aki");
            set => queryParms["aki"] = value;
        }

        /**
         * Get certificates that have been revoked after this date
         *
         * @param revokedStart Revoked after date
         * @throws InvalidArgumentException Date can't be null
         */
        public DateTime RevokedStart
        {
            get => queryParms.GetOrNull("revoked_start")?.ToHyperDate() ?? new DateTime();
            set => queryParms["revoked_start"] = value.ToHyperDate();
        }


        /**
         * Get certificates that have been revoked before this date
         *
         * @param revokedEnd Revoked before date
         * @throws InvalidArgumentException Date can't be null
         */
        public DateTime RevokedEnd
        {
            get => queryParms.GetOrNull("revoked_end")?.ToHyperDate() ?? new DateTime();
            set => queryParms["revoked_end"] = value.ToHyperDate();
        }

        /**
         * Get certificates that have expired after this date
         *
         * @param expiredStart Expired after date
         * @throws InvalidArgumentException Date can't be null
         */
        public DateTime ExpiredStart
        {
            get => queryParms.GetOrNull("expired_start")?.ToHyperDate() ?? new DateTime();
            set => queryParms["expired_start"] = value.ToHyperDate();
        }

        /**
         * Get certificates that have expired before this date
         *
         * @param expiredEnd Expired end date
         * @throws InvalidArgumentException Date can't be null
         */
        public DateTime ExpiredEnd
        {
            get => queryParms.GetOrNull("expired_end")?.ToHyperDate() ?? new DateTime();
            set => queryParms["expired_end"] = value.ToHyperDate();
        }

        /**
         * Get certificates that include/exclude expired certificates
         *
         * @param expired Boolean indicating if expired certificates should be excluded
         */
        public bool Expired
        {
            get => !bool.Parse(queryParms.GetOrNull("notexpired") ?? "true");
            set => queryParms["notexpired"] = value ? "false" : "true";
        }

        /**
         * Get certificates that include/exclude revoked certificates
         *
         * @param revoked Boolean indicating if revoked certificates should excluded
         */
        public bool Revoked
        {
            get => !bool.Parse(queryParms.GetOrNull("notrevoked") ?? "true");
            set => queryParms["notrevoked"] = value ? "false" : "true";
        }

        /**
         * Get all the filter parameters for this certificate request
         *
         * @return A map of filters that will be used as query parameters in GET request
         */
        public Dictionary<string, string> QueryParameters => queryParms.ToDictionary(a=>a.Key,a=>a.Value);
    }
}