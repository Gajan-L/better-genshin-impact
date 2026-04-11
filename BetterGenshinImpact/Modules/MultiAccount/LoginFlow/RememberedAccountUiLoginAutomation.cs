using System.Diagnostics;
using BetterGenshinImpact.Core.BgiVision;
using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.GameTask.Common;
using BetterGenshinImpact.GameTask.GameLoading;
using BetterGenshinImpact.GameTask.GameLoading.Assets;
using BetterGenshinImpact.GameTask.Model.Area;
using BetterGenshinImpact.Service;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace BetterGenshinImpact.Modules.MultiAccount.LoginFlow;

public class RememberedAccountUiLoginAutomation
{
    private sealed class RememberedAccountCandidate
    {
        public RememberedAccountCandidate(Region region)
        {
            Region = region;
            Entry = new RememberedAccountEntry(region.Text, region.Left, region.Top, region.Width, region.Height);
        }

        public Region Region { get; }

        public RememberedAccountEntry Entry { get; }

        public string Text => Entry.Text;
    }

    private static readonly Rect RememberedAccountDialogRoi1080 = new(560, 320, 820, 470);
    private static readonly Rect LogoutDialogRoi1080 = new(590, 350, 760, 450);
    private static readonly Rect LogoutDialogButtonRoi1080 = new(650, 660, 620, 120);
    private static readonly Rect SwitchAccountConfirmDialogRoi1080 = new(620, 430, 700, 320);
    private static readonly Rect SwitchAccountConfirmButtonRoi1080 = new(700, 560, 560, 140);
    private static readonly Rect ChooserButtonRoi1080 = new(640, 650, 640, 140);

    private const string LogoutDialogTitleText = "\u9000\u51fa\u767b\u5f55";
    private const string KeepLoginRecordText = "\u9000\u51fa\u5e76\u4fdd\u7559\u767b\u5f55\u8bb0\u5f55";
    private const string OtherAccountText = "\u767b\u5f55\u5176\u4ed6\u8d26\u53f7";
    private const string EnterGameText = "\u8fdb\u5165\u6e38\u620f";
    private const string LogoutButtonText = "\u9000\u51fa";
    private const string SwitchAccountConfirmTitleText = "\u786e\u8ba4\u5207\u6362\u8d26\u53f7";
    private const string SwitchAccountConfirmTitleFallbackText = "\u5207\u6362\u8d26\u53f7";
    private const string ConfirmButtonText = "\u786e\u5b9a";

    private const double LogoutIconX = 1825;
    private const double LogoutIconY = 1005;
    private const double KeepLoginRecordFallbackX = 720;
    private const double KeepLoginRecordFallbackY = 620;
    private const double ConfirmLogoutFallbackX = 1095;
    private const double ConfirmLogoutFallbackY = 726;
    private const double ConfirmSwitchAccountFallbackX = 1085;
    private const double ConfirmSwitchAccountFallbackY = 618;
    private const double AccountSelectorFallbackX = 960;
    private const double AccountSelectorFallbackY = 520;
    private const double GlobalAccountSelectorArrowFallbackX = 1205;
    private const double GlobalAccountSelectorArrowFallbackY = 540;

    private readonly ILogger<RememberedAccountUiLoginAutomation> _logger = App.GetLogger<RememberedAccountUiLoginAutomation>();

    private static GameLoadingAssets GameLoadingAssets => GameLoadingAssets.Instance;

