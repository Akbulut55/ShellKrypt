using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Ports;
using ShellKrypt.Desktop.Features.ActivityLogs;
using ShellKrypt.Desktop.Shell.Dialogs;
using ShellKrypt.Desktop.Shell.Runtime;
using ShellKrypt.Infrastructure.Crypto;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class ActivityLogsRefactorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ContentChecksum_IsCanonicalAndExcludesVaultPath()
    {
        var entry = Entry("one", Now, "audit", "Reviewed", "Safe detail", "info", "Item");
        var first = ActivityContentChecksum.Compute(entry);
        var second = ActivityContentChecksum.Compute(entry with { VaultPath = "/different/location.skvault" });
        ActivityLogEntry[] changedEntries =
        [
            entry with { Id = "two" },
            entry with { TimestampUtc = Now.AddSeconds(1).ToString("O") },
            entry with { Category = "vault" },
            entry with { Title = "Changed title" },
            entry with { Detail = "Changed detail" },
            entry with { Severity = "warning" },
            entry with { AffectedItem = "Changed item" }
        ];

        Assert.Equal(first, second);
        Assert.All(changedEntries, changed => Assert.NotEqual(first, ActivityContentChecksum.Compute(changed)));
        Assert.Equal(64, first.Length);
        Assert.Equal(first.ToLowerInvariant(), first);
        Assert.Matches("^[0-9a-f]{64}$", first);

        var nullAffectedItem = ActivityContentChecksum.Compute(entry with { AffectedItem = null });
        var emptyAffectedItem = ActivityContentChecksum.Compute(entry with { AffectedItem = string.Empty });
        Assert.Equal(nullAffectedItem, emptyAffectedItem);
    }

    [Fact]
    public void ActivityLogService_SanitizesEveryUserFacingTextField()
    {
        var store = new CapturingStore();
        var service = new ActivityLogService(store);
        var result = service.Append(Entry("one", Now, "test", "token=title-secret", "password=detail-secret", "warning", "api_key=affected-secret"), new byte[32]);

        Assert.True(result.Success);
        Assert.NotNull(store.Appended);
        Assert.DoesNotContain("title-secret", store.Appended.Title);
        Assert.DoesNotContain("detail-secret", store.Appended.Detail);
        Assert.DoesNotContain("affected-secret", store.Appended.AffectedItem);
    }

    [Fact]
    public void ListViewModel_CombinesInvestigationFiltersAndPreservesSelection()
    {
        var localization = new LocalizationService();
        var list = new ActivityLogListViewModel(localization, new FixedTimeProvider(Now));
        var recentWarning = Entry("recent", Now.AddDays(-2), "audit", "Recent warning", "matched detail", "warning", "Audit");
        var oldWarning = Entry("old", Now.AddDays(-40), "audit", "Old warning", "matched detail", "warning", "Audit");
        var recentInfo = Entry("info", Now.AddDays(-1), "vault", "Recent info", "other", "info", "Vault");
        list.Load([recentInfo, oldWarning, recentWarning]);
        list.SelectedItem = list.Items.Single(item => item.Id == "recent");

        list.ShowAuditCommand.Execute(null);
        list.SelectedSeverityOption = list.SeverityOptions.Single(option => option.Id == "warning");
        list.SelectedDateRangeOption = list.DateRangeOptions.Single(option => option.Id == "last30");
        list.SearchText = "matched";
        list.SelectedSortOption = list.SortOptions.Single(option => option.Id == "oldest");

        var visible = Assert.Single(list.Items);
        Assert.Equal("recent", visible.Id);
        Assert.Equal("recent", list.SelectedItem?.Id);
        Assert.True(list.HasNarrowingFilter);
        Assert.Equal(3, list.TotalEvents);
        Assert.Equal(1, list.FilteredEventCount);
    }

    [Fact]
    public void ListViewModel_DatePresetsUseInclusiveLocalCalendarBoundaries()
    {
        var localZone = TimeZoneInfo.CreateCustomTimeZone("ActivityTests+03", TimeSpan.FromHours(3), "ActivityTests+03", "ActivityTests+03");
        var list = new ActivityLogListViewModel(new LocalizationService(), new FixedTimeProvider(Now, localZone));
        var todayStart = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.FromHours(3));
        var sevenDayStart = todayStart.AddDays(-6);
        var thirtyDayStart = todayStart.AddDays(-29);
        list.Load(
        [
            Entry("today-start", todayStart, "audit", "Today", "Safe", "info", "Audit"),
            Entry("before-today", todayStart.AddTicks(-1), "audit", "Yesterday", "Safe", "info", "Audit"),
            Entry("seven-start", sevenDayStart, "audit", "Seven", "Safe", "info", "Audit"),
            Entry("before-seven", sevenDayStart.AddTicks(-1), "audit", "Before seven", "Safe", "info", "Audit"),
            Entry("thirty-start", thirtyDayStart, "audit", "Thirty", "Safe", "info", "Audit"),
            Entry("before-thirty", thirtyDayStart.AddTicks(-1), "audit", "Before thirty", "Safe", "info", "Audit"),
            Entry("future", Now.AddSeconds(1), "audit", "Future", "Safe", "info", "Audit")
        ]);

        list.SelectedDateRangeOption = list.DateRangeOptions.Single(option => option.Id == "today");
        Assert.Equal(["today-start"], list.Items.Select(item => item.Id));

        list.SelectedDateRangeOption = list.DateRangeOptions.Single(option => option.Id == "last7");
        Assert.Equal(["today-start", "before-today", "seven-start"], list.Items.Select(item => item.Id));

        list.SelectedDateRangeOption = list.DateRangeOptions.Single(option => option.Id == "last30");
        Assert.Equal(["today-start", "before-today", "seven-start", "before-seven", "thirty-start"], list.Items.Select(item => item.Id));
    }

    [Fact]
    public void ActivityItem_RelativeTimestampUsesInjectedClockAndHandlesFutureValues()
    {
        var localization = new LocalizationService();
        var clock = new FixedTimeProvider(Now);

        string Display(DateTimeOffset timestamp)
            => new ActivityItemVm(Entry(Guid.NewGuid().ToString("N"), timestamp, "audit", "Event", "Safe", "info", "Audit"), localization, clock).TimestampDisplay;

        Assert.Equal(localization.Get("Activity.Time.JustNow"), Display(Now.AddSeconds(-30)));
        Assert.Equal(localization.Get("Activity.Time.MinutesAgo", 5), Display(Now.AddMinutes(-5)));
        Assert.Equal(localization.Get("Activity.Time.HoursAgo", 2), Display(Now.AddHours(-2)));
        Assert.Equal(localization.Get("Activity.Time.DaysAgo", 2), Display(Now.AddDays(-2)));
        Assert.Equal("Jul 12, 2026 | 12:00", Display(Now.AddDays(-8)));
        Assert.Equal("Jul 20, 2026 | 13:00", Display(Now.AddHours(1)));
    }

    [Fact]
    public void Report_DescribesScopeFiltersAndNonAuthenticatingChecksumWithoutSearchText()
    {
        var localization = new LocalizationService();
        var row = new ActivityItemVm(Entry("one", Now, "audit", "Reviewed", "needle detail", "info", "Audit"), localization, new FixedTimeProvider(Now));
        var service = new ActivityReportService(new FixedTimeProvider(Now));

        var json = service.BuildJson("Filtered", "Synthetic", [row], 4, new("audit", "info", "last7", "oldest", true));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("Filtered", root.GetProperty("ExportScope").GetString());
        Assert.Equal(4, root.GetProperty("SourceTotalEvents").GetInt32());
        Assert.Equal(1, root.GetProperty("TotalEvents").GetInt32());
        Assert.True(root.GetProperty("AppliedFilters").GetProperty("SearchApplied").GetBoolean());
        Assert.Contains("does not prove", root.GetProperty("ChecksumNotice").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(root.GetProperty("Events")[0].TryGetProperty("ContentChecksum", out _));
        Assert.DoesNotContain("IntegrityHash", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchText", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ActivityWorkspace_SubscriptionFollowsActivationAndShowsCorruptCount()
    {
        var recorder = new TestRecorder();
        var store = new CapturingStore { LoadResult = new([Entry("one", Now, "audit", "Reviewed", "Safe", "info", "Audit")], 2, ActivityLogFailureKind.None) };
        var model = CreateWorkspace(recorder, store);

        model.Activate();
        model.Activate();
        Assert.Equal(1, recorder.SubscriptionCount);
        Assert.True(model.HasCorruptionWarning);
        Assert.Equal(2, model.SkippedCorruptEntries);

        model.Deactivate();
        Assert.Equal(0, recorder.SubscriptionCount);
    }

    [Fact]
    public void ActivityWorkspace_ShowsSafeRecorderFailure()
    {
        var recorder = new TestRecorder();
        var model = CreateWorkspace(recorder, new CapturingStore());
        model.Activate();

        recorder.Emit(new(ActivityLogFailureKind.WriteFailed));

        Assert.True(model.Management.HasError);
        Assert.DoesNotContain("/", model.Management.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ActivityRecorder_EmitsTypedSuccessAndFailureResults()
    {
        var store = new CapturingStore();
        var session = new TestSession();
        var recorder = new ActivityRecorder(new ActivityLogService(store), session);
        var results = new List<ActivityLogOperationResult>();
        recorder.Changed += (_, args) => results.Add(args.Result);

        recorder.Log("audit", "Recorded", "Safe detail");
        store.AppendResult = new(ActivityLogFailureKind.WriteFailed);
        recorder.Log("audit", "Failed", "Safe detail");
        store.AppendResult = new(ActivityLogFailureKind.Unavailable);
        session.IsUnlocked = false;
        recorder.Log("audit", "Unavailable", "Safe detail");

        Assert.Equal(
            [
                ActivityLogOperationResult.Succeeded,
                new(ActivityLogFailureKind.WriteFailed),
                new(ActivityLogFailureKind.Unavailable)
            ],
            results);
    }

    [Fact]
    public void ActivityWorkspace_ClearsWarningsAndDisplayedEntriesOnSubsequentReloads()
    {
        var store = new CapturingStore
        {
            LoadResult = new([Entry("one", Now, "audit", "Reviewed", "Safe", "info", "Audit")], 2, ActivityLogFailureKind.None)
        };
        var model = CreateWorkspace(new TestRecorder(), store);
        model.Activate();
        Assert.True(model.HasCorruptionWarning);
        Assert.Single(model.List.Items);

        store.LoadResult = new([Entry("two", Now, "vault", "Clean", "Safe", "success", "Vault")], 0, ActivityLogFailureKind.None);
        model.RefreshCommand.Execute(null);
        Assert.False(model.HasCorruptionWarning);
        Assert.Equal("two", Assert.Single(model.List.Items).Id);

        store.LoadResult = new([], 0, ActivityLogFailureKind.ReadFailed);
        model.RefreshCommand.Execute(null);
        Assert.True(model.HasLoadFailure);
        Assert.Empty(model.List.Items);
        Assert.Null(model.List.SelectedItem);
    }

    [Fact]
    public void ActivityPreviewData_ProvidesEverySyntheticState()
    {
        Assert.Empty(ActivityViewDesignData.CreateEmpty().List.Items);

        var populated = ActivityViewDesignData.CreatePopulated();
        Assert.NotEmpty(populated.List.Items);
        Assert.Null(populated.List.SelectedItem);

        Assert.NotNull(ActivityViewDesignData.CreateSelected().List.SelectedItem);

        var filtered = ActivityViewDesignData.CreateFiltered();
        Assert.True(filtered.List.HasNarrowingFilter);
        Assert.InRange(filtered.List.FilteredEventCount, 1, filtered.List.TotalEvents - 1);

        Assert.True(ActivityViewDesignData.CreateWarning().HasCorruptionWarning);
        Assert.True(ActivityViewDesignData.CreateFailure().HasLoadFailure);
    }

    [Fact]
    public async Task SqliteStore_ReportsCorruptRowsWithoutHidingReadableEntries()
    {
        using var workspace = new TempWorkspace();
        var vaultPath = workspace.FilePath("activity-corruption.skvault");
        var vaultService = new SqliteVaultService();
        await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");
        var unlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");
        Assert.True(unlock.Success, unlock.Error);
        var service = new ActivityLogService(new SqliteActivityLogStore());
        Assert.True(service.Append(Entry("readable", Now, "audit", "Readable", "Safe", "info", "Audit") with { VaultPath = vaultPath }, unlock.VaultKey).Success);
        Assert.True(service.Append(Entry("corrupt", Now.AddMinutes(-1), "audit", "Corrupt", "Safe", "info", "Audit") with { VaultPath = vaultPath }, unlock.VaultKey).Success);

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = vaultPath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE activity_logs SET encryptedPayload = $payload WHERE id = 'corrupt';";
        command.Parameters.Add("$payload", SqliteType.Blob).Value = RandomNumberGenerator.GetBytes(64);
        await command.ExecuteNonQueryAsync();

        var result = service.Load(vaultPath, unlock.VaultKey);
        Assert.True(result.Success);
        Assert.Equal(1, result.SkippedCorruptEntries);
        Assert.Equal("readable", Assert.Single(result.Entries).Id);
    }

    [Fact]
    public async Task SqliteStore_CountsAuthenticatedNullPayloadAsCorrupt()
    {
        using var workspace = new TempWorkspace();
        var vaultPath = workspace.FilePath("activity-null-payload.skvault");
        var vaultService = new SqliteVaultService();
        await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");
        var unlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");
        Assert.True(unlock.Success, unlock.Error);
        var service = new ActivityLogService(new SqliteActivityLogStore());
        Assert.True(service.Append(Entry("readable", Now, "audit", "Readable", "Safe", "info", "Audit") with { VaultPath = vaultPath }, unlock.VaultKey).Success);

        const string id = "null-payload";
        var timestamp = Now.AddMinutes(-1).ToString("O");
        var encryptedPayload = AesGcmBlob.Encrypt(
            unlock.VaultKey!,
            Encoding.UTF8.GetBytes("null"),
            AesGcmBlob.CreateAssociatedData("activity-log", "v2", id, timestamp));
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = vaultPath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO activity_logs (id, timestampUtc, encryptedPayload) VALUES ($id, $timestamp, $payload);";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$timestamp", timestamp);
        command.Parameters.Add("$payload", SqliteType.Blob).Value = encryptedPayload;
        await command.ExecuteNonQueryAsync();

        var result = service.Load(vaultPath, unlock.VaultKey);
        Assert.True(result.Success);
        Assert.Equal(1, result.SkippedCorruptEntries);
        Assert.Equal("readable", Assert.Single(result.Entries).Id);
    }

    [Fact]
    public async Task SqliteStore_RetainsOnlyTheNewestFourHundredEntries()
    {
        using var workspace = new TempWorkspace();
        var vaultPath = workspace.FilePath("activity-retention.skvault");
        var vaultService = new SqliteVaultService();
        await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");
        var unlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");
        Assert.True(unlock.Success, unlock.Error);
        var service = new ActivityLogService(new SqliteActivityLogStore());

        for (var index = 0; index < 401; index++)
        {
            var entry = Entry($"event-{index:D3}", Now.AddMinutes(index), "audit", $"Event {index}", "Safe", "info", "Audit") with { VaultPath = vaultPath };
            Assert.True(service.Append(entry, unlock.VaultKey).Success);
        }

        var result = service.Load(vaultPath, unlock.VaultKey);
        Assert.True(result.Success);
        Assert.Equal(400, result.Entries.Count);
        Assert.DoesNotContain(result.Entries, entry => entry.Id == "event-000");
        Assert.Contains(result.Entries, entry => entry.Id == "event-400");
    }

    [Fact]
    public void SqliteStore_ReturnsSafeFailureKinds()
    {
        var store = new SqliteActivityLogStore();
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.skvault");
        var key = new byte[32];

        Assert.Equal(ActivityLogFailureKind.Unavailable, store.Load(null, key).FailureKind);
        Assert.Equal(ActivityLogFailureKind.Unavailable, store.Load(missingPath, null).FailureKind);
        Assert.Equal(ActivityLogFailureKind.ReadFailed, store.Load(missingPath, key).FailureKind);
        Assert.Equal(ActivityLogFailureKind.WriteFailed, store.Append(Entry("one", Now, "audit", "Write", "Safe", "info", "Audit") with { VaultPath = missingPath }, key).FailureKind);
        Assert.Equal(ActivityLogFailureKind.ClearFailed, store.Clear(missingPath, key).FailureKind);
    }

    [Fact]
    public async Task SqliteStore_ClearOnlyDeletesTheSelectedVaultActivity()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var firstPath = workspace.FilePath("first.skvault");
        var secondPath = workspace.FilePath("second.skvault");
        await vaultService.CreateAsync(firstPath, "First Vault Master Passphrase 2026");
        await vaultService.CreateAsync(secondPath, "Second Vault Master Passphrase 2026");
        var first = await vaultService.UnlockAsync(firstPath, "First Vault Master Passphrase 2026");
        var second = await vaultService.UnlockAsync(secondPath, "Second Vault Master Passphrase 2026");
        Assert.True(first.Success, first.Error);
        Assert.True(second.Success, second.Error);
        var service = new ActivityLogService(new SqliteActivityLogStore());
        Assert.True(service.Append(Entry("first", Now, "audit", "First", "Safe", "info", "Audit") with { VaultPath = firstPath }, first.VaultKey).Success);
        Assert.True(service.Append(Entry("second", Now, "audit", "Second", "Safe", "info", "Audit") with { VaultPath = secondPath }, second.VaultKey).Success);

        Assert.True(service.Clear(firstPath, first.VaultKey).Success);

        Assert.Empty(service.Load(firstPath, first.VaultKey).Entries);
        Assert.Equal("second", Assert.Single(service.Load(secondPath, second.VaultKey).Entries).Id);
    }

    [Fact]
    public async Task Management_ClearIsConfirmationGatedAndRecordsSuccessfulClear()
    {
        var recorder = new TestRecorder();
        var store = new CapturingStore { LoadResult = new([Entry("one", Now, "audit", "Reviewed", "Safe", "info", "Audit")], 0, ActivityLogFailureKind.None) };
        var dialogs = new TestDialogs();
        var model = CreateWorkspace(recorder, store, dialogs);
        model.Activate();

        await model.Management.ClearCommand.ExecuteAsync(null);
        Assert.Equal(0, store.ClearCalls);

        dialogs.ConfirmDangerous = true;
        await model.Management.ClearCommand.ExecuteAsync(null);
        Assert.Equal(1, store.ClearCalls);
        Assert.Equal(1, recorder.LogCalls);
    }

    [Fact]
    public async Task Management_ClearFailureIsSafeAndDoesNotRecordSuccess()
    {
        var recorder = new TestRecorder();
        var store = new CapturingStore
        {
            LoadResult = new([Entry("one", Now, "audit", "Reviewed", "Safe", "info", "Audit")], 0, ActivityLogFailureKind.None),
            ClearResult = new(ActivityLogFailureKind.ClearFailed)
        };
        var model = CreateWorkspace(recorder, store, new TestDialogs { ConfirmDangerous = true });
        model.Activate();

        await model.Management.ClearCommand.ExecuteAsync(null);

        Assert.True(model.Management.HasError);
        Assert.Single(model.List.Items);
        Assert.Equal(0, recorder.LogCalls);
    }

    [Fact]
    public async Task Management_ExportFilteredRequiresNarrowingFilterAndWritesScopedReport()
    {
        using var workspace = new TempWorkspace();
        var recorder = new TestRecorder();
        var store = new CapturingStore
        {
            LoadResult = new(
                [
                    Entry("audit", Now, "audit", "Audit event", "Safe", "info", "Audit"),
                    Entry("vault", Now.AddMinutes(-1), "vault", "Vault event", "Safe", "success", "Vault")
                ],
                0,
                ActivityLogFailureKind.None)
        };
        var output = workspace.FilePath("filtered.json");
        var dialogs = new TestDialogs { SavePath = output };
        var model = CreateWorkspace(recorder, store, dialogs);
        model.Activate();
        Assert.False(model.Management.CanExportFiltered);

        model.List.SelectedSortOption = model.List.SortOptions.Single(option => option.Id == "oldest");
        Assert.False(model.Management.CanExportFiltered);

        model.List.ShowAuditCommand.Execute(null);
        Assert.True(model.Management.CanExportFiltered);
        await model.Management.ExportFilteredCommand.ExecuteAsync(null);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(output));
        Assert.Equal("Filtered", document.RootElement.GetProperty("ExportScope").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("SourceTotalEvents").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("TotalEvents").GetInt32());
        Assert.Equal(1, recorder.LogCalls);
    }

    [Fact]
    public async Task Management_ExportAllIgnoresNarrowingFiltersAndUsesSelectedSort()
    {
        using var workspace = new TempWorkspace();
        var recorder = new TestRecorder();
        var store = new CapturingStore
        {
            LoadResult = new(
                [
                    Entry("newer-audit", Now, "audit", "Audit event", "Safe", "info", "Audit"),
                    Entry("older-vault", Now.AddMinutes(-1), "vault", "Vault event", "Safe", "success", "Vault")
                ],
                0,
                ActivityLogFailureKind.None)
        };
        var output = workspace.FilePath("all.json");
        var model = CreateWorkspace(recorder, store, new TestDialogs { SavePath = output });
        model.Activate();
        model.List.SelectedSortOption = model.List.SortOptions.Single(option => option.Id == "oldest");
        model.List.ShowAuditCommand.Execute(null);

        await model.Management.ExportAllCommand.ExecuteAsync(null);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(output));
        var root = document.RootElement;
        Assert.Equal("All", root.GetProperty("ExportScope").GetString());
        Assert.Equal(2, root.GetProperty("TotalEvents").GetInt32());
        Assert.Equal("all", root.GetProperty("AppliedFilters").GetProperty("Category").GetString());
        Assert.Equal("oldest", root.GetProperty("AppliedFilters").GetProperty("Sort").GetString());
        Assert.Equal(["older-vault", "newer-audit"], root.GetProperty("Events").EnumerateArray().Select(item => item.GetProperty("Id").GetString()));
        Assert.Equal(1, recorder.LogCalls);
    }

    [Fact]
    public async Task Management_ExportFilteredSnapshotsEventsMetadataAndVaultBeforePickerCompletes()
    {
        using var workspace = new TempWorkspace();
        var output = workspace.FilePath("snapshot.json");
        var recorder = new TestRecorder();
        var store = new CapturingStore
        {
            LoadResult = new(
                [
                    Entry("audit-original", Now, "audit", "Original audit", "Safe", "info", "Audit"),
                    Entry("vault-original", Now.AddMinutes(-1), "vault", "Original vault", "Safe", "success", "Vault")
                ],
                0,
                ActivityLogFailureKind.None)
        };
        var dialogs = new TestDialogs
        {
            SavePathCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var model = CreateWorkspace(recorder, store, dialogs);
        model.Activate();
        model.List.ShowAuditCommand.Execute(null);

        var exportTask = model.Management.ExportFilteredCommand.ExecuteAsync(null);
        Assert.True(model.Management.IsBusy);

        store.LoadResult = new(
            [
                Entry("audit-original", Now, "audit", "Original audit", "Safe", "info", "Audit"),
                Entry("vault-original", Now.AddMinutes(-1), "vault", "Original vault", "Safe", "success", "Vault"),
                Entry("audit-new", Now.AddMinutes(1), "audit", "New audit", "Safe", "warning", "Audit")
            ],
            0,
            ActivityLogFailureKind.None);
        recorder.Emit(ActivityLogOperationResult.Succeeded);
        model.List.ShowVaultCommand.Execute(null);
        dialogs.SavePathCompletion.SetResult(output);
        await exportTask;

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(output));
        var root = document.RootElement;
        Assert.Equal("Test", root.GetProperty("Vault").GetString());
        Assert.Equal(2, root.GetProperty("SourceTotalEvents").GetInt32());
        Assert.Equal(1, root.GetProperty("TotalEvents").GetInt32());
        Assert.Equal("audit", root.GetProperty("AppliedFilters").GetProperty("Category").GetString());
        Assert.Equal("audit-original", root.GetProperty("Events")[0].GetProperty("Id").GetString());
    }

    [Fact]
    public async Task Management_ExportFailureShowsOnlySafeLocalizedError()
    {
        using var workspace = new TempWorkspace();
        var directoryPath = workspace.FilePath("directory-target");
        Directory.CreateDirectory(directoryPath);
        var recorder = new TestRecorder();
        var store = new CapturingStore { LoadResult = new([Entry("one", Now, "audit", "Reviewed", "Safe", "info", "Audit")], 0, ActivityLogFailureKind.None) };
        var model = CreateWorkspace(recorder, store, new TestDialogs { SavePath = directoryPath });
        model.Activate();

        await model.Management.ExportAllCommand.ExecuteAsync(null);

        Assert.True(model.Management.HasError);
        Assert.DoesNotContain(directoryPath, model.Management.Error, StringComparison.Ordinal);
        Assert.Equal(0, recorder.LogCalls);
    }

    [Fact]
    public void ActivityWorkspace_RefreshesLocalizedRowAndFilterPresentation()
    {
        var localization = new LocalizationService();
        var store = new CapturingStore { LoadResult = new([Entry("one", Now, "audit", "Reviewed", "Safe", "warning", "Audit")], 0, ActivityLogFailureKind.None) };
        var runtime = new ActivityLogsRuntime(new TestSession(), localization, new TestRecorder(), new TestDialogs());
        var model = new ActivityViewModel(runtime, new ActivityLogService(store), new FixedTimeProvider(Now));
        model.Activate();
        var englishSeverity = model.List.Items[0].SeverityChipText;

        localization.SetLanguage("tr");
        model.RefreshLocalization();

        Assert.NotEqual(englishSeverity, model.List.Items[0].SeverityChipText);
        Assert.Equal(localization.Get("Activity.Severity.Warning"), model.List.Items[0].SeverityChipText);
        Assert.Equal(localization.Get("Activity.Filter.AllSeverities"), model.List.SeverityOptions[0].Label);
    }

    private static ActivityViewModel CreateWorkspace(TestRecorder recorder, CapturingStore store, TestDialogs? dialogs = null)
    {
        var runtime = new ActivityLogsRuntime(new TestSession(), new LocalizationService(), recorder, dialogs ?? new TestDialogs());
        return new ActivityViewModel(runtime, new ActivityLogService(store), new FixedTimeProvider(Now));
    }

    private static ActivityLogEntry Entry(string id, DateTimeOffset timestamp, string category, string title, string detail, string severity, string? affected)
        => new(id, timestamp.ToString("O"), category, title, detail, severity, "/synthetic/Test.skvault") { AffectedItem = affected };

    private sealed class CapturingStore : IActivityLogStore
    {
        public ActivityLogEntry? Appended { get; private set; }
        public ActivityLogLoadResult LoadResult { get; set; } = ActivityLogLoadResult.Empty;
        public ActivityLogOperationResult AppendResult { get; set; } = ActivityLogOperationResult.Succeeded;
        public ActivityLogOperationResult ClearResult { get; set; } = ActivityLogOperationResult.Succeeded;
        public int ClearCalls { get; private set; }
        public ActivityLogLoadResult Load(string? vaultPath, byte[]? vaultKey) => LoadResult;
        public ActivityLogOperationResult Append(ActivityLogEntry entry, byte[]? vaultKey) { Appended = entry; return AppendResult; }
        public ActivityLogOperationResult Clear(string? vaultPath, byte[]? vaultKey)
        {
            ClearCalls++;
            if (ClearResult.Success)
                LoadResult = ActivityLogLoadResult.Empty;
            return ClearResult;
        }
    }

    private sealed class TestRecorder : IActivityRecorder
    {
        private EventHandler<ActivityRecorderChangedEventArgs>? _changed;
        public int SubscriptionCount { get; private set; }
        public int LogCalls { get; private set; }
        public ActivityLogOperationResult LogResult { get; set; } = ActivityLogOperationResult.Succeeded;
        public event EventHandler<ActivityRecorderChangedEventArgs>? Changed
        {
            add { _changed += value; SubscriptionCount++; }
            remove { _changed -= value; SubscriptionCount--; }
        }
        public ActivityLogOperationResult Log(string category, string title, string detail, string severity = "info", string? vaultPath = null, string? affectedItem = null)
        {
            LogCalls++;
            return LogResult;
        }
        public void Emit(ActivityLogOperationResult result) => _changed?.Invoke(this, new(result));
    }

    private sealed class TestSession : IVaultSessionController
    {
        public string? VaultPath { get; set; } = "/synthetic/Test.skvault";
        public bool IsUnlocked { get; set; } = true;
        public byte[] VaultKey { get; } = new byte[32];
        public event EventHandler? StateChanged { add { } remove { } }
        public void SetVaultPath(string? path) { }
        public void SetVaultKey(byte[] vaultKey) { }
        public void ClearSensitive() { }
    }

    private sealed class TestDialogs : IDesktopDialogService
    {
        public bool ConfirmDangerous { get; set; }
        public string? SavePath { get; set; }
        public TaskCompletionSource<string?>? SavePathCompletion { get; set; }
        public Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName) => Task.FromResult<string?>(null);
        public Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName)
            => SavePathCompletion?.Task ?? Task.FromResult(SavePath);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
        public Task<bool> ConfirmDangerousActionAsync(string title, string message, string detail, string confirmText) => Task.FromResult(ConfirmDangerous);
        public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) => Task.FromResult(false);
        public Task<string?> PromptPasswordAsync(string title, string message, string detail, string confirmText) => Task.FromResult<string?>(null);
        public Task<(bool Confirmed, string VaultPath, string DisplayName)> ShowImportVaultDialogAsync(string? initialPath = null, string? initialDisplayName = null) => Task.FromResult((false, "", ""));
        public Task<(bool Confirmed, string DisplayName, string Description)> ShowEditVaultDialogAsync(string displayName, string description, string vaultPath) => Task.FromResult((false, displayName, description));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now, TimeZoneInfo? timeZone = null) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => timeZone ?? TimeZoneInfo.Utc;
    }

    private sealed class TempWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "shellkrypt-activity-tests", Guid.NewGuid().ToString("N"));
        public TempWorkspace() => Directory.CreateDirectory(_root);
        public string FilePath(string name) => Path.Combine(_root, name);
        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }
    }
}
