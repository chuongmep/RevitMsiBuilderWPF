using System.IO;

namespace RevitMsiBuilder.Models;

/// <summary>
/// Configuration for the MSI build process
/// </summary>
public class BuildConfiguration
{
    /// <summary>
    /// Output directory for the MSI file
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "RevitMsiBuilder", "Output");
        
    /// <summary>
    /// Target Revit version
    /// </summary>
    public string RevitVersion { get; set; }
        
    /// <summary>
    /// Name of the project/add-in
    /// </summary>
    public string? ProjectName { get; set; }

    public string ProjectGUID { get; set; }

    public string ProjectDescription { get; set; }
        
    /// <summary>
    /// Version string for the MSI
    /// </summary>
    public string Version { get; set; }
        
    /// <summary>
    /// Whether to create a ZIP file in addition to the MSI
    /// </summary>
    public bool CreateZip { get; set; } = true;
        
    /// <summary>
    /// Whether to install for all users (requires elevation)
    /// </summary>
    public bool InstallForAllUsers { get; set; } = false;
        
    /// <summary>
    /// Custom manufacturer name (defaults to vendor name in addin or "Autodesk")
    /// </summary>
    public string Manufacturer { get; set; } = "Autodesk";
}