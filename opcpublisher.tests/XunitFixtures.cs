// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Docker.DotNet;
using Docker.DotNet.Models;
using Opc.Ua;
using Opc.Ua.Configuration;
using OpcPublisher.Configurations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OpcPublisher
{
    public sealed class PlcOpcUaServer
    {
        public PlcOpcUaServer()
        {
            Uri dockerUri = null;
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    dockerUri = new Uri("tcp://127.0.0.1:2375");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    dockerUri = new Uri("unix:///var/run/docker.sock");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    dockerUri = new Uri("not supported");
                }
                _dockerClient = new DockerClientConfiguration(dockerUri).CreateClient();
            }
            catch
            {
                throw new Exception($"Please adjust your docker deamon endpoint '{dockerUri}' for your configuration.");
            }

            // cleanup all PLC containers
            CleanupContainerAsync().Wait();

            // pull the latest image
            ImagesCreateParameters createParameters = new ImagesCreateParameters();
            createParameters.FromImage = _plcImage;
            createParameters.Tag = "latest";
            try
            {
                _dockerClient.Images.CreateImageAsync(createParameters, new AuthConfig(), new Progress<JSONMessage>()).Wait();

            }
            catch (Exception)
            {
                throw new Exception($"Cannot pull image '{_plcImage}");
            }

            ImageInspectResponse imageInspectResponse = _dockerClient.Images.InspectImageAsync(_plcImage).Result;

            // create a new container
            CreateContainerParameters containerParams = new CreateContainerParameters();
            containerParams.Image = _plcImage;
            containerParams.Hostname = "opcplc";
            containerParams.Name = "opcplc";
            containerParams.Cmd = new string[]
            {
                "--aa",
                "--pn", $"{_plcPort}"
            };
            // workaround .NET2.1 issue for private key access
            if (imageInspectResponse.Os.Equals("windows", StringComparison.InvariantCultureIgnoreCase))
            {
                containerParams.Cmd.Add("--at");
                containerParams.Cmd.Add("X509Store");
            }
            containerParams.ExposedPorts = new Dictionary<string, EmptyStruct>();
            containerParams.ExposedPorts.Add(new KeyValuePair<string, EmptyStruct>($"{_plcPort}/tcp", new EmptyStruct()));
            containerParams.HostConfig = new HostConfig();
            PortBinding portBinding = new PortBinding();
            portBinding.HostPort = _plcPort;
            portBinding.HostIP = null;
            List<PortBinding> portBindings = new List<PortBinding>();
            portBindings.Add(portBinding);
            containerParams.HostConfig.PortBindings = new Dictionary<string, IList<PortBinding>>();
            containerParams.HostConfig.PortBindings.Add($"{_plcPort}/tcp", portBindings);
            CreateContainerResponse response = null;
            try
            {
                response = _dockerClient.Containers.CreateContainerAsync(containerParams).Result;
                _plcContainerId = response.ID;
            }
            catch (Exception)
            {
                throw;
            }

            try
            {
                _dockerClient.Containers.StartContainerAsync(_plcContainerId, new ContainerStartParameters()).Wait();
            }
            catch (Exception)
            {
                throw;

            }
        }

        private async Task CleanupContainerAsync()
        {
            IList<ContainerListResponse> containers = await _dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    Limit = 10,
                });

            foreach (var container in containers)
            {
                if (container.Image.Equals(_plcImage, StringComparison.InvariantCulture))
                {
                    try
                    {
                        await _dockerClient.Containers.StopContainerAsync(container.ID, new ContainerStopParameters());
                    }
                    catch (Exception)
                    {
                        throw new Exception($"Cannot stop the PLC container with id '{container.ID}'");
                    }
                    try
                    {
                        await _dockerClient.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters());
                    }
                    catch (Exception)
                    {
                        throw new Exception($"Cannot remove the PLC container with id '{container.ID}'");
                    }
                }
            }
        }

        // when testing locally, spin up your own registry and put the image in here
        //string _plcImage = "localhost:5000/opc-plc";
        readonly string _plcImage = "mcr.microsoft.com/iotedge/opc-plc";
        readonly string _plcPort = "50000";
        readonly DockerClient _dockerClient;
        readonly string _plcContainerId = string.Empty;
    }

    public sealed class PlcOpcUaServerFixture
    {
        public PlcOpcUaServer Plc { get; private set; }

        public PlcOpcUaServerFixture()
        {
            try
            {
                // Disable in CI
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH")))
                {
                    Plc = new PlcOpcUaServer();
                }
            }
            catch
            {
                Plc = null;
            }
        }
    }

    public sealed class TestDirectoriesFixture
    {

        public TestDirectoriesFixture()
        {
            try
            {
                if (!Directory.Exists($"{Directory.GetCurrentDirectory()}/tempdata"))
                {
                    Directory.CreateDirectory($"{Directory.GetCurrentDirectory()}/tempdata");
                }
            }
            catch (Exception)
            {
                throw;
            }
            try
            {
                if (File.Exists(SettingsConfiguration.LogFileName))
                {
                    File.Delete(SettingsConfiguration.LogFileName);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

    }

    public sealed class OpcPublisherFixture
    {
        public OpcPublisherFixture()
        {
            // init publisher logging
            //LogLevel = "debug";
            SettingsConfiguration.LogLevel = "info";
            if (Program.Instance.Logger == null)
            {
                Program.Instance.InitLogging();
            }

            ApplicationInstance _application = new ApplicationInstance {
                ApplicationName = "OpcPublisherUnitTest",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Opc.Publisher"
            };

            _application.LoadApplicationConfiguration(false).Wait();

            // check the application certificate.
            bool certOK = _application.CheckApplicationInstanceCertificate(false, 0).Result;
            if (!certOK)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            // create cert validator
            _application.ApplicationConfiguration.CertificateValidator = new CertificateValidator();
            _application.ApplicationConfiguration.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);


            // configure hub communication
            SettingsConfiguration.DefaultSendIntervalSeconds = 0;
            SettingsConfiguration.HubMessageSize = 0;

            // tie our unit test app it to out Program instance
            Program.Instance._application = _application;
        }

        private static void CertificateValidator_CertificateValidation(Opc.Ua.CertificateValidator validator, Opc.Ua.CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = true;
            }
        }
    }
}
