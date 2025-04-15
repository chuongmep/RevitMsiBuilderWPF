using System.Windows;
using RevitMsiBuilder.Services;
using RevitMsiBuilder.ViewModels;

namespace RevitMsiBuilder;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    
    private readonly MainViewModel _viewModel;
    public MainWindow()
    {
        InitializeComponent();
        Topmost = true;
        // Initialize ViewModel with required services
        Logger logger = new Logger();
        AddinFileParser parser = new AddinFileParser(logger);
        WixSharpMsiBuilder msiBuilder = new WixSharpMsiBuilder(logger);
        _viewModel = new MainViewModel(parser, 
            msiBuilder,
            logger);
        // Set DataContext for data binding
        DataContext = _viewModel;
            
        // Subscribe to log events to update UI
        _viewModel.PropertyChanged += (sender, args) => {
            if (args.PropertyName == nameof(MainViewModel.LogOutput))
            {
                LogConsole.Text = _viewModel.LogOutput;
                LogConsole.ScrollToEnd();
            }
        };
    }
}