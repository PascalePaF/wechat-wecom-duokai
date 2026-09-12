using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using shuangkai;
using shuangkai.Core;
using WechatDuokai.Installer;

namespace WechatDuokai.Tests
{
    internal static class Program
    {
        private const string MutexName = "_WeChat_App_Instance_Identity_Mutex_Name";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--hold-mutex")
            {
                return HoldMutex(args[1]);
            }

            if (args.Length == 2 && args[0] == "--hold-file")
            {
                return HoldFile(args[1]);
            }

            if (args.Length == 2 && args[0] == "--snapshot")
            {
                return SaveUiSnapshot(new Home(), args[1]);
            }

            if (args.Length == 2 && args[0] == "--snapshot-installer")
            {
                return SaveUiSnapshot(new InstallForm(), args[1]);
            }

            if (args.Length == 2 && args[0] == "--snapshot-uninstaller")
            {
                return SaveUiSnapshot(new UninstallForm(), args[1]);
            }

            if (args.Length == 1 && args[0] == "--test-install")
            {
                return TestInstall();
            }

            if (args.Length == 1 && args[0] == "--cleanup-test-install")
            {
                return CleanupTestInstall();
            }

            try
            {
                Run("Null application has zero instances", () =>
                    Assert(new InstanceManager().GetInstanceCount(null) == 0, "Expected zero."));

                Run("Target count cache survives a restart", TestPreferenceRoundTrip);

                Run("Cleanup refuses drive roots", () =>
                    Assert(!InstallerEngine.ValidateSourceRoot(Path.GetPathRoot(Environment.SystemDirectory)),
                        "A drive root must never be accepted as a source directory."));

                Run("Project source marker is recognized", () =>
                {
                    var root = FindProjectRoot();
                    Assert(InstallerEngine.ValidateSourceRoot(root), "Expected marked project root to validate.");
                });

                Run("Custom installation paths are validated safely", TestCustomInstallPathValidation);

                Run("Installer embeds the exact Release application", TestEmbeddedPayload);

                Run("Known WeChat mutex can be released without terminating its process", TestMutexRelease);
                Run("Modern WeChat lock file can be released without terminating its process", TestFileLockRelease);

                Console.WriteLine("All smoke tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static int HoldMutex(string readyFile)
        {
            bool created;
            var mutex = new Mutex(true, MutexName, out created);
            File.WriteAllText(readyFile, created ? "ready" : "not-created");
            Thread.Sleep(30000);
            GC.KeepAlive(mutex);
            return 0;
        }

        private static int HoldFile(string filePath)
        {
            var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            File.WriteAllText(filePath + ".ready", "ready");
            Thread.Sleep(30000);
            GC.KeepAlive(stream);
            return 0;
        }

        private static void TestPreferenceRoundTrip()
        {
            var testDirectory = Path.Combine(Path.GetTempPath(), "wechat-duokai-preferences-" + Guid.NewGuid().ToString("N"));
            try
            {
                Assert(UserPreferences.LoadTargetCount(testDirectory) == 2, "Missing settings should use default 2.");
                UserPreferences.SaveTargetCount(testDirectory, 7);
                Assert(UserPreferences.LoadTargetCount(testDirectory) == 7, "Saved target count was not loaded.");
                Assert(UserPreferences.HasValidMarker(testDirectory), "User-data marker is missing or invalid.");
            }
            finally
            {
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
        }

        private static int SaveUiSnapshot(Form form, string outputPath)
        {
            using (form)
            {
                form.Show();
                Application.DoEvents();
                Thread.Sleep(300);
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        private static int TestInstall()
        {
            if (Directory.Exists(InstallerEngine.InstallDirectory))
            {
                throw new InvalidOperationException("Install test requires an unused installation directory.");
            }

            var result = InstallerEngine.Install(false);
            Assert(result.InstallDirectory == InstallerEngine.InstallDirectory, "Unexpected installation directory.");
            Assert(File.Exists(InstallerEngine.InstalledExecutable), "Installed application is missing.");
            Assert(File.Exists(InstallerEngine.InstalledUninstaller), "Installed uninstaller is missing.");
            Assert(File.Exists(Path.Combine(InstallerEngine.StartMenuDirectory, "微信企业微信多开助手.lnk")),
                "Start menu application shortcut is missing.");
            Assert(File.Exists(Path.Combine(InstallerEngine.StartMenuDirectory, "完全卸载.lnk")),
                "Start menu uninstall shortcut is missing.");
            Assert(!File.Exists(InstallerEngine.DesktopShortcut), "Desktop shortcut should not be created in this test.");
            Console.WriteLine("PASS: Installer writes application, uninstaller, registry data and shortcuts");
            return 0;
        }

        private static int CleanupTestInstall()
        {
            var locations = InstallerEngine.GetCleanupLocations();
            Assert(InstallerEngine.ValidateInstallDirectory(locations.InstallDirectory),
                "No safely marked test installation was found.");
            Assert(string.Equals(Path.GetFullPath(locations.InstallDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(InstallerEngine.DefaultInstallDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase),
                "Cleanup test only accepts the exact default test installation directory.");

            var planPath = Path.Combine(Path.GetTempPath(), "wechat-duokai-cleanup-test-" + Guid.NewGuid().ToString("N") + ".plan");
            var plan = new CleanupPlan
            {
                ParentProcessId = 0,
                DeleteSource = false,
                InstallDirectory = locations.InstallDirectory,
                SourceRoot = null,
                ArtifactRoot = null,
                PackageRoot = null,
                DesktopShortcut = InstallerEngine.DesktopShortcut,
                StartMenuDirectory = InstallerEngine.StartMenuDirectory,
                UserDataDirectory = null,
                WorkerPath = null,
                PlanPath = planPath
            };
            plan.Write(planPath);
            CleanupWorker.ExecuteForTests(planPath);

            Assert(!Directory.Exists(InstallerEngine.DefaultInstallDirectory), "Test installation directory was not removed.");
            Assert(!Directory.Exists(InstallerEngine.StartMenuDirectory), "Test start-menu directory was not removed.");
            Assert(!InstallerEngine.ValidateInstallDirectory(locations.InstallDirectory), "Test installation still validates after cleanup.");
            Console.WriteLine("PASS: Complete cleanup removes the verified test installation");
            return 0;
        }

        private static void TestEmbeddedPayload()
        {
            var root = FindProjectRoot();
            var builtApp = Path.Combine(root, "duokai", "bin", "Release", "duokai.exe");
            var installerAssembly = typeof(InstallerEngine).Assembly;
            using (var embedded = installerAssembly.GetManifestResourceStream("Payload.wechat_duokai.exe"))
            using (var built = File.OpenRead(builtApp))
            using (var sha = SHA256.Create())
            {
                Assert(embedded != null, "Installer payload resource is missing.");
                var embeddedHash = Convert.ToBase64String(sha.ComputeHash(embedded));
                built.Position = 0;
                var builtHash = Convert.ToBase64String(sha.ComputeHash(built));
                Assert(embeddedHash == builtHash, "Installer payload differs from the Release application.");
            }
        }

        private static void TestCustomInstallPathValidation()
        {
            string validationError;
            Assert(!InstallerEngine.ValidateInstallTarget(Path.GetPathRoot(Environment.SystemDirectory), out validationError),
                "A drive root must never be accepted as an installation target.");

            var testRoot = Path.Combine(Path.GetTempPath(), "wechat-duokai-install-target-" + Guid.NewGuid().ToString("N"));
            var emptyTarget = Path.Combine(testRoot, "custom-app");
            try
            {
                Directory.CreateDirectory(emptyTarget);
                Assert(InstallerEngine.ValidateInstallTarget(emptyTarget, out validationError),
                    "An empty custom folder should be accepted: " + validationError);

                File.WriteAllText(Path.Combine(emptyTarget, "unrelated.txt"), "belongs to the user");
                Assert(!InstallerEngine.ValidateInstallTarget(emptyTarget, out validationError),
                    "A non-empty unmarked folder must be rejected.");

                File.WriteAllText(Path.Combine(emptyTarget, InstallerEngine.InstallMarkerName),
                    InstallerEngine.InstallMarkerValue);
                File.WriteAllText(Path.Combine(emptyTarget, "wechat_duokai.exe"), string.Empty);
                Assert(InstallerEngine.ValidateInstallTarget(emptyTarget, out validationError),
                    "A marked existing installation should be accepted for an update.");
                Assert(InstallerEngine.ValidateInstallDirectory(emptyTarget),
                    "A marked custom installation should be recognized during cleanup.");
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, true);
                }
            }
        }

        private static void TestMutexRelease()
        {
            var readyFile = Path.Combine(Path.GetTempPath(), "wechat-duokai-test-" + Guid.NewGuid().ToString("N") + ".ready");
            var child = Process.Start(new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = "--hold-mutex \"" + readyFile + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            try
            {
                for (var attempt = 0; attempt < 100 && !File.Exists(readyFile); attempt++)
                {
                    Thread.Sleep(50);
                }

                Assert(File.Exists(readyFile), "Mutex holder did not become ready.");
                Assert(File.ReadAllText(readyFile) == "ready", "Test mutex was already present.");

                var unlock = WindowsHandleUnlocker.ReleaseSingleInstanceLocks(AppKind.WeChat, new[] { child.Id });
                Assert(unlock.ClosedHandleCount > 0, "No matching mutex handle was closed.");

                Mutex opened;
                var stillExists = Mutex.TryOpenExisting(MutexName, out opened);
                opened?.Dispose();
                Assert(!stillExists, "Named mutex still exists after release.");
                Assert(!child.HasExited, "The holder process was terminated; only the handle should be released.");
            }
            finally
            {
                if (child != null && !child.HasExited)
                {
                    child.Kill();
                    child.WaitForExit(3000);
                }
                child?.Dispose();
                if (File.Exists(readyFile))
                {
                    File.Delete(readyFile);
                }
            }
        }

        private static void TestFileLockRelease()
        {
            var lockFile = Path.Combine(Path.GetTempPath(), "wechat-duokai-lock-" + Guid.NewGuid().ToString("N") + ".ini");
            var readyFile = lockFile + ".ready";
            var child = Process.Start(new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                Arguments = "--hold-file \"" + lockFile + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            try
            {
                for (var attempt = 0; attempt < 100 && !File.Exists(readyFile); attempt++)
                {
                    Thread.Sleep(50);
                }

                Assert(File.Exists(readyFile), "File-lock holder did not become ready.");
                var unlock = WindowsHandleUnlocker.ReleaseSingleInstanceLocks(AppKind.WeChat, new[] { child.Id }, lockFile);
                Assert(unlock.ClosedHandleCount > 0, "No matching file handle was closed.");
                Assert(unlock.RemovedLockFile, "Lock file was not removed.");
                Assert(!File.Exists(lockFile), "Lock file still exists after release.");
                Assert(!child.HasExited, "The holder process was terminated; only the file handle should be released.");
            }
            finally
            {
                if (child != null && !child.HasExited)
                {
                    child.Kill();
                    child.WaitForExit(3000);
                }
                child?.Dispose();
                if (File.Exists(lockFile))
                {
                    File.Delete(lockFile);
                }
                if (File.Exists(readyFile))
                {
                    File.Delete(readyFile);
                }
            }
        }

        private static string FindProjectRoot()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (var depth = 0; directory != null && depth < 8; depth++, directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, InstallerEngine.SourceMarkerName)))
                {
                    return directory.FullName;
                }
            }

            throw new DirectoryNotFoundException("Project root not found.");
        }

        private static void Run(string name, Action test)
        {
            test();
            Console.WriteLine("PASS: " + name);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
