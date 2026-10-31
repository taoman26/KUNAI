using System.Text.RegularExpressions;
using KUNAI.Core.Domain;
using KUNAI.Core.Services;
using KUNAI.Core.Shortcuts;

namespace KUNAI.Tests.Core;

public sealed class ProfileRepositoryTests : IDisposable
{
    private readonly string _root;
    private readonly string _profilesDir;
    private readonly string _defaultsDir;

    public ProfileRepositoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kunai-tests-" + Guid.NewGuid().ToString("N"));
        _profilesDir = Path.Combine(_root, "profiles");
        _defaultsDir = Path.Combine(_root, "defaults");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ProfileRepository CreateRepository(bool withDefaults = false) =>
        new(new ProfileRepositoryOptions(_profilesDir, withDefaults ? _defaultsDir : null));

    private static Profile CreateSampleProfile(string name = "coding")
    {
        var profile = Profile.CreateEmpty(name);
        profile.Layers[0].Mappings.Add(new ButtonMapping { Button = WiimoteButton.A, Action = "Enter" });
        profile.Layers[1].Mappings.Add(new ButtonMapping
        {
            Button = WiimoteButton.Plus,
            Macro = new List<string> { "Ctrl+S", "Delay(300)", "Alt+Tab" }
        });
        return profile;
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsProfile()
    {
        var repository = CreateRepository();
        await repository.SaveAsync(CreateSampleProfile());

        var loaded = await repository.LoadAsync("coding");

        Assert.Equal("coding", loaded.Name);
        Assert.Equal(4, loaded.Layers.Count);
        var a = loaded.FindMapping(0, WiimoteButton.A);
        Assert.NotNull(a);
        Assert.Equal("Enter", a!.Action);
        Assert.False(a.IsMacro);

        var plus = loaded.FindMapping(1, WiimoteButton.Plus);
        Assert.NotNull(plus);
        Assert.True(plus!.IsMacro);
        Assert.Equal(new[] { "Ctrl+S", "Delay(300)", "Alt+Tab" }, plus.Macro!);
    }

    [Fact]
    public async Task Save_WritesEnumsAsStrings_AndOmitsNullMacro()
    {
        var repository = CreateRepository();
        await repository.SaveAsync(CreateSampleProfile());

        var json = await File.ReadAllTextAsync(Path.Combine(_profilesDir, "coding.json"));

        Assert.Contains("\"button\": \"A\"", json);
        Assert.Contains("\"layerNumber\": 1", json);
        Assert.DoesNotContain("isMacro", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", json);
    }

    [Fact]
    public async Task Save_OverwritesExisting_AndLeavesNoTempFile()
    {
        var repository = CreateRepository();
        await repository.SaveAsync(CreateSampleProfile());

        var updated = CreateSampleProfile();
        updated.Layers[0].Mappings[0].Action = "Space";
        await repository.SaveAsync(updated);

        var loaded = await repository.LoadAsync("coding");
        Assert.Equal("Space", loaded.FindMapping(0, WiimoteButton.A)!.Action);
        Assert.Empty(Directory.GetFiles(_profilesDir, "*.tmp"));
    }

    [Fact]
    public async Task Load_AcceptsNameWithJsonExtension()
    {
        var repository = CreateRepository();
        await repository.SaveAsync(CreateSampleProfile());

        var loaded = await repository.LoadAsync("coding.json");

        Assert.Equal("coding", loaded.Name);
    }

    [Fact]
    public async Task Load_MissingProfile_ThrowsFileNotFound()
    {
        var repository = CreateRepository();

        await Assert.ThrowsAsync<FileNotFoundException>(() => repository.LoadAsync("nothing"));
    }

    [Fact]
    public async Task Load_InvalidJson_ThrowsInvalidData()
    {
        Directory.CreateDirectory(_profilesDir);
        await File.WriteAllTextAsync(Path.Combine(_profilesDir, "broken.json"), "{ not json");
        var repository = CreateRepository();

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.LoadAsync("broken"));
    }

    [Fact]
    public async Task Load_JsonNull_ThrowsInvalidData()
    {
        Directory.CreateDirectory(_profilesDir);
        await File.WriteAllTextAsync(Path.Combine(_profilesDir, "empty.json"), "null");
        var repository = CreateRepository();

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.LoadAsync("empty"));
    }

