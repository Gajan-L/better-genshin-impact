using BetterGenshinImpact.GameTask;
using BetterGenshinImpact.Modules.MultiAccount;
using BetterGenshinImpact.Modules.MultiAccount.LoginFlow;
using BetterGenshinImpact.Modules.MultiAccount.Runner;
using BetterGenshinImpact.UnitTest;

namespace BetterGenshinImpact.UnitTest.MultiAccountTests;

public class AccountBatchOrchestratorTests
{
    [Fact]
    public async Task RunBatchAsync_CompletesHappyPathAndWritesLogs()
    {
        using var tempDirectory = new TemporaryDirectory();
        var loginFlow = new FakeLoginFlow();
        var runner = new FakeOneDragonRunner();
        var gameSessionController = new FakeGameSessionController();
        var oneDragonConfigCatalog = CreateOneDragonConfigCatalog(tempDirectory.Path, "dragon-one");
        var orchestrator = new AccountBatchOrchestrator(
            runner,
            new FakeLoginFlowFactory(loginFlow),
            gameSessionController,
            oneDragonConfigCatalog,
            new FakeLogger<AccountBatchOrchestrator>(),
            Path.Combine(tempDirectory.Path, "logs"));

        var profile = CreateProfile("account-one", 0);

        var records = await orchestrator.RunBatchAsync([profile]);

        Assert.Single(records);
        Assert.True(records[0].Success);
        Assert.Equal(["account-one"], loginFlow.EnteredProfileIds);
        Assert.Equal(["dragon-one"], runner.ConfigNames);
        Assert.Equal(["account-one"], gameSessionController.EnsureClosedProfileIds);
        Assert.Equal(["account-one"], gameSessionController.ClosedProfileIds);
        Assert.Null(TaskContext.Instance().CurrentLaunchContext);
        Assert.Single(Directory.GetFiles(Path.Combine(tempDirectory.Path, "logs"), "*.jsonl"));
    }

    [Fact]
    public async Task RunBatchAsync_FailsFastWhenOneDragonConfigFileIsMissing()
    {
        using var tempDirectory = new TemporaryDirectory();
        var loginFlow = new FakeLoginFlow();
        var runner = new FakeOneDragonRunner();
        var gameSessionController = new FakeGameSessionController();
        var orchestrator = new AccountBatchOrchestrator(
            runner,
            new FakeLoginFlowFactory(loginFlow),
            gameSessionController,
            new OneDragonConfigCatalog(Path.Combine(tempDirectory.Path, "OneDragon")),
            new FakeLogger<AccountBatchOrchestrator>(),
            Path.Combine(tempDirectory.Path, "logs"));

        var records = await orchestrator.RunBatchAsync([CreateProfile("account-one", 0)]);

        Assert.Single(records);
        Assert.False(records[0].Success);
        Assert.Contains("OneDragon config does not exist", records[0].Message);
        Assert.Empty(loginFlow.EnteredProfileIds);
        Assert.Empty(runner.ConfigNames);
    }

    [Fact]
    public async Task RunBatchAsync_StopsAfterLoginFailure()
    {
        using var tempDirectory = new TemporaryDirectory();
        var loginFlow = new FakeLoginFlow
        {
            ExceptionToThrow = new InvalidOperationException("launch failed"),
        };
        var runner = new FakeOneDragonRunner();
        var gameSessionController = new FakeGameSessionController();
        var oneDragonConfigCatalog = CreateOneDragonConfigCatalog(tempDirectory.Path, "dragon-one");
        var orchestrator = new AccountBatchOrchestrator(
            runner,
            new FakeLoginFlowFactory(loginFlow),
            gameSessionController,
            oneDragonConfigCatalog,
            new FakeLogger<AccountBatchOrchestrator>(),
            Path.Combine(tempDirectory.Path, "logs"));

        var records = await orchestrator.RunBatchAsync(
        [
            CreateProfile("account-one", 0),
            CreateProfile("account-two", 1),
        ]);

        Assert.Single(records);
        Assert.False(records[0].Success);
        Assert.Equal(["account-one"], loginFlow.EnteredProfileIds);
        Assert.Empty(runner.ConfigNames);
        Assert.Equal(["account-one"], gameSessionController.EnsureClosedProfileIds);
        Assert.Empty(gameSessionController.ClosedProfileIds);
    }

    [Fact]
    public async Task RunBatchAsync_StopsAfterRunnerFailure()
    {
        using var tempDirectory = new TemporaryDirectory();
        var loginFlow = new FakeLoginFlow();
        var runner = new FakeOneDragonRunner
        {
            ExceptionToThrow = new InvalidOperationException("runner failed"),
        };
        var gameSessionController = new FakeGameSessionController();
        var oneDragonConfigCatalog = CreateOneDragonConfigCatalog(tempDirectory.Path, "dragon-one");
        var orchestrator = new AccountBatchOrchestrator(
            runner,
            new FakeLoginFlowFactory(loginFlow),
            gameSessionController,
            oneDragonConfigCatalog,
            new FakeLogger<AccountBatchOrchestrator>(),
            Path.Combine(tempDirectory.Path, "logs"));

        var records = await orchestrator.RunBatchAsync(
        [
            CreateProfile("account-one", 0),
            CreateProfile("account-two", 1),
        ]);

        Assert.Single(records);
        Assert.False(records[0].Success);
        Assert.Equal(["account-one"], loginFlow.EnteredProfileIds);
        Assert.Equal(["dragon-one"], runner.ConfigNames);
        Assert.Equal(["account-one"], gameSessionController.EnsureClosedProfileIds);
        Assert.Empty(gameSessionController.ClosedProfileIds);
    }

