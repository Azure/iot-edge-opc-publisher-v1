This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments

# OPC Publisher Module for Azure IoT Edge
This reference implementation demonstrates how Azure IoT Edge can be used to connect to existing OPC UA servers and publishes JSON encoded telemetry data from these servers in OPC UA "Pub/Sub" format (using a JSON payload) to Azure IoT Hub. All transport protocols supported by Azure IoT Edge can be used, i.e. HTTPS, AMQP and MQTT. The transport is selected in the transport setting in the gatewayconfig.json file.

This module, apart from including an OPC UA *client* for connecting to existing OPC UA servers you have on your network, also includes an OPC UA *server* on port 62222 that can be used to manage the module.

This module uses the OPC Foundations's OPC UA reference stack and therefore licensing restrictions apply. Visit http://opcfoundation.github.io/UA-.NETStandardLibrary/ for OPC UA documentation and licensing terms.

|Branch|Status|
|------|-------------|
|master|[![Build status](https://ci.appveyor.com/api/projects/status/6t7ru6ow7t9uv74r/branch/master?svg=true)](https://ci.appveyor.com/project/marcschier/iot-gateway-opc-ua-r4ba5/branch/master) [![Build Status](https://travis-ci.org/Azure/iot-gateway-opc-ua.svg?branch=master)](https://travis-ci.org/Azure/iot-gateway-opc-ua)|

# Directory Structure

## /src
This folder contains the source code of the module, a managed gateway loader and a library to handle IoT Hub credentials.

# Configuring the Module
The OPC UA nodes whose values should be published to Azure IoT Hub can be configured by creating a "publishednodes.json" file. This file is auto-generated and persisted by the module automatically when using the Publisher's OPC UA server interface from a client. If you want to create the file manually, below is a sample publishednodes.json file:
```
[
  {
    "EndpointUrl": "opc.tcp://myopcservername:51210/UA/SampleServer",
    "NodeId": { "Identifier": "ns=1;i=123" }
  }
  {
    "EndpointUrl": "opc.tcp:// myopcservername:51210/UA/SampleServer",
    "NodeId": { "Identifier": "ns=2;i=456" }
  }
]
```

The NodeId identifier conforms to the UPC UA node standard. You can read more about the syntax at http://documentation.unified-automation.com/uasdkhp/1.0.0/html/_l2_ua_node_ids.html. 

# Configuring the Gateway
The ```Configuration``` Section must contain at a minimum all items shown in the provide file. The JSON type conforms to the OPC UA reference stack serialization of the ```ApplicationConfiguration``` type.  

You should pass your application name and the IoT Hub owner connection string (which can be read out for your IoT Hub from portal.azure.com) as command line arguments. The IoT Hub owner connection string is only required for device registration with IoT Hub on first run.

# Building the Module

This module requires the .NET Core SDK V1.1. You can build the module from Visual Studio 2017 by opening the solution file, right clicking the GatewayApp.NetCore project and selecting "publish".

# Running the module

## Compiling and running from source

You can run the module through the supplied gateway app GatewayApp.NetCore on Windows along with the Gateway SDK and IoT Hub module directly via Visual Studio 2017 by hitting F5 (after publishing GatewayApp.NetCore). Don't forget your command line arguments!

**Detailed steps:**
1. ```git clone https://github.com/Azure/iot-edge-opc-publisher.git```
1. Open Opc.Ua.Publisher.Module.sln in Visual Studio 2017
1. Click on ```gatewayconfig.json``` and ensure **Copy to Output Directory** is set to **Copy always**
1. Right-click **GatewayApp.NetCore**, and select **Publish**, keeping default settings.
1. Create and save ```publishednodes.json``` to **src\GatewayApp.NetCore\bin\Release\PublishOutput**
1. ```cd src\GatewayApp.NetCore\bin\Release\PublishOutput```
1. Run the gateway by executing ```dotnet GatewayApp.Netcore.dll <applicationName> <IoTHubOwnerConnectionString>```
1. The first time you connect to your OPC Server a certificate error may be thrown. Copy the rejected certificate it from the **CertificateStores/Rejected Certificates/certs** to the **CertificateStores/UA Applications/certs** folder and restart the gateway.
1. You can validate that data is flowing into IoT Hub using **Device Explorer**. The device will appear as the ```<applicationName>```.

## Compiling and running using Docker

You can also run the module in a Docker container using the Dockerfile provided. From the root of the repo, in a console, type:

```docker build -t gw .```

On first run, for one-time IoT Hub registration:

```docker run -it --rm gw <applicationName> <IoTHubOwnerConnectionString>```

From then on:

```docker run -it --rm gw <applicationName>```

You can also pass in the location of the "publishednodes.json" file using one of the following methods. By default it expects the file to be under ```src\GatewayApp.NetCore\bin\Debug\netcoreapp1.1```.

- Use the -e docker switch and pass in the ```_GW_PNFP``` environmental variable 
    - ```-e _GW_PNFP="/build/src/GatewayApp.NetCore/publishednodes.json" ```
- Use a shared drive on the Docker host and pass in the shared path
    - ```-v //c/docker:/shared -e \_GW\_PNFP="/shared/publishednodes.JSON"```
