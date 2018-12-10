# fabric-sdk-net

**v1.3.0 in process, code complete, removing errors...**

[![Build status](https://ci.appveyor.com/api/projects/status/yli69cn4iq5c5lel/branch/master?svg=true)](https://ci.appveyor.com/project/maxpiva/fabric-sdk-net/branch/master)

Direct .NET port from [fabric-sdk-java](https://github.com/hyperledger/fabric-sdk-java)

Alpha Version (not for production)

* SDK Porting from JAVA done.
* Both sdk compile ok.
* Unit testing SDK_CA passing.
* Unit testing SDK_CA_Integration passing
* Unit testing SDK passing.
* Unit testing SDK_Integration passing
* All the code is async to the bone. But Sync methods are presented on both sdk for easy porting.

[.NET SHIM](https://github.com/maxpiva/fabric-chaincode-net)

**Integration Testing on Windows 10 and Visual Studio How-To**

1) Install Docker For Windows https://docs.docker.com/docker-for-windows/install

   Install Ubuntu Store App https://www.microsoft.com/en-us/p/ubuntu/9nblggh4msv6?activetab=pivot:overviewtab
   

2) Follow this Guide replacing "1.1.0" with "1.3.0" if you targeting Fabric 1.3.0

   https://medium.com/coinmonks/hyperledger-fabric-1-1-0-on-windows-fd142651a904


3) In Bash

   cd "/c/[FABRIC-SDK-NET-REPO]/TestData/Fixture/SdkIntegration"

   For Every SDK Integration Playlist or Test

   ./fabric.sh restart (since a clean sheet is needed for the tests)

**TODO**

On FABRIC (Help requested)
- Creation of the docker script for microsoft/dotnet:sdk   (Fabric will call it, will build the .net chaincode).
- Creation of the docker script for dotnet:runtime into fabric (the .net chaincode build will be injected into bin/chaincode of this image)
- Add the required platform code in fabric to support the above.


On this SDK
- Add .NET chaincode upload (source and/or compiled)
- More Cleanup, and .NET Design Guidelines.
- Remove JAVA idiosyncrasies 
- Continue with the split and re-order of the source code, some files are becoming unmanageable.
- Better Multi-Thread and Locking approach on some Methods.

On the SHIM
- Maybe Visual Studio Templates







