using SmetaNA.Core.Model;
using SmetaNA.Formats.ProjectFile;
using SmetaNA.Formats.ProjectFile.Templates;
using Xunit;

namespace SmetaNA.Formats.Tests;

/// <summary>
/// Автосохранение и восстановление после сбоя (задача 4.1): периодические
/// атомарные снимки, маркер незакрытой сессии, обнаружение и восстановление.
/// </summary>
public sealed class AutosaveRecoveryTests
{
    [Fact]
    public void SaveSnapshot_WritesProjectSnapshotAndSessionMarker()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        using var service = new AutosaveService(
            AutosaveKey.FromUntitled(Guid.NewGuid()),
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root });

        service.OpenSession(projectPath: @"C:\proj\test.sna");
        AutosaveSnapshot snapshot = service.SaveSnapshot();

        Assert.True(File.Exists(snapshot.Path));
        Assert.True(File.Exists(service.SessionMarkerPath));
        Assert.Equal(LengthUnit.Centimeter, ProjectFileStore.Load(snapshot.Path).Document.Unit);
        Assert.NotNull(service.Session?.LastAutosaveAtUtc);
    }

    [Fact]
    public void Crash_WithoutCloseSession_IsDetected_AndRestores()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string key = AutosaveKey.FromProjectPath(@"C:\proj\crash.sna");
        var service = new AutosaveService(
            key,
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root });
        string projectPath = @"C:\proj\crash.sna";

        service.OpenSession(projectPath);
        service.SaveSnapshot();
        service.Dispose(); // «Сбой»: маркер сессии не удалён.

        RecoveryCandidate candidate = AutosaveRecovery.Find(temp.Root, key)
            ?? throw new InvalidOperationException("Незакрытое автосохранение не обнаружено.");
        Assert.True(candidate.IsValid);
        Assert.Null(candidate.Problem);
        Assert.Equal(projectPath, candidate.ProjectPath);

        ProjectLoadResult restored = AutosaveRecovery.Restore(candidate);
        ProjectAssert.DocumentsEqual(document, restored.Document);
    }

    [Fact]
    public void CloseSession_RemovesRecoveryState()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string key = AutosaveKey.FromUntitled(Guid.NewGuid());
        using var service = new AutosaveService(
            key,
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root });

        service.OpenSession();
        service.SaveSnapshot();
        service.CloseSession();

        Assert.Null(AutosaveRecovery.Find(temp.Root, key));
        Assert.Empty(AutosaveRecovery.FindAll(temp.Root));
    }

    [Fact]
    public void CloseSession_KeepsSnapshot_WhenConfigured()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string key = AutosaveKey.FromUntitled(Guid.NewGuid());
        using var service = new AutosaveService(
            key,
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root, DeleteSnapshotOnClose = false });

        service.OpenSession();
        AutosaveSnapshot snapshot = service.SaveSnapshot();
        service.CloseSession();

        // Снимок остаётся как резервная копия, но сессия закрыта — восстановление не предлагается.
        Assert.True(File.Exists(snapshot.Path));
        Assert.Empty(AutosaveRecovery.FindAll(temp.Root));
    }

    [Fact]
    public void PeriodicAutosave_WritesSnapshotsWhileRunning()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string key = AutosaveKey.FromUntitled(Guid.NewGuid());
        using var service = new AutosaveService(
            key,
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root, Interval = TimeSpan.FromMilliseconds(50) });

        service.OpenSession();
        service.Start();
        try
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!File.Exists(service.AutosavePath) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(20);
            }

            Assert.True(File.Exists(service.AutosavePath), "периодическое автосохранение не записало снимок");
            Assert.Null(service.LastError);
            Assert.Equal(LengthUnit.Centimeter, ProjectFileStore.Load(service.AutosavePath).Document.Unit);
        }
        finally
        {
            service.Stop();
        }
    }

    [Fact]
    public void TruncatedSnapshot_IsReportedInvalid_AndRestoreThrows()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string key = AutosaveKey.FromUntitled(Guid.NewGuid());
        using var service = new AutosaveService(
            key,
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root });

        service.OpenSession();
        service.SaveSnapshot();

        // «Обрыв» при записи снимка: половина контейнера.
        byte[] whole = File.ReadAllBytes(service.AutosavePath);
        File.WriteAllBytes(service.AutosavePath, whole[..(whole.Length / 2)]);

        RecoveryCandidate candidate = AutosaveRecovery.Find(temp.Root, key)
            ?? throw new InvalidOperationException("Незакрытое автосохранение не обнаружено.");
        Assert.False(candidate.IsValid);
        Assert.NotNull(candidate.Problem);
        Assert.Throws<ProjectFileException>(() => AutosaveRecovery.Restore(candidate));
    }

    [Fact]
    public void Discard_RemovesRecoveryState()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string key = AutosaveKey.FromUntitled(Guid.NewGuid());
        using var service = new AutosaveService(
            key,
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root, DeleteSnapshotOnClose = false });

        service.OpenSession();
        service.SaveSnapshot();
        service.Dispose();

        RecoveryCandidate candidate = AutosaveRecovery.Find(temp.Root, key)
            ?? throw new InvalidOperationException("Незакрытое автосохранение не обнаружено.");
        AutosaveRecovery.Discard(candidate);

        Assert.Null(AutosaveRecovery.Find(temp.Root, key));
        Assert.Empty(AutosaveRecovery.FindAll(temp.Root));
    }

    [Fact]
    public void Find_WithoutSnapshot_ReturnsNull()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string key = AutosaveKey.FromUntitled(Guid.NewGuid());
        using var service = new AutosaveService(
            key,
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root });

        service.OpenSession();

        Assert.Null(AutosaveRecovery.Find(temp.Root, key));
    }

    [Fact]
    public void SaveSnapshot_Failure_KeepsPreviousSnapshot()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectScenes.ComplexDocument();
        string key = AutosaveKey.FromUntitled(Guid.NewGuid());
        using var service = new AutosaveService(
            key,
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root });

        service.OpenSession();
        service.SaveSnapshot();
        byte[] goodSnapshot = File.ReadAllBytes(service.AutosavePath);

        // Последующий снимок падает в середине записи — прежний снимок цел.
        using var broken = new AutosaveService(
            key,
            () => throw new IOException("Имитация сбоя чтения модели."),
            new AutosaveOptions { RootDirectory = temp.Root });
        Assert.Throws<IOException>(() => broken.SaveSnapshot());

        Assert.Equal(goodSnapshot, File.ReadAllBytes(service.AutosavePath));
    }

    [Fact]
    public void Templates_RoundTripThroughAutosaveSnapshot()
    {
        using var temp = new TempDirectory();
        ProjectDocument document = ProjectTemplates.CreateDocument(ProjectTemplates.CupId);
        string key = AutosaveKey.FromUntitled(Guid.NewGuid());
        using var service = new AutosaveService(
            key,
            () => document,
            new AutosaveOptions { RootDirectory = temp.Root });

        service.OpenSession();
        service.SaveSnapshot();

        RecoveryCandidate candidate = AutosaveRecovery.Find(temp.Root, key)
            ?? throw new InvalidOperationException("Незакрытое автосохранение не обнаружено.");
        ProjectAssert.DocumentsEqual(document, AutosaveRecovery.Restore(candidate).Document);
    }
}
