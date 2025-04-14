using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using WixSharp;
using WixSharp.CommonTasks;
using WixSharp.Controls;
using File = System.IO.File;
using Path = System.IO.Path;

namespace RevitMsiBuilder;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    const string installationDir = @"%AppDataFolder%\Autodesk\Revit\Addins\";
    const string defaultOutputDir = "output";
    static string version = $"1.0.{GetLastTwoDigitOfYear()}.{GetDayInYear()}";
    static readonly Regex versionRegex = new Regex(@"\d{4}");
    const string defaultProjectName = "RevitAddinMsi"; // Fallback if no name provided
    private string _addinPath;
    private ObservableCollection<FileItem> _files = new ObservableCollection<FileItem>();
    private List<string> _revitVersions = new List<string>();
    private TextWriter _consoleWriter;

    public MainWindow()
    {
        InitializeComponent();
        FilesDataGrid.ItemsSource = _files;
        _consoleWriter = new TextBoxWriter(LogConsole);
        Console.SetOut(_consoleWriter);
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Addin files (*.addin)|*.addin|All files (*.*)|*.*",
            Title = "Select .addin File"
        };

        if (dialog.ShowDialog() == true)
        {
            _addinPath = dialog.FileName;
            SelectedPathText.Text = _addinPath;
            LoadFilesAndVersions();
        }
    }

    private void LoadFilesAndVersions()
    {
        _files.Clear();
        _revitVersions.Clear();
        RevitVersionComboBox.Items.Clear();

        if (string.IsNullOrEmpty(_addinPath) || !File.Exists(_addinPath))
        {
            LogConsole.AppendText("Error: Invalid .addin file path.\n");
            return;
        }

        // Parse .addin file
        try
        {
            var (assemblyPaths, addinName) = ParseAddinFile(_addinPath);
            foreach (var path in assemblyPaths)
            {
                _files.Add(new FileItem
                {
                    FileName = Path.GetFileName(path),
                    FilePath = path
                });
            }

            // Infer Revit versions
            _revitVersions = InferRevitVersions(Path.GetDirectoryName(_addinPath)).ToList();
            if (!_revitVersions.Any())
            {
                // get current Year and create list with 10 years close to it
                var nextYear = DateTime.Now.Year+1;
                var years = new List<int>();
                for (int i = 0; i < 10; i++)
                {
                    years.Add(nextYear - i);
                }

                _revitVersions.AddRange(years.Select(y => $"{y}"));
            }

            foreach (var version in _revitVersions)
            {
                RevitVersionComboBox.Items.Add(version);
            }

            if (_revitVersions.Any())
            {
                RevitVersionComboBox.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            LogConsole.AppendText($"Error parsing .addin file: {ex.Message}\n");
        }
    }

    static (List<string> AssemblyPaths, string AddinName) ParseAddinFile(string addinFile)
    {
        var doc = XDocument.Load(addinFile);
        var assemblyPaths = new List<string>();
        string addinName = "Unknown";

        var addins = doc.Descendants("AddIn");
        foreach (var addin in addins)
        {
            var assembly = addin.Element("Assembly")?.Value;
            addinName = addin.Element("Name")?.Value ?? addinName;
            if (string.IsNullOrEmpty(assembly))
            {
                Console.WriteLine($"Warning: No Assembly specified in .addin file for AddIn {addinName}.");
                continue;
            }

            string resolvedPath;
            if (Path.IsPathRooted(assembly))
            {
                resolvedPath = assembly;
            }
            else
            {
                resolvedPath = Path.Combine(Path.GetDirectoryName(addinFile), assembly);
            }

            try
            {
                resolvedPath = Path.GetFullPath(resolvedPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Invalid assembly path '{assembly}' in .addin file: {ex.Message}");
                continue;
            }

            if (File.Exists(resolvedPath))
            {
                if (!assemblyPaths.Contains(resolvedPath))
                {
                    assemblyPaths.Add(resolvedPath);
                    Console.WriteLine($"Found assembly: {resolvedPath}");
                }
            }
            else
            {
                Console.WriteLine($"Warning: Assembly '{resolvedPath}' specified in .addin file does not exist.");
            }
        }

        return (assemblyPaths, addinName);
    }

    static IEnumerable<string> InferRevitVersions(string addinDir)
    {
        var versions = new HashSet<string>();
        if (Directory.Exists(addinDir))
        {
            var dirs = Directory.GetDirectories(addinDir);
            foreach (var d in dirs)
            {
                var match = versionRegex.Match(Path.GetFileName(d));
                if (match.Success && int.TryParse(match.Value, out int year) && year >= 2018 && year <= 2025)
                {
                    versions.Add(match.Value);
                }
            }
        }

        return versions;
    }
    static string GetDayInYear()
    {
        DateTime now = DateTime.Now;
        DateTime startOfYear = new DateTime(now.Year, 1, 1);
        return (now - startOfYear).Days.ToString();
    }

    static string GetLastTwoDigitOfYear()
    {
        DateTime now = DateTime.Now;
        return now.Year.ToString().Substring(2, 2);
    }


    private void RevitVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Optional: Update UI or logic based on selected version
    }

    private void DeployButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_addinPath))
        {
            LogConsole.AppendText("Error: No .addin file selected.\n");
            return;
        }

        if (RevitVersionComboBox.SelectedItem == null)
        {
            LogConsole.AppendText("Error: No Revit version selected.\n");
            return;
        }

        try
        {
            string selectedVersion = RevitVersionComboBox.SelectedItem.ToString();
            string outputDir = "output";
            string projectName = SanitizeProjectName(Path.GetFileNameWithoutExtension(_addinPath));

            var (assemblyPaths, addinName) = ParseAddinFile(_addinPath);
            string msiPath = BuildMsi(_addinPath, assemblyPaths, new[] { selectedVersion }, outputDir, addinName,
                projectName);

            if (msiPath != null)
            {
                string zipPath = Path.Combine(outputDir, $"{projectName}-{version}.zip");
                CompressFile(msiPath, zipPath);
                LogConsole.AppendText($"MSI and ZIP created successfully at {msiPath} and {zipPath}\n");
            }
            else
            {
                LogConsole.AppendText("Error: MSI build failed.\n");
            }
        }
        catch (Exception ex)
        {
            LogConsole.AppendText($"Error during deployment: {ex.Message}\n");
        }
    }

    static string BuildMsi(string addinFile, List<string> assemblyPaths, IEnumerable<string> revitVersions,
        string outputDir, string addinName, string projectName)
    {
        var fileName = new StringBuilder().Append(projectName).Append("-").Append(version);
        var project = new Project
        {
            Name = projectName,
            OutDir = outputDir,
            Platform = Platform.x64,
            Description = $"Revit Add-in: {addinName}",
            UI = WUI.WixUI_InstallDir,
            Version = new Version(version),
            OutFileName = fileName.ToString(),
            Scope = InstallScope.perUser,
            MajorUpgrade = MajorUpgrade.Default,
            GUID = new Guid("A46C86A0-71A5-460B-8536-98D3ED43B574"),
            BackgroundImage = File.Exists(@"Resources/Icons/BackgroundImage.png")
                ? @"Resources/Icons/BackgroundImage.png"
                : null,
            BannerImage = File.Exists(@"Resources/Icons/BannerImage.png") ? @"Resources/Icons/BannerImage.png" : null,
            ControlPanelInfo =
            {
                Manufacturer = "Autodesk",
                HelpLink = "https://github.com/chuongmep/RevitAddInManager/issues",
                Comments = $"Revit Add-in: {addinName}",
                ProductIcon = File.Exists(@"Resources/Icons/ShellIcon.ico") ? @"Resources/Icons/ShellIcon.ico" : null
            }
        };

        MajorUpgrade.Default.AllowSameVersionUpgrades = true;
        project.RemoveDialogsBetween(NativeDialogs.WelcomeDlg, NativeDialogs.InstallDirDlg);

        var versionStorages = new Dictionary<string, List<WixEntity>>();
        foreach (var version in revitVersions)
        {
            var versionFiles = new List<WixEntity>();
            var contentsFiles = new List<WixEntity>();

            if (File.Exists(addinFile))
            {
                versionFiles.Add(new WixSharp.File(addinFile));
                Console.WriteLine($"Added .addin file for version {version}: {addinFile}");
            }

            foreach (var assemblyPath in assemblyPaths)
            {
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                var parentDirName = Path.GetFileName(assemblyDir);
                bool isVersionSpecific = versionRegex.IsMatch(parentDirName) && parentDirName == version;

                if (isVersionSpecific || !versionRegex.IsMatch(parentDirName))
                {
                    if (File.Exists(assemblyPath) && (assemblyPath.EndsWith(".dll") || assemblyPath.EndsWith(".exe") ||
                                                      assemblyPath.EndsWith(".config")))
                    {
                        contentsFiles.Add(new WixSharp.File(assemblyPath));
                        Console.WriteLine($"Added file to contents for version {version}: {assemblyPath}");
                    }

                    if (Directory.Exists(assemblyDir))
                    {
                        var relatedFiles = Directory.GetFiles(assemblyDir, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => f.EndsWith(".dll") || f.EndsWith(".exe") || f.EndsWith(".config"))
                            .Where(f => !contentsFiles.Any(cf =>
                                cf is WixSharp.File wf && wf.Name.Equals(f, StringComparison.OrdinalIgnoreCase)));

                        foreach (var file in relatedFiles)
                        {
                            contentsFiles.Add(new WixSharp.File(file));
                            Console.WriteLine($"Added related file to contents for version {version}: {file}");
                        }
                    }
                }
            }

            if (contentsFiles.Any())
            {
                versionFiles.Add(new Dir("contents", contentsFiles.ToArray()));
            }

            if (versionFiles.Any())
            {
                versionStorages[version] = versionFiles;
            }
            else
            {
                Console.WriteLine($"Warning: No files added for version {version}.");
            }
        }

        if (!versionStorages.Any())
        {
            Console.WriteLine("Error: No files found for any Revit version.");
            return null;
        }

        project.Dirs = new Dir[]
        {
            new InstallDir(installationDir, versionStorages.Select(v =>
                new Dir(v.Key, v.Value.ToArray())).Cast<WixEntity>().ToArray())
        };

        Directory.CreateDirectory(outputDir);
        try
        {
            return project.BuildMsi();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error building MSI: {ex.Message}");
            return null;
        }
    }

    static string SanitizeProjectName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return defaultProjectName;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? defaultProjectName : sanitized.Trim();
    }

    static void CompressFile(string filePath, string outputFilePath, int compressLevel = 9)
    {
        try
        {
            using (var outputStream = new ZipOutputStream(File.Create(outputFilePath)))
            {
                outputStream.SetLevel(compressLevel);
                var buffer = new byte[4096];
                var entry = new ZipEntry(Path.GetFileName(filePath)) { DateTime = DateTime.Now };
                outputStream.PutNextEntry(entry);

                using (var fs46 = File.OpenRead(filePath))
                {
                    int sourceBytes;
                    do
                    {
                        sourceBytes = fs46.Read(buffer, 0, buffer.Length);
                        outputStream.Write(buffer, 0, sourceBytes);
                    } while (sourceBytes > 0);
                }

                outputStream.Finish();
                outputStream.Close();
                Console.WriteLine($"Zip file created: {outputFilePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating ZIP: {ex.Message}");
        }
    }

    // Helper class for DataGrid items
    public class FileItem
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
    }

    // Console output redirector
    private class TextBoxWriter : TextWriter
    {
        private readonly TextBox _textBox;
        public TextBoxWriter(TextBox textBox) => _textBox = textBox;
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            _textBox.Dispatcher.Invoke(() => _textBox.AppendText(value.ToString()));
        }

        public override void Write(string value)
        {
            _textBox.Dispatcher.Invoke(() => _textBox.AppendText(value));
        }

        public override void WriteLine(string value)
        {
            _textBox.Dispatcher.Invoke(() => _textBox.AppendText(value + "\n"));
        }
    }
}