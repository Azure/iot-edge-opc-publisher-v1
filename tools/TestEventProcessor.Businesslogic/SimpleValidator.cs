// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace TestEventProcessor.Businesslogic
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.EventHubs;
    using Azure.Messaging.EventHubs.Consumer;
    using Azure.Messaging.EventHubs.Processor;
    using Azure.Storage.Blobs;
    using Newtonsoft.Json;
    using Serilog;

    /// <summary>
    /// Validates the value changes within IoT Hub Methods
    /// </summary>
    public class SimpleValidator
    {
        /// <summary>
        /// Dictionary containing all sequence numbers related to a timestamp
        /// </summary>
        private ConcurrentDictionary<string, List<int>> _missingSequences;
        /// <summary>
        /// Dictionary containing timestamps the were observed
        /// </summary>
        private ConcurrentQueue<string> _observedTimestamps;

        private ConcurrentDictionary<string, DateTime> _iotHubMessageEnqueuedTimes;
        /// <summary>
        /// Number of value changes per timestamp
        /// </summary>
        private uint _expectedValueChangesPerTimestamp;
        /// <summary>
        /// Time difference between values changes in milliseconds
        /// </summary>
        private uint _expectedIntervalOfValueChanges;
        /// <summary>
        /// Time difference between OPC UA Server fires event until Changes Received in IoT Hub in milliseconds 
        /// </summary>
        private uint _expectedMaximalDuration;
        /// <summary>
        /// Value that will be used to define range within timings expected as equal (in milliseconds)
        ///  Current Value need to be within range of Expected Value +/- threshold
        /// </summary>
        private int _thresholdValue;
        /// <summary>
        /// Format to be used for Timestamps
        /// </summary>
        private const string _dateTimeFormat = "yyyy-MM-dd HH:mm:ss.fffffffZ";
        /// <summary>
        /// Instance to write logs 
        /// </summary>
        private ILogger _logger;
        /// <summary>
        /// Connection string for integrated event hub endpoint of IoTHub
        /// </summary>
        private readonly string _iotHubConnectionString;
        /// <summary>
        /// Connection string for storage account
        /// </summary>
        private readonly string _storageConnectionString;
        /// <summary>
        /// Identifier of blob container within storage account
        /// </summary>
        /// <remarks>Default: Checkpoint</remarks>
        private readonly string _blobContainerName;
        /// <summary>
        /// Identifier of consumer group of event hub
        /// </summary>
        /// <remarks>Default: $Default</remarks>
        private readonly string _eventHubConsumerGroup;

        /// <summary>
        /// All expected value changes for timestamp are received
        /// </summary>
        public event EventHandler<TimestampCompletedEventArgs> TimestampCompleted;
        /// <summary>
        /// Missing timestamp is detected
        /// </summary>
        public event EventHandler<MissingTimestampEventArgs> MissingTimestamp;
        /// <summary>
        /// Total duration between sending from OPC UA Server until receiving at IoT Hub, was too long
        /// </summary>
        public event EventHandler<DurationExceededEventArgs> DurationExceeded;

        /// <summary>
        /// Create instance of SimpleValidator 
        /// </summary>
        /// <param name="logger">Instance to write logs</param>
        /// <param name="iotHubConnectionString">Connection string for integrated event hub endpoint of IoTHub</param>
        /// <param name="storageConnectionString">Connection string for storage account</param>
        /// <param name="expectedValueChangesPerTimestamp">Number of value changes per timestamp</param>
        /// <param name="expectedIntervalOfValueChanges">Time difference between values changes in milliseconds</param>
        /// <param name="blobContainerName">Identifier of blob container within storage account</param>
        /// <param name="eventHubConsumerGroup">Identifier of consumer group of event hub</param>
        /// <param name="thresholdValue">Value that will be used to define range within timings expected as equal (in milliseconds)</param>
        public SimpleValidator(ILogger logger,
            string iotHubConnectionString,
            string storageConnectionString,
            uint expectedValueChangesPerTimestamp,
            uint expectedIntervalOfValueChanges,
            uint expectedMaximalDuration,
            string blobContainerName = "checkpoint",
            string eventHubConsumerGroup = "$Default",
            int thresholdValue = 50)
        {
            if (expectedValueChangesPerTimestamp == 0)
            {
                throw new ArgumentNullException("Invalid configuration detected, expected value changes per timestamp can't be zero");
            }
            if (expectedIntervalOfValueChanges == 0)
            {
                throw new ArgumentNullException("Invalid configuration detected, expected interval of value changes can't be zero");
            }
            if (expectedMaximalDuration == 0)
            {
                throw new ArgumentNullException("Invalid configuration detected, maximal total duration can't be zero");
            }
            if (thresholdValue <= 0)
            {
                throw new ArgumentNullException("Invalid configuration detected, threshold can't be negative or zero");
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _iotHubConnectionString = iotHubConnectionString ?? throw new ArgumentNullException(nameof(iotHubConnectionString));
            _storageConnectionString = storageConnectionString ?? throw new ArgumentNullException(nameof(storageConnectionString));
            _blobContainerName = blobContainerName ?? throw new ArgumentNullException(nameof(blobContainerName));
            _eventHubConsumerGroup = eventHubConsumerGroup ?? throw new ArgumentNullException(nameof(eventHubConsumerGroup));
            _expectedValueChangesPerTimestamp = expectedValueChangesPerTimestamp;
            _expectedIntervalOfValueChanges = expectedIntervalOfValueChanges;
            _expectedMaximalDuration = expectedMaximalDuration;
            _thresholdValue = thresholdValue;
            _missingSequences = new ConcurrentDictionary<string, List<int>>(4, 500);
            _iotHubMessageEnqueuedTimes = new ConcurrentDictionary<string, DateTime>(4, 500);
            _observedTimestamps = new ConcurrentQueue<string>();
        }

        /// <summary>
        /// Method that runs asynchronously to connect to event hub and check
        /// a) if all expected value changes are delivered
        /// b) that time between value changes is expected
        /// </summary>
        /// <param name="token">Token to cancel the operation</param>
        /// <returns>Task that run until token is canceled</returns>
        public async Task RunAsync(CancellationToken token)
        {
            EventProcessorClient client = null;
            try
            {
                token.ThrowIfCancellationRequested();
                _logger.Information("Connecting to blob storage...");

                var blobContainerClient = new BlobContainerClient(_storageConnectionString, _blobContainerName);

                _logger.Information("Connecting to IoT Hub...");

                client = new EventProcessorClient(blobContainerClient, _eventHubConsumerGroup, _iotHubConnectionString);
                client.PartitionInitializingAsync += Client_PartitionInitializingAsync;
                client.ProcessEventAsync += Client_ProcessEventAsync;
                client.ProcessErrorAsync += Client_ProcessErrorAsync;

                _logger.Information("Starting monitoring of events...");
                await client.StartProcessingAsync(token);
                CheckForMissingValueChangesAsync(token).Start();
                CheckForMissingTimestampsAsync(token).Start();
                await Task.Delay(-1, token);
                
            }
            catch (OperationCanceledException oce)
            {
                if (oce.CancellationToken != token)
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while processing events from Event Processor host");
            }
            finally
            {
                if (client != null)
                {
                    client.PartitionInitializingAsync -= Client_PartitionInitializingAsync;
                    client.ProcessEventAsync -= Client_ProcessEventAsync;
                    client.ProcessErrorAsync -= Client_ProcessErrorAsync;
                }
                _logger.Information("Stopped monitoring of events...");
            }
        }

        /// <summary>
        /// Running a thread that analyze the value changes per timestamp 
        /// </summary>
        /// <param name="token">Token to cancel the thread</param>
        /// <returns>Task that run until token is canceled</returns>
        private Task CheckForMissingValueChangesAsync(CancellationToken token)
        {
            return new Task(() => {
                try
                {
                    var formatInfoProvider = new DateTimeFormatInfo();
                    token.ThrowIfCancellationRequested();
                    while (!token.IsCancellationRequested)
                    {
                        var entriesToDelete = new List<string>(50);
                        foreach (var missingSequence in _missingSequences)
                        {
                            var numberOfValueChanges = missingSequence.Value.Count;
                            if (numberOfValueChanges >= _expectedValueChangesPerTimestamp)
                            {
                                _logger.Information(
                                    "Received {NumberOfValueChanges} value changes for timestamp {Timestamp}",
                                    numberOfValueChanges, missingSequence.Key);

                                TimestampCompleted?.Invoke(this, new TimestampCompletedEventArgs(missingSequence.Key, numberOfValueChanges));
                                // don't check for gaps of sequence numbers because they reflect the for number of messages  
                                // send from OPC server to OPC publisher, it should be internally handled in OPCF stack

                                entriesToDelete.Add(missingSequence.Key);
                            }

                            // Check the total duration from OPC UA Server until IoT Hub
                            bool success = DateTime.TryParseExact(missingSequence.Key, _dateTimeFormat,
                                formatInfoProvider, DateTimeStyles.None, out var timeStamp);

                            if (!success)
                            {
                                _logger.Warning("Can't recreate Timestamp from string");
                            }

                            var iotHubEnqueuedTime = _iotHubMessageEnqueuedTimes[missingSequence.Key];
                            var durationDifference = iotHubEnqueuedTime.Subtract(timeStamp);
                            if (durationDifference.TotalMilliseconds < 0)
                            {
                                _logger.Warning("Total duration is negative number, , OPC UA Server time {OPCUATime}, IoTHub enqueue time {IoTHubTime}, delta {Diff}",
                                    timeStamp.ToString(_dateTimeFormat, formatInfoProvider),
                                    iotHubEnqueuedTime.ToString(_dateTimeFormat, formatInfoProvider),
                                    durationDifference);
                            }
                            if (Math.Round(durationDifference.TotalMilliseconds) > _expectedMaximalDuration)
                            {
                                _logger.Information("Total duration exceeded limit, OPC UA Server time {OPCUATime}, IoTHub enqueue time {IoTHubTime}, delta {Diff}",
                                    timeStamp.ToString(_dateTimeFormat, formatInfoProvider),
                                    iotHubEnqueuedTime.ToString(_dateTimeFormat, formatInfoProvider),
                                    durationDifference);

                                DurationExceeded?.Invoke(this, new DurationExceededEventArgs(timeStamp, iotHubEnqueuedTime));
                            }

                            // don'T check for duration between enqueued in IoTHub until processed here
                            // IoT Hub publish notifications slower than they can be received by IoT Hub
                            // ==> with longer runtime the difference between enqueued time and processing time will increase
                        }

                        // Remove all timestamps that are completed (all value changes received)
                        foreach (var entry in entriesToDelete)
                        {
                            var success = _missingSequences.TryRemove(entry, out var values);
                            success &= _iotHubMessageEnqueuedTimes.TryRemove(entry, out var enqueuedTime);

                            if (!success)
                            {
                                _logger.Error(
                                    "Could not remove timestamp {Timestamp} with all value changes from internal list",
                                    entry);
                            }
                            else
                            {
                                _logger.Information("[Success] All value changes received for {Timestamp}", entry);
                            }
                        }

                        // Log total amount of missing value changes for each timestamp that already reported 80% of value changes
                        foreach (var missingSequence in _missingSequences)
                        {
                            if (missingSequence.Value.Count > (int)(_expectedValueChangesPerTimestamp * 0.8))
                            {
                                _logger.Information(
                                    "For timestamp {Timestamp} there are {NumberOfMissing} value changes missing",
                                    missingSequence.Key,
                                    _expectedValueChangesPerTimestamp - missingSequence.Value.Count);
                            }
                        }
                        
                        Task.Delay(10000, token).Wait(token);
                    }
                }
                catch (OperationCanceledException oce)
                {
                    if (oce.CancellationToken == token)
                    {
                        return;
                    }
                    throw;
                }
            }, token);
        }

        /// <summary>
        /// Running a thread that analyze that timestamps continually received (with expected interval)
        /// </summary>
        /// <param name="token">Token to cancel the thread</param>
        /// <returns>Task that run until token is canceled</returns>
        private Task CheckForMissingTimestampsAsync(CancellationToken token)
        {
            return new Task(() => {
                try
                {
                    var formatInfoProvider = new DateTimeFormatInfo();
                    while (!token.IsCancellationRequested)
                    {
                        if (_observedTimestamps.Count >= 2)
                        {
                            bool success = _observedTimestamps.TryDequeue(out var olderTimestamp);
                            success &= _observedTimestamps.TryDequeue(out var newTimestamp);
                            success &= DateTime.TryParseExact(olderTimestamp, _dateTimeFormat,
                                formatInfoProvider, DateTimeStyles.None, out var older);
                            success &= DateTime.TryParseExact(newTimestamp, _dateTimeFormat,
                                formatInfoProvider, DateTimeStyles.None, out var newer);
                            if (!success)
                            {
                                _logger.Error("Can't dequeue timestamps from internal storage");
                            }

                            // compare on milliseconds isn't useful, instead try time window of 100 milliseconds
                            var expectedTime = older.AddMilliseconds(_expectedIntervalOfValueChanges);
                            if (newer.Hour != expectedTime.Hour
                            || newer.Minute != expectedTime.Minute
                            || newer.Second != expectedTime.Second
                            || newer.Millisecond < (expectedTime.Millisecond - _thresholdValue)
                            || newer.Millisecond > (expectedTime.Millisecond + _thresholdValue))
                            {
                                var expectedTS = expectedTime.ToString(_dateTimeFormat);
                                var olderTS = older.ToString(_dateTimeFormat);
                                var newerTS = newer.ToString(_dateTimeFormat);
                                _logger.Warning(
                                    "Missing timestamp, value changes for {ExpectedTs} not received, predecessor {Older} successor {Newer}",
                                    expectedTS,
                                    olderTS,
                                    newerTS);

                                MissingTimestamp?.Invoke(this, new MissingTimestampEventArgs(expectedTS, olderTS, newerTS));
                            }
                        }

                        Task.Delay(20000, token).Wait(token);
                    }
                }
                catch (OperationCanceledException oce)
                {
                    if (oce.CancellationToken == token)
                    {
                        return;
                    }
                    throw;
                }
            }, token);
        }

        /// <summary>
        /// Analyze payload of IoTHub message, adding timestamp and related sequence numbers into temporary
        /// </summary>
        /// <param name="arg"></param>
        /// <returns>Task that run until token is canceled</returns>
        private async Task Client_ProcessEventAsync(ProcessEventArgs arg)
        {
            if (!arg.HasEvent)
            {
                _logger.Warning("Received partition event without content");
                return;
            }

            var body = arg.Data.Body.ToArray();
            var content = Encoding.UTF8.GetString(body);
            dynamic json = JsonConvert.DeserializeObject(content);
            int valueChangesCount = 0;

            // TODO build variant that works with PubSub

            foreach (dynamic entry in json)
            {
                var sequence = (int)entry.SequenceNumber;
                var timestamp = ((DateTime)entry.Value.SourceTimestamp).ToString(_dateTimeFormat);

                _missingSequences.AddOrUpdate(
                    timestamp,
                    (ts) => {
                        return new List<int>(500) { sequence };
                    },
                    (ts, list) => {
                        list.Add(sequence);
                        return list;
                    });

                valueChangesCount++;

                if (!_observedTimestamps.Contains(timestamp))
                {
                    _observedTimestamps.Enqueue(timestamp);
                    
                    _iotHubMessageEnqueuedTimes.AddOrUpdate(
                        timestamp,
                        (_) => arg.Data.EnqueuedTime.UtcDateTime,
                        (ts, _) => arg.Data.EnqueuedTime.UtcDateTime);
                }
            }

            _logger.Verbose("Received {NumberOfValueChanges} from IoTHub, partition {PartitionId}, ", 
                valueChangesCount, 
                arg.Partition.PartitionId);
        }

        /// <summary>
        /// Event handler that ensures only newest events are processed
        /// </summary>
        /// <param name="arg">Init event args</param>
        /// <returns>Completed Task, no async work needed</returns>
        private Task Client_PartitionInitializingAsync(PartitionInitializingEventArgs arg)
        {
            _logger.Information("EventProcessorClient initializing, start with latest position for partition {PartitionId}", arg.PartitionId);
            arg.DefaultStartingPosition = EventPosition.Latest;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Event handler that logs errors from EventProcessorClient
        /// </summary>
        /// <param name="arg">Error event args</param>
        /// <returns>Completed Task, no async work needed</returns>
        private Task Client_ProcessErrorAsync(ProcessErrorEventArgs arg)
        {
            _logger.Error(arg.Exception, "Issue reported by EventProcessorClient, partition {PartitionId}, operation {Operation}",
                arg.PartitionId,
                arg.Operation);
            return Task.CompletedTask;
        }

    }
}
