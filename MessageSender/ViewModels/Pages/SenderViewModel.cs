using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using MessageSender.Models;
using MessageSender.Services.CDM;
using MessageSender.Services.Interaction;
using MessageSender.State;
using MessageSender.Utils;
using MessageSender.Utils.ActionWrapper;
using MessageSender.ViewModels.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MessageSender.ViewModels.Pages;

public partial class SenderViewModel : ViewModelBase
{
    private readonly MqttService _mqttService;
    private readonly ActionDispatcher _dispatcher;
    private readonly CdmDefinitionService _cdmService;
    private readonly CdmRenderer _cdmRenderer;

    private IMessagingService _activeMessagingService;

    private static JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [ObservableProperty]
    private string _connectButtonText = "Connect";

    [ObservableProperty]
    private TextDocument _incomingMessage = new("{}");

    [ObservableProperty]
    private DeviceMessage? _selectedMessage;

    [ObservableProperty]
    private StoredMessage? _selectedStoredMessage;

    // ── CDM view state ──────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isCdmMessage;

    [ObservableProperty]
    private bool _isCdmEditMode;

    [ObservableProperty]
    private TextDocument _cdmRenderedBody = new("{}");

    /// <summary>True when showing the CDM human-readable view (read-only).</summary>
    public bool IsCdmViewMode => IsCdmMessage && !IsCdmEditMode;

    /// <summary>True when showing the raw JSON editor while a CDM message is active.</summary>
    public bool IsCdmEditActive => IsCdmMessage && IsCdmEditMode;

    [ObservableProperty]
    private TextDocument _cdmRenderedProperties = new("{}");

    /// <summary>True when CDM definitions are loaded and available for rendering.</summary>
    public bool CdmDefinitionsLoaded => _cdmService.IsLoaded;

    /// <summary>True when message is CDM format but definitions are not loaded.</summary>
    public bool ShouldShowCdmWarning => IsCdmMessage && !CdmDefinitionsLoaded;

    public SenderViewModel(MqttService mqttService, AppState appState, ActionDispatcher dispatcher, CdmDefinitionService cdmService)
    {
        _mqttService = mqttService;
        _dispatcher = dispatcher;
        _cdmService = cdmService;
        _cdmRenderer = new CdmRenderer(_cdmService);

        _activeMessagingService = _mqttService; // To support multiple service like MQTT , AMQP and etc.
        _activeMessagingService.DisconnectedFromMessageSource = Disconnected;
        _activeMessagingService.ConnectedToMessageSource = Connected;
        _activeMessagingService.MessageReceived = MessageReceived;

        AppState = appState;
        AppState.AppData.PropertyChanged += OnAppDataPropertyChanged;
    }

    public AppState AppState { get; private set; }

    [RelayCommand]
    public async Task Connect()
    {
        await _dispatcher
            .Action(async () =>
            {
                if (AppState.AppData.IsDeviceConnected)
                {
                    await _mqttService.Disconnect();
                    IsBackgroundTaskRunning = false;
                }
                else
                {
                    await _mqttService
                        .Connect(
                            AppState.AppData.SelectedDevice!.DeviceId,
                            AppState.AppData.SelectedDevice.Key,
                            AppState.AppData.SelectedDevice.ServerHost);
                    IsBackgroundTaskRunning = true;
                }
            })
            .Run();
    }

    [RelayCommand]
    public async Task SendMessage()
    {
        await _dispatcher
            .Action(async () =>
            {
                var deviceMessage = PrepareDeviceMessage();
                await _mqttService.SendMessage(deviceMessage);
                AppState.AppData.DeviceMessages.Add(deviceMessage);
            })
            .WithNotification(new("D2C Message", "Message sent", NotificationType.Information))
            .Run();
    }

    [RelayCommand]
    public async Task Beautify()
    {
        await _dispatcher
            .Action(() =>
            {
                AppState.AppData.MessageBody =
                    new TextDocument(JsonNode.Parse(AppState.AppData.MessageBody.Text)!.ToJsonString(_serializerOptions));
                AppState.AppData.UserProperties =
                    new TextDocument(JsonNode.Parse(AppState.AppData.UserProperties.Text)!.ToJsonString(_serializerOptions));

                return Task.CompletedTask;
            })
            .Run();
    }

    [RelayCommand]
    public async Task ClearIncomingMessages()
    {
        await _dispatcher
            .Action(() =>
            {
                AppState.AppData.DeviceMessages.Clear();
                IncomingMessage = new TextDocument("{}");

                return Task.CompletedTask;
            })
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

    // ── CDM internal helpers ────────────────────────────────────────────────

    private void OnAppDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppData.UserProperties))
            RefreshCdmStatus();
    }

    private void RefreshCdmStatus()
    {
        var wasCdm = IsCdmMessage;
        IsCdmMessage = CdmRenderer.IsCdmMessage(AppState.AppData.UserProperties.Text);

        // Reset edit mode when the message type changes so we default to CDM view
        if (IsCdmMessage != wasCdm)
            IsCdmEditMode = false;

        if (IsCdmMessage && !IsCdmEditMode)
            RenderCdmBody();
    }

    private void RenderCdmBody()
    {
        var rendered = _cdmRenderer.Render(
            AppState.AppData.MessageBody.Text,
            AppState.AppData.UserProperties.Text);
        CdmRenderedBody = new TextDocument(rendered);

        var renderedProps = _cdmRenderer.RenderUserProperties(AppState.AppData.UserProperties.Text);
        CdmRenderedProperties = new TextDocument(renderedProps);
    }

    [RelayCommand]
    public async Task SaveSelectedMessage()
    {
        await _dispatcher
            .Action(() =>
            {
                if (SelectedStoredMessage == null) return Task.CompletedTask;
                SelectedStoredMessage.MessageBody = new TextDocument(AppState.AppData.MessageBody.Text);
                SelectedStoredMessage.UserProperties = new TextDocument(AppState.AppData.UserProperties.Text);
                return Task.CompletedTask;
            })
            .WithNotification(new($"'{SelectedStoredMessage?.Name}' Saved", "Message updated", NotificationType.Success))
            .Run();
    }

    [RelayCommand]
    public async Task SaveAsMessages()
    {
        await _dispatcher
            .Action(async () =>
            {
                var result = await DialogHost.Show(new AddEditMessageViewModel(AppState)
                {
                    Text = "Add new Message",
                    IsFromSender = true,
                    DeviceType = AppState.AppData.SelectedDevice?.DeviceType ?? string.Empty
                });
            })
            .Run();
    }


    private DeviceMessage PrepareDeviceMessage()
    {
        var jsonNode = JsonNode.Parse(AppState.AppData.MessageBody.Text); // To validate that text is valid JSON
        var props = JsonSerializer.Deserialize<Dictionary<string, string>>(AppState.AppData.UserProperties.Text) ?? [];

        return new DeviceMessage
        {
            DeviceId = AppState.AppData.SelectedDevice!.DeviceId,
            Direction = MessageDirection.Out,
            Source = MessageSource.MessageSender,
            Timestamp = DateTimeOffset.Now,
            Payload = jsonNode!.ToJsonString(),
            Properties = props,
        };
    }

    // Event handler
    private void Disconnected(object? sender, string? args)
    {
        AppState.AppData.IsDeviceConnected = false;
        ConnectButtonText = "Connect";
        Dispatcher.UIThread.Post(() =>
            Infrastructure
                .GlobalNotificationManager
                .Show(new Notification("Disconnected", "Device disconnected", NotificationType.Information)));
    }

    // Event handler
    private void Connected(object? sender, string? args)
    {
        AppState.AppData.IsDeviceConnected = true;
        ConnectButtonText = "Disconnect";
        Dispatcher.UIThread.Post(() =>
            Infrastructure
                .GlobalNotificationManager
                .Show(new Notification("Connected", "Device connected", NotificationType.Success)));
    }

    // Event handler
    private void MessageReceived(object? sender, DeviceMessage? args)
    {
        if (args is null)
        {
            return;
        }

        args.Source = MessageSource.Cloud;
        args.Timestamp = DateTimeOffset.Now;
        args.Direction = MessageDirection.In;
        AppState.AppData.DeviceMessages.Add(args);
    }

    partial void OnSelectedStoredMessageChanged(StoredMessage? value)
    {
        if (value == null) return;
        AppState.AppData.MessageBody = new TextDocument(value.MessageBody.Text);
        AppState.AppData.UserProperties = new TextDocument(value.UserProperties.Text);
        // CDM detection is triggered by the UserProperties PropertyChanged event above,
        // but also reset edit mode so we default to the CDM rendered view.
        IsCdmEditMode = false;
    }

    partial void OnSelectedMessageChanged(DeviceMessage? value)
    {
        if (value is null)
        {
            return;
        }

        try
        {
            var formattedPayload = value.Payload is null
                 ? JsonNode.Parse("{}")
                 : JsonNode.Parse(value.Payload);

            IncomingMessage =
                new TextDocument(JsonSerializer.Serialize(
                    new
                    {
                        payload = formattedPayload,
                        userPoperties = value.Properties
                    },
                    _serializerOptions));
        }
        catch (Exception e)
        {
            Dispatcher.UIThread.Post(() =>
               Infrastructure
                   .GlobalNotificationManager
                   .Show(new Notification("Error", e.Message, NotificationType.Error)));
        }
    }
}