    [Fact]
    public async Task RunBatchAsync_FailsFastWhenRememberedAccountLabelIsMissing()
    {
        using var tempDirectory = new TemporaryDirectory();
        var loginFlow = new FakeLoginFlow();
        var runner = new FakeOneDragonRunner();
        var gameSessionController = new FakeGameSessionController();
        var oneDragonConfigCatalog = CreateOneDragonConfigCatalog(tempDirectory.Path, "dragon-one");
        var orchestrator = new AccountBatchOrchestrator(
            runner,
            new FakeLoginFlowFactory(loginFlow),
            gameSessionController,
            oneDragonConfigCatalog,
            new FakeLogger<AccountBatchOrchestrator>(),
            Path.Combine(tempDirectory.Path, "logs"));

        var profile = CreateProfile("account-one", 0);
        profile.RememberedAccountLabel = string.Empty;

        var records = await orchestrator.RunBatchAsync([profile]);

        Assert.Single(records);
        Assert.False(records[0].Success);
        Assert.Contains("Remembered account label is missing", records[0].Message);
        Assert.Empty(loginFlow.EnteredProfileIds);
    }

    [Fact]
    public async Task RunBatchAsync_UsesProvidedProfileOrder()
    {
        using var tempDirectory = new TemporaryDirectory();
        var loginFlow = new FakeLoginFlow();
        var runner = new FakeOneDragonRunner();
        var gameSessionController = new FakeGameSessionController();
        var oneDragonConfigCatalog = CreateOneDragonConfigCatalog(tempDirectory.Path, "dragon-one");
        var orchestrator = new AccountBatchOrchestrator(
            runner,
            new FakeLoginFlowFactory(loginFlow),
            gameSessionController,
            oneDragonConfigCatalog,
            new FakeLogger<AccountBatchOrchestrator>(),
            Path.Combine(tempDirectory.Path, "logs"));

        var secondProfile = CreateProfile("account-two", 1);
        var firstProfile = CreateProfile("account-one", 0);

        var records = await orchestrator.RunBatchAsync([secondProfile, firstProfile]);

        Assert.Equal(["account-two", "account-one"], loginFlow.EnteredProfileIds);
        Assert.Equal(["account-two", "account-one"], records.Select(record => record.ProfileId));
    }

    private static OneDragonConfigCatalog CreateOneDragonConfigCatalog(string root, params string[] configNames)
    {
        var configDirectory = Path.Combine(root, "OneDragon");
        Directory.CreateDirectory(configDirectory);

        foreach (var configName in configNames)
        {
            File.WriteAllText(Path.Combine(configDirectory, $"{configName}.json"), "{}");
        }

        return new OneDragonConfigCatalog(configDirectory);
    }

    private static MultiAccountProfile CreateProfile(string id, int order)
    {
        return new MultiAccountProfile
        {
            Id = id,
            Name = id,
            Order = order,
            Region = GameRegion.CNOfficial,
            InstallPath = $@"D:\Games\{id}.exe",
            OneDragonConfigName = "dragon-one",
            RememberedAccountLabel = "175******25",
        };
    }

    private sealed class FakeLoginFlow : IGameLoginFlow
    {
        public GameRegion Region => GameRegion.CNOfficial;

        public List<string> EnteredProfileIds { get; } = [];

        public Exception? ExceptionToThrow { get; init; }

        public Task EnterGameAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default)
        {
            EnteredProfileIds.Add(profile.Id);
            return ExceptionToThrow == null
                ? Task.CompletedTask
                : Task.FromException(ExceptionToThrow);
        }
    }

    private sealed class FakeLoginFlowFactory(FakeLoginFlow loginFlow) : IGameLoginFlowFactory
    {
        public IGameLoginFlow Create(GameRegion region)
        {
            return loginFlow;
        }
    }

    private sealed class FakeOneDragonRunner : IOneDragonRunner
    {
        public List<string> ConfigNames { get; } = [];

        public Exception? ExceptionToThrow { get; init; }

        public Task RunAsync(string configName, bool ensureGameStarted = true, CancellationToken cancellationToken = default)
        {
            ConfigNames.Add(configName);
            return ExceptionToThrow == null
                ? Task.CompletedTask
                : Task.FromException(ExceptionToThrow);
        }
    }

    private sealed class FakeGameSessionController : IGameSessionController
    {
        public List<string> EnsureClosedProfileIds { get; } = [];

        public List<string> ClosedProfileIds { get; } = [];

        public Task EnsureGameClosedAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default)
        {
            EnsureClosedProfileIds.Add(profile.Id);
            return Task.CompletedTask;
        }

        public Task CloseGameAsync(MultiAccountProfile profile, CancellationToken cancellationToken = default)
        {
            ClosedProfileIds.Add(profile.Id);
            return Task.CompletedTask;
        }
    }
}
