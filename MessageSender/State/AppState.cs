using MessageSender.ViewModels;
using System;

namespace MessageSender.State;

public class AppState : ViewModelBase
{
    public Settings Settings { get; set; } = new();

    public AppData AppData { get; set; } = new();

    /// <summary>Invoked when CDM definitions load status changes, allowing other ViewModels to refresh computed properties.</summary>
    public Action? OnCdmStatusChanged { get; set; }
}
