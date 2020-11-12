using System;
using System.Threading.Tasks;

namespace TestEventProcessor.Businesslogic
{
    /// <summary>
    /// Interface to validate incoming message on an IoT Hub.
    /// </summary>
    public interface ISimpleValidator
    {
        /// <summary>
        /// All expected value changes for timestamp are received
        /// </summary>
        event EventHandler<TimestampCompletedEventArgs> TimestampCompleted;

        /// <summary>
        /// Missing timestamp is detected
        /// </summary>
        event EventHandler<MissingTimestampEventArgs> MissingTimestamp;

        /// <summary>
        /// Total duration between sending from OPC UA Server until receiving at IoT Hub, was too long
        /// </summary>
        event EventHandler<DurationExceededEventArgs> DurationExceeded;

        /// <summary>
        /// Method that runs asynchronously to connect to event hub and check
        /// a) if all expected value changes are delivered
        /// b) that time between value changes is expected
        /// </summary>
        /// <param name="token">Token to cancel the operation</param>
        /// <returns>Task that run until token is canceled</returns>
        Task StartAsync(ValidatorConfiguration configuration);

        Task StopAsync();

        ValidationStatus Status { get; }
    }
}