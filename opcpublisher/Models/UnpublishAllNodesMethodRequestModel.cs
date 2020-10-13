// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace OpcPublisher
{
    /// <summary>
    /// Model for an unpublish all nodes request.
    /// </summary>
    public class UnpublishAllNodesMethodRequestModel
    {
        public UnpublishAllNodesMethodRequestModel(string endpointUrl = null)
        {
            EndpointUrl = endpointUrl;
        }

        public string EndpointUrl { get; set; }
    }
}
