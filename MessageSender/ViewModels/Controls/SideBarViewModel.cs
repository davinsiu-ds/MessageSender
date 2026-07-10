using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MessageSender.Services.CDM;
using MessageSender.Services.Routing;
using MessageSender.State;
using MessageSender.Utils.ActionWrapper;
using MessageSender.ViewModels.Dialogs;
using MessageSender.ViewModels.Pages;
using MessageSender.Views.Pages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MessageSender.ViewModels.Controls;

public partial class SideBarViewModel : ViewModelBase
{
    private readonly HistoryRouter<ViewModelBase> _router;
    private readonly AppState _appState;
    private readonly CdmDefinitionService _cdmService;
    private readonly ActionDispatcher _dispatcher;

    [ObservableProperty]
    private NavigationItemViewModel _currentPage = default!;

    [ObservableProperty]
    private ViewModelBase _page = default!;

    [ObservableProperty]
    private bool _isCdmMessage;

    /// <summary>True when CDM definitions are loaded and available for rendering.</summary>
    public bool CdmDefinitionsLoaded => _cdmService.IsLoaded;

    /// <summary>True when message is CDM format but definitions are not loaded.</summary>
    public bool ShouldShowCdmWarning => IsCdmMessage && !CdmDefinitionsLoaded;

    public SideBarViewModel(HistoryRouter<ViewModelBase> router, AppState appState, CdmDefinitionService cdmService, ActionDispatcher dispatcher)
    {
        _router = router;
        _appState = appState;
        _cdmService = cdmService;
        _dispatcher = dispatcher;
        _router.CurrentViewModelChanged += Router_CurrentViewModelChanged;
        
        // Subscribe to UserProperties changes to detect CDM messages
        _appState.AppData.PropertyChanged += AppData_PropertyChanged;
    }

    public AppState AppState => _appState;

    private void Router_CurrentViewModelChanged(ViewModelBase obj)
    {
        Page = obj;
    }

    private void AppData_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_appState.AppData.UserProperties))
        {
            IsCdmMessage = CdmRenderer.IsCdmMessage(_appState.AppData.UserProperties.Text);
            OnPropertyChanged(nameof(ShouldShowCdmWarning));
        }
    }

    [RelayCommand]
    private async Task SelectCdmDefinitionsPath()
    {
        var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (mainWindow?.StorageProvider is null) return;

        var folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select CDM Definitions Folder",
            AllowMultiple = false,
        });

        if (folders.Count == 0) return;

        var path = folders[0].Path.LocalPath;
        var result = _cdmService.Load(path);

        if (result.Success)
        {
            _appState.Settings.CdmDefinitionsPath = path;
            OnPropertyChanged(nameof(CdmDefinitionsLoaded));
            OnPropertyChanged(nameof(ShouldShowCdmWarning));
            _appState.OnCdmStatusChanged?.Invoke();

            await _dispatcher
                .Action(() => Task.CompletedTask)
                .WithNotification(new("CDM Definitions Loaded", result.Message, NotificationType.Success))
                .Run();
        }
        else
        {
            OnPropertyChanged(nameof(CdmDefinitionsLoaded));
            OnPropertyChanged(nameof(ShouldShowCdmWarning));
            _appState.OnCdmStatusChanged?.Invoke();

            await _dispatcher
                .Action(() => Task.CompletedTask)
                .WithNotification(new("Failed to Load CDM Definitions", result.Message, NotificationType.Error))
                .Run();
        }
    }

    public List<NavigationItemViewModel> Navigations =>
        [
            new(nameof(SenderView), "Send", "SendRegular"),
            new(nameof(DeviceManagementView), "Device", "DeviceRegular"),
            new(nameof(MessageManagementView), "Message", "MessageRegular"),
            //new(nameof(LogManagementView), "Log", "LogRegular"),
        ];

    partial void OnCurrentPageChanged(NavigationItemViewModel? oldValue, NavigationItemViewModel newValue)
    {
        ViewModelBase vm = newValue.View switch
        {
            nameof(SenderView) => _router.GoTo<SenderViewModel>(),
            nameof(DeviceManagementView) => _router.GoTo<DeviceManagementViewModel>(),
            nameof(MessageManagementView) => _router.GoTo<MessageManagementViewModel>(),
            nameof(LogManagementView) => _router.GoTo<LogManagementViewModel>(),
            _ => new ConfirmationDialogViewModel()
        };

        newValue.ViewModel = vm;
    }
}