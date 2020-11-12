namespace TestEventProcessor.Businesslogic
{
    /// <summary>
    /// Status of the current validator.
    /// </summary>
    public class ValidationStatus
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="isRunning"></param>
        public ValidationStatus(bool isRunning)
        {
            IsRunning = isRunning;
        }

        /// <summary>
        /// Flag if the Validation is currently running
        /// </summary>
        public bool IsRunning { get; }
    }
}