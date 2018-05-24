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
using System.Collections.Generic;
using System.IO;
using System.Text;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.NetExtensions;
using Utils = Hyperledger.Fabric.SDK.Helper.Utils;

namespace Hyperledger.Fabric.SDK.Transaction
{
    public class InstallProposalBuilder : LSCCProposalBuilder
    {

    private static readonly ILog logger = LogProvider.GetLogger(typeof(InstantiateProposalBuilder));
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private static readonly Config config = Config.GetConfig();
        private static readonly DiagnosticFileDumper diagnosticFileDumper = IS_TRACE_LEVEL
            ? config.GetDiagnosticFileDumper() : null;
        
    private string chaincodePath;

    private DirectoryInfo chaincodeSource;
    private string chaincodeName;
    private string chaincodeVersion;
    private TransactionRequest.Type chaincodeLanguage;
    protected string action = "install";
    private Stream chaincodeInputStream;
    private DirectoryInfo chaincodeMetaInfLocation;

    protected InstallProposalBuilder()
    {
    }

    public new static InstallProposalBuilder Create() {
        return new InstallProposalBuilder();

    }

    public InstallProposalBuilder ChaincodePath(string chaincodePath) {

        this.chaincodePath = chaincodePath;

        return this;

    }

    public InstallProposalBuilder ChaincodeName(string chaincodeName) {

        this.chaincodeName = chaincodeName;

        return this;

    }

    public InstallProposalBuilder ChaincodeSource(DirectoryInfo chaincodeSource) {
        this.chaincodeSource = chaincodeSource;

        return this;
    }

    public InstallProposalBuilder ChaincodeMetaInfLocation(DirectoryInfo chaincodeMetaInfLocation) {

        this.chaincodeMetaInfLocation = chaincodeMetaInfLocation;
        return this;
    }


    public override Proposal Build()
    {

        ConstructInstallProposal();
        return base.Build();
    }

    private void ConstructInstallProposal()
    {

        try {

            CreateNetModeTransaction();

        } catch (IOException exp) {
            logger.ErrorException(exp.Message,exp);
            throw new ProposalException("IO Error while creating install proposal", exp);
        }
    }

