using Opc.Ua;
using Opc.Ua.Client;

namespace OpcPublisher
{
    /// <summary>
    /// Class to encapsulate OPC UA monitored item API.
    /// </summary>
    public class OpcUaMonitoredItemWrapper
    {
        public MonitoredItem MonitoredItem => _monitoredItem;

        public uint AttributeId
        {
            get
            {
                return _monitoredItem.AttributeId;
            }
            set
            {
                _monitoredItem.AttributeId = value;
            }
        }

        public bool DiscardOldest
        {
            get
            {
                return _monitoredItem.DiscardOldest;
            }
            set
            {
                _monitoredItem.DiscardOldest = value;
            }
        }

        public string DisplayName
        {
            get
            {
                return _monitoredItem.DisplayName;
            }
            set
            {
                _monitoredItem.DisplayName = value;
            }
        }

        public MonitoringMode MonitoringMode
        {
            get
            {
                return _monitoredItem.MonitoringMode;
            }
            set
            {
                _monitoredItem.MonitoringMode = value;
            }
        }

        public uint QueueSize
        {
            get
            {
                return _monitoredItem.QueueSize;
            }
            set
            {
                _monitoredItem.QueueSize = value;
            }
        }

        public int SamplingInterval
        {
            get
            {
                return _monitoredItem.SamplingInterval;
            }
            set
            {
                _monitoredItem.SamplingInterval = value;
            }
        }

        public NodeId StartNodeId
        {
            get
            {
                return _monitoredItem.StartNodeId;
            }
            set
            {
                _monitoredItem.StartNodeId = value;
            }
        }

        public OpcUaMonitoredItemWrapper()
        {
            _monitoredItem = new MonitoredItem();
        }


        public event MonitoredItemNotificationEventHandler Notification
        {
            add
            {
                _monitoredItem.Notification += value;

            }
            remove
            {
                _monitoredItem.Notification -= value;
            }
        }

        private readonly MonitoredItem _monitoredItem;
    }
}
