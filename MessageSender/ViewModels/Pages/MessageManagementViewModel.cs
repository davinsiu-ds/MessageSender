using Avalonia.Collections;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using MessageSender.Models;
using MessageSender.Services.CDM;
using MessageSender.State;
using MessageSender.Utils.ActionWrapper;
using MessageSender.ViewModels.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MessageSender.ViewModels.Pages;

public partial class MessageManagementViewModel : ViewModelBase
{
    private readonly ActionDispatcher _dispatcher;
    private readonly CdmDefinitionService _cdmService;
    private readonly CdmRenderer _cdmRenderer;

    [ObservableProperty]
    private bool _canEditMessage = false;

    [ObservableProperty]
    private StoredMessage? _selectedMessage;

    [ObservableProperty]
    private TextDocument _selectedMessageBody = new("{}");

    [ObservableProperty]
    private TextDocument _selectedMessageUserProperties = new("{}");

    [ObservableProperty]
    private bool _canDelete;

    // ── CDM view state ──────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isCdmMessage;

    [ObservableProperty]
    private bool _isCdmEditMode;

    [ObservableProperty]
    private TextDocument _cdmRenderedBody = new("{}");

    [ObservableProperty]
    private TextDocument _cdmRenderedProperties = new("{}");

    /// <summary>True when showing the CDM human-readable view (read-only).</summary>
    public bool IsCdmViewMode => IsCdmMessage && !IsCdmEditMode;

    /// <summary>True when showing the raw JSON editor while a CDM message is active.</summary>
    public bool IsCdmEditActive => IsCdmMessage && IsCdmEditMode;

    /// <summary>True when CDM definitions are loaded and available for rendering.</summary>
    public bool CdmDefinitionsLoaded => _cdmService.IsLoaded;

    /// <summary>True when message is CDM format but definitions are not loaded.</summary>
    public bool ShouldShowCdmWarning => IsCdmMessage && !CdmDefinitionsLoaded;

    private static JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public MessageManagementViewModel(AppState appState, ActionDispatcher dispatcher, CdmDefinitionService cdmService)
    {
        _dispatcher = dispatcher;
        _cdmService = cdmService;
        _cdmRenderer = new CdmRenderer(_cdmService);
        
        AppState = appState;
        AppState.OnCdmStatusChanged += RefreshCdmComputedProperties;

        FillDataGreed();
    }

    public AppState AppState { get; set; }

    public DataGridCollectionView? MessagesDataGrid { get; set; }

    private void FillDataGreed()
    {
        MessagesDataGrid = new DataGridCollectionView(AppState.AppData.Messages);
        MessagesDataGrid.GroupDescriptions.Add(new DataGridPathGroupDescription(nameof(StoredMessage.DeviceType)));
    }

    [RelayCommand]
    private async Task DeleteMessage()
    {
        await _dispatcher
            .Action(() =>
            {
                var msg = AppState.AppData.Messages.FirstOrDefault(m => m.Id == SelectedMessage?.Id);
                if (msg != null) 
                {
                    AppState.AppData.Messages.Remove(msg);
                }

                return Task.CompletedTask;
            })
            .WithConfirmationDialog(new DialogOptions() { Message = $"Do you want to remove the message '{SelectedMessage?.Name}'" })
            .Run();
    }

    [RelayCommand]
    private async Task AddMessage()
    {
        await _dispatcher
            .Action(async () =>
            {
                var result = await DialogHost.Show(new AddEditMessageViewModel(AppState) { Text = "Add new Message" });
            })
            .Run();
    }

    [RelayCommand]
    public async Task Beautify()
    {
        await _dispatcher
            .Action(() =>
            {
                SelectedMessage?.MessageBody =
                    new TextDocument(JsonNode.Parse(SelectedMessage.MessageBody.Text)?.ToJsonString(_serializerOptions));
                SelectedMessage?.UserProperties =
                    new TextDocument(JsonNode.Parse(SelectedMessage.UserProperties.Text)?.ToJsonString(_serializerOptions));

                return Task.CompletedTask;
            })
            .Run();
    }

    [RelayCommand]
    public async Task SetInSender()
    {
        await _dispatcher
            .Action(() =>
            {
                var body =
                    new TextDocument(JsonNode.Parse(SelectedMessage!.MessageBody.Text)?.ToJsonString(_serializerOptions));
                var userProperties =
                     new TextDocument(JsonNode.Parse(SelectedMessage!.UserProperties.Text)?.ToJsonString(_serializerOptions));

                AppState.AppData.MessageBody = body;
                AppState.AppData.UserProperties = userProperties;

                return Task.CompletedTask;
            })
            .WithConfirmationDialog(new DialogOptions() { Message = $"Copy this '{SelectedMessage?.Name}' to Sender Page?" })
            .Run();
    }

    // ── CDM commands ────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleCdmEditMode()
    {
        IsCdmEditMode = !IsCdmEditMode;
        // When switching back to view mode, re-render with the latest body content
        if (!IsCdmEditMode)
            RenderCdmBody();
    }

    // ── CDM derived-property notifications ─────────────────────────────────

    partial void OnIsCdmMessageChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCdmViewMode));
        OnPropertyChanged(nameof(IsCdmEditActive));
        OnPropertyChanged(nameof(ShouldShowCdmWarning));
    }

    partial void OnIsCdmEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCdmViewMode));
        OnPropertyChanged(nameof(IsCdmEditActive));
    }

    /// <summary>Refresh CDM computed properties when CDM service status changes (called from AppState.OnCdmStatusChanged).</summary>
    private void RefreshCdmComputedProperties()
    {
        OnPropertyChanged(nameof(CdmDefinitionsLoaded));
        OnPropertyChanged(nameof(ShouldShowCdmWarning));
    }

    // ── CDM internal helpers ────────────────────────────────────────────────

    private void RefreshCdmStatus()
    {
        if (SelectedMessage == null) return;

        var wasCdm = IsCdmMessage;
        IsCdmMessage = CdmRenderer.IsCdmMessage(SelectedMessage.UserProperties.Text);

        // Reset edit mode when the message type changes so we default to CDM view
        if (IsCdmMessage != wasCdm)
            IsCdmEditMode = false;

        if (IsCdmMessage && !IsCdmEditMode)
            RenderCdmBody();
    }

    private void RenderCdmBody()
    {
        if (SelectedMessage == null) return;

        var rendered = _cdmRenderer.Render(
            SelectedMessage.MessageBody.Text,
            SelectedMessage.UserProperties.Text);
        CdmRenderedBody = new TextDocument(rendered);

        var renderedProps = _cdmRenderer.RenderUserProperties(SelectedMessage.UserProperties.Text);
        CdmRenderedProperties = new TextDocument(renderedProps);
    }

    partial void OnSelectedMessageChanged(StoredMessage? value)
    {
        if (value == null)
        {
            CanEditMessage = false;
            CanDelete = false;
            IsCdmMessage = false;
            return;
        }

        CanEditMessage = true;
        CanDelete = true;
        SelectedMessageBody = new TextDocument(value?.MessageBody);
        SelectedMessageUserProperties = new TextDocument(value?.UserProperties);
        RefreshCdmStatus();
    }
}
