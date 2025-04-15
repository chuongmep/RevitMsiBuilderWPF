using System.IO;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.Zip;
using RevitMsiBuilder.Models;
using WixSharp;
using WixSharp.CommonTasks;
using WixSharp.Controls;
using File = System.IO.File;

namespace RevitMsiBuilder.Services;

public interface IMsiBuilder
{
    string GenerateVersionString();
    string BuildMsi(AddinFile addinFile, IEnumerable<string> revitVersions, BuildConfiguration config);
    void CompressFile(string filePath, string outputFilePath, int compressionLevel = 9);
}

public class WixSharpMsiBuilder : IMsiBuilder
{
    private readonly ILogger Logger;

    public WixSharpMsiBuilder(ILogger logger)
    {
        Logger = logger;
    }

    public string GenerateVersionString()
    {
        // Version format: 1.0.YY.DDD where YY is last two digits of year and DDD is day of year
        DateTime now = DateTime.Now;
        string yearDigits = now.Year.ToString().Substring(2, 2);
        DateTime startOfYear = new DateTime(now.Year, 1, 1);
        string dayOfYear = (now - startOfYear).Days.ToString();

        return $"1.0.{yearDigits}.{dayOfYear}";
    }

    public string BuildMsi(AddinFile addinFile, IEnumerable<string> revitVersions, BuildConfiguration config)
    {
        // Ensure only one Revit version is used for naming (take the first one if multiple)
        string selectedRevitVersion = revitVersions.FirstOrDefault() ?? "Unknown";
        string formattedProjectName = $"{config.ProjectName}.{selectedRevitVersion}.{config.Version}";

        var project = new Project
        {
            Name = formattedProjectName,
            OutDir = config.OutputDirectory,
            Platform = Platform.x64,
            Description = config.ProjectDescription,
            UI = WUI.WixUI_InstallDir,
            Version = new Version(config.Version),
            OutFileName = formattedProjectName,
            Scope = InstallScope.perUser,
            MajorUpgrade = MajorUpgrade.Default,
            GUID = config.ProjectGUID,
            BackgroundImage = File.Exists(@"Resources/Icons/BackgroundImage.png")
                ? @"Resources/Icons/BackgroundImage.png"
                : null,
            BannerImage = File.Exists(@"Resources/Icons/BannerImage.png")
                ? @"Resources/Icons/BannerImage.png"
                : null,
            ControlPanelInfo =
            {
                Manufacturer =config.Manufacturer,
                HelpLink = "https://github.com/chuongmep/RevitMsiBuilderWPF/issues",
                Comments = $"Revit Add-in: {addinFile.Name}",
                ProductIcon = File.Exists(@"Resources/Icons/ShellIcon.ico")
                    ? @"Resources/Icons/ShellIcon.ico"
                    : null
            }
        };

        // Configure MSI project
        MajorUpgrade.Default.AllowSameVersionUpgrades = true;
        project.RemoveDialogsBetween(NativeDialogs.WelcomeDlg, NativeDialogs.InstallDirDlg);

        // Build directory structure
        project.Dirs = BuildDirectoryStructure(addinFile, revitVersions, config.InstallForAllUsers);

        // Create output directory if it doesn't exist
        Directory.CreateDirectory(config.OutputDirectory);

        try
        {
            Logger.Log($"Building MSI for {addinFile.Name}...");
            return project.BuildMsi();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error building MSI: {ex.Message}");
            Logger.Log($"Error building MSI: {ex.Message}");
            return null;
        }
    }

    private Dir[] BuildDirectoryStructure(AddinFile addinFile, IEnumerable<string> revitVersions,
        bool installForAllUsers)
    {
        string installationDir = installForAllUsers
            ? @"%CommonAppDataFolder%\Autodesk\Revit\Addins\"
            : @"%AppDataFolder%\Autodesk\Revit\Addins\";
        Regex versionRegex = new Regex(@"\d{4}");
        // Use HashSet to track unique files
        var uniqueFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var versionStorages = new Dictionary<string, List<WixEntity>>();

        foreach (var version in revitVersions)
        {
            var versionFiles = new List<WixEntity>();
            var contentsFiles = new List<WixEntity>();

            // Add .addin file for this version
            if (File.Exists(addinFile.FilePath) && uniqueFiles.Add(addinFile.FilePath))
            {
                versionFiles.Add(new WixSharp.File(addinFile.FilePath)
                {
                    Id = new Id($"addin_{version}_{Path.GetFileNameWithoutExtension(addinFile.FilePath)}")
                });
            }

            // Add assembly files
            foreach (var assemblyPath in addinFile.AssemblyPaths)
            {
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                var parentDirName = Path.GetFileName(assemblyDir);
                bool isVersionSpecific = versionRegex.IsMatch(parentDirName) && parentDirName == version;

                // Add files if they match this version or are not version-specific
                if (isVersionSpecific || !versionRegex.IsMatch(parentDirName))
                {
                    // Add the specific DLL/EXE file
                    if (File.Exists(assemblyPath) &&
                        (assemblyPath.EndsWith(".dll") || assemblyPath.EndsWith(".exe") ||
                         assemblyPath.EndsWith(".config")) &&
                        uniqueFiles.Add(assemblyPath))
                    {
                        contentsFiles.Add(new WixSharp.File(assemblyPath)
                        {
                            Id = new Id($"file_{version}_{Path.GetFileNameWithoutExtension(assemblyPath)}")
                        });
                    }

                    // Add related files from the same directory
                    if (Directory.Exists(assemblyDir))
                    {
                        var relatedFiles = Directory.GetFiles(assemblyDir, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => f.EndsWith(".dll") || f.EndsWith(".exe") || f.EndsWith(".config"))
                            .Where(f => uniqueFiles.Add(f));

                        foreach (var file in relatedFiles)
                        {
                            contentsFiles.Add(new WixSharp.File(file)
                            {
                                Id = new Id($"file_{version}_{Path.GetFileNameWithoutExtension(file)}")
                            });
                        }
                    }
                }
            }

            // Add content files to the version directory
            if (contentsFiles.Any())
            {
                versionFiles.Add(new Dir("contents", contentsFiles.ToArray()));
            }

            // Add all files for this version
            if (versionFiles.Any())
            {
                versionStorages[version] = versionFiles;
            }
        }

        // Return the directory structure
        return new Dir[]
        {
            new InstallDir(installationDir, versionStorages.Select(v =>
                new Dir(v.Key, v.Value.ToArray())).Cast<WixEntity>().ToArray())
        };
    }

    public void CompressFile(string filePath, string outputFilePath, int compressionLevel = 9)
    {
        try
        {
            using (var outputStream = new ZipOutputStream(File.Create(outputFilePath)))
            {
                outputStream.SetLevel(compressionLevel);
                var buffer = new byte[4096];
                var entry = new ZipEntry(Path.GetFileName(filePath)) { DateTime = DateTime.Now };
                outputStream.PutNextEntry(entry);

                using (var fileStream = File.OpenRead(filePath))
                {
                    int bytesRead;
                    do
                    {
                        bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                        outputStream.Write(buffer, 0, bytesRead);
                    } while (bytesRead > 0);
                }

                outputStream.Finish();
                outputStream.Close();
                Console.WriteLine($"Zip file created: {outputFilePath}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error compressing file: {ex.Message}");
            Console.WriteLine($"Error creating ZIP: {ex.Message}");
        }
    }
}