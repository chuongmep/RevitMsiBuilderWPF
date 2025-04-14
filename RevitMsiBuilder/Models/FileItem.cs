using System.IO;

namespace RevitMsiBuilder.Models;

/// <summary>
/// Represents a file in the file list
/// </summary>
public class FileItem
{
    /// <summary>
    /// Name of the file (without path)
    /// </summary>
    public string FileName { get; set; }
        
    /// <summary>
    /// Full path to the file
    /// </summary>
    public string FilePath { get; set; }
        
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize
    {
        get
        {
            if (File.Exists(FilePath))
            {
                return new FileInfo(FilePath).Length;
            }
            return 0;
        }
    }
        
    /// <summary>
    /// Last modified date of the file
    /// </summary>
    public DateTime LastModified
    {
        get
        {
            if (File.Exists(FilePath))
            {
                return File.GetLastWriteTime(FilePath);
            }
            return DateTime.MinValue;
        }
    }
        
    /// <summary>
    /// File extension
    /// </summary>
    public string Extension => Path.GetExtension(FilePath);
}