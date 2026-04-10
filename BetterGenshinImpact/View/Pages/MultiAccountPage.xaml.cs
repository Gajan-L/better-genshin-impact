using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using BetterGenshinImpact.ViewModel.Pages;

namespace BetterGenshinImpact.View.Pages;

public partial class MultiAccountPage : Page
{
    public MultiAccountPageViewModel ViewModel { get; }

    public MultiAccountPage(MultiAccountPageViewModel viewModel)
    {
        DataContext = ViewModel = viewModel;
        InitializeComponent();
    }

    private void DetailsScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || e.Handled)
        {
            return;
        }

        if (IsPopupContext(e.OriginalSource as DependencyObject))
        {
            return;
        }

        scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - (e.Delta / 3d));
        e.Handled = true;
    }

    private static bool IsPopupContext(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is Popup or ComboBoxItem)
            {
                return true;
            }

            current = current switch
            {
                Visual visual => VisualTreeHelper.GetParent(visual),
                _ => LogicalTreeHelper.GetParent(current)
            };
        }

        return false;
    }
}
