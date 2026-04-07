using System.Windows;
using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.Core.Script;
using BetterGenshinImpact.Core.Script.Group;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Common.Job;
using BetterGenshinImpact.Helpers;
using BetterGenshinImpact.Model;
using BetterGenshinImpact.Service;
using BetterGenshinImpact.Service.Interface;
using BetterGenshinImpact.Service.Notification;
using BetterGenshinImpact.Service.Notification.Model.Enum;
using BetterGenshinImpact.ViewModel.Pages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BetterGenshinImpact.Modules.MultiAccount.Runner;

public class OneDragonRunner(
    IScriptService scriptService,
    OneDragonConfigCatalog configCatalog,
    ILogger<OneDragonRunner> logger) : IOneDragonRunner
{
    private static readonly HashSet<string> DefaultTaskNames =
    [
        "领取邮件",
        "合成树脂",
        "自动秘境",
        "自动幽境危战",
        "自动地脉花",
        "领取每日奖励",
        "领取尘歌壶奖励",
    ];

    private static readonly string ScriptGroupFolder = Global.Absolute(@"User\ScriptGroup");

    public async Task RunAsync(string configName, bool ensureGameStarted = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configName))
        {
            throw new InvalidOperationException("OneDragon config name is required.");
        }

        var config = await LoadConfigAsync(configName, cancellationToken)
                     ?? throw new FileNotFoundException($"OneDragon config does not exist: {configName}");

        if (config.TaskEnabledList.Count == 0 || config.TaskEnabledList.All(item => !item.Value))
        {
            throw new InvalidOperationException($"OneDragon config has no enabled tasks: {configName}");
        }

        if (ensureGameStarted)
        {
            await ScriptService.StartGameTask(waitForMainUi: true);
        }

        Notify.Event(NotificationEvent.DragonStart).Success("一条龙启动");

        var orderedTasks = config.TaskEnabledList
            .Select(item => new OneDragonTaskItem(item.Key) { IsEnabled = item.Value })
            .ToList();

        foreach (var task in orderedTasks)
        {
            task.InitAction(config);
        }

        var enabledDefaultTaskCount = orderedTasks.Count(task => task.IsEnabled && DefaultTaskNames.Contains(task.Name));
        var enabledScriptGroupCount = orderedTasks.Count(task => task.IsEnabled && !DefaultTaskNames.Contains(task.Name));
        var defaultTaskIndex = 1;
        var scriptGroupIndex = 1;

        logger.LogInformation(
            "Running OneDragon config {ConfigName}. Default tasks: {DefaultTasks}, script groups: {ScriptGroups}",
            configName,
            enabledDefaultTaskCount,
            enabledScriptGroupCount);

        foreach (var task in orderedTasks.Where(task => task.IsEnabled))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DefaultTaskNames.Contains(task.Name))
            {
                logger.LogInformation(
                    "Running OneDragon task {TaskName} ({Index}/{Total})",
                    task.Name,
                    defaultTaskIndex++,
                    enabledDefaultTaskCount);

                await new TaskRunner().RunThreadAsync(async () =>
                {
                    if (task.Action != null)
                    {
                        await task.Action();
                    }

                    await Task.Delay(1000, cancellationToken);
                });
            }
            else
            {
                logger.LogInformation(
                    "Running OneDragon script group {TaskName} ({Index}/{Total})",
                    task.Name,
                    scriptGroupIndex++,
                    enabledScriptGroupCount);
                await RunScriptGroupAsync(task.Name, cancellationToken);
            }

            if (CancellationContext.Instance.Cts.IsCancellationRequested)
            {
                logger.LogInformation("OneDragon execution cancelled.");
                if (!CancellationContext.Instance.IsManualStop)
                {
                    Notify.Event(NotificationEvent.DragonEnd).Success("一条龙和配置组任务结束");
                }

                return;
            }
        }

        await new TaskRunner().RunThreadAsync(async () =>
        {
            await new CheckRewardsTask().Start(CancellationContext.Instance.Cts.Token);
            await Task.Delay(500, cancellationToken);
            if (!CancellationContext.Instance.IsManualStop)
            {
                Notify.Event(NotificationEvent.DragonEnd).Success("一条龙和配置组任务结束");
            }
        });

        HandleCompletionAction(config);
    }

    private async Task<OneDragonFlowConfig?> LoadConfigAsync(string configName, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(configCatalog.ConfigDirectory);
        var path = configCatalog.GetConfigPath(configName);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonConvert.DeserializeObject<OneDragonFlowConfig>(json);
    }

    private async Task RunScriptGroupAsync(string groupName, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(ScriptGroupFolder, $"{groupName}.json");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Script group does not exist: {groupName}");
        }

        var group = ScriptGroup.FromJson(await File.ReadAllTextAsync(filePath, cancellationToken));
        await scriptService.RunMulti(ScriptControlViewModel.GetNextProjects(group), group.Name);
        await Task.Delay(1000, cancellationToken);
    }

    private static void HandleCompletionAction(OneDragonFlowConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.CompletionAction))
        {
            return;
        }

        switch (config.CompletionAction)
        {
            case "关闭游戏":
                SystemControl.CloseGame();
                break;
            case "关闭游戏和软件":
                SystemControl.CloseGame();
                Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
                break;
            case "关机":
                SystemControl.CloseGame();
                SystemControl.Shutdown();
                break;
        }
    }
}
