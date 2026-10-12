using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpookysAutomod.Core.Models;

namespace SpookysAutomod.Skse.Services;

public sealed record F4seProjectConfig(string Name, string Target, string SdkPath);
public sealed record F4seBuildResult(string Dll, string PackageDirectory, string BuildLog, bool MockHostPassed);

/// <summary>Creates and builds public-API F4SE projects for explicitly supported runtimes.</summary>
public sealed class F4seProjectService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static (string Runtime, string Macro) Target(string target) => target switch
    {
        "vr" => ("0x01020482u", "RUNTIME_VR_VERSION_1_2_72"),
        "desktop-legacy" => ("0x010A0A30u", "RUNTIME_VERSION_1_10_163"),
        _ => throw new ArgumentException("Target must be vr (1.2.72) or desktop-legacy (1.10.163). Newer desktop runtimes are not supported by this template.")
    };

    private static void Validate(F4seProjectConfig config)
    {
        if (!Regex.IsMatch(config.Name, "^[A-Za-z][A-Za-z0-9_]{0,63}$"))
            throw new ArgumentException("Project name must start with a letter and contain only letters, numbers and underscores (maximum 64 characters).");
        var target = Target(config.Target);
        if (!Path.IsPathFullyQualified(config.SdkPath)) throw new ArgumentException("SDK path must be absolute.");
        foreach (var file in new[] { "f4se/PluginAPI.h", "f4se_common/f4se_version.h" })
            if (!File.Exists(Path.Combine(config.SdkPath, file))) throw new ArgumentException($"SDK file missing: {file}. Supply the official SDK directory containing f4se and f4se_common.");
        if (!new[] { "../common/ITypes.h", "../common/common/ITypes.h" }.Any(file => File.Exists(Path.Combine(config.SdkPath, file))))
            throw new ArgumentException("SDK common/ITypes.h is missing. Extract the complete source archive.");
        var version = File.ReadAllText(Path.Combine(config.SdkPath, "f4se_common/f4se_version.h"));
        if (!Regex.IsMatch(version, @"#define\s+CURRENT_RELEASE_RUNTIME\s+" + target.Macro + @"\b"))
            throw new ArgumentException("SDK runtime does not match the selected target.");
    }

    public Result<string> Create(string name, string outputDirectory, string target, string sdk)
    {
        try
        {
            var config = new F4seProjectConfig(name, target, Path.GetFullPath(sdk));
            Validate(config);
            var directory = Path.Combine(Path.GetFullPath(outputDirectory), name);
            if (Directory.Exists(directory) || File.Exists(directory)) throw new IOException("Project output already exists; choose a new name or directory.");
            // Resolve resources before creating output so missing templates cannot leave an empty project.
            var templates = new Dictionary<string, string>();
            foreach (var file in new[] { "CMakeLists.txt", "main.cpp", "host.cpp" })
            {
                using var stream = typeof(F4seProjectService).Assembly.GetManifestResourceStream("F4se." + file)
                    ?? throw new InvalidOperationException($"Template missing: {file}");
                using var reader = new StreamReader(stream);
                templates[file] = reader.ReadToEnd().Replace("@NAME@", name).Replace("@RUNTIME@", Target(target).Runtime);
            }
            Directory.CreateDirectory(directory);
            foreach (var (file, content) in templates) File.WriteAllText(Path.Combine(directory, file), content);
            File.WriteAllText(Path.Combine(directory, "f4se-project.json"), JsonSerializer.Serialize(config, Json));
            File.WriteAllText(Path.Combine(directory, ".gitignore"), "/build/\n/package/\n");
            File.WriteAllText(Path.Combine(directory, "README.md"), $"""
                # {name}

                Target: {target}. SDK: {config.SdkPath}

                Build and test: `spookys-automod f4se build "{directory}" --json`

                This starter exports F4SEPlugin_Query/Load, rejects the wrong runtime and editor,
                registers a game-loaded listener and writes messages to the Windows debugger output.
                It uses the official public PluginAPI headers, without linking engine functions.
                Add functionality in main.cpp. Engine hooks, Papyrus bindings and game classes require
                additional matching SDK source/libraries or a VR-compatible framework.

                A build also runs a standalone mock-host contract test. It is NOT an in-game test.
                package/F4SE/Plugins contains the DLL for manual installation as an MO2 mod.
                No game files are changed by these commands. Verify actual loading in the F4SE log
                after manually enabling the test plugin and launching the matching extender.
                Keep the SDK external; see its readme for distribution terms. F4SE asks plugin
                authors to make plugin source publicly available when distributing a plugin.
                """);
            return Result<string>.Ok(directory);
        }
        catch (Exception ex) { return Result<string>.Fail(ex.Message); }
    }

    public Result<F4seProjectConfig> Info(string directory)
    {
        try
        {
            var config = JsonSerializer.Deserialize<F4seProjectConfig>(File.ReadAllText(Path.Combine(directory, "f4se-project.json")))
                ?? throw new ArgumentException("Invalid project configuration.");
            Validate(config);
            return Result<F4seProjectConfig>.Ok(config);
        }
        catch (Exception ex) { return Result<F4seProjectConfig>.Fail(ex.Message); }
    }

    public async Task<Result<F4seBuildResult>> Build(string directory)
    {
        try
        {
            directory = Path.GetFullPath(directory);
            var info = Info(directory);
            if (!info.Success) return Result<F4seBuildResult>.Fail(info.Error!);
            var config = info.Value!;
            var build = Path.Combine(directory, "build");
            var package = Path.Combine(directory, "package");
            Directory.CreateDirectory(build);
            var logPath = Path.Combine(build, "toolkit-build.log");
            File.WriteAllText(logPath, "F4SE build and mock-host validation\n");
            await Run("cmake", new[] { "-S", directory, "-B", build, "-A", "x64", "-DBUILD_TESTING=ON", "-DF4SE_SDK_ROOT=" + config.SdkPath }, logPath);
            await Run("cmake", new[] { "--build", build, "--config", "Release", "--parallel", "2" }, logPath);
            await Run("ctest", new[] { "--test-dir", build, "-C", "Release", "--output-on-failure", "--no-tests=error" }, logPath);
            await Run("cmake", new[] { "--install", build, "--config", "Release", "--prefix", package }, logPath);
            var dll = Path.Combine(package, "F4SE", "Plugins", config.Name + ".dll");
            if (!File.Exists(dll)) throw new IOException("Build finished without the expected DLL.");
            return Result<F4seBuildResult>.Ok(new(dll, package, logPath, true));
        }
        catch (Exception ex) { return Result<F4seBuildResult>.Fail(ex.Message); }
    }

    private static async Task Run(string executable, string[] arguments, string log)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException($"Could not start {executable}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw new TimeoutException($"{executable} exceeded 10 minutes. See {log}"); }
        await File.AppendAllTextAsync(log, $"\n{executable}\n{await stdout}\n{await stderr}\n");
        if (process.ExitCode != 0) throw new IOException($"{executable} failed with exit code {process.ExitCode}. See {log}");
    }
}
