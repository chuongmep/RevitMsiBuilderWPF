using System;
using System.Collections.Generic;
using System.IO;

namespace RevitMsiBuilder.Services
{
    /// <summary>
    /// Interface for logging functionality
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Event that fires when a new log message is added
        /// </summary>
        event EventHandler<string> LogAdded;
        
        /// <summary>
        /// Logs a message to the configured output
        /// </summary>
        /// <param name="message">The message to log</param>
        void Log(string message);
        
        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="exception">The exception if available</param>
        void LogError(string message, Exception exception = null);
        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">The warning message</param>
        void LogWarning(string message);
        
        /// <summary>
        /// Saves the log to a file
        /// </summary>
        /// <param name="filePath">Path to save the log file</param>
        /// <returns>True if successful, false otherwise</returns>
        bool SaveLogToFile(string filePath);
    }
    
    /// <summary>
    /// Default implementation of ILogger
    /// </summary>
    public class Logger : ILogger
    {
        private readonly List<string> _logEntries = new List<string>();
        
        /// <summary>
        /// Event that fires when a new log message is added
        /// </summary>
        public event EventHandler<string> LogAdded;
        
        /// <summary>
        /// Controls whether log messages are also written to the console
        /// </summary>
        public bool EnableConsoleOutput { get; set; } = true;
        
        /// <summary>
        /// Controls whether timestamps are included in log messages
        /// </summary>
        public bool IncludeTimestamps { get; set; } = true;
        
        /// <summary>
        /// Logs a message to the configured output
        /// </summary>
        /// <param name="message">The message to log</param>
        public void Log(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
                
            string formattedMessage = FormatLogMessage(message);
            _logEntries.Add(formattedMessage);
            
            if (EnableConsoleOutput)
            {
                Console.WriteLine(formattedMessage);
            }
            
            // Raise the event
            LogAdded?.Invoke(this, formattedMessage);
        }
        
        /// <summary>
        /// Logs an error message
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="exception">The exception if available</param>
        public void LogError(string message, Exception exception = null)
        {
            string errorMessage = $"ERROR: {message}";
            
            if (exception != null)
            {
                errorMessage += $" - {exception.Message}";
                if (exception.StackTrace != null)
                {
                    errorMessage += $"\nStack Trace: {exception.StackTrace}";
                }
            }
            
            Log(errorMessage);
            
            if (EnableConsoleOutput)
            {
                // Save console color
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(FormatLogMessage(errorMessage));
                // Restore console color
                Console.ForegroundColor = originalColor;
            }
        }
        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">The warning message</param>
        public void LogWarning(string message)
        {
            string warningMessage = $"WARNING: {message}";
            Log(warningMessage);
            
            if (EnableConsoleOutput)
            {
                // Save console color
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(FormatLogMessage(warningMessage));
                // Restore console color
                Console.ForegroundColor = originalColor;
            }
        }
        
        /// <summary>
        /// Formats a log message with optional timestamp
        /// </summary>
        /// <param name="message">The message to format</param>
        /// <returns>The formatted log message</returns>
        private string FormatLogMessage(string message)
        {
            return IncludeTimestamps
                ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}"
                : message;
        }
        
        /// <summary>
        /// Saves the log to a file
        /// </summary>
        /// <param name="filePath">Path to save the log file</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveLogToFile(string filePath)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Write all log entries to file
                File.WriteAllLines(filePath, _logEntries);
                Log($"Log saved to: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to save log to file: {filePath}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Clears all log entries
        /// </summary>
        public void ClearLog()
        {
            _logEntries.Clear();
            Log("Log cleared");
        }
        
        /// <summary>
        /// Gets all log entries as a single string
        /// </summary>
        /// <returns>All log entries concatenated with newlines</returns>
        public string GetFullLog()
        {
            return string.Join(Environment.NewLine, _logEntries);
        }
    }
}