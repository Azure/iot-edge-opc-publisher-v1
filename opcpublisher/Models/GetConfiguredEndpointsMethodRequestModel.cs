// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;

namespace OpcPublisher
{
    /// <summary>
    /// Model for a get configured endpoints request.
    /// </summary>
    public class GetConfiguredEndpointsMethodRequestModel
    {
        public GetConfiguredEndpointsMethodRequestModel(ulong? continuationToken = null)
        {
            ContinuationToken = continuationToken;
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public ulong? ContinuationToken { get; set; }
    }
}
