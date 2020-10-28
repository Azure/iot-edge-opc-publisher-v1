Please note: This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information, see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.



# Microsoft OPC Publisher
OPC Publisher is a fully supported Microsoft product, developed in the open, that bridges the gap between your industrial assets and the Microsoft Azure cloud. It does so by connecting to your OPC UA-enabled assets or industrial connectivity software and publishes telemetry data to [Azure IoT Hub](https://azure.microsoft.com/en-us/services/iot-hub/) in IEC62541 OPC UA PubSub standard format.

It runs on [Azure IoT Edge](https://azure.microsoft.com/en-us/services/iot-edge/) as a Module or on plain Docker as a container. Since it leverages the [.Net cross-platform runtime](https://docs.microsoft.com/en-us/dotnet/core/introduction), it also runs natively on Linux and Windows 10.



## Getting Started

Please use our released Docker containers for OPC Publisher available in the Microsoft Container Registry, rather than building from sources. The easiest way to deploy OPC Publisher is through the [Azure Marketplace](https://azuremarketplace.microsoft.com/en-us/marketplace/apps/microsoft_iot.iotedge-opc-publisher). 

[<img src="image-20201028141833399.png" style="zoom:50%;" />](https://azuremarketplace.microsoft.com/en-us/marketplace/apps/microsoft_iot.iotedge-opc-publisher)

Simply click the Get It Now button, pick the IoT Hub (the OPC Publisher is supposed to send data to) as well as the IoT Edge device (the OPC Publisher is supposed to run on) and click Create.

##### Accessing the Microsoft Container Registry Docker containers for OPC Publisher manually

If you want to deploy the latest released version of OPC Publisher manually, you can do so by running:

```
docker run mcr.microsoft.com/iotedge/opc-publisher:latest <name>
```

Where '<name>' is the name you want to give to the container.



## Configuring OPC Publisher

OPC Publisher has several interfaces that can be used to configure it.

### Configuring Security

IoT Edge provides OPC Publisher with its security configuration for accessing IoT Hub. Should you need to run OPC Publisher as a standalone Docker container, you can specify a device connection string for accessing IoT Hub via the --dc command line parameter. You can create a device for IoT Hub and retrieve its connection string via the Azure Portal.

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

We have provided a [sample configuration application](https://github.com/Azure-Samples/iot-edge-opc-publisher-nodeconfiguration) as well as an [application for reading diagnostic information](https://github.com/Azure-Samples/iot-edge-opc-publisher-diagnostics) from OPC Publisher open-source that leverages this interface for your reference.

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

If you need to connect to an OPC UA server using the hostname of the OPC UA server and have no DNS configured on your network, you can enable resolving the hostname by adding an `ExtraHosts` configuration to the `HostConfig` object:

```
"HostConfig": {
    "ExtraHosts": [
        "opctestsvr:192.168.178.26"
    ]
}
```



## Performance and Memory Tuning OPC Publisher V2.5 and below
When running OPC Publisher you need to be aware of your performance requirements and the memory resources you have available on your platform.
Since both are interdependent and both depend on the configuration of how many nodes are configured to publish, you should ensure that the parameters you are using for:

* IoTHub send interval (`--si`)
* IoTHub message size (`--ms`)
* Monitored Items queue capacity (`--mq`)
do meet your requirements.

The `--mq` parameter controls the upper bound of the capacity of the internal queue, which buffers all notifications if a value of an OPC node changes. If OPC Publisher is not able to send messages to IoTHub fast enough,
then this queue buffers those notifications. The parameter sets the number of notifications which can be buffered. If you seen the number of items in this queue increasing in your test runs, you need to:

* decrease the IoTHub send interval (`--si`)
* increase the IoTHub message size (`--ms`)
otherwise you will loose the data values of those OPC node changes. The `--mq` parameter at the same time allows to prevent controlling the upper bound of the memory resources used by OPC Publisher.

The `--si` parameter enforces OPC Publisher to send messages to IoTHub as the specified interval. If there is an IoTHub message size specified via the `--ms` parameter (or by the default value for it),
then a message will be sent either when the message size is reached (in this case the interval is restarted) or when the specified interval time has passed. If you disable the message size by `--ms 0`, OPC Publisher
uses the maximal possible IoTHub message size of 256 kB to batch data.

The `--ms` parameter allows you to enable batching of messages sent to IoTHub. Depending on the protocol you are using, the overhead to send a message to IoTHub is high compared to the actual time of sending the payload.
If your scenario allows latency for the data ingested, you should configure OPC Publisher to use the maximal message size of 256 kB.

Before you use OPC Publisher in production scenarios, you need to test the performance and memory under production conditions. You can use the `--di` command line parameter to specify a interval in seconds,
which will trigger the output of diagnostic information at this interval.

### Test measurements
Here are some measurements with different values for `--si` and `--ms` parameters publishing 500 nodes with an OPC publishing interval of 1 second.
OPC Publisher was used as debug build on Windows 10 natively for 120 seconds. The IoTHub protocol was the default MQTT protocol.

#### Default configuration (--si 10 --ms 262144)
```
        ==========================================================================
        OpcPublisher status @ 26.10.2017 15:33:05 (started @ 26.10.2017 15:31:09)
        ---------------------------------
        OPC sessions: 1
        connected OPC sessions: 1
        connected OPC subscriptions: 5
        OPC monitored items: 500
        ---------------------------------
        monitored items queue bounded capacity: 8192
        monitored items queue current items: 0
        monitored item notifications enqueued: 54363
        monitored item notifications enqueue failure: 0
        monitored item notifications dequeued: 54363
        ---------------------------------
        messages sent to IoTHub: 109
        last successful msg sent @: 26.10.2017 15:33:04
        bytes sent to IoTHub: 12709429
        avg msg size: 116600
        msg send failures: 0
        messages too large to sent to IoTHub: 0
        times we missed send interval: 0
        ---------------------------------
        current working set in MB: 90
        --si setting: 10
        --ms setting: 262144
        --ih setting: Mqtt
        ==========================================================================
```

The default configuration sends data to IoTHub each 10 seconds or when 256 kB of data to ingest is available. This adds a moderate latency of max 10 seconds, but has lowest probability of loosing data because of the large message size.
As you see in the diagnostics output there are no OPC node updates lost (`monitored item notifications enqueue failure`).

#### Constant send inverval (--si 1 --ms 0)
```
        ==========================================================================
        OpcPublisher status @ 26.10.2017 15:35:59 (started @ 26.10.2017 15:34:03)
        ---------------------------------
        OPC sessions: 1
        connected OPC sessions: 1
        connected OPC subscriptions: 5
        OPC monitored items: 500
        ---------------------------------
        monitored items queue bounded capacity: 8192
        monitored items queue current items: 0
        monitored item notifications enqueued: 54243
        monitored item notifications enqueue failure: 0
        monitored item notifications dequeued: 54243
        ---------------------------------
        messages sent to IoTHub: 109
        last successful msg sent @: 26.10.2017 15:35:59
        bytes sent to IoTHub: 12683836
        avg msg size: 116365
        msg send failures: 0
        messages too large to sent to IoTHub: 0
        times we missed send interval: 0
        ---------------------------------
        current working set in MB: 90
        --si setting: 1
        --ms setting: 0
        --ih setting: Mqtt
        ==========================================================================
```
When the message size is set to 0 and there is a send interval configured (or the default of 1 second is used), then OPC Publisher does use internally batch data using the maximal supported IoTHub message size, which is 256 kB. As you see in the diagnostic output,
the average message size is 115019 byte. In this configuration we do not loose any OPC node value updates and compared to the default it adds lower latency.

#### Send each OPC node value update (--si 0 --ms 0)
```
        ==========================================================================
        OpcPublisher status @ 26.10.2017 15:39:33 (started @ 26.10.2017 15:37:37)
        ---------------------------------
        OPC sessions: 1
        connected OPC sessions: 1
        connected OPC subscriptions: 5
        OPC monitored items: 500
        ---------------------------------
        monitored items queue bounded capacity: 8192
        monitored items queue current items: 8184
        monitored item notifications enqueued: 54232
        monitored item notifications enqueue failure: 44624
        monitored item notifications dequeued: 1424
        ---------------------------------
        messages sent to IoTHub: 1423
        last successful msg sent @: 26.10.2017 15:39:33
        bytes sent to IoTHub: 333046
        avg msg size: 234
        msg send failures: 0
        messages too large to sent to IoTHub: 0
        times we missed send interval: 0
        ---------------------------------
        current working set in MB: 96
        --si setting: 0
        --ms setting: 0
        --ih setting: Mqtt
        ==========================================================================
```
This configuration sends for each OPC node value change a message to IoTHub. You see the average message size of 234 byte is pretty small. The advantage of this configuration is that OPC Publisher does not add any latency to the ingest data path. The number of
lost OPC node value updates (`monitored item notifications enqueue failure: 44624`) is the highest of all compared configurations, which make this configuration not recommendable for use cases, when a lot of telemetry should be published.

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



## Performance and Memory Tuning OPC Publisher V2.6 and above

TODO



## Source Code Status

|Branch|Status|
|------|-------------|
|master|[![Build status](https://ci.appveyor.com/api/projects/status/6t7ru6ow7t9uv74r/branch/master?svg=true)](https://ci.appveyor.com/project/marcschier/iot-gateway-opc-ua-r4ba5/branch/master) [![Build Status](https://travis-ci.org/Azure/iot-gateway-opc-ua.svg?branch=master)](https://travis-ci.org/Azure/iot-gateway-opc-ua)|
