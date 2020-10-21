// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Globalization;

namespace TestEventProcessor
{
    using Azure.Messaging.EventHubs;
    using Azure.Messaging.EventHubs.Consumer;
    using Azure.Messaging.EventHubs.Processor;
    using Azure.Storage.Blobs;
    using Mono.Options;
    using Newtonsoft.Json;
    using Serilog;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    class Program
    {
        /// <summary>
        /// Dictionary containing all sequence numbers related to a timestamp
        /// </summary>
        private static ConcurrentDictionary<string, List<int>> _missingSequences;
        /// <summary>
        /// Dictionary containing timestamps the were observed
        /// </summary>
        private static ConcurrentQueue<string> _observedTimestamps;
        /// <summary>
        /// Number of value changes per timestamp
        /// </summary>
        private static int _expectedValueChangesPerTimestamp;
        /// <summary>
        /// Time difference between values changes in milliseconds
        /// </summary>
        private static uint _expectedIntervalOfValueChanges;
        /// <summary>
        /// Format to be used for Timestamps
        /// </summary>
        private const string _dateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        static async Task Main(string[] args)
        {
            string iotHubConnectionString = null;
            string storageConnectionString = null;
            string blobContainerName = "checkpoint";
            string eventHubConsumerGroup = "$Default";
            bool showHelp = false;
            _missingSequences = new ConcurrentDictionary<string, List<int>>(4, 500);
            _observedTimestamps = new ConcurrentQueue<string>();

            var options = new OptionSet
            {
                {"c|connectionString=", "The connection string of the IoT Hub Device/Module that receives telemetry", s => iotHubConnectionString = s },
                {"sc|storageConnectionString=", "The connection string of the storage account to store checkpoints.", s => storageConnectionString = s },
                {"ee|expectedEvents=", "The amount of value changes per ServerTimestamp that is expected", (int i) => _expectedValueChangesPerTimestamp = i},
                {"ei|expectedInterval=", "The time in milliseconds between value changes that is expected", (uint i) => _expectedIntervalOfValueChanges = i},
                {"h|help",  "show this message and exit", b => showHelp = b != null }
            };

            options.Parse(args);

            if (showHelp)
            {
                ShowHelp(options);
                return;
            }

            Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}][{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

            Log.Information("Connecting to blob storage...");

            var blobContainerClient = new BlobContainerClient(storageConnectionString, blobContainerName);

            Log.Information("Connecting to IoT Hub...");

            var client = new EventProcessorClient(blobContainerClient, eventHubConsumerGroup, iotHubConnectionString);
            client.PartitionInitializingAsync += Client_PartitionInitializingAsync;
            client.ProcessEventAsync += Client_ProcessEventAsync;
            client.ProcessErrorAsync += Client_ProcessErrorAsync;

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, cancelArgs) =>
            {
                if (cancelArgs.SpecialKey == ConsoleSpecialKey.ControlC)
                {
                    cts.Cancel();
                }
            };

            Log.Information("Starting monitoring of events...");
            await client.StartProcessingAsync(cts.Token);
            CheckForGapsAndMissingValueChangesAsync(cts.Token).Start();
            CheckForMissingTimestampsAsync(cts.Token).Start();
            await Task.Delay(-1, cts.Token);

            Log.Information("Stopped monitoring of events...");
        }

