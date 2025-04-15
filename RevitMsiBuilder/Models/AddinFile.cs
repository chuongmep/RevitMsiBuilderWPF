namespace RevitMsiBuilder.Models;

/// <summary>
/// Represents a Revit .addin file and its associated assemblies
/// </summary>
public class AddinFile
{
    /// <summary>
    /// Name of the add-in
    /// </summary>
    public string? Name { get; set; }
        
    /// <summary>
    /// Path to the .addin file
    /// </summary>
    public string FilePath { get; set; }
        
    /// <summary>
    /// List of paths to assemblies referenced by the add-in
    /// </summary>
    public List<string> AssemblyPaths { get; set; } = new List<string>();
        
    /// <summary>
    /// Type of add-in (Application or Command)
    /// </summary>
    public string? AddinType { get; set; }
        
    /// <summary>
    /// Add-in GUID if specified in the .addin file
    /// </summary>
    public Guid AddinGuid { get; set; }
        
    /// <summary>
    /// Vendor name if specified in the .addin file
    /// </summary>
    public string? VendorDescription { get; set; }

    /// <summary>
    /// Vendor ID if specified in the .addin file
    /// </summary>
    public string? VendorId { get; set; }

    /// <summary>
    /// Add-in description if specified in the .addin file
    /// </summary>
    public string? Description { get; set; }
}