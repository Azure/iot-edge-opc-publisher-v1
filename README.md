Please note: This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information, see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.



# Microsoft OPC Publisher
OPC Publisher is a fully supported Microsoft product, developed in the open, that bridges the gap between industrial assets and the Microsoft Azure cloud. It does so by connecting to OPC UA-enabled assets or industrial connectivity software and publishes telemetry data to [Azure IoT Hub](https://azure.microsoft.com/en-us/services/iot-hub/) in IEC62541 OPC UA PubSub standard format.

It runs on [Azure IoT Edge](https://azure.microsoft.com/en-us/services/iot-edge/) as a Module or on plain Docker as a container. Since it leverages the [.Net cross-platform runtime](https://docs.microsoft.com/en-us/dotnet/core/introduction), it also runs natively on Linux and Windows 10.



## Getting Started

Please use our released Docker containers for OPC Publisher available in the Microsoft Container Registry, rather than building from sources. The easiest way to deploy OPC Publisher is through the [Azure Marketplace](https://azuremarketplace.microsoft.com/en-us/marketplace/apps/microsoft_iot.iotedge-opc-publisher). 

[<img src="image-20201028141833399.png" style="zoom:50%;" />](https://azuremarketplace.microsoft.com/en-us/marketplace/apps/microsoft_iot.iotedge-opc-publisher)

Simply click the Get It Now button, pick the IoT Hub (the OPC Publisher is supposed to send data to) as well as the IoT Edge device (the OPC Publisher is supposed to run on) and click Create.

##### Accessing the Microsoft Container Registry Docker containers for OPC Publisher manually

The latest released version of OPC Publisher can be run manually via:

```
docker run mcr.microsoft.com/iotedge/opc-publisher:latest <name>
```

Where "name" is the name for the container.



## Configuring OPC Publisher

OPC Publisher has several interfaces that can be used to configure it.

### Configuring Security

IoT Edge provides OPC Publisher with its security configuration for accessing IoT Hub automatically. OPC Publisher can also run as a standalone Docker container by specifying a device connection string for accessing IoT Hub via the --dc command line parameter. A device for IoT Hub can be created and its connection string retrieved through the Azure Portal.

For accessing OPC UA-enabled assets, X.509 certificates and their associated private keys are used by OPC UA. OPC Publisher uses a file system-based certificate store to manage all certificates. During startup, OPC Publisher checks if there is a certificate it can use in this certificate stores and creates a new self-signed certificate and new associated private key if there is none. Self-signed certificates provide weak authentication, since they are not signed by a trusted CA, but at least the communication to the OPC UA-enabled asset can be encrypted this way.

To persist the security configuration of OPC Publisher across restarts, the certificate and private key located in the the certificate store directory must be mapped to the IoT Edge host OS filesystem. Please see [Specifying Container Create Options in the Azure Portal](https://github.com/Azure/iot-edge-opc-publisher/tree/docs#specifying-container-create-options-in-the-azure-portal).

### Configuration via Configuration File

The simplest way to configure OPC Publisher is via a configuration file. An example configuration file as well as documentation regarding its format is provided via the file `publishednodes.json` in this repository.
Configuration file syntax has changed over time and OPC Publisher still can read old formats, but converts them into the latest format when persisting the configuration, done regularly in an automated fashion.

An basic configuration file looks like this:
```
[
  {
    "EndpointUrl": "opc.tcp://testserver:62541/Quickstarts/ReferenceServer",
    "UseSecurity": false,
    "OpcNodes": [
      {
        "Id": "i=2258",
        "OpcSamplingInterval": 2000,
        "OpcPublishingInterval": 5000,
        "DisplayName": "Current time"
      }
    ]
  }
]
```

OPC UA assets optimizes network bandwidth by only sending data changes to OPC Publisher when the data has changed. If data changes need to be published more often or at regular intervals, OPC Publisher supports a "heartbeat" for every configured data item that can be enabled by additionally specifying the HeartbeatInterval key in the data item's configuration. The interval is specified in seconds:
```
    "HeartbeatInterval": 3600,
```

An OPC UA asset always send the current value of a data item when OPC Publisher first connects to it. To prevent publishing this data to IoT Hub, the SkipFirst key can be additionally specified in the data item's configuration:
```
    "SkipFirst": true,
```

Both settings can be enabled globally via command line options, too.

### Configuration via Command Line Arguments

There are several command line arguments that can be used to set global settings for OPC Publisher. They are described [here](CommandLineArguments.md).


### Configuration via the built-in OPC UA Server Interface
**Please note: This feature is only available in version 2.5 and below of OPC Publisher.**

OPC Publisher has a built-in OPC UA server, running on port 62222. It implements three OPC UA methods:

  - PublishNode
  - UnpublishNode
  - GetPublishedNodes

This interface can be accessed using an OPC UA client application, for example [UA Expert](https://www.unified-automation.com/products/development-tools/uaexpert.html).

### Configuration via IoTHub Direct Methods

**Please note: This feature is only available in version 2.5 and below of OPC Publisher.**

OPC Publisher implements the following [IoTHub Direct Methods](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-direct-methods) which can be called from an application (from anywhere in the world) leveraging the [IoT Hub Device SDK](https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-sdks):

  - PublishNodes
  - UnpublishNodes
  - UnpublishAllNodes
  - GetConfiguredEndpoints
  - GetConfiguredNodesOnEndpoint
  - GetDiagnosticInfo
  - GetDiagnosticLog
  - GetDiagnosticStartupLog
  - ExitApplication
  - GetInfo

We have provided a [sample configuration application](https://github.com/Azure-Samples/iot-edge-opc-publisher-nodeconfiguration) as well as an [application for reading diagnostic information](https://github.com/Azure-Samples/iot-edge-opc-publisher-diagnostics) from OPC Publisher open-source, leveraging this interface.

### Configuration via Cloud-based, Companion REST Microservice

**Please note: This feature is only available in version 2.6 and above of OPC Publisher.**

A cloud-based, companion microservice with a REST interface is described and available [here](https://github.com/Azure/Industrial-IoT/blob/master/docs/services/publisher.md). It can be used to configure OPC Publisher via an OpenAPI-compatible interface, for example through Swagger.



## OPC Publisher Telemetry Format

OPC Publisher version 2.6 and above supports standardized OPC UA PubSub JSON format as specified in [part 14 of the OPC UA specification](https://opcfoundation.org/developer-tools/specifications-unified-architecture/part-14-pubsub/). 

In addition, all versions of OPC Publisher support a non-standardized, simple JSON telemetry format, which is compatible with [Azure Time Series Insights](https://azure.microsoft.com/en-us/services/time-series-insights/) and looks like this:
```
{
    "NodeId": "i=2058",
    "ApplicationUri": "urn:myopcserver",
    "DisplayName": "CurrentTime",
    "Value": {
        "Value": "10.11.2017 14:03:17",
        "SourceTimestamp": "2017-11-10T14:03:17Z"
    }
}
```

If OPC Publisher is configured to batch several JSON telemetry messages into a single IoT Hub message, the batched JSON telemetry messages are sent as a JSON array.

### Configuration of the simple JSON telemetry format via Separate Configuration File

**Please note: This feature is only available in version 2.5 and below of OPC Publisher.**

OPC Publisher allows filtering the parts of the non-standardized, simple telemetry format via a separate configuration file, which can be specified via the `--tc` command line option. If no configuration file is specified, the full JSON telemetry format is sent to IoT Hub. The format of the separate telemetry configuration file is described [here](TelemetryFormatConfiguration.md).



## Specifying Container Create Options in the Azure Portal
When deploying OPC Publisher through the Azure Portal, container create options can be specified in the Update IoT Edge Module page of OPC Publisher. These create options must be in JSON format. The OPC Publisher command line arguments can be specified via the Cmd key, e.g.:
```
"Cmd": [
    "--pf=./pn.json",
    "--aa"
],
```

A typical set of IoT Edge Module Container Create Options for OPC Publisher is:
```
{
    "Hostname": "opcpublisher",
    "Cmd": [
        "--pf=./pn.json",
        "--aa"
    ],
    "HostConfig": {
        "Binds": [
            "/iiotedge:/appdata"
        ]
    }
}
```

With these options specified, OPC Publisher will read the configuration file `./pn.json`. The OPC Publisher's working directory is set to
`/appdata` at startup and thus OPC Publisher will read the file `/appdata/pn.json` inside its Docker container. 
OPC Publisher's log file `publisher-publisher.log` (the default name) will be written to `/appdata` and the `CertificateStores` directory (used for OPC UA certificates) will also be created in this directory. To make these files available in the IoT Edge host file system the container configuration requires a bind mount volume. The `/iiotedge:/appdata` bind will map the directory `/appdata` (which is the current working directory on container startup) to the host directory `/iiotedge` (which will be created by the IoT Edge runtime if it doesn't exist). 

**Without this bind mount volume, all OPC Publisher configuration files will be lost when the container is restarted.**

A connection to an OPC UA server using the hostname of the OPC UA server without a DNS server configured on the network can be achieved by adding an `ExtraHosts` configuration to the `HostConfig` object:

```
"HostConfig": {
    "ExtraHosts": [
        "opctestsvr:192.168.178.26"
    ]
}
```



## Performance and Memory Tuning for OPC Publisher
When running OPC Publisher in production setups, network performance requirements (throughput and latency) and memory resources must be considered. OPC Publisher exposes the following command line parameters to help meet these requirements:

* Message queue capacity (`--mq`)
* IoTHub send interval (`--si`)
* IoTHub message size (`--ms`)

The `--mq` parameter controls the upper bound of the capacity of the internal message queue. This queue buffers all messages before they are sent to IoT Hub. If OPC Publisher is not able to send messages to IoT Hub fast enough, this queue buffers those messages and starts to grow. If the number of items in this queue increasing in test runs, please:

* decrease the IoTHub send interval (`--si`)

* increase the IoTHub message size (`--ms`)

Otherwise, messages will be lost. The `--mq` parameter also controls the upper bound of the memory resources used by OPC Publisher.

The `--si` parameter enforces OPC Publisher to send messages to IoTHub at the specified interval. A message is sent either when the message size is reached (triggering the send interval to reset) or when the specified interval time has passed. If the message size parameter is disabled by setting it to 0, OPC Publisher uses the maximal possible IoTHub message size of 256 kB to batch data.

The `--ms` parameter enables batching of messages sent to IoTHub. In most network setups, the latency of sending a single message to IoTHub is high, compared to the time it takes to transmit the payload.
If a small delay for the data to arrive in the cloud is acceptable, OPC Publisher should be configured to use the maximal message size of 256 kB.

To measure the performance of OPC Publisher,  the `--di` command line parameter can be specified, which will print performance metrics to the trace log in the interval specified (in seconds).

The default configuration sends data to IoT Hub each 10 seconds or when 256 kB of message data is available. This adds a moderate delay of 10 seconds max, but has lowest probability of losing data because of the large message size.
The metric `monitored item notifications enqueue failure` shows how many messages were lost in the logs.

When the message size is set to 0 and there is a send interval configured (or the default of 1 second is used), OPC Publisher uses batching with the largest supported IoTHub message size, which is 256 kB. 

When both send interval and message size are set to 0, OPC Publisher sends a message to IoTHub for every data item. The results in an average message size of just 234 bytes, which is small. However, the advantage of this configuration is that OPC Publisher sends the data straight away as it comes in from the connected asset. The number of lost messages (`monitored item notifications enqueue failure`) will be high, which means that this configuration is not recommendable for use cases where a large amount of data must be published.

#### Maximum batching (--si 0 --ms 262144)
```
        ==========================================================================
        OpcPublisher status @ 26.10.2017 15:42:55 (started @ 26.10.2017 15:41:00)
        ---------------------------------
        OPC sessions: 1
        connected OPC sessions: 1
        connected OPC subscriptions: 5
        OPC monitored items: 500
        ---------------------------------
        monitored items queue bounded capacity: 8192
        monitored items queue current items: 0
        monitored item notifications enqueued: 54137
        monitored item notifications enqueue failure: 0
        monitored item notifications dequeued: 54137
        ---------------------------------
        messages sent to IoTHub: 48
        last successful msg sent @: 26.10.2017 15:42:55
        bytes sent to IoTHub: 12565544
        avg msg size: 261782
        msg send failures: 0
        messages too large to sent to IoTHub: 0
        times we missed send interval: 0
        ---------------------------------
        current working set in MB: 90
        --si setting: 0
        --ms setting: 262144
        --ih setting: Mqtt
        ==========================================================================
```
This configuration batches as much OPC node value updates as possible. The maximum IoTHub message size is 256 kB, which is configured here. There is no send interval requested, which makes the time when data is ingested
completely controlled by the data itself. This configuration has the least probability of loosing any OPC node values and can be used for publishing a high number of nodes.
When using this configuration you need to ensure, that your scenario does not have conditions where high latency is introduced (because the message size of 256 kB is not reached).



## Source Code Status

|Branch|Status|
|------|-------------|
|master|[![Build status](https://ci.appveyor.com/api/projects/status/6t7ru6ow7t9uv74r/branch/master?svg=true)](https://ci.appveyor.com/project/marcschier/iot-gateway-opc-ua-r4ba5/branch/master) [![Build Status](https://travis-ci.org/Azure/iot-gateway-opc-ua.svg?branch=master)](https://travis-ci.org/Azure/iot-gateway-opc-ua)|
