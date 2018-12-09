/*
 *  Copyright 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

using System;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.Requests;

namespace Hyperledger.Fabric.SDK.Builders
{
    public class InstallProposalBuilder : LSCCProposalBuilder
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(InstantiateProposalBuilder));
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();
        private readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL ? Config.Instance.GetDiagnosticFileDumper() : null;
        protected string action = "install";
        private Stream chaincodeInputStream;
        private TransactionRequest.Type chaincodeLanguage;
        private string chaincodeMetaInfLocation;
        private string chaincodeName;
        private string chaincodePath;
        private string chaincodeSource;
        private string chaincodeVersion;

        protected InstallProposalBuilder()
        {
        }

        public new static InstallProposalBuilder Create()
        {
            return new InstallProposalBuilder();
        }

        public InstallProposalBuilder ChaincodePath(string ccodePath)
        {
            chaincodePath = ccodePath;

            return this;
        }

        public InstallProposalBuilder ChaincodeName(string ccodeName)
        {
            chaincodeName = ccodeName;

            return this;
        }

        public InstallProposalBuilder ChaincodeSource(string ccodeLocation)
        {
            chaincodeSource = ccodeLocation;

            return this;
        }

        public InstallProposalBuilder ChaincodeMetaInfLocation(string ccodeMetaInfLocation)
        {
            chaincodeMetaInfLocation = ccodeMetaInfLocation;
            return this;
        }


        public override Proposal Build()
        {
            ConstructInstallProposal();
            return base.Build();
        }

        private void ConstructInstallProposal()
        {
            try
            {
                CreateNetModeTransaction();
            }
            catch (IOException exp)
            {
                logger.ErrorException(exp.Message, exp);
                throw new ProposalException("IO Error while creating install proposal", exp);
            }
        }

        private void CreateNetModeTransaction()
        {
            logger.Debug("createNetModeTransaction");
            if (null == chaincodeSource && chaincodeInputStream == null)
                throw new ArgumentException("Missing chaincodeSource or chaincodeInputStream in InstallRequest");
            if (null != chaincodeSource && chaincodeInputStream != null)
                throw new ArgumentException("Both chaincodeSource and chaincodeInputStream in InstallRequest were set. Specify one or the other");
            ChaincodeSpec.Types.Type ctyp;
            string projectSourceDir = null;
            string targetPathPrefix = null;
            string dplang;
            string metainf = null;
            if (null != chaincodeMetaInfLocation)
            {
                if (!Directory.Exists(chaincodeMetaInfLocation))
                    throw new ArgumentException($"Directory to find chaincode META-INF {chaincodeMetaInfLocation} does not exist");
                /*
                if (!chaincodeMetaInfLocation==null) {
                    throw new IllegalArgumentException(format("Directory to find chaincode META-INF %s is not a directory", chaincodeMetaInfLocation.getAbsolutePath()));
                }*/
                metainf = Path.Combine(chaincodeMetaInfLocation, "META-INF");
                logger.Trace($"META-INF directory is {metainf}");
                if (!Directory.Exists(metainf))
                    throw new ArgumentException($"The META-INF directory does not exist in {chaincodeMetaInfLocation}");
                string[] files = Directory.GetFileSystemEntries(metainf).ToArray();
                /*
                if (files == null) {
                    throw new IllegalArgumentException("null for listFiles on: " + chaincodeMetaInfLocation.getAbsolutePath());
                }
                */
                if (files.Length < 1)
                    throw new ArgumentException($"The META-INF directory {metainf} is empty.");
                logger.Trace($"chaincode META-INF found {metainf}");
            }

            switch (chaincodeLanguage)
            {
                case TransactionRequest.Type.GO_LANG:
                    // chaincodePath is mandatory
                    // chaincodeSource may be a File or InputStream
                    //   Verify that chaincodePath is being passed
                    if (string.IsNullOrEmpty(chaincodePath))
                        throw new ArgumentException("Missing chaincodePath in InstallRequest");
                    dplang = "Go";
                    ctyp = ChaincodeSpec.Types.Type.Golang;
                    if (null != chaincodeSource)
                    {
                        projectSourceDir = Path.Combine(chaincodeSource, "src", chaincodePath);
                        targetPathPrefix = Path.Combine("src", chaincodePath);
                    }

                    break;

                case TransactionRequest.Type.JAVA:

                    // chaincodePath is not applicable and must be null
                    // chaincodeSource may be a File or InputStream
                    //   Verify that chaincodePath is null
                    if (!string.IsNullOrEmpty(chaincodePath))
                        throw new ArgumentException("chaincodePath must be null for Java chaincode");
                    dplang = "Java";
                    ctyp = ChaincodeSpec.Types.Type.Java;
                    if (null != chaincodeSource)
                    {
                        targetPathPrefix = "src";
                        projectSourceDir = chaincodeSource;
                    }

                    break;

                case TransactionRequest.Type.NODE:

                    // chaincodePath is not applicable and must be null
                    // chaincodeSource may be a File or InputStream

                    //   Verify that chaincodePath is null
                    if (!string.IsNullOrEmpty(chaincodePath))
                        throw new ArgumentException("chaincodePath must be null for Node chaincode");
                    dplang = "Node";
                    ctyp = ChaincodeSpec.Types.Type.Node;
                    if (null != chaincodeSource)
                    {
                        projectSourceDir = chaincodeSource;
                        targetPathPrefix = "src"; //Paths.get("src", chaincodePath).toString();
                    }

                    break;
                default:
                    throw new ArgumentException("Unexpected chaincode language: " + chaincodeLanguage);
            }

            CcType(ctyp);

            byte[] data;
            string chaincodeID = chaincodeName + "::" + chaincodePath + "::" + chaincodeVersion;

            if (chaincodeSource != null)
            {
                if (!Directory.Exists(projectSourceDir))
                {
                    string message = "The project source directory does not exist: " + projectSourceDir;
                    logger.Error(message);
                    throw new ArgumentException(message);
                }

                /*
                if (!projectSourceDir.isDirectory()) {
                    final String message = "The project source directory is not a directory: " + projectSourceDir.getAbsolutePath();
                    logger.error(message);
                    throw new IllegalArgumentException(message);
                }
                */
                logger.Info($"Installing '{chaincodeID}' language {dplang} chaincode from directory: '{projectSourceDir}' with source location: '{targetPathPrefix}'. chaincodePath:'{chaincodePath}'", chaincodeID, dplang, projectSourceDir, targetPathPrefix, chaincodePath);

                // generate chaincode source tar
                data = Utils.GenerateTarGz(projectSourceDir, targetPathPrefix, metainf);
                if (null != diagnosticFileDumper)
                    logger.Trace($"Installing '{chaincodeID}' language {dplang} chaincode from directory: '{projectSourceDir}' with source location: '{targetPathPrefix}'. chaincodePath:'{chaincodePath}' tar file dump {diagnosticFileDumper.CreateDiagnosticTarFile(data)}");
            }
            else
            {
                logger.Info($"Installing '{chaincodeID}' language {dplang} chaincode chaincodePath:'{chaincodePath}' from input stream");
                data = chaincodeInputStream.ToByteArray();
                if (data.Length == 0)
                    throw new ArgumentException("Chaincode input stream was empty");
                if (null != diagnosticFileDumper)
                    logger.Trace($"Installing '{chaincodeID}' language {dplang} chaincode from input stream tar file dump {diagnosticFileDumper.CreateDiagnosticTarFile(data)}");
            }

            ChaincodeDeploymentSpec depspec = ProtoUtils.CreateDeploymentSpec(ctyp, chaincodeName, chaincodePath, chaincodeVersion, null, data);

            // set args
            AddArg(action);
            AddArg(depspec.ToByteString());
        }

        public InstallProposalBuilder ChaincodeLanguage(TransactionRequest.Type ccodeLanguage)
        {
            chaincodeLanguage = ccodeLanguage;
            return this;
        }

        public InstallProposalBuilder ChaincodeVersion(string ccodeVersion)
        {
            chaincodeVersion = ccodeVersion;
            return this;
        }

        public void SetChaincodeInputStream(Stream ccodeInputStream)
        {
            chaincodeInputStream = ccodeInputStream;
        }
    }
}