using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using RevitMsiBuilder.Models;

namespace RevitMsiBuilder.Services
{
    /// <summary>
    /// Interface for parsing Revit .addin files
    /// </summary>
    public interface IAddinFileParser
    {
        /// <summary>
        /// Parses a Revit .addin file and returns an AddinFile object
        /// </summary>
        /// <param name="filePath">Path to the .addin file</param>
        /// <returns>AddinFile object containing parsed data</returns>
        AddinFile ParseAddinFile(string filePath);
        
        /// <summary>
        /// Infers Revit versions based on directory structure or other indicators
        /// </summary>
        /// <param name="directoryPath">Base directory path to search from</param>
        /// <returns>List of detected Revit versions</returns>
        IEnumerable<string> InferRevitVersions(string directoryPath);
    }
    
    /// <summary>
    /// Default implementation of IAddinFileParser
    /// </summary>
    public class AddinFileParser : IAddinFileParser
    {
        private readonly ILogger _logger;
        private static readonly Regex VersionRegex = new Regex(@"\d{4}");
        
        public AddinFileParser(ILogger logger)
        {
            _logger = logger;
        }
        
        public AddinFile ParseAddinFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Addin file not found: {filePath}");
            }
            
            _logger.Log($"Parsing addin file: {filePath}");
            
            try
            {
                // Load and parse XML
                XDocument xdoc = XDocument.Load(filePath);
                XElement addInElement = xdoc.Root;
                
                if (addInElement == null || addInElement.Name.LocalName != "RevitAddIns")
                {
                    throw new InvalidDataException("Invalid addin file format: Root element must be 'RevitAddIns'");
                }
                
                // Get the first AddIn element (either Application or Command)
                XElement addIn = addInElement.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "AddIn");
                
                if (addIn == null)
                {
                    throw new InvalidDataException("No AddIn element found in the addin file");
                }
                
                // Get the name from the Name element
                string name = addIn.Element("Name")?.Value;
                if (string.IsNullOrEmpty(name))
                {
                    // Fallback to using filename without extension
                    name = Path.GetFileNameWithoutExtension(filePath);
                }
                
                // Get the assembly path
                string assemblyPath = addIn.Element("Assembly")?.Value;
                if (string.IsNullOrEmpty(assemblyPath))
                {
                    throw new InvalidDataException("Assembly path not found in addin file");
                }
                
                // Resolve relative path to absolute path
                string baseDir = Path.GetDirectoryName(filePath);
                string fullAssemblyPath = Path.GetFullPath(Path.Combine(baseDir, assemblyPath));
                
                // Create and return the AddinFile object
                var result = new AddinFile
                {
                    Name = name,
                    FilePath = filePath,
                    AssemblyPaths = new List<string> { fullAssemblyPath }
                };
                
                // Try to find dependencies
                FindDependencies(result, baseDir);
                
                _logger.Log($"Successfully parsed addin file: {name} with {result.AssemblyPaths.Count} assemblies");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Log($"Error parsing addin file: {ex.Message}");
                throw;
            }
        }
        
        private void FindDependencies(AddinFile addinFile, string baseDir)
        {
            // Get the directory of the main assembly
            if (addinFile.AssemblyPaths.Count == 0)
                return;
                
            string mainAssemblyPath = addinFile.AssemblyPaths[0];
            string assemblyDir = Path.GetDirectoryName(mainAssemblyPath);
            
            if (string.IsNullOrEmpty(assemblyDir) || !Directory.Exists(assemblyDir))
                return;
                
            // Get all DLL files in the same directory
            var dllFiles = Directory.GetFiles(assemblyDir, "*.dll", SearchOption.TopDirectoryOnly);
            
            // Add dependencies to the assembly paths if they're not already included
            foreach (var dllFile in dllFiles)
            {
                if (!addinFile.AssemblyPaths.Contains(dllFile, StringComparer.OrdinalIgnoreCase))
                {
                    addinFile.AssemblyPaths.Add(dllFile);
                }
            }
            
            // Also look for configuration files
            var configFiles = Directory.GetFiles(assemblyDir, "*.config", SearchOption.TopDirectoryOnly);
            foreach (var configFile in configFiles)
            {
                if (!addinFile.AssemblyPaths.Contains(configFile, StringComparer.OrdinalIgnoreCase))
                {
                    addinFile.AssemblyPaths.Add(configFile);
                }
            }
        }
        
        public IEnumerable<string> InferRevitVersions(string directoryPath)
        {
            var versions = new HashSet<string>();
            
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                _logger.Log($"Directory not found: {directoryPath}");
                return versions;
            }
            
            try
            {
                // Method 1: Look for year folders in the parent directory
                var parentDir = Directory.GetParent(directoryPath)?.FullName;
                if (parentDir != null)
                {
                    foreach (var dir in Directory.GetDirectories(parentDir))
                    {
                        string dirName = Path.GetFileName(dir);
                        if (VersionRegex.IsMatch(dirName))
                        {
                            versions.Add(dirName);
                        }
                    }
                }
                
                // Method 2: Look for version-named folders within the current directory
                // foreach (var dir in Directory.GetDirectories(directoryPath))
                // {
                //     string dirName = Path.GetFileName(dir);
                //     if (VersionRegex.IsMatch(dirName))
                //     {
                //         versions.Add(dirName);
                //     }
                // }
                
                // Method 3: Look for version in the directory name itself
                string currentDirName = Path.GetFileName(directoryPath);
                if (VersionRegex.IsMatch(currentDirName))
                {
                    versions.Add(currentDirName);
                }
                
                // If no versions found, add some default versions
                if (versions.Count == 0)
                {
                    // Get current year and next year
                    int currentYear = DateTime.Now.Year + 1;
                    for (int i = 0; i < 10; i++)
                    {
                        versions.Add((currentYear - i).ToString());
                    }
                }
                
                _logger.Log($"Inferred Revit versions: {string.Join(", ", versions)}");
                return versions;
            }
            catch (Exception ex)
            {
                _logger.Log($"Error inferring Revit versions: {ex.Message}");
                return versions;
            }
        }
    }
}