// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Newtonsoft.Json;

namespace OpcPublisher
{
    /// <summary>
    /// Model for an exit application request.
    /// </summary>
    public class ExitApplicationMethodRequestModel
    {
        public ExitApplicationMethodRequestModel()
        {
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public int SecondsTillExit { get; set; }
    }
}
