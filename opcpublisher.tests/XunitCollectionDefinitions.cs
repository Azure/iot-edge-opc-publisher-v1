// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace OpcPublisher
{
    using Xunit;

    /// <summary>
    /// Collection of tests which require the PLC container and OPC Publisher configuration.
    /// </summary>
    [CollectionDefinition("Need PLC and publisher config")]
    public class PlcAndAppConfigCollection : ICollectionFixture<TestDirectoriesFixture>, ICollectionFixture<PlcOpcUaServerFixture>, ICollectionFixture<OpcPublisherFixture>
    {
    }
}
