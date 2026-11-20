using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mcctl.App.Mvvm;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Raises PropertyChanged for computed properties that depend on another one.</summary>
    protected void OnPropertiesChanged(params ReadOnlySpan<string> names)
    {
        foreach (var name in names)
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>
    /// Assigns and notifies when the value changed. Meant for C# 14 <c>field</c> backed properties:
    /// <code>public string Name { get; set => Set(ref field, value); } = "";</code>
    /// </summary>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