    public async Task EnterGameAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default)
    {
        if (!profile.Region.SupportsRememberedAccountUiSwitch())
        {
            throw new InvalidOperationException($"Remembered-account UI switching is not supported for {profile.Region}.");
        }

        if (string.IsNullOrWhiteSpace(profile.RememberedAccountLabel))
        {
            throw new InvalidOperationException($"Remembered account label is missing for profile {profile.Name}.");
        }

        await ScriptService.StartGameTask(profile.ToLaunchContext(autoEnterGame: false), waitForMainUi: false);
        await WaitForLoginSurfaceAsync(cancellationToken);

        await EnsureRememberedAccountChooserAsync(profile.RememberedAccountLabel, cancellationToken);

        if (!IsCurrentRememberedAccountMatch(profile.RememberedAccountLabel))
        {
            await EnsureRememberedAccountListExpandedAsync(profile.Region, profile.RememberedAccountLabel, cancellationToken);

            if (IsCurrentRememberedAccountMatch(profile.RememberedAccountLabel))
            {
                _logger.LogInformation("Current remembered account matched {AccountLabel} after expansion check; skipping account selection.", profile.RememberedAccountLabel);
            }
            else
            {
                if (!await TrySelectRememberedAccountAsync(profile.Region, profile.RememberedAccountLabel, cancellationToken))
                {
                    var visibleLabels = await ReadVisibleAccountTextsAsync(cancellationToken);
                    var details = visibleLabels.Count == 0 ? "No remembered-account entries were detected." : $"Visible entries: {string.Join(", ", visibleLabels)}";
                    throw new InvalidOperationException($"Could not find remembered account '{profile.RememberedAccountLabel}'. {details}");
                }

                await ConfirmSwitchAccountIfNeededAsync(cancellationToken);
            }
        }
        else
        {
            _logger.LogInformation("Current remembered account already matches {AccountLabel}; skipping account selection.", profile.RememberedAccountLabel);
        }

        await ClickChooseEnterGameAsync(cancellationToken);

        TaskContext.Instance().SetRuntimeLaunchContext(profile.ToLaunchContext());
        GameLoadingTrigger.GlobalEnabled = true;
        await ScriptService.WaitForMainUiAsync(cancellationToken);
    }

    private async Task WaitForLoginSurfaceAsync(CancellationToken cancellationToken)
    {
        var page = new BvPage(cancellationToken);
        var timeoutAt = Stopwatch.StartNew();
        var lastFocusRecoveryAt = TimeSpan.Zero;
        var focusRecoveryCount = 0;

        while (timeoutAt.Elapsed < TimeSpan.FromSeconds(60))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((await page.Locator(GameLoadingAssets.EnterGameRo).TryWaitFor(600)).Count > 0
                || (await page.Locator(GameLoadingAssets.ChooseEnterGameRo).TryWaitFor(600)).Count > 0)
            {
                return;
            }

            if (timeoutAt.Elapsed >= TimeSpan.FromSeconds(5)
                && timeoutAt.Elapsed - lastFocusRecoveryAt >= TimeSpan.FromSeconds(2))
            {
                TryRecoverGameWindowFocus(++focusRecoveryCount);
                lastFocusRecoveryAt = timeoutAt.Elapsed;
            }

            await page.Wait(300);
        }

        throw new TimeoutException("Timed out waiting for the login page.");
    }

    private void TryRecoverGameWindowFocus(int attempt)
    {
        try
        {
            if (SystemControl.IsGenshinImpactActiveByProcess())
            {
                return;
            }

            var gameHandle = TaskContext.Instance().IsInitialized
                ? TaskContext.Instance().GameHandle
                : SystemControl.FindGenshinImpactHandle();

            if (gameHandle == 0)
            {
                return;
            }

            _logger.LogInformation(
                "Login page not ready yet; recovering game focus. Attempt {Attempt}",
                attempt);

            if (attempt >= 4)
            {
                SystemControl.MinimizeAndActivateWindow(gameHandle);
                return;
            }

            SystemControl.ActivateWindow(gameHandle);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to recover game focus while waiting for the login page.");
        }
    }

    private async Task EnsureRememberedAccountChooserAsync(string targetAccountLabel, CancellationToken cancellationToken)
    {
        if (await IsRememberedAccountChooserVisibleAsync(cancellationToken))
        {
            _logger.LogInformation("Remembered-account chooser is already visible.");
            return;
        }

        _logger.LogInformation("Opening remembered-account chooser...");

        if (!await TryOpenRememberedAccountChooserAsync(cancellationToken))
        {
            if (await IsRememberedAccountChooserVisibleAsync(cancellationToken) && IsCurrentRememberedAccountMatch(targetAccountLabel))
            {
                _logger.LogInformation("Logout dialog did not appear, but the current remembered account already matches {AccountLabel}; continuing.", targetAccountLabel);
                return;
            }

            throw new TimeoutException("Timed out waiting for the logout dialog.");
        }
    }

    private async Task<bool> TrySelectRememberedAccountAsync(GameRegion region, string targetAccountLabel, CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(12))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidates = ReadRememberedAccountCandidates();
            if (!RememberedAccountChooserLayout.IsExpanded(candidates.Select(static candidate => candidate.Entry)))
            {
                await EnsureRememberedAccountListExpandedAsync(region, targetAccountLabel, cancellationToken);
                await Task.Delay(250, cancellationToken);
                continue;
            }

            var candidate = TryFindRememberedAccountCandidate(candidates, targetAccountLabel);
            if (candidate != null)
            {
                _logger.LogInformation("Selecting remembered account {AccountLabel}", targetAccountLabel);
                candidate.Region.Click();
                await Task.Delay(350, cancellationToken);
                return true;
            }

            await Task.Delay(450, cancellationToken);
        }

        return false;
    }

    private RememberedAccountCandidate? TryFindRememberedAccountCandidate(IReadOnlyList<RememberedAccountCandidate> candidates, string targetAccountLabel)
    {
        var targetEntry = RememberedAccountChooserLayout.TryPickTargetEntry(
            candidates.Select(static candidate => candidate.Entry),
            targetAccountLabel);

        if (targetEntry == null)
        {
            return null;
        }

        return candidates.FirstOrDefault(candidate => candidate.Entry == targetEntry);
    }

    private List<RememberedAccountCandidate> ReadRememberedAccountCandidates()
    {
        using var screen = TaskControl.CaptureToRectArea();
        using var dialog = screen.DeriveCrop(RememberedAccountDialogRoi1080);
        var regions = dialog.FindMulti(new BetterGenshinImpact.Core.Recognition.RecognitionObject
        {
            RecognitionType = BetterGenshinImpact.Core.Recognition.RecognitionTypes.Ocr,
        });

        return regions
            .Select(static region => new RememberedAccountCandidate(region))
            .ToList();
    }

    private async Task<IReadOnlyList<string>> ReadVisibleAccountTextsAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        return ReadRememberedAccountCandidates()
            .Where(candidate => RememberedAccountChooserLayout.IsMaskedAccountText(candidate.Text))
            .Select(candidate => RememberedAccountMatcher.Normalize(candidate.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool IsCurrentRememberedAccountMatch(string targetAccountLabel)
    {
        return RememberedAccountChooserLayout.IsCurrentAccountMatch(
            ReadRememberedAccountCandidates().Select(static candidate => candidate.Entry),
            targetAccountLabel);
    }

    private async Task EnsureRememberedAccountListExpandedAsync(GameRegion region, string targetAccountLabel, CancellationToken cancellationToken)
    {
        if (IsRememberedAccountListExpanded())
        {
            return;
        }

        _logger.LogInformation("Expanding remembered-account list...");
        var timeoutAt = Stopwatch.StartNew();
        while (timeoutAt.Elapsed < TimeSpan.FromSeconds(8))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsRememberedAccountListExpanded())
            {
                return;
            }

            var anchorCandidate = TryGetChooserAnchorCandidate();
            ClickChooserAnchor(region, anchorCandidate);

            await Task.Delay(region == GameRegion.Global ? 650 : 450, cancellationToken);
        }

        if (IsCurrentRememberedAccountMatch(targetAccountLabel))
        {
            _logger.LogInformation("Remembered-account list did not expand, but the current account already matches {AccountLabel}; continuing.", targetAccountLabel);
            return;
        }

        throw new TimeoutException("Timed out expanding the remembered-account list.");
    }

    private bool IsRememberedAccountListExpanded()
    {
        return RememberedAccountChooserLayout.IsExpanded(
            ReadRememberedAccountCandidates().Select(static candidate => candidate.Entry));
    }

    private RememberedAccountCandidate? TryGetChooserAnchorCandidate()
    {
        var candidates = ReadRememberedAccountCandidates();
        var anchor = RememberedAccountChooserLayout.TryGetChooserAnchor(
            candidates.Select(static candidate => candidate.Entry));

        if (anchor == null)
        {
            return null;
        }

        return candidates.FirstOrDefault(candidate => candidate.Entry == anchor);
    }

    private void ClickChooserAnchor(GameRegion region, RememberedAccountCandidate? anchorCandidate)
    {
        if (region == GameRegion.Global)
        {
            var clickY = anchorCandidate == null
                ? GlobalAccountSelectorArrowFallbackY
                : Math.Clamp(anchorCandidate.Region.Top + Math.Max(anchorCandidate.Region.Height / 2d, 20d), 500d, 565d);

            GameCaptureRegion.GameRegion1080PPosClick(GlobalAccountSelectorArrowFallbackX, clickY);
            return;
        }

        if (anchorCandidate != null)
        {
            anchorCandidate.Region.Click();
            return;
        }

        GameCaptureRegion.GameRegion1080PPosClick(AccountSelectorFallbackX, AccountSelectorFallbackY);
    }

    private async Task ClickChooseEnterGameAsync(CancellationToken cancellationToken)
    {
        var page = new BvPage(cancellationToken);
        var chooseEnterButton = (await page.Locator(GameLoadingAssets.ChooseEnterGameRo).TryWaitFor(2500)).FirstOrDefault();
        if (chooseEnterButton != null)
        {
            chooseEnterButton.Click();
            await Task.Delay(300, cancellationToken);
            return;
        }

        var chooseEnterText = await page.GetByText(EnterGameText, ChooserButtonRoi1080).TryWaitFor(2000);
        if (chooseEnterText.Count > 0)
        {
            chooseEnterText[0].Click();
            await Task.Delay(300, cancellationToken);
            return;
        }

        throw new TimeoutException("Timed out waiting for the chooser Enter Game button.");
    }

    private async Task<bool> TryWaitForLogoutDialogAsync(CancellationToken cancellationToken)
    {
        var timeoutAt = Stopwatch.StartNew();
        while (timeoutAt.Elapsed < TimeSpan.FromSeconds(6))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsLogoutDialogVisibleAsync(cancellationToken))
            {
                return true;
            }

            GameCaptureRegion.GameRegion1080PPosClick(LogoutIconX, LogoutIconY);
            await Task.Delay(550, cancellationToken);
        }

        return false;
    }

    private async Task<bool> TryOpenRememberedAccountChooserAsync(CancellationToken cancellationToken)
    {
        var timeoutAt = Stopwatch.StartNew();
        while (timeoutAt.Elapsed < TimeSpan.FromSeconds(10))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsRememberedAccountChooserVisibleAsync(cancellationToken))
            {
                return true;
            }

            if (await IsLogoutDialogVisibleAsync(cancellationToken))
            {
                await SelectKeepLoginRecordOptionAsync(cancellationToken);
                await ConfirmLogoutAsync(cancellationToken);
                await WaitForRememberedAccountChooserAsync(cancellationToken);
                return true;
            }

            if (await IsSwitchAccountConfirmationVisibleAsync(cancellationToken))
            {
                _logger.LogInformation("Direct switch-account confirmation appeared while opening chooser.");
                await ConfirmVisibleSwitchAccountAsync(cancellationToken);
                await WaitForRememberedAccountChooserAsync(cancellationToken);
                return true;
            }

            GameCaptureRegion.GameRegion1080PPosClick(LogoutIconX, LogoutIconY);
            await Task.Delay(550, cancellationToken);
        }

        return false;
    }

    private async Task SelectKeepLoginRecordOptionAsync(CancellationToken cancellationToken)
    {
        var page = new BvPage(cancellationToken);
        var keepOption = await page.GetByText(KeepLoginRecordText, LogoutDialogRoi1080).TryWaitFor(2500);
        if (keepOption.Count > 0)
        {
            keepOption[0].Click();
            await Task.Delay(250, cancellationToken);
            return;
        }

        _logger.LogWarning("Keep-login-record option OCR did not match; using fallback click.");
        GameCaptureRegion.GameRegion1080PPosClick(KeepLoginRecordFallbackX, KeepLoginRecordFallbackY);
        await Task.Delay(250, cancellationToken);
    }

    private async Task ConfirmLogoutAsync(CancellationToken cancellationToken)
    {
        var page = new BvPage(cancellationToken);
        var buttons = await page.GetByText(LogoutButtonText, LogoutDialogButtonRoi1080).TryWaitFor(2500);
        var confirmButton = buttons
            .OrderByDescending(static region => region.Left)
            .FirstOrDefault();

        if (confirmButton != null)
        {
            confirmButton.Click();
            await Task.Delay(800, cancellationToken);
            return;
        }

        _logger.LogWarning("Logout confirm button OCR did not match; using fallback click.");
        GameCaptureRegion.GameRegion1080PPosClick(ConfirmLogoutFallbackX, ConfirmLogoutFallbackY);
        await Task.Delay(800, cancellationToken);
    }

    private async Task ConfirmSwitchAccountIfNeededAsync(CancellationToken cancellationToken)
    {
        if (!await WaitForSwitchAccountConfirmationAsync(cancellationToken))
        {
            _logger.LogInformation("Switch-account confirmation dialog did not appear; continuing.");
            return;
        }

        _logger.LogInformation("Confirming remembered-account switch...");
        await ConfirmVisibleSwitchAccountAsync(cancellationToken);
    }

    private async Task ConfirmVisibleSwitchAccountAsync(CancellationToken cancellationToken)
    {
        var page = new BvPage(cancellationToken);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var buttons = await page.GetByText(ConfirmButtonText, SwitchAccountConfirmButtonRoi1080).TryWaitFor(800);
            var confirmButton = buttons
                .OrderByDescending(static region => region.Left)
                .FirstOrDefault();

            if (confirmButton != null)
            {
                confirmButton.Click();
            }
            else
            {
                _logger.LogWarning("Switch-account confirm button OCR did not match on attempt {Attempt}; using fallback click.", attempt + 1);
                GameCaptureRegion.GameRegion1080PPosClick(ConfirmSwitchAccountFallbackX, ConfirmSwitchAccountFallbackY);
            }

            await Task.Delay(650, cancellationToken);
            if (!await IsSwitchAccountConfirmationVisibleAsync(cancellationToken))
            {
                return;
            }
        }

        throw new TimeoutException("Timed out confirming the switch-account dialog.");
    }

    private async Task WaitForRememberedAccountChooserAsync(CancellationToken cancellationToken)
    {
        var timeoutAt = Stopwatch.StartNew();
        while (timeoutAt.Elapsed < TimeSpan.FromSeconds(8))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await IsRememberedAccountChooserVisibleAsync(cancellationToken))
            {
                return;
            }

            await Task.Delay(300, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for the remembered-account chooser.");
    }

    private async Task<bool> IsRememberedAccountChooserVisibleAsync(CancellationToken cancellationToken)
    {
        var page = new BvPage(cancellationToken);
        var chooseEnter = (await page.Locator(GameLoadingAssets.ChooseEnterGameRo).TryWaitFor(500)).Count > 0;
        if (chooseEnter)
        {
            return true;
        }

        var chooseEnterText = await page.GetByText(EnterGameText, ChooserButtonRoi1080).TryWaitFor(500);
        if (chooseEnterText.Count > 0)
        {
            return true;
        }

        var otherAccount = await page.GetByText(OtherAccountText, RememberedAccountDialogRoi1080).TryWaitFor(500);
        if (otherAccount.Count > 0)
        {
            return true;
        }

        var accountEntries = ReadRememberedAccountCandidates();
        return accountEntries.Any(candidate => RememberedAccountChooserLayout.IsMaskedAccountText(candidate.Text));
    }

    private async Task<bool> IsLogoutDialogVisibleAsync(CancellationToken cancellationToken)
    {
        var page = new BvPage(cancellationToken);

        var title = await page.GetByText(LogoutDialogTitleText, LogoutDialogRoi1080).TryWaitFor(400);
        if (title.Count > 0)
        {
            return true;
        }

        var keepOption = await page.GetByText(KeepLoginRecordText, LogoutDialogRoi1080).TryWaitFor(400);
        if (keepOption.Count > 0)
        {
            return true;
        }

        var buttons = await page.GetByText(LogoutButtonText, LogoutDialogButtonRoi1080).TryWaitFor(400);
        return buttons.Count > 0;
    }

    private async Task<bool> IsSwitchAccountConfirmationVisibleAsync(CancellationToken cancellationToken)
    {
        var page = new BvPage(cancellationToken);

        var title = await page.GetByText(SwitchAccountConfirmTitleText, SwitchAccountConfirmDialogRoi1080).TryWaitFor(600);
        if (title.Count > 0)
        {
            return true;
        }

        var fallbackTitle = await page.GetByText(SwitchAccountConfirmTitleFallbackText, SwitchAccountConfirmDialogRoi1080).TryWaitFor(400);
        if (fallbackTitle.Count > 0)
        {
            return true;
        }

        var buttons = await page.GetByText(ConfirmButtonText, SwitchAccountConfirmButtonRoi1080).TryWaitFor(400);
        return buttons.Count > 0;
    }

    private async Task<bool> WaitForSwitchAccountConfirmationAsync(CancellationToken cancellationToken)
    {
        var timeoutAt = Stopwatch.StartNew();
        while (timeoutAt.Elapsed < TimeSpan.FromSeconds(4))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await IsSwitchAccountConfirmationVisibleAsync(cancellationToken))
            {
                return true;
            }

            await Task.Delay(250, cancellationToken);
        }

        return false;
    }
}