    private void CreateNetModeTransaction()
    {
        logger.Debug("createNetModeTransaction");

        if (null == chaincodeSource && chaincodeInputStream == null) {
            throw new ArgumentException("Missing chaincodeSource or chaincodeInputStream in InstallRequest");
        }

        if (null != chaincodeSource && chaincodeInputStream != null) {
            throw new ArgumentException("Both chaincodeSource and chaincodeInputStream in InstallRequest were set. Specify one or the other");
        }

        ChaincodeSpec.Types.Type ccType;
        DirectoryInfo projectSourceDir = null;
        String targetPathPrefix = null;
        String dplang;

        DirectoryInfo metainf = null;
        if (null != chaincodeMetaInfLocation)
        {
            if (!chaincodeMetaInfLocation.Exists)
            {
                throw new ArgumentException($"Directory to find chaincode META-INF {chaincodeMetaInfLocation.FullName} does not exist");
            }
            /*
            if (!chaincodeMetaInfLocation==null) {
                throw new IllegalArgumentException(format("Directory to find chaincode META-INF %s is not a directory", chaincodeMetaInfLocation.getAbsolutePath()));
            }*/
            try
            {
                metainf = new DirectoryInfo(Path.Combine(chaincodeMetaInfLocation.FullName, "META-INF"));
            }
            catch (Exception e)
            {
                throw new ArgumentException($"The META-INF in {Path.Combine(chaincodeMetaInfLocation.FullName, "META-INF")} is not a directory.");
            }

            logger.Trace("META-INF directory is " + metainf.FullName);
            if (!metainf.Exists) {

                throw new ArgumentException($"The META-INF directory does not exist in {chaincodeMetaInfLocation.FullName}");
            }
            
            FileInfo[] files = metainf.GetFiles();
            /*
            if (files == null) {
                throw new IllegalArgumentException("null for listFiles on: " + chaincodeMetaInfLocation.getAbsolutePath());
            }
            */
            if (files.Length < 1) {

                throw new ArgumentException($"The META-INF directory {metainf.FullName} is empty.");
            }

            logger.Trace($"chaincode META-INF found {metainf.FullName}");

        }

        switch (chaincodeLanguage) {
            case TransactionRequest.Type.GO_LANG:

                // chaincodePath is mandatory
                // chaincodeSource may be a File or InputStream

                //   Verify that chaincodePath is being passed
                if (string.IsNullOrEmpty(chaincodePath)) {
                    throw new ArgumentException("Missing chaincodePath in InstallRequest");
                }

                dplang = "Go";
                ccType = ChaincodeSpec.Types.Type.Golang;
                if (null != chaincodeSource) {

                    projectSourceDir = new DirectoryInfo(Path.Combine(chaincodeSource.FullName,"src", chaincodePath));
                    targetPathPrefix = Path.Combine("src", chaincodePath);
                }
                break;

            case TransactionRequest.Type.JAVA:

                // chaincodePath is not applicable and must be null
                // chaincodeSource may be a File or InputStream

                //   Verify that chaincodePath is null
                if (!string.IsNullOrEmpty(chaincodePath)) {
                    throw new ArgumentException("chaincodePath must be null for Java chaincode");
                }

                dplang = "Java";
                ccType = ChaincodeSpec.Types.Type.Java;
                if (null != chaincodeSource) {
                    targetPathPrefix = "src";
                    projectSourceDir = chaincodeSource;

                }
                break;

            case TransactionRequest.Type.NODE:

                    // chaincodePath is not applicable and must be null
                    // chaincodeSource may be a File or InputStream

                    //   Verify that chaincodePath is null
                if (!string.IsNullOrEmpty(chaincodePath)) {
                        throw new ArgumentException("chaincodePath must be null for Node chaincode");
                }

                dplang = "Node";
                ccType = ChaincodeSpec.Types.Type.Node;
                if (null != chaincodeSource)
                {

                    projectSourceDir = chaincodeSource;
                    targetPathPrefix = "src"; //Paths.get("src", chaincodePath).toString();
                }
                break;
            default:
                throw new ArgumentException("Unexpected chaincode language: " + chaincodeLanguage);
        }

        CcType(ccType);

        byte[] data;
        string chaincodeID = chaincodeName + "::" + chaincodePath + "::" + chaincodeVersion;

        if (chaincodeSource != null) {
            if (!projectSourceDir.Exists)
            {
                string message = "The project source directory does not exist: " + projectSourceDir.FullName;
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
            logger.Info($"Installing '{chaincodeID}' language {dplang} chaincode from directory: '{projectSourceDir.FullName}' with source location: '{targetPathPrefix}'. chaincodePath:'{chaincodePath}'",
                    chaincodeID, dplang, projectSourceDir.FullName, targetPathPrefix, chaincodePath);

            // generate chaincode source tar
            data = Utils.GenerateTarGz(projectSourceDir.FullName, targetPathPrefix, metainf.FullName);

            if (null != diagnosticFileDumper)
            {
                logger.Trace($"Installing '{chaincodeID}' language {dplang} chaincode from directory: '{projectSourceDir.FullName}' with source location: '{targetPathPrefix}'. chaincodePath:'{chaincodePath}' tar file dump {diagnosticFileDumper.CreateDiagnosticTarFile(data)}");
            }

        } else {
            logger.Info($"Installing '{chaincodeID}' language {dplang} chaincode chaincodePath:'{chaincodePath}' from input stream");
            data = chaincodeInputStream.ToByteArray();
                
            if (null != diagnosticFileDumper)
            {
                logger.Trace($"Installing '{chaincodeID}' language {dplang} chaincode from input stream tar file dump {diagnosticFileDumper.CreateDiagnosticTarFile(data)}");
            }

        }

        ChaincodeDeploymentSpec depspec = ProtoUtils.CreateDeploymentSpec(ccType, this.chaincodeName, this.chaincodePath, this.chaincodeVersion, null, data);
        
        // set args
        AddArg(action);
        AddArg(depspec.ToByteString());

    }
  
    public InstallProposalBuilder ChaincodeLanguage(TransactionRequest.Type chaincodeLanguage)
    {
        this.chaincodeLanguage = chaincodeLanguage;
        return this;
    }

    public InstallProposalBuilder ChaincodeVersion(string chaincodeVersion) {
        this.chaincodeVersion = chaincodeVersion;
        return this;
    }

    public void SetChaincodeInputStream(Stream chaincodeInputStream) {
        this.chaincodeInputStream = chaincodeInputStream;

    }
}
}