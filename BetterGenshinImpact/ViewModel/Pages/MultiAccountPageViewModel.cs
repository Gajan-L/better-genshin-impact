using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Modules.MultiAccount;
using BetterGenshinImpact.Modules.MultiAccount.Runner;
using BetterGenshinImpact.Service.Interface;
using BetterGenshinImpact.View.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Wpf.Ui.Violeta.Controls;

namespace BetterGenshinImpact.ViewModel.Pages;

public partial class MultiAccountPageViewModel : ViewModel
{
    private readonly ILogger<MultiAccountPageViewModel> _logger;
    private readonly IMultiAccountProfileStore _profileStore;
    private readonly AccountBatchOrchestrator _batchOrchestrator;
    private readonly GameExecutablePathResolver _gameExecutablePathResolver;
    private readonly OneDragonConfigCatalog _oneDragonConfigCatalog;
    private readonly ITranslationService _translationService;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private bool _isLoading;
    private bool _suppressAutoSave;
    private string? _statusTextFormat;
    private object?[] _statusTextArgs = [];
    private string? _busyTextFormat;
    private object?[] _busyTextArgs = [];

    public ObservableCollection<MultiAccountProfile> Profiles { get; } = [];

    public ObservableCollection<string> AvailableOneDragonConfigs { get; } = [];

    public IReadOnlyList<KeyValuePair<GameRegion, string>> RegionOptions { get; } =
    [
        new(GameRegion.CNOfficial, "国服官服"),
        new(GameRegion.Global, "国际服"),
    ];

    [ObservableProperty]
    private MultiAccountProfile? _selectedProfile;

    [ObservableProperty]
    private bool _hasSelectedProfile;

    [ObservableProperty]
    private string _selectedRememberedAccountStatusText = "记住账号切换支持匹配完整账号，也支持匹配界面上看到的半隐藏账号。";

    [ObservableProperty]
    private string _selectedOneDragonConfigStatusText = "请选择一个账号后查看或修改它绑定的一条龙配置。";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    [ObservableProperty]
    private string _statusText = "账号配置保存在 User/MultiAccount 下。当前仅支持国服官服和国际服，只保留记住账号切换方案，不会读取、写入或恢复注册表设置。";

    [ObservableProperty]
    private int _enabledProfileCount;

    public MultiAccountPageViewModel(
        IMultiAccountProfileStore profileStore,
        AccountBatchOrchestrator batchOrchestrator,
        GameExecutablePathResolver gameExecutablePathResolver,
        OneDragonConfigCatalog oneDragonConfigCatalog)
    {
        _logger = App.GetLogger<MultiAccountPageViewModel>();
        _profileStore = profileStore;
        _batchOrchestrator = batchOrchestrator;
        _gameExecutablePathResolver = gameExecutablePathResolver;
        _oneDragonConfigCatalog = oneDragonConfigCatalog;
        _translationService = App.GetService<ITranslationService>() ?? throw new NullReferenceException();

        Profiles.CollectionChanged += ProfilesCollectionChanged;

        WeakReferenceMessenger.Default.Register<PropertyChangedMessage<object>>(this, (_, msg) =>
        {
            if (msg.PropertyName == nameof(OtherConfig.UiCultureInfoName))
            {
                RefreshLocalizedState();
            }
        });

        SetStatusText("账号配置保存在 User/MultiAccount 下。当前仅支持国服官服和国际服，只保留记住账号切换方案，不会读取、写入或恢复注册表设置。");
        UpdateSelectedProfileStateLocalized();
    }

    public override async Task OnNavigatedToAsync()
    {
        await LoadAsync();
    }

    partial void OnSelectedProfileChanged(MultiAccountProfile? value)
    {
        HasSelectedProfile = value != null;
        UpdateSelectedProfileStateLocalized();
        NotifyCommandStateChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        NotifyCommandStateChanged();
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanAddProfile))]
    private async Task AddProfileAsync()
    {
        var configuredInstallPath = TaskContext.Instance().Config.GenshinStartConfig.InstallPath;
        var inferredRegion = InferRegionFromInstallPath(configuredInstallPath);
        var defaultInstallPath = ResolveSuggestedInstallPath(inferredRegion, configuredInstallPath);
        var profile = new MultiAccountProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = GetNextLocalizedProfileName(),
            IsEnabled = true,
            Order = Profiles.Count,
            Region = inferredRegion,
            InstallPath = defaultInstallPath,
            LaunchArgs = TaskContext.Instance().Config.GenshinStartConfig.GenshinStartArgs,
            OneDragonConfigName = GetDefaultOneDragonConfigName(),
        };

        Profiles.Add(profile);
        SelectedProfile = profile;
        SetStatusText("已创建账号“{0}”。", profile.Name);
        Toast.Success(StatusText);
        await SaveProfilesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedProfile))]
    private async Task DeleteSelectedProfileAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        var profile = SelectedProfile;
        var result = await ThemedMessageBox.ShowAsync(
            FormatText("确定删除账号“{0}”吗？", profile.Name),
            Tr("删除账号"),
            MessageBoxButton.YesNo,
            ThemedMessageBox.MessageBoxIcon.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Profiles.Remove(profile);
        await _profileStore.DeleteAsync(profile.Id);

        SelectedProfile = Profiles.FirstOrDefault();
        NormalizeOrders();
        SetStatusText("已删除账号“{0}”。", profile.Name);
        Toast.Success(StatusText);
        await SaveProfilesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedProfileUp))]
    private async Task MoveSelectedProfileUpAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        var index = Profiles.IndexOf(SelectedProfile);
        if (index <= 0)
        {
            return;
        }

        Profiles.Move(index, index - 1);
        NormalizeOrders();
        await SaveProfilesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedProfileDown))]
    private async Task MoveSelectedProfileDownAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        var index = Profiles.IndexOf(SelectedProfile);
        if (index < 0 || index >= Profiles.Count - 1)
        {
            return;
        }

        Profiles.Move(index, index + 1);
        NormalizeOrders();
        await SaveProfilesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanBrowseInstallPath))]
    private async Task BrowseInstallPathAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        var initialPath = SelectedProfile.InstallPath;
        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(initialPath);
            dialog.FileName = Path.GetFileName(initialPath);
        }

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SelectedProfile.InstallPath = dialog.FileName;
        SelectedProfile.Region = InferRegionFromInstallPath(dialog.FileName);
        SetStatusText("已更新账号“{0}”的游戏路径。", SelectedProfile.Name);
        await SaveProfilesAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunBatch))]
    private async Task RunBatchAsync()
    {
        if (AvailableOneDragonConfigs.Count == 0)
        {
            Toast.Warning(Tr("没有找到已保存的一条龙配置，请先到一条龙页面保存至少一套配置。"));
            return;
        }

        var enabledProfiles = Profiles
            .Where(profile => profile.IsEnabled)
            .OrderBy(profile => profile.Order)
            .ToList();

        if (enabledProfiles.Count == 0)
        {
            Toast.Warning(Tr("请至少启用一个账号后再运行批次。"));
            return;
        }

        await RunBusyAsync("正在运行多账户批次...", async () =>
        {
            var records = await _batchOrchestrator.RunBatchAsync(enabledProfiles);
            var failedRecord = records.FirstOrDefault(record => !record.Success);

            if (failedRecord != null)
            {
                SetStatusText("批次在“{0}”处停止：{1}", failedRecord.ProfileName, failedRecord.Message);
                Toast.Error(StatusText);
            }
            else
            {
                SetStatusText("批次已完成，共运行 {0} 个账号。", records.Count);
                Toast.Success(StatusText);
            }
        });
    }

    private bool CanRefresh() => !IsBusy;

    private bool CanAddProfile() => !IsBusy;

    private bool CanDeleteSelectedProfile() => !IsBusy && SelectedProfile != null;

    private bool CanMoveSelectedProfileUp() =>
        !IsBusy && SelectedProfile != null && Profiles.IndexOf(SelectedProfile) > 0;

    private bool CanMoveSelectedProfileDown() =>
        !IsBusy && SelectedProfile != null && Profiles.IndexOf(SelectedProfile) >= 0 &&
        Profiles.IndexOf(SelectedProfile) < Profiles.Count - 1;

    private bool CanBrowseInstallPath() => !IsBusy && SelectedProfile != null;

    private bool CanRunBatch() => !IsBusy && Profiles.Any(profile => profile.IsEnabled);

    private async Task LoadAsync()
    {
        await RunBusyAsync("正在加载多账户配置...", async () =>
        {
            var selectedProfileId = SelectedProfile?.Id;
            var dirty = false;

            _isLoading = true;
            try
            {
                foreach (var profile in Profiles.ToList())
                {
                    DetachProfile(profile);
                }

                Profiles.Clear();
                LoadOneDragonConfigs();

                var loadedProfiles = await _profileStore.LoadAsync();
                foreach (var profile in loadedProfiles.OrderBy(profile => profile.Order).ThenBy(profile => profile.Name))
                {
                    if (string.IsNullOrWhiteSpace(profile.Id))
                    {
                        profile.Id = Guid.NewGuid().ToString("N");
                        dirty = true;
                    }

                    dirty |= NormalizeOneDragonConfig(profile);
                    dirty |= ApplySuggestedInstallPath(profile, clearWhenUnavailable: false);
                    Profiles.Add(profile);
                }

                dirty |= NormalizeOrders();
                SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == selectedProfileId) ?? Profiles.FirstOrDefault();
            }
            finally
            {
                _isLoading = false;
            }

            UpdateSelectedProfileStateLocalized();
            UpdateSummaryState();
            if (Profiles.Count == 0)
            {
                SetStatusText("先创建第一个账号，然后填写目标账号并绑定一条龙配置。");
            }
            else
            {
                SetStatusText("已加载 {0} 个账号。", Profiles.Count);
            }

            if (dirty)
            {
                await SaveProfilesAsync();
            }
        });
    }

    private void LoadOneDragonConfigs()
    {
        AvailableOneDragonConfigs.Clear();
        foreach (var configName in _oneDragonConfigCatalog.GetAvailableConfigNames())
        {
            AvailableOneDragonConfigs.Add(configName);
        }
    }

    private void ProfilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (MultiAccountProfile profile in e.OldItems)
            {
                DetachProfile(profile);
            }
        }

        if (e.NewItems != null)
        {
            foreach (MultiAccountProfile profile in e.NewItems)
            {
                AttachProfile(profile);
            }
        }

        UpdateSelectedProfileStateLocalized();
        UpdateSummaryState();
        NotifyCommandStateChanged();

        if (_isLoading || _suppressAutoSave)
        {
            return;
        }

        NormalizeOrders();
        _ = SaveProfilesAsync();
    }

    private void AttachProfile(MultiAccountProfile profile)
    {
        profile.PropertyChanged += ProfileOnPropertyChanged;
    }

    private void DetachProfile(MultiAccountProfile profile)
    {
        profile.PropertyChanged -= ProfileOnPropertyChanged;
    }

    private void ProfileOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading || _suppressAutoSave)
        {
            return;
        }

        if (sender is MultiAccountProfile profile && e.PropertyName == nameof(MultiAccountProfile.Region))
        {
            ApplySuggestedInstallPath(profile, clearWhenUnavailable: true);
        }

        if (sender == SelectedProfile)
        {
            UpdateSelectedProfileStateLocalized();
        }

        UpdateSummaryState();
        NotifyCommandStateChanged();
        _ = SaveProfilesAsync();
    }

    private bool NormalizeOrders()
    {
        var dirty = false;
        _suppressAutoSave = true;
        try
        {
            for (var index = 0; index < Profiles.Count; index++)
            {
                if (Profiles[index].Order == index)
                {
                    continue;
                }

                Profiles[index].Order = index;
                dirty = true;
            }
        }
        finally
        {
            _suppressAutoSave = false;
        }

        return dirty;
    }

    private async Task SaveProfilesAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            await _profileStore.SaveAllAsync(Profiles.OrderBy(profile => profile.Order));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save multi-account profiles");
            SetStatusText("保存多账户配置失败：{0}", ex.Message);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task RunBusyAsync(string busyText, Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetBusyText(busyText);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Multi-account action failed");
            SetStatusTextLiteral(Tr(ex.Message));
            Toast.Error(Tr(ex.Message));
        }
        finally
        {
            ClearBusyText();
            IsBusy = false;
        }
    }

    private void RefreshLocalizedState()
    {
        if (!string.IsNullOrWhiteSpace(_statusTextFormat))
        {
            StatusText = FormatText(_statusTextFormat!, _statusTextArgs);
        }

        if (!string.IsNullOrWhiteSpace(_busyTextFormat))
        {
            BusyText = FormatText(_busyTextFormat!, _busyTextArgs);
        }

        UpdateSelectedProfileStateLocalized();
    }

    private void UpdateSelectedProfileStateLocalized()
    {
        if (SelectedProfile == null)
        {
            SelectedRememberedAccountStatusText = Tr("记住账号切换支持匹配完整账号，也支持匹配界面上看到的半隐藏账号。");
            SelectedOneDragonConfigStatusText = Tr("请选择一个账号后查看或修改它绑定的一条龙配置。");
            return;
        }

        SelectedRememberedAccountStatusText = GetLocalizedRememberedAccountStatusText(SelectedProfile);
        SelectedOneDragonConfigStatusText = GetLocalizedOneDragonConfigStatusText(SelectedProfile);
    }

    private string GetLocalizedOneDragonConfigStatusText(MultiAccountProfile profile)
    {
        if (AvailableOneDragonConfigs.Count == 0)
        {
            return Tr("没有找到已保存的一条龙配置，请先到一条龙页面保存至少一套配置。");
        }

        if (string.IsNullOrWhiteSpace(profile.OneDragonConfigName))
        {
            return Tr("请为当前账号选择一套已保存的一条龙配置。");
        }

        if (!_oneDragonConfigCatalog.Exists(profile.OneDragonConfigName))
        {
            return FormatText("当前绑定的一条龙配置在磁盘上不存在：{0}。", profile.OneDragonConfigName);
        }

        return FormatText("当前使用的一条龙配置：{0}。", profile.OneDragonConfigName);
    }

    private string GetLocalizedRememberedAccountStatusText(MultiAccountProfile profile)
    {
        if (!profile.Region.SupportsRememberedAccountUiSwitch())
        {
            return FormatText("当前区服 {0} 还不支持记住账号切换。", GetRegionDisplayName(profile.Region));
        }

        if (string.IsNullOrWhiteSpace(profile.RememberedAccountLabel))
        {
            return Tr("这里既可以填完整账号，也可以填账号列表里看到的半隐藏账号，例如 17512345625、75******25 或 ab***@mail.com。");
        }

        return FormatText("运行时会优先判断当前显示账号是否已经匹配“{0}”；如果未匹配，才会展开账号列表并切换。", profile.RememberedAccountLabel);
    }

    private string GetRegionDisplayName(GameRegion region)
    {
        return region switch
        {
            GameRegion.CNOfficial => Tr("国服官服"),
            GameRegion.Global => Tr("国际服"),
            _ => region.ToString(),
        };
    }

    private string Tr(string text)
    {
        return _translationService.Translate(text, TranslationSourceInfo.From(MissingTextSource.UiDynamicBinding));
    }

    private string FormatText(string format, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Tr(format), args);
    }

    private void SetStatusText(string format, params object?[] args)
    {
        _statusTextFormat = format;
        _statusTextArgs = args;
        StatusText = FormatText(format, args);
    }

    private void SetStatusTextLiteral(string text)
    {
        _statusTextFormat = null;
        _statusTextArgs = [];
        StatusText = text;
    }

    private void SetBusyText(string format, params object?[] args)
    {
        _busyTextFormat = format;
        _busyTextArgs = args;
        BusyText = FormatText(format, args);
    }

    private void ClearBusyText()
    {
        _busyTextFormat = null;
        _busyTextArgs = [];
        BusyText = string.Empty;
    }

    private void UpdateSummaryState()
    {
        EnabledProfileCount = Profiles.Count(profile => profile.IsEnabled);
    }

    private string GetDefaultOneDragonConfigName()
    {
        return AvailableOneDragonConfigs.FirstOrDefault() ?? string.Empty;
    }

    private string GetNextLocalizedProfileName()
    {
        for (var index = 1; ; index++)
        {
            var candidate = FormatText("账号 {0}", index);
            if (Profiles.All(profile => !string.Equals(profile.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    private bool NormalizeOneDragonConfig(MultiAccountProfile profile)
    {
        if (_oneDragonConfigCatalog.Exists(profile.OneDragonConfigName))
        {
            return false;
        }

        if (AvailableOneDragonConfigs.Count == 1)
        {
            var fallbackConfig = AvailableOneDragonConfigs[0];
            if (!string.Equals(profile.OneDragonConfigName, fallbackConfig, StringComparison.Ordinal))
            {
                profile.OneDragonConfigName = fallbackConfig;
                return true;
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(profile.OneDragonConfigName))
        {
            profile.OneDragonConfigName = string.Empty;
            return true;
        }

        return false;
    }

    private static GameRegion InferRegionFromInstallPath(string? installPath)
    {
        var fileName = Path.GetFileName(installPath);
        if (string.Equals(fileName, "GenshinImpact.exe", StringComparison.OrdinalIgnoreCase))
        {
            return GameRegion.Global;
        }

        return GameRegion.CNOfficial;
    }

    private bool ApplySuggestedInstallPath(MultiAccountProfile profile, bool clearWhenUnavailable)
    {
        var suggestedInstallPath = ResolveSuggestedInstallPath(profile.Region, profile.InstallPath);
        if (!string.IsNullOrWhiteSpace(suggestedInstallPath))
        {
            if (string.Equals(profile.InstallPath, suggestedInstallPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return UpdateInstallPath(profile, suggestedInstallPath);
        }

        if (clearWhenUnavailable && !string.IsNullOrWhiteSpace(profile.InstallPath) && !PathMatchesRegion(profile.InstallPath, profile.Region))
        {
            return UpdateInstallPath(profile, string.Empty);
        }

        return false;
    }

    private string ResolveSuggestedInstallPath(GameRegion region, params string?[] preferredPaths)
    {
        var configuredInstallPath = TaskContext.Instance().Config.GenshinStartConfig.InstallPath;
        return _gameExecutablePathResolver.Find(region, preferredPaths.Append(configuredInstallPath).ToArray()) ?? string.Empty;
    }

    private static bool PathMatchesRegion(string? path, GameRegion region)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return string.Equals(
            Path.GetFileName(path),
            region.GetDefaultExecutableName(),
            StringComparison.OrdinalIgnoreCase);
    }

    private bool UpdateInstallPath(MultiAccountProfile profile, string installPath)
    {
        _suppressAutoSave = true;
        try
        {
            profile.InstallPath = installPath;
        }
        finally
        {
            _suppressAutoSave = false;
        }

        return true;
    }

    private void NotifyCommandStateChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        AddProfileCommand.NotifyCanExecuteChanged();
        DeleteSelectedProfileCommand.NotifyCanExecuteChanged();
        MoveSelectedProfileUpCommand.NotifyCanExecuteChanged();
        MoveSelectedProfileDownCommand.NotifyCanExecuteChanged();
        BrowseInstallPathCommand.NotifyCanExecuteChanged();
        RunBatchCommand.NotifyCanExecuteChanged();
    }
}
