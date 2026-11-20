using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

namespace Mcctl.App.Localization;

/// <summary>UI language choice; <see cref="System"/> follows the Windows display language.</summary>
public enum AppLanguage
{
    System,
    Japanese,
    English,
}

/// <summary>
/// UI strings from Strings.resx / Strings.ja.resx (generated from strings.tsv).
/// XAML binds to the indexer through <see cref="TrExtension"/>, so changing <see cref="Language"/>
/// updates every label immediately without restarting.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources = new("Mcctl.App.Localization.Strings", typeof(Loc).Assembly);
    private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    private Loc()
    {
    }

    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changed, for view models that build text in code.</summary>
    public event EventHandler? LanguageChanged;

    public AppLanguage Language
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            Culture = Resolve(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    } = AppLanguage.System;

    public CultureInfo Culture { get; private set; } = Resolve(AppLanguage.System);

    public string this[string key] => Resources.GetString(key, Culture) ?? key;

    public static string Get(string key) => Instance[key];

    public static string Format(string key, params object?[] args) => string.Format(Instance.Culture, Instance[key], args);

    private static CultureInfo Resolve(AppLanguage language) => language switch
    {
        AppLanguage.Japanese => Japanese,
        AppLanguage.English => English,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja" ? Japanese : English,
    };
}

/// <summary>
/// <c>{l:Tr Key}</c> – a one-way binding to <see cref="Loc"/>'s indexer, so the text follows language changes.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
        => new Binding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
