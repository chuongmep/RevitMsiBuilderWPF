using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RevitMsiBuilder.Models;
using RevitMsiBuilder.Services;
using Clipboard = System.Windows.Clipboard;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace RevitMsiBuilder.ViewModels;

public partial class MainViewModel : ObservableObject
{

    // Data properties with notification
    [ObservableProperty] private AddinFile _currentAddinFile;

    partial void OnCurrentAddinFileChanged(AddinFile? oldValue, AddinFile newValue)
    {

        DeployAddinCommand.NotifyCanExecuteChanged();
    }



    [ObservableProperty] private ObservableCollection<FileItem> _files = new ObservableCollection<FileItem>();

    [ObservableProperty] private ObservableCollection<string> _revitVersions = new ObservableCollection<string>();


    [ObservableProperty] private string _selectedRevitVersion;



    [ObservableProperty] private string _logOutput = string.Empty;


    [ObservableProperty]
    private string? selectedFolderPath;



    [ObservableProperty] private bool _isInstallForAllUsers;



    [ObservableProperty] private bool _isCompressMsi;



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

    }
    [RelayCommand]

    private void ClearLogBuildMsi()
    {
        _logger.ClearLog();
        LogOutput = string.Empty;
    }

    [RelayCommand]

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


    [RelayCommand]
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

    [RelayCommand]

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
                Version = _msiBuilder.GenerateVersionString(),
                InstallForAllUsers = IsInstallForAllUsers,
                ProjectGUID = CurrentAddinFile.AddinGuid,
                ProjectDescription = CurrentAddinFile.Description ?? "Project Automation Revit",
                OutputDirectory = SelectedFolderPath ??= Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "RevitMsiBuilder", "Output")
            };

            _logger.Log($"Building MSI for {config.ProjectName} version {config.Version}...");

            string msiPath = _msiBuilder.BuildMsi(CurrentAddinFile, new[] { config.RevitVersion }, config);
            if (string.IsNullOrEmpty(msiPath))
            {
                _logger.Log("Error: MSI build failed.");
                return;
            }
            _logger.Log("Msi created successfully at " + msiPath);
            if (!string.IsNullOrEmpty(msiPath) && IsCompressMsi)
            {
                string zipPath = Path.Combine(config.OutputDirectory, $"{config.ProjectName}-{config.Version}.zip");
                _msiBuilder.CompressFile(msiPath, zipPath);
                _logger.Log("Msi compressed into zip successfully at " + zipPath);
            }
        }
        catch (Exception ex)
        {
            _logger.Log($"Error during deployment: {ex.Message}");
        }
    }


    [RelayCommand]
    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        DialogResult result = dialog.ShowDialog();
        if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            // Handle the selected folder path
            SelectedFolderPath = dialog.SelectedPath;
        }
    }

}