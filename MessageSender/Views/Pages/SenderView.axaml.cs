using ActiproSoftware.UI.Avalonia.Themes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Indentation.CSharp;
using AvaloniaEdit.TextMate;
using MessageSender.ViewModels.Pages;
using TextMateSharp.Grammars;
namespace MessageSender.Views.Pages;

public partial class SenderView : UserControl
{
    public SenderView()
    {
        InitializeComponent();
        Application.Current!.ActualThemeVariantChanged += Current_ActualThemeVariantChanged;
        InitiateEditors(GetThemeVariant(Application.Current.ActualThemeVariant));
        AddHandler(KeyDownEvent, OnKeyDown, handledEventsToo: false);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+S: Save selected message
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            if (DataContext is SenderViewModel vm && vm.SaveSelectedMessageCommand.CanExecute(null))
            {
                vm.SaveSelectedMessageCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void InitiateEditors(ThemeName themeName)
    {
        Color textSelectionColor;
        Color textSearchSectionColor;
        Color searchPanelColor;

        if (themeName == ThemeName.DarkPlus)
        {
            textSelectionColor = Color.Parse("#23405a");
            textSearchSectionColor = Color.Parse("#952354");
            searchPanelColor = Color.Parse("#F8F8F9");
        }
        else
        {
            textSelectionColor = Color.Parse("#ADD6FF");
            textSearchSectionColor = Color.Parse("#F8C9AB");
            searchPanelColor = Color.Parse("#F8F8F9");
        }

        var registryOptions = new RegistryOptions(themeName);
        Language jsonSyntax = registryOptions.GetLanguageByExtension(".json");

        MessageBodyEditor.ShowLineNumbers = true;
        MessageBodyEditor.Options.ConvertTabsToSpaces = true;
        MessageBodyEditor.Options.AllowScrollBelowDocument = true;
        MessageBodyEditor.Options.HighlightCurrentLine = true;
        MessageBodyEditor.Options.EnableEmailHyperlinks = false;
        MessageBodyEditor.Options.EnableHyperlinks = false;
        MessageBodyEditor.SearchResultsBrush = new SolidColorBrush(textSearchSectionColor);
        MessageBodyEditor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(MessageBodyEditor.Options);
        MessageBodyEditor.TextArea.RightClickMovesCaret = true;
        MessageBodyEditor.TextArea.SelectionBrush = new SolidColorBrush(textSelectionColor);

        TextMate.Installation messageBodyEditorTextMateInstallation = MessageBodyEditor.InstallTextMate(registryOptions);
        messageBodyEditorTextMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(jsonSyntax.Id));

        CdmBodyEditor.ShowLineNumbers = true;
        CdmBodyEditor.Options.ConvertTabsToSpaces = true;
        CdmBodyEditor.Options.AllowScrollBelowDocument = true;
        CdmBodyEditor.Options.HighlightCurrentLine = true;
        CdmBodyEditor.Options.EnableEmailHyperlinks = false;
        CdmBodyEditor.Options.EnableHyperlinks = false;
        CdmBodyEditor.SearchResultsBrush = new SolidColorBrush(textSearchSectionColor);
        CdmBodyEditor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(CdmBodyEditor.Options);
        CdmBodyEditor.TextArea.RightClickMovesCaret = true;
        CdmBodyEditor.TextArea.SelectionBrush = new SolidColorBrush(textSelectionColor);

        TextMate.Installation cdmBodyEditorTextMateInstallation = CdmBodyEditor.InstallTextMate(registryOptions);
        cdmBodyEditorTextMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(jsonSyntax.Id));

        MessageHeadersEditor.ShowLineNumbers = true;
        MessageHeadersEditor.Options.ConvertTabsToSpaces = true;
        MessageHeadersEditor.Options.AllowScrollBelowDocument = true;
        MessageHeadersEditor.Options.HighlightCurrentLine = true;
        MessageHeadersEditor.Options.EnableEmailHyperlinks = false;
        MessageHeadersEditor.Options.EnableHyperlinks = false;
        MessageHeadersEditor.SearchResultsBrush = new SolidColorBrush(textSearchSectionColor);
        MessageHeadersEditor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(MessageHeadersEditor.Options);
        MessageHeadersEditor.TextArea.RightClickMovesCaret = true;
        MessageHeadersEditor.TextArea.SelectionBrush = new SolidColorBrush(textSelectionColor);

        TextMate.Installation messageHeadersEditorTextMateInstallation = MessageHeadersEditor.InstallTextMate(registryOptions);
        messageHeadersEditorTextMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(jsonSyntax.Id));

        CdmPropertiesEditor.ShowLineNumbers = true;
        CdmPropertiesEditor.Options.ConvertTabsToSpaces = true;
        CdmPropertiesEditor.Options.AllowScrollBelowDocument = true;
        CdmPropertiesEditor.Options.HighlightCurrentLine = true;
        CdmPropertiesEditor.Options.EnableEmailHyperlinks = false;
        CdmPropertiesEditor.Options.EnableHyperlinks = false;
        CdmPropertiesEditor.SearchResultsBrush = new SolidColorBrush(textSearchSectionColor);
        CdmPropertiesEditor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(CdmPropertiesEditor.Options);
        CdmPropertiesEditor.TextArea.RightClickMovesCaret = true;
        CdmPropertiesEditor.TextArea.SelectionBrush = new SolidColorBrush(textSelectionColor);

        TextMate.Installation cdmPropertiesEditorTextMateInstallation = CdmPropertiesEditor.InstallTextMate(registryOptions);
        cdmPropertiesEditorTextMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(jsonSyntax.Id));

        SelectedIncomingMessageEditor.ShowLineNumbers = true;
        SelectedIncomingMessageEditor.Options.ConvertTabsToSpaces = true;
        SelectedIncomingMessageEditor.Options.AllowScrollBelowDocument = true;
        SelectedIncomingMessageEditor.Options.HighlightCurrentLine = true;
        SelectedIncomingMessageEditor.Options.EnableEmailHyperlinks = false;
        SelectedIncomingMessageEditor.Options.EnableHyperlinks = false;
        SelectedIncomingMessageEditor.SearchResultsBrush = new SolidColorBrush(textSearchSectionColor);
        SelectedIncomingMessageEditor.TextArea.IndentationStrategy = new CSharpIndentationStrategy(SelectedIncomingMessageEditor.Options);
        SelectedIncomingMessageEditor.TextArea.RightClickMovesCaret = true;
        SelectedIncomingMessageEditor.TextArea.SelectionBrush = new SolidColorBrush(textSelectionColor);

        TextMate.Installation selectedIncomingMessageEditorTextMateInstallation = SelectedIncomingMessageEditor.InstallTextMate(registryOptions);
        selectedIncomingMessageEditorTextMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(jsonSyntax.Id));
    }

    private void Current_ActualThemeVariantChanged(object? sender, System.EventArgs e)
    {
        InitiateEditors(Application.Current!.ActualThemeVariant.IsDark()
            ? ThemeName.DarkPlus
            : ThemeName.LightPlus);
    }

    private ThemeName GetThemeVariant(ThemeVariant themeVariant)
    {
        return themeVariant.IsDark()
            ? ThemeName.DarkPlus
            : ThemeName.LightPlus;
    }
}