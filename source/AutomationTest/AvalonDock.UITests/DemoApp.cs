using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Maui.DevFlow.Driver;
using NUnit.Framework;

namespace AvalonDock.UITests;

/// <summary>The demo applications the UI tests drive.</summary>
public enum DemoApp
{
	/// <summary>source/TestApp: the classic DockingManager test application.</summary>
	TestApp,

	/// <summary>source/AvalonDockCodeApp: the ToggleDockingManager application.</summary>
	CodeApp,
}

/// <summary>The UI framework build of a demo application.</summary>
public enum UiFramework
{
	Wpf,
	Avalonia,
}

/// <summary>
/// A running demo application with its DevFlow agent. The agent starts because the process gets
/// DEVFLOW_AGENT_PORT; see source/AutomationTest/DevFlow/DevFlowAgent.cs.
/// </summary>
public sealed class DemoAppSession : IDisposable
{
	private const string PortVariable = "DEVFLOW_AGENT_PORT";

	private readonly Process _process;

	private DemoAppSession(Process process, int port, UiFramework framework)
	{
		_process = process;
		Port = port;
		Framework = framework;
		Client = new AgentClient("localhost", port)
		{
			MutationLeaseId = "avalondock-uitests-" + Guid.NewGuid().ToString("N"),
			MutationLeaseHolderKind = "driver",
			MutationLeaseLabel = "AvalonDock.UITests",
			AutoAcquireMutationLease = true,
		};
	}

	public AgentClient Client { get; }

	public int Port { get; }

	public UiFramework Framework { get; }

	public bool HasExited => _process.HasExited;

	/// <summary>
	/// The framework to test: AVALONDOCK_UI (wpf or avalonia), else WPF on Windows and Avalonia elsewhere.
	/// </summary>
	public static UiFramework SelectedFramework
	{
		get
		{
			var value = Environment.GetEnvironmentVariable("AVALONDOCK_UI");
			if (!string.IsNullOrWhiteSpace(value))
				return Enum.Parse<UiFramework>(value, ignoreCase: true);

			return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? UiFramework.Wpf : UiFramework.Avalonia;
		}
	}

	public static async Task<DemoAppSession> StartAsync(DemoApp app, UiFramework framework)
	{
		var (fileName, arguments, workingDirectory) = ResolveLaunchCommand(app, framework);
		var port = GetFreePort();
		var startInfo = new ProcessStartInfo(fileName, arguments)
		{
			UseShellExecute = false,
			WorkingDirectory = workingDirectory,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
		};
		startInfo.Environment[PortVariable] = port.ToString();

		TestContext.Progress.WriteLine($"[DemoApp] Starting {app} ({framework}) on port {port}: {fileName} {arguments}");
		var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
		process.OutputDataReceived += (_, e) => { if (e.Data != null) TestContext.Progress.WriteLine($"[{app}] {e.Data}"); };
		process.ErrorDataReceived += (_, e) => { if (e.Data != null) TestContext.Progress.WriteLine($"[{app}] {e.Data}"); };
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		var session = new DemoAppSession(process, port, framework);
		try
		{
			await session.WaitForAgentAsync(TimeSpan.FromSeconds(60));
		}
		catch
		{
			session.Dispose();
			throw;
		}

		return session;
	}

	public void Dispose()
	{
		Client.Dispose();
		try
		{
			if (!_process.HasExited)
			{
				_process.Kill(entireProcessTree: true);
				_process.WaitForExit(10_000);
			}
		}
		catch (InvalidOperationException)
		{
		}

		_process.Dispose();
	}

	private async Task WaitForAgentAsync(TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			if (_process.HasExited)
				throw new InvalidOperationException($"The demo application exited with code {_process.ExitCode} before its DevFlow agent answered.");

			try
			{
				var status = await Client.GetStatusAsync();
				if (status?.Running == true)
					return;
			}
			catch (HttpRequestException)
			{
			}
			catch (TaskCanceledException)
			{
			}

			await Task.Delay(250);
		}

		throw new TimeoutException($"The DevFlow agent of the demo application did not answer on port {Port} within {timeout}.");
	}

	private static (string FileName, string Arguments, string WorkingDirectory) ResolveLaunchCommand(DemoApp app, UiFramework framework)
	{
		var sourceDir = FindSourceDirectory();
		var configuration = GetBuildConfiguration();
		var (folder, assembly) = app switch
		{
			DemoApp.TestApp => ("TestApp", "TestApp"),
			DemoApp.CodeApp => ("AvalonDockCodeApp", "AvalonDockCodeApp"),
			_ => throw new ArgumentOutOfRangeException(nameof(app)),
		};

		if (framework == UiFramework.Wpf)
		{
			var exe = Path.Combine(sourceDir, folder, "bin", configuration, "net10.0-windows", assembly + ".exe");
			RequireFile(exe, $"{folder}\\{assembly}.csproj");
			return (exe, string.Empty, Path.GetDirectoryName(exe)!);
		}

		// Run the Avalonia build through the dotnet host, which works the same way on every OS.
		var dll = Path.Combine(sourceDir, folder, "bin", "Avalonia", configuration, "net10.0", assembly + ".Avalonia.dll");
		RequireFile(dll, $"{folder}/{assembly}.Avalonia.csproj");
		var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
		return (string.IsNullOrEmpty(dotnet) ? "dotnet" : dotnet, $"\"{dll}\"", Path.GetDirectoryName(dll)!);
	}

	private static void RequireFile(string path, string project)
	{
		if (!File.Exists(path))
			throw new FileNotFoundException($"{path} was not found. Build {project} in the configuration of the tests first.", path);
	}

	private static string FindSourceDirectory()
	{
		for (var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); dir != null; dir = dir.Parent)
		{
			if (File.Exists(Path.Combine(dir.FullName, "AvalonDock.sln")))
				return dir.FullName;
		}

		throw new DirectoryNotFoundException("Could not find the source folder (the one with AvalonDock.sln) above the test directory.");
	}

	// The tests run from bin/<Configuration>/net10.0, and drive the demos built in the same configuration.
	private static string GetBuildConfiguration()
		=> new DirectoryInfo(TestContext.CurrentContext.TestDirectory).Parent?.Name ?? "Debug";

	private static int GetFreePort()
	{
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		return ((IPEndPoint)listener.LocalEndpoint).Port;
	}
}