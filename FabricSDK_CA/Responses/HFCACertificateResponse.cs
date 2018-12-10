using System.Collections.Generic;
using System.Linq;

namespace Hyperledger.Fabric_CA.SDK.Responses
{
    public class HFCACertificateResponse
    {
        private readonly List<HFCACredential> certs;

        /**
         * Contains the response from the server with status code and credentials requested
         *
         * @param statusCode Status code of the HTTP request
         * @param certs The certificates return from the GET request
         */
        public HFCACertificateResponse(int statusCode, IEnumerable<HFCACredential> certs)
        {
            StatusCode = statusCode;
            this.certs = certs.ToList();
        }

        /**
         * Returns the status code of the request
         *
         * @return HTTP status code
         */
        public int StatusCode { get; }

        /**
         * Returns the certificates that were retrieved from the GET certificate request
         *
         * @return Certificates
         */
        public IReadOnlyCollection<HFCACredential> Certs => certs;
    }
}