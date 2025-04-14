using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using RevitMsiBuilder.Helpers;
using RevitMsiBuilder.Models;
using RevitMsiBuilder.Services;

namespace RevitMsiBuilder.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // Command properties
    public ICommand BrowseCommand { get; }
    public ICommand DeployCommand { get; }
    
    public ICommand ClearLogCommand { get; set; }
    public ICommand CopyLogCommand { get; set; }

    // Data properties with notification
    private AddinFile _currentAddinFile;

    public AddinFile CurrentAddinFile
    {
        get => _currentAddinFile;
        set
        {
            _currentAddinFile = value;
            OnPropertyChanged(nameof(CurrentAddinFile));
        }
    }

    private ObservableCollection<FileItem> _files = new ObservableCollection<FileItem>();
    public ObservableCollection<FileItem> Files => _files;

    private ObservableCollection<string> _revitVersions = new ObservableCollection<string>();
    public ObservableCollection<string> RevitVersions => _revitVersions;

    private string _selectedRevitVersion;

    public string SelectedRevitVersion
    {
        get => _selectedRevitVersion;
        set
        {
            _selectedRevitVersion = value;
            OnPropertyChanged(nameof(SelectedRevitVersion));
        }
    }

    private string _logOutput = string.Empty;

    public string LogOutput
    {
        get => _logOutput;
        set
        {
            _logOutput = value;
            OnPropertyChanged(nameof(LogOutput));
        }
    }

    // Services to be injected
    private readonly IAddinFileParser _addinParser;
    private readonly IMsiBuilder _msiBuilder;
    private readonly ILogger _logger;

    public MainViewModel(IAddinFileParser addinParser, IMsiBuilder msiBuilder, ILogger logger)
    {
        _addinParser = addinParser;
        _msiBuilder = msiBuilder;
        _logger = logger;

        // Hook up logger events
        _logger.LogAdded += (sender, message) => { LogOutput += message + Environment.NewLine; };

        // Initialize commands
        BrowseCommand = new RelayCommand(BrowseForAddinFile);
        DeployCommand = new RelayCommand(DeployAddin, CanDeployAddin);
        ClearLogCommand = new RelayCommand(ClearLogBuildMsi);
        CopyLogCommand = new RelayCommand(CopyLogBuildMsi);
    }
    private void ClearLogBuildMsi()
    {
        _logger.ClearLog();
        LogOutput = string.Empty;
    }
    private void CopyLogBuildMsi()
    {
        if (string.IsNullOrEmpty(LogOutput))
        {
            _logger.Log("No log to copy.");
            return;
        }

        try
        {
            Clipboard.SetText(LogOutput);
            _logger.Log("Log copied to clipboard.");
        }
        catch (Exception ex)
        {
            _logger.Log($"Error copying log: {ex.Message}");
        }
    }

    private void BrowseForAddinFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Addin files (*.addin)|*.addin|All files (*.*)|*.*",
            Title = "Select .addin File"
        };

        if (dialog.ShowDialog() == true)
        {
            _logger.Log("Loading addin file: " + dialog.FileName);
            try
            {
                // Parse addin file
                CurrentAddinFile = _addinParser.ParseAddinFile(dialog.FileName);

                // Clear and repopulate files collection
                _files.Clear();
                foreach (var path in CurrentAddinFile.AssemblyPaths)
                {
                    _files.Add(new FileItem
                    {
                        FileName = Path.GetFileName(path),
                        FilePath = path
                    });
                }

                // Load Revit versions
                LoadRevitVersions(Path.GetDirectoryName(dialog.FileName));
            }
            catch (Exception ex)
            {
                _logger.Log($"Error parsing .addin file: {ex.Message}");
            }
        }
    }

    private void LoadRevitVersions(string addinDir)
    {
        _revitVersions.Clear();

        var versions = _addinParser.InferRevitVersions(addinDir);
        foreach (var version in versions)
        {
            _revitVersions.Add(version);
        }

        if (_revitVersions.Count > 0)
        {
            SelectedRevitVersion = _revitVersions[0];
        }
    }

    private bool CanDeployAddin()
    {
        return CurrentAddinFile != null && !string.IsNullOrEmpty(SelectedRevitVersion);
    }

    private void DeployAddin()
    {
        if (!CanDeployAddin())
        {
            _logger.Log("Cannot deploy: Missing addin file or Revit version selection");
            return;
        }

        try
        {
            var config = new BuildConfiguration
            {
                RevitVersion = SelectedRevitVersion,
                ProjectName = CurrentAddinFile.Name,
                Version = _msiBuilder.GenerateVersionString()
            };

            _logger.Log($"Building MSI for {config.ProjectName} version {config.Version}...");

            string msiPath = _msiBuilder.BuildMsi(CurrentAddinFile, new[] { config.RevitVersion }, config);

            if (!string.IsNullOrEmpty(msiPath))
            {
                _logger.Log("Msi created successfully at " + msiPath);
                string zipPath = Path.Combine(config.OutputDirectory, $"{config.ProjectName}-{config.Version}.zip");
                _msiBuilder.CompressFile(msiPath, zipPath);
                _logger.Log("Msi compressed into zip successfully at " + zipPath);
            }
            else
            {
                _logger.Log("Error: MSI build failed.");
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Error during deployment: {ex.Message}");
        }
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}