    [Fact]
    public async Task Load_FillsMissingLayersAndName()
    {
        Directory.CreateDirectory(_profilesDir);
        await File.WriteAllTextAsync(
            Path.Combine(_profilesDir, "minimal.json"),
            """{ "layers": [ { "layerNumber": 2, "mappings": [ { "button": "a", "action": "Enter" } ] } ] }""");
        var repository = CreateRepository();

        var loaded = await repository.LoadAsync("minimal");

        Assert.Equal("minimal", loaded.Name);
        Assert.Equal(new[] { 0, 1, 2, 3 }, loaded.Layers.Select(l => l.LayerNumber));
        Assert.Equal("Enter", loaded.FindMapping(2, WiimoteButton.A)!.Action);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../evil")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("..")]
    [InlineData(".json")]
    public async Task InvalidNames_AreRejected(string name)
    {
        var repository = CreateRepository();

        await Assert.ThrowsAsync<ArgumentException>(() => repository.LoadAsync(name));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(new Profile { Name = name }));
    }

    [Fact]
    public async Task GetProfileNames_ReturnsSortedJsonFilesOnly()
    {
        var repository = CreateRepository();
        await repository.SaveAsync(CreateSampleProfile("writing"));
        await repository.SaveAsync(CreateSampleProfile("browser"));
        await repository.SaveAsync(CreateSampleProfile("Coding"));
        await File.WriteAllTextAsync(Path.Combine(_profilesDir, "notes.txt"), "x");

        var names = await repository.GetProfileNamesAsync();

        Assert.Equal(new[] { "browser", "Coding", "writing" }, names);
    }

    [Fact]
    public async Task GetProfileNames_WhenDirectoryMissing_ReturnsEmpty()
    {
        var repository = CreateRepository();

        Assert.Empty(await repository.GetProfileNamesAsync());
    }

    [Fact]
    public async Task Initialize_CopiesDefaults_WhenProfilesDirectoryIsEmpty()
    {
        Directory.CreateDirectory(_defaultsDir);
        await File.WriteAllTextAsync(Path.Combine(_defaultsDir, "default.json"), """{ "name": "default" }""");
        await File.WriteAllTextAsync(Path.Combine(_defaultsDir, "coding.json"), """{ "name": "coding" }""");
        var repository = CreateRepository(withDefaults: true);

        await repository.InitializeAsync();

        Assert.Equal(new[] { "coding", "default" }, await repository.GetProfileNamesAsync());
    }

    [Fact]
    public async Task Initialize_DoesNotTouchExistingProfiles()
    {
        Directory.CreateDirectory(_defaultsDir);
        await File.WriteAllTextAsync(Path.Combine(_defaultsDir, "default.json"), """{ "name": "default" }""");
        var repository = CreateRepository(withDefaults: true);
        await repository.SaveAsync(CreateSampleProfile("mine"));

        await repository.InitializeAsync();

        Assert.Equal(new[] { "mine" }, await repository.GetProfileNamesAsync());
    }

    [Fact]
    public async Task Initialize_WithoutDefaults_CreatesEmptyDirectory()
    {
        var repository = CreateRepository();

        await repository.InitializeAsync();

        Assert.True(Directory.Exists(_profilesDir));
        Assert.Empty(await repository.GetProfileNamesAsync());
    }

    [Theory]
    [InlineData("default")]
    [InlineData("coding")]
    [InlineData("writing")]
    [InlineData("browser")]
    public async Task BundledProfiles_LoadAndContainOnlyValidActions(string name)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "bundled-profiles");
        var repository = new ProfileRepository(new ProfileRepositoryOptions(bundled));

        var profile = await repository.LoadAsync(name);

        Assert.Equal(name, profile.Name);
        Assert.Equal(4, profile.Layers.Count);
        Assert.All(profile.Layers.SelectMany(l => l.Mappings), mapping =>
        {
            var steps = mapping.IsMacro ? mapping.Macro! : new List<string> { mapping.Action };
            Assert.All(steps, step => Assert.True(IsValidStep(step), $"Invalid step '{step}' in {name}"));
        });
    }

    private static bool IsValidStep(string step) =>
        Enum.TryParse<StandardAction>(step, ignoreCase: false, out _)
        || Regex.IsMatch(step, @"^Delay\(\d+\)$")
        || ShortcutParser.TryParse(step, out _);
}
