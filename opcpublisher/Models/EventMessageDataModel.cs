// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace OpcPublisher
{
    /// <summary>
    /// Class used to pass data from the Event MonitoredItem event notification to the hub message processing.
    /// </summary>
    public class EventMessageDataModel : MessageDataModel
    {
        /// <summary>
        /// The value of the node.
        /// </summary>
        public List<EventValue> EventValues { get; set; }

        /// <summary>
        /// The publish time of the event.
        /// </summary>
        public string PublishTime { get; set; }

        /// <summary>
        /// Ctor of the object.
        /// </summary>
        public EventMessageDataModel()
        {
            EventValues = new List<EventValue>();
            PublishTime = null;
        }
    }

    /// <summary>
    /// Class used to pass key/value pairs of event field data from the MonitoredItem event notification to the hub message processing.
    /// </summary>
    public class EventValue
    {
        /// <summary>
        /// Ctor of the class
        /// </summary>
        public EventValue()
        {
            Name = string.Empty;
            Value = string.Empty;
            PreserveValueQuotes = false;
        }

        /// <summary>
        /// The name of the field.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The value of the field
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Flag to control quote handling in the value.
        /// </summary>
        public bool PreserveValueQuotes { get; set; }
    }
}
