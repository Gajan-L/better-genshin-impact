using BetterGenshinImpact.Helpers.DpiAwareness;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.ViewModel;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Helpers.Ui;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;
using Wpf.Ui.Tray.Controls;

namespace BetterGenshinImpact.View;

public partial class MainWindow : FluentWindow, INavigationWindow
{
    private readonly ILogger<MainWindow> _logger = App.GetLogger<MainWindow>();

    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel, INavigationService navigationService, ISnackbarService snackbarService)
    {
        _logger.LogDebug("主窗体实例化");
        DataContext = ViewModel = viewModel;

        InitializeComponent();
        this.InitializeDpiAwareness();

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        navigationService.SetNavigationControl(RootNavigation);

        Application.Current.MainWindow = this;

        WeakReferenceMessenger.Default.Register<PropertyChangedMessage<object>>(this, static (recipient, message) =>
        {
            if (message.PropertyName != nameof(OtherConfig.UiCultureInfoName))
            {
                return;
            }

            ((MainWindow)recipient).RefreshNavigationLocalization();
        });

        Loaded += (s, e) => Activate();
        Loaded += (_, _) => RefreshNavigationLocalization();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowHelper.TryApplySystemBackdrop(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.LogDebug("主窗体退出");
        WeakReferenceMessenger.Default.UnregisterAll(this);
        base.OnClosed(e);
        App.GetService<NotifyIconViewModel>()?.Exit();
    }

    private void OnNotifyIconLeftDoubleClick(NotifyIcon sender, RoutedEventArgs e)
    {
        App.GetService<NotifyIconViewModel>()?.ShowOrHide();
    }

    public INavigationView GetNavigation() => RootNavigation;

    public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        throw new NotImplementedException();
    }

    public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) =>
        RootNavigation.SetPageProviderService(navigationViewPageProvider);

    public void ShowWindow() => Show();

    public void CloseWindow() => Close();

    private void RefreshNavigationLocalization()
    {
        Dispatcher.BeginInvoke(() =>
        {
            RefreshNavigationItems(RootNavigation.MenuItems);
            RefreshNavigationItems(RootNavigation.FooterMenuItems);
            RootNavigation.InvalidateVisual();
            RootNavigation.UpdateLayout();
        });
    }

    private static void RefreshNavigationItems(IEnumerable items)
    {
        foreach (var item in items)
        {
            if (item is not NavigationViewItem navigationItem)
            {
                continue;
            }

            BindingOperations.GetBindingExpressionBase(navigationItem, ContentControl.ContentProperty)?.UpdateTarget();

            RefreshNavigationItems(navigationItem.MenuItems);
        }
    }
}
