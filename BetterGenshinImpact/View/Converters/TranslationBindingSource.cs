using BetterGenshinImpact.Core.Config;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BetterGenshinImpact.View.Converters;

public sealed partial class TranslationBindingSource : ObservableObject
{
    [ObservableProperty]
    private string _stamp = string.Empty;

    public TranslationBindingSource()
    {
        WeakReferenceMessenger.Default.Register<PropertyChangedMessage<object>>(this, static (recipient, message) =>
        {
            if (message.PropertyName != nameof(OtherConfig.UiCultureInfoName))
            {
                return;
            }

            ((TranslationBindingSource)recipient).Stamp = message.NewValue?.ToString() ?? string.Empty;
        });
    }
}
