using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Security
{
    [TestClass]
    [TestCategory("SDK")]
    public class TLSCertGenTest
    {
        [TestMethod]
        public void SelfSignedTLSCertTest()
        {
            bool handshakeOccured = false;
            Server sv = new Server();
            TLSCertificateKeyPair serverCert = TLSCertificateKeyPair.CreateServerCert("localhost");
            TLSCertificateKeyPair clientCert = TLSCertificateKeyPair.CreateClientCert();
            KeyCertificatePair pair = new KeyCertificatePair(serverCert.CertPEMBytes.ToUTF8String(), serverCert.KeyPEMBytes.ToUTF8String());
            ServerCredentials credentials = new SslServerCredentials(new[] {pair}, clientCert.CertPEMBytes.ToUTF8String(), true);
            sv.Ports.Add(new ServerPort("localhost", 0, credentials));
            sv.Services.Add(Endorser.BindService(new MockEndorser()).Intercept(new MultalTLSInterceptor(clientCert.CertPEMBytes, (b) => handshakeOccured = b)));
            sv.Start();
            int port = sv.Ports.First().BoundPort;
            ChannelCredentials cred = new SslCredentials(serverCert.CertPEMBytes.ToUTF8String(), new KeyCertificatePair(clientCert.CertPEMBytes.ToUTF8String(), clientCert.KeyPEMBytes.ToUTF8String()));
            Channel chan = new Channel("localhost", port, cred);
            SignedProposal prop = new SignedProposal();
            Endorser.EndorserClient cl = new Endorser.EndorserClient(chan);
            cl.ProcessProposal(prop);
            Assert.IsTrue(handshakeOccured, "Handshake didn't occur");
            chan.ShutdownAsync().RunAndUnwarp();
            sv.ShutdownAsync().RunAndUnwarp();
        }

        private class MockEndorser : Endorser.EndorserBase
        {
            public override Task<ProposalResponse> ProcessProposal(SignedProposal request, ServerCallContext context)
            {
                return Task.FromResult(new ProposalResponse());
            }
        }


        private class MultalTLSInterceptor : Interceptor
        {
            private readonly byte[] expectedClientCert;
            private readonly Action<bool> toggleHandshakeFunction;

            public MultalTLSInterceptor(byte[] clientCert, Action<bool> toggleHandshakefunc)
            {
                expectedClientCert = clientCert;
                toggleHandshakeFunction = toggleHandshakefunc;
            }

            public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
            {
                try
                {
                    byte[] certChain = context.AuthContext.Properties.ToList().FirstOrDefault(a => a.Name == "x509_pem_cert")?.ValueBytes;
                    Assert.IsFalse(certChain == null || certChain.Length == 0, "Client didn't send TLS certificate");
                    byte[] certRAW = Certificate.ExtractDER(certChain.ToUTF8String());
                    byte[] origRAW = Certificate.ExtractDER(expectedClientCert.ToUTF8String());
                    bool equalCerts = StructuralComparisons.StructuralEqualityComparer.Equals(certRAW, origRAW);
                    Assert.IsTrue(equalCerts, "Expected certificate doesn't match actual");
                    toggleHandshakeFunction(true);
                }
                catch (System.Exception e)
                {
                    Assert.Fail($"Uncaught exception: {e.Message}");
                }

                return base.UnaryServerHandler(request, context, continuation);
            }
        }
    }
}