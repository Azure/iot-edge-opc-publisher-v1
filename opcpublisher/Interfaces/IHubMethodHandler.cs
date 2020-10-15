// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Azure.Devices.Client;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpcPublisher.Interfaces
{
    /// <summary>
    /// Class to handle all IoTHub/EdgeHub communication.
    /// </summary>
    public interface IHubMethodHandler
    {
        /// <summary>
        /// Dictionary of available IoTHub direct methods.
        /// </summary>
        Dictionary<string, MethodCallback> IotHubDirectMethods { get; }

        /// <summary>
        /// Handle publish node method call.
        /// </summary>
        Task<MethodResponse> HandlePublishNodesMethodAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Handle unpublish node method call.
        /// </summary>
        Task<MethodResponse> HandleUnpublishNodesMethodAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Handle unpublish all nodes method call.
        /// </summary>
        Task<MethodResponse> HandleUnpublishAllNodesMethodAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Handle method call to get all endpoints which published nodes.
        /// </summary>
        Task<MethodResponse> HandleGetConfiguredEndpointsMethodAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Handle method call to get list of configured nodes on a specific endpoint.
        /// </summary>
        Task<MethodResponse> HandleGetConfiguredNodesOnEndpointMethodAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Handle method call to get diagnostic information.
        /// </summary>
        Task<MethodResponse> HandleGetDiagnosticInfoMethodAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Handle method call to get log information.
        /// </summary>
        Task<MethodResponse> HandleGetDiagnosticStartupLogMethodAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Handle method call to get log information.
        /// </summary>
        Task<MethodResponse> HandleExitApplicationMethodAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Handle method call to get application information.
        /// </summary>
        Task<MethodResponse> HandleGetInfoMethodAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Method that is called for any unimplemented call. Just returns that info to the caller
        /// </summary>
        Task<MethodResponse> DefaultMethodHandlerAsync(MethodRequest methodRequest, object userContext);

        /// <summary>
        /// Exit the application.
        /// </summary>
        Task ExitApplicationAsync(int secondsTillExit);
    }
}
