# fabric-sdk-net

[![Build status](https://ci.appveyor.com/api/projects/status/yli69cn4iq5c5lel/branch/master?svg=true)](https://ci.appveyor.com/project/maxpiva/fabric-sdk-net/branch/master)

Direct .NET port from [fabric-sdk-java](https://github.com/hyperledger/fabric-sdk-java)

Alpha Version

* SDK Porting from JAVA done.
* Both sdk compile ok.
* Unit testing SDK_CA passing.
* Unit testing SDK_CA_Integration passing
* Unit testing SDK passing.
* Unit testing SDK_Integration passing


[.NET SHIM](https://github.com/maxpiva/fabric-chaincode-net)


**TODO**

On FABRIC (Help requested)
- Creation of the docker script for microsoft/dotnet:sdk   (Fabric will call it, will build the .net chaincode).
- Creation of the docker script for dotnet:runtime-deps into fabric (the .net chaincode build will be injected into bin/chaincode of this image)
- Add the required platform code in fabric.

On this SDK
- Add .NET chaincode upload (source and/or compiled)
- More Cleanup, and .NET Design Guidelines.





