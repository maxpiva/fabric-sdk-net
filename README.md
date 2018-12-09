# fabric-sdk-net

**v1.3.0 in process, code complete, removing errors**

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

Integration Testing How-To

* Coming soon

**TODO**

On FABRIC (Help requested)
- Creation of the docker script for microsoft/dotnet:sdk   (Fabric will call it, will build the .net chaincode).
- Creation of the docker script for dotnet:runtime-deps into fabric (the .net chaincode build will be injected into bin/chaincode of this image)
- Add the required platform code in fabric.

On this SDK
- Add .NET chaincode upload (source and/or compiled)
- More Cleanup, and .NET Design Guidelines.
- Remove JAVA idiosyncrasies 
- Continue with the split and re-order of the source code, some files are becoming unmanageable.
- Better Multi-Thread and Locking approach on some Methods.

On the SHIM
- Maybe Visual Studio Templates







