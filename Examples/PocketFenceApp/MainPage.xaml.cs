using System.ComponentModel;
using System.Windows.Input;
using PocketFence.Library;
using Microsoft.Maui.Graphics;

namespace PocketFenceApp;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

public class MainPageViewModel : INotifyPropertyChanged
{
    private readonly PocketFenceEngine _pocketFence;
    private bool _isEngineReady;
    private string _urlToCheck = "";
    private string _contentToAnalyze = "";
    private bool _isChildModeEnabled = true;
    private bool _hasUrlResult;
    private bool _hasContentResult;
    private bool _showStats;
    private string _urlResultText = "";
    private string _urlResultReason = "";
    private string _urlThreatScore = "";
    private string _contentResultText = "";
    private string _contentCategory = "";
    private string _contentSafetyScore = "";
    private string _contentFlags = "";
    private bool _hasContentFlags;
    private FilteringStats _stats = new();
    
    public MainPageViewModel(PocketFenceEngine pocketFence)
    {
        _pocketFence = pocketFence;
        
        CheckUrlCommand = new Command(async () => await CheckUrl());
        AnalyzeContentCommand = new Command(async () => await AnalyzeContent());
        ViewStatsCommand = new Command(async () => await ViewStats());
        
        _ = InitializeEngine();
    }
    
    private async Task InitializeEngine()
    {
        try
        {
            await _pocketFence.InitializeAsync();
            IsEngineReady = true;
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", 
                $"Failed to initialize PocketFence: {ex.Message}", "OK");
        }
    }
    
    public ICommand CheckUrlCommand { get; }
    public ICommand AnalyzeContentCommand { get; }
    public ICommand ViewStatsCommand { get; }
    
    public bool IsEngineReady
    {
        get => _isEngineReady;
        set
        {
            _isEngineReady = value;
            OnPropertyChanged();
        }
    }
    
    public string UrlToCheck
    {
        get => _urlToCheck;
        set
        {
            _urlToCheck = value;
            OnPropertyChanged();
        }
    }
    
    public string ContentToAnalyze
    {
        get => _contentToAnalyze;
        set
        {
            _contentToAnalyze = value;
            OnPropertyChanged();
        }
    }
    
    public bool IsChildModeEnabled
    {
        get => _isChildModeEnabled;
        set
        {
            _isChildModeEnabled = value;
            _pocketFence?.SetChildMode(value);
            OnPropertyChanged();
        }
    }
    
    public bool HasUrlResult
    {
        get => _hasUrlResult;
        set
        {
            _hasUrlResult = value;
            OnPropertyChanged();
        }
    }
    
    public bool HasContentResult
    {
        get => _hasContentResult;
        set
        {
            _hasContentResult = value;
            OnPropertyChanged();
        }
    }
    
    public bool ShowStats
    {
        get => _showStats;
        set
        {
            _showStats = value;
            OnPropertyChanged();
        }
    }
    
    public string UrlResultText
    {
        get => _urlResultText;
        set
        {
            _urlResultText = value;
            OnPropertyChanged();
        }
    }
    
    public string UrlResultReason
    {
        get => _urlResultReason;
        set
        {
            _urlResultReason = value;
            OnPropertyChanged();
        }
    }
    
    public string UrlThreatScore
    {
        get => _urlThreatScore;
        set
        {
            _urlThreatScore = value;
            OnPropertyChanged();
        }
    }
    
    public Color UrlResultColor => 
        UrlResultText.Contains("BLOCKED") ? Colors.LightCoral : Colors.LightGreen;
    
    public string ContentResultText
    {
        get => _contentResultText;
        set
        {
            _contentResultText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ContentResultColor));
        }
    }
    
    public string ContentCategory
    {
        get => _contentCategory;
        set
        {
            _contentCategory = value;
            OnPropertyChanged();
        }
    }
    
    public string ContentSafetyScore
    {
        get => _contentSafetyScore;
        set
        {
            _contentSafetyScore = value;
            OnPropertyChanged();
        }
    }
    
    public string ContentFlags
    {
        get => _contentFlags;
        set
        {
            _contentFlags = value;
            OnPropertyChanged();
        }
    }
    
    public bool HasContentFlags
    {
        get => _hasContentFlags;
        set
        {
            _hasContentFlags = value;
            OnPropertyChanged();
        }
    }
    
    public Color ContentResultColor => 
        ContentResultText.Contains("UNSAFE") ? Colors.LightCoral : Colors.LightGreen;
    
    public FilteringStats Stats
    {
        get => _stats;
        set
        {
            _stats = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BlockRateText));
        }
    }
    
    public string BlockRateText => $"{Stats.BlockRate:P1}";
    
    private async Task CheckUrl()
    {
        if (string.IsNullOrWhiteSpace(UrlToCheck))
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", 
                "Please enter a URL to check", "OK");
            return;
        }
        
        try
        {
            var result = await _pocketFence.CheckUrlAsync(UrlToCheck);
            
            UrlResultText = result.IsBlocked ? 
                "🚫 BLOCKED - Not Safe for Children" : 
                "✅ ALLOWED - Safe to Visit";
            UrlResultReason = result.Reason;
            UrlThreatScore = $"Threat Score: {result.ThreatScore:F2}/1.0";
            
            HasUrlResult = true;
            OnPropertyChanged(nameof(UrlResultColor));
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", 
                $"Failed to check URL: {ex.Message}", "OK");
        }
    }
    
    private async Task AnalyzeContent()
    {
        if (string.IsNullOrWhiteSpace(ContentToAnalyze))
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", 
                "Please enter content to analyze", "OK");
            return;
        }
        
        try
        {
            var result = await _pocketFence.AnalyzeContentAsync(ContentToAnalyze);
            
            ContentResultText = result.IsChildSafe ? 
                "✅ SAFE - Appropriate Content" : 
                "⚠️ UNSAFE - May Not Be Appropriate";
            ContentCategory = $"Category: {result.Category}";
            ContentSafetyScore = $"Safety Score: {result.SafetyScore:F2}/1.0";
            
            if (result.Flags.Any())
            {
                ContentFlags = $"⚠️ Flags: {string.Join(", ", result.Flags)}";
                HasContentFlags = true;
            }
            else
            {
                ContentFlags = "";
                HasContentFlags = false;
            }
            
            HasContentResult = true;
            OnPropertyChanged(nameof(ContentResultColor));
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", 
                $"Failed to analyze content: {ex.Message}", "OK");
        }
    }
    
    private async Task ViewStats()
    {
        try
        {
            Stats = _pocketFence.GetStatistics();
            ShowStats = !ShowStats;
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", 
                $"Failed to load statistics: {ex.Message}", "OK");
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}