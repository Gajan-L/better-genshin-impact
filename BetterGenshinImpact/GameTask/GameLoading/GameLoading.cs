using BetterGenshinImpact.Core.Config;
using BetterGenshinImpact.GameTask.GameLoading.Assets;
using System;
using System.Diagnostics;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.Common.BgiVision;
using BetterGenshinImpact.GameTask.Common.Element.Assets;
using BetterGenshinImpact.GameTask.Model.Area;
using BetterGenshinImpact.Modules.MultiAccount;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.Win32;

namespace BetterGenshinImpact.GameTask.GameLoading;

public class GameLoadingTrigger : ITaskTrigger
{
    public static bool GlobalEnabled = true;
    
    public string Name => "自动开门";

    public bool IsEnabled { get => GlobalEnabled; set {} }

    public int Priority => 999;

    public bool IsExclusive => false;

    public bool IsBackgroundRunning => true;

    private readonly GameLoadingAssets _assets;
    private readonly ElementAssets _elementAssets;

    private readonly GenshinStartConfig _config = TaskContext.Instance().Config.GenshinStartConfig;
    private static ILogger<GameLoadingTrigger> _logger = App.GetLogger<GameLoadingTrigger>();
    private DateTime _prevExecuteTime = DateTime.MinValue;

    private DateTime _triggerStartTime = DateTime.Now;

    private string GameServer = "";

    public GameLoadingTrigger()
    {
        GameLoadingAssets.DestroyInstance();
        _assets = GameLoadingAssets.Instance;
        _elementAssets = ElementAssets.Instance;
    }

    public void InnerSetEnabled(bool enabled)
    {
        GlobalEnabled = enabled;
    }

    public void Init()
    {
        if (!TaskContext.Instance().ShouldAutoEnterGame())
        {
            InnerSetEnabled(false);
            return;
        }

        // // 前面没有联动启动原神，这个任务也不用启动
        // if ((DateTime.Now - TaskContext.Instance().LinkedStartGenshinTime).TotalMinutes >= 5)
        // {
        //     IsEnabled = false;
        // }
        if (!_config.RecordGameTimeEnabled)
        {
            return;
        }

        var installPath = TaskContext.Instance().ResolveGenshinInstallPath();
        var runtimeRegion = TaskContext.Instance().ResolveGameRegion();
        var fileName = Path.GetFileName(installPath);
        if (runtimeRegion != null)
        {
            GameServer = runtimeRegion.Value.GetGameServerCode();
            StartStarward();
            return;
        }

        if (string.Equals(fileName, "GenshinImpact.exe", StringComparison.OrdinalIgnoreCase))
        {
            GameServer = GameRegion.Global.GetGameServerCode();
            StartStarward();
            return;
        }

        if (string.Equals(fileName, "YuanShen.exe", StringComparison.OrdinalIgnoreCase))
        {
            GameServer = GameRegion.CNOfficial.GetGameServerCode();
            StartStarward();
            return;
        }
    }

    public bool StartStarward()
    {
        if (string.IsNullOrWhiteSpace(GameServer))
        {
            return false;
        }

        try
        {
            Debug.WriteLine($"[GameLoading] 服务器：{GameServer}");
            if (IsStarwardProtocolRegistered())
            {
                Process.Start(new ProcessStartInfo($"starward://playtime/{GameServer}") { UseShellExecute = true });
                return true;
            }
            else
            {
                // TaskControl.Logger.LogWarning("没有检测到 Starward 协议注册，请查看帮助文档！");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[GameLoading] Starward记录时间失败");
            return false;
        }
    }

    public bool IsStarwardProtocolRegistered()
    {
        try
        {
            // 打开注册表路径 HKEY_CLASSES_ROOT\starward
            using (RegistryKey key = Registry.ClassesRoot.OpenSubKey("starward"))
            {
                // 如果键存在
                if (key != null)
                {
                    // 检查是否存在 URL Protocol 值
                    object urlProtocol = key.GetValue("URL Protocol");
                    // 如果 URL Protocol 存在且值为空字符串（标准配置），认为协议已注册
                    if (urlProtocol != null && urlProtocol.ToString() == "")
                    {
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 如果访问注册表时发生错误，记录调试信息
            Debug.WriteLine($"[GameLoading] 检查 Starward 协议时发生错误: {ex.Message}");
        }

        // 如果键不存在或不符合条件，返回 false
        return false;
    }

    public void OnCapture(CaptureContent content)
    {
        // 2s 一次
        if ((DateTime.Now - _prevExecuteTime).TotalMilliseconds <= 2000)
        {
            return;
        }

        _prevExecuteTime = DateTime.Now;
        // 5min 后自动停止
        if ((DateTime.Now - _triggerStartTime).TotalMinutes >= 5)
        {
            InnerSetEnabled(false);
            return;
        }
        
        // 成功进入游戏判断    
        if (Bv.IsInMainUi(content.CaptureRectArea) || Bv.IsInAnyClosableUi(content.CaptureRectArea) || Bv.IsInDomain(content.CaptureRectArea))
        {
            // _logger.LogInformation("当前在游戏主界面");
            InnerSetEnabled(false);
            return;
        }
        
        // 适龄提示窗口自动关闭
        var agePopup = content.CaptureRectArea.Find(_elementAssets.BtnWhiteConfirm);
        if (!agePopup.IsEmpty())
        {
            agePopup.Click();
        }

        var extraEnterGameBtn = content.CaptureRectArea.Find(_assets.ChooseEnterGameRo);
        if (!extraEnterGameBtn.IsEmpty())
        {
            extraEnterGameBtn.Click();
            return;
        }

        var enterGameBtn = content.CaptureRectArea.Find(_assets.EnterGameRo);
        if (!enterGameBtn.IsEmpty())
        {
            TaskContext.Instance().PostMessageSimulator.LeftButtonClickBackground();
            return;
        }

        if (Bv.IsInBlessingOfTheWelkinMoon(content.CaptureRectArea))
        {
            GameCaptureRegion.GameRegion1080PPosMove(100, 100);
            TaskContext.Instance().PostMessageSimulator.LeftButtonClickBackground();
            Debug.WriteLine("[GameLoading] Click blessing of the welkin moon");
            // TaskControl.Logger.LogInformation("自动点击月卡");
            return;
        }

        // 原石
        var ysRa = content.CaptureRectArea.Find(ElementAssets.Instance.PrimogemRo);
        if (!ysRa.IsEmpty())
        {
            GameCaptureRegion.GameRegion1080PPosMove(100, 100);
            TaskContext.Instance().PostMessageSimulator.LeftButtonClickBackground();
            Debug.WriteLine("[GameLoading] 跳过原石");
            return;
        }
    }

};
