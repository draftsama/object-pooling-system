using UnityEngine;
namespace OPS
{
    /// <summary>
    /// Log level for ObjectPooler diagnostic messages.
    /// </summary>
    public enum PoolLogLevel
    {
        None,       // No logging
        Errors,     // Only errors
        Warnings,   // Errors and warnings
        All         // All messages including info
    }

    /// <summary>
    /// Centralized logging for the ObjectPooling system.
    /// </summary>
    public static class PoolLogger
    {
        /// <summary>
        /// Controls the verbosity of diagnostic logging.
        /// Set to PoolLogLevel.None to disable all logging.
        /// </summary>
        public static PoolLogLevel LogLevel { get; set; } = PoolLogLevel.Warnings;

        public static void LogInfo(string message)
        {
            if (LogLevel >= PoolLogLevel.All)
            {
                Debug.Log($"[ObjectPooler] {message}");
            }
        }

        public static void LogWarning(string message)
        {
            if (LogLevel >= PoolLogLevel.Warnings)
            {
                Debug.LogWarning($"[ObjectPooler] {message}");
            }
        }

        public static void LogError(string message)
        {
            if (LogLevel >= PoolLogLevel.Errors)
            {
                Debug.LogError($"[ObjectPooler] {message}");
            }
        }
    }
}