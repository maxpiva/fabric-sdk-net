# fabric-sdk-net


[![Build status](https://ci.appveyor.com/api/projects/status/yli69cn4iq5c5lel/branch/master?svg=true)](https://ci.appveyor.com/project/maxpiva/fabric-sdk-net/branch/master)

Direct .NET port from [fabric-sdk-java](https://github.com/hyperledger/fabric-sdk-java)

Master branch is 1.3 WIP
There is a working 1.1 version in tags

Both versions are alpha, but 1.3 is wip.


* SDK Porting from JAVA done.
* Both sdk compile ok.
* Unit testing SDK_CA passing.
* Unit testing SDK_CA_Integration passing
* Unit testing SDK passing.
* Unit testing SDK_Integration falling (Work in Progress)
* All the code is async to the bone. But Sync methods are presented on both sdk for easy porting.
* Net Standard 2.0. Both .NET Core and .NET Framework are supported

[.NET SHIM](https://github.com/maxpiva/fabric-chaincode-net) (Currently outdated)

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
- More Cleanup, and .NET Design Guidelines. Code already start to diverge from the original JAVA version
- Remove JAVA idiosyncrasies 
- Continue with the split and re-order of the source code, some files are becoming unmanageable, like channel
- Better Multi-Thread and Locking approach on some Methods. Like ServiceDiscovery Thread (Not felling right about it)

On the SHIM
- Async support
- Visual Studio Templates?