        /// <summary>
        /// Running a thread that analyze the value changes per timestamp and identifying gaps
        /// </summary>
        /// <param name="token">Token to cancel the thread</param>
        /// <returns>Task that run until token is canceled</returns>
        private static Task CheckForGapsAndMissingValueChangesAsync(CancellationToken token)
        {
            return new Task(() =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    while (!token.IsCancellationRequested)
                    {
                        var entriesToDelete = new List<string>(50);
                        foreach (var missingSequence in _missingSequences)
                        {
                            var numberOfValueChanges = missingSequence.Value.Count;
                            if (numberOfValueChanges >= _expectedValueChangesPerTimestamp)
                            {
                                Log.Information(
                                    "Received {NumberOfValueChanges} value changes for timestamp {Timestamp}",
                                    numberOfValueChanges, missingSequence.Key);

                                // Analyze gaps in sequence number of value changes
                                var orderedSequences = missingSequence.Value.Distinct().OrderBy(i => i).ToList();

                                for (int i = 0, j = 1; i < (orderedSequences.Count - 1); i++, j++)
                                {
                                    var nextSequence = orderedSequences[i] + 1;
                                    if (orderedSequences[i] != orderedSequences[j]
                                        && nextSequence != orderedSequences[j])
                                    {
                                        Log.Warning(
                                            "Gap in sequence number for timestamp {Timestamp} expected {expected1} or {expected2} but was {actual} (Missing {MissingValueChanges})",
                                            missingSequence.Key,
                                            orderedSequences[i],
                                            nextSequence,
                                            orderedSequences[j],
                                            orderedSequences[j] - nextSequence);
                                    }
                                }

                                entriesToDelete.Add(missingSequence.Key);
                            }
                        }

                        // Remove all timestamps that are completed (all value changes received)
                        foreach (var entry in entriesToDelete)
                        {
                            var success = _missingSequences.TryRemove(entry, out var values);
                            if (!success)
                            {
                                Log.Error(
                                    "Could not remove timestamp {Timestamp} with all value changes from internal list",
                                    entry);
                            }
                            else
                            {
                                Log.Information("[Success] All value changes received for {Timestamp}", entry);
                            }
                        }

                        // Log total amount of missing value changes for each timestamp that already reported 80% of value changes
                        foreach (var missingSequence in _missingSequences)
                        {
                            if (missingSequence.Value.Count > (int) (_expectedValueChangesPerTimestamp * 0.8))
                            {
                                Log.Information(
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
        private static Task CheckForMissingTimestampsAsync(CancellationToken token)
        {
            return new Task(() =>
            {
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
                                Log.Error("Can't dequeue timestamps from internal storage");
                            }

                            // compare on milliseconds isn't useful, instead try time window of 100 milliseconds
                            var expectedTime = older.AddMilliseconds(_expectedIntervalOfValueChanges);
                            if (newer.Hour != expectedTime.Hour
                            || newer.Minute != expectedTime.Minute
                            || newer.Second != expectedTime.Second
                            || newer.Millisecond < (expectedTime.Millisecond - 50)
                            || newer.Millisecond > (expectedTime.Millisecond + 50))
                            {
                                Log.Warning(
                                    "Missing timestamp, value changes for {ExpectedTs} not received, predecessor {Older} successor {Newer}",
                                    expectedTime.ToString(_dateTimeFormat),
                                    older.ToString(_dateTimeFormat),
                                    newer.ToString(_dateTimeFormat));
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
        /// <returns></returns>
        private static async Task Client_ProcessEventAsync(ProcessEventArgs arg)
        {
            var body = arg.Data.Body.ToArray();
            var content = Encoding.UTF8.GetString(body);
            dynamic json = JsonConvert.DeserializeObject(content);
            int valueChangesCount = 0;

            foreach (dynamic entry in json)
            {
                var sequence = (int)entry.SequenceNumber;
                var timestamp = ((DateTime)entry.Value.SourceTimestamp).ToString(_dateTimeFormat);

                _missingSequences.AddOrUpdate(
                    timestamp,
                    (ts) =>
                    {
                        return new List<int>(500) {sequence};
                    },
                    (ts, list) =>
                    {
                        list.Add(sequence);
                        return list;
                    });

                valueChangesCount++;

                if (!_observedTimestamps.Contains(timestamp))
                {
                    _observedTimestamps.Enqueue(timestamp);
                }
            }
        }

        /// <summary>
        /// Print the command line options 
        /// </summary>
        /// <param name="optionSet">configured Options</param>
        private static void ShowHelp(OptionSet optionSet)
        {
            if (optionSet == null)
            {
                throw new ArgumentNullException(nameof(optionSet));
            }

            Console.WriteLine("Usage: TesEventProcessor");
            Console.WriteLine();
            Console.WriteLine("Options:");
            optionSet.WriteOptionDescriptions(Console.Out);
        }

        /// <summary>
        /// Event handler that ensures only newest events are processed
        /// </summary>
        /// <param name="arg">Init event args</param>
        /// <returns>Completed Task, no async work needed</returns>
        private static Task Client_PartitionInitializingAsync(PartitionInitializingEventArgs arg)
        {
            arg.DefaultStartingPosition = EventPosition.Latest;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Event handler that logs errors from EventProcessorClient
        /// </summary>
        /// <param name="arg">Error event args</param>
        /// <returns>Completed Task, no async work needed</returns>
        private static Task Client_ProcessErrorAsync(ProcessErrorEventArgs arg)
        {
            Log.Error(arg.Exception, "Issue reported by EventProcessorClient");
            return Task.CompletedTask;
        }
    }
}
