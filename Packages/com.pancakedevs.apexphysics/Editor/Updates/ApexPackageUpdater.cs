using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace PancakeDevs.ApexPhysics.Editor
{
    internal enum ApexPackageUpdateStatus
    {
        Idle = 0,
        Checking = 1,
        UpToDate = 2,
        UpdateAvailable = 3,
        Installing = 4,
        Error = 5
    }

    [Serializable]
    internal sealed class ApexRemotePackageManifest
    {
        public string name;
        public string version;
    }

    [Serializable]
    internal sealed class ApexGitHubBranchResponse
    {
        public ApexGitHubCommit commit;
    }

    [Serializable]
    internal sealed class ApexGitHubCommit
    {
        public string sha;
    }

    /// <summary>
    /// Checks the public Apex repository for a newer package manifest and installs the
    /// exact latest commit through Unity Package Manager.
    /// </summary>
    [InitializeOnLoad]
    internal static class ApexPackageUpdater
    {
        internal const string PackageName = "com.pancakedevs.apexphysics";
        internal const string UpdateBranch = "agent/apex-foundation";

        private const string Repository = "Corbino50211/Apex-Physics-Engine";
        private const string PackagePath = "/Packages/com.pancakedevs.apexphysics";
        private const string PackageGitUrl =
            "https://github.com/Corbino50211/Apex-Physics-Engine.git?path=" + PackagePath;
        private const string RemoteManifestUrl =
            "https://api.github.com/repos/" + Repository +
            "/contents/Packages/com.pancakedevs.apexphysics/package.json?ref=agent%2Fapex-foundation";
        private const string RemoteBranchUrl =
            "https://api.github.com/repos/" + Repository +
            "/branches/agent%2Fapex-foundation";

        private const string AutoCheckKey = "PancakeDevs.ApexPhysics.Updater.AutoCheck";
        private const string AutoInstallKey = "PancakeDevs.ApexPhysics.Updater.AutoInstall";
        private const string LastCheckTicksKey = "PancakeDevs.ApexPhysics.Updater.LastCheckTicks";
        private const string PendingVersionKey = "PancakeDevs.ApexPhysics.Updater.PendingVersion";
        private const double AutomaticCheckIntervalHours = 12d;

        private static UnityWebRequest manifestRequest;
        private static UnityWebRequest branchRequest;
        private static AddRequest addRequest;
        private static bool userInitiatedCheck;
        private static bool installAfterCheck;

        static ApexPackageUpdater()
        {
            InstalledVersion = ResolveInstalledVersion();
            Status = ApexPackageUpdateStatus.Idle;
            Message = "Ready to check for Apex Physics Engine updates.";

            string pendingVersion = SessionState.GetString(PendingVersionKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(pendingVersion))
            {
                SessionState.EraseString(PendingVersionKey);
                EditorApplication.delayCall += () =>
                    Debug.Log($"Apex Physics Engine updated to {pendingVersion}.");
            }

            EditorApplication.delayCall += RunAutomaticCheckAfterLoad;
        }

        internal static event Action Changed;

        internal static ApexPackageUpdateStatus Status { get; private set; }
        internal static string InstalledVersion { get; private set; }
        internal static string LatestVersion { get; private set; } = string.Empty;
        internal static string LatestCommit { get; private set; } = string.Empty;
        internal static string Message { get; private set; }

        internal static bool IsBusy =>
            Status == ApexPackageUpdateStatus.Checking ||
            Status == ApexPackageUpdateStatus.Installing;

        internal static bool AutomaticallyCheck
        {
            get => EditorPrefs.GetBool(AutoCheckKey, true);
            set => EditorPrefs.SetBool(AutoCheckKey, value);
        }

        internal static bool AutomaticallyInstall
        {
            get => EditorPrefs.GetBool(AutoInstallKey, false);
            set => EditorPrefs.SetBool(AutoInstallKey, value);
        }

        internal static string LatestPackageUrl => string.IsNullOrWhiteSpace(LatestCommit)
            ? PackageGitUrl + "#" + UpdateBranch
            : PackageGitUrl + "#" + LatestCommit;

        internal static void CheckForUpdates(bool userInitiated, bool installWhenFound = false)
        {
            if (IsBusy)
            {
                return;
            }

            DisposeWebRequests();
            InstalledVersion = ResolveInstalledVersion();
            LatestVersion = string.Empty;
            LatestCommit = string.Empty;
            userInitiatedCheck = userInitiated;
            installAfterCheck = installWhenFound;
            SetStatus(ApexPackageUpdateStatus.Checking, "Checking the Apex release branch...");

            manifestRequest = UnityWebRequest.Get(RemoteManifestUrl);
            ConfigureGitHubRequest(manifestRequest);
            manifestRequest.SetRequestHeader("Accept", "application/vnd.github.raw+json");
            manifestRequest.SendWebRequest();
            EditorApplication.update += PollManifestRequest;
        }

        internal static void InstallLatest()
        {
            if (IsBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(LatestCommit))
            {
                CheckForUpdates(true, true);
                return;
            }

            SessionState.SetString(
                PendingVersionKey,
                string.IsNullOrWhiteSpace(LatestVersion) ? "the latest revision" : LatestVersion);

            SetStatus(
                ApexPackageUpdateStatus.Installing,
                $"Installing Apex Physics Engine {LatestVersion} through Package Manager...");

            addRequest = Client.Add(LatestPackageUrl);
            EditorApplication.update += PollAddRequest;
        }

        internal static void ForceReinstallLatest()
        {
            if (IsBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(LatestCommit))
            {
                CheckForUpdates(true, true);
                return;
            }

            InstallLatest();
        }

        private static void RunAutomaticCheckAfterLoad()
        {
            if (!AutomaticallyCheck || IsBusy || !AutomaticCheckIsDue())
            {
                return;
            }

            CheckForUpdates(false, AutomaticallyInstall);
        }

        private static void PollManifestRequest()
        {
            if (manifestRequest == null || !manifestRequest.isDone)
            {
                return;
            }

            EditorApplication.update -= PollManifestRequest;
            if (!RequestSucceeded(manifestRequest))
            {
                Fail($"Update check failed while reading package.json: {BuildRequestError(manifestRequest)}");
                manifestRequest.Dispose();
                manifestRequest = null;
                return;
            }

            string manifestJson = manifestRequest.downloadHandler.text;
            manifestRequest.Dispose();
            manifestRequest = null;

            ApexRemotePackageManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<ApexRemotePackageManifest>(manifestJson);
            }
            catch (Exception exception)
            {
                Fail("Apex received an invalid remote package manifest: " + exception.Message);
                return;
            }

            if (manifest == null || manifest.name != PackageName || string.IsNullOrWhiteSpace(manifest.version))
            {
                Fail("The remote Apex package manifest is missing its package name or version.");
                return;
            }

            LatestVersion = manifest.version.Trim();
            branchRequest = UnityWebRequest.Get(RemoteBranchUrl);
            ConfigureGitHubRequest(branchRequest);
            branchRequest.SetRequestHeader("Accept", "application/vnd.github+json");
            branchRequest.SendWebRequest();
            EditorApplication.update += PollBranchRequest;
            NotifyChanged();
        }

        private static void PollBranchRequest()
        {
            if (branchRequest == null || !branchRequest.isDone)
            {
                return;
            }

            EditorApplication.update -= PollBranchRequest;
            if (!RequestSucceeded(branchRequest))
            {
                Fail($"Update check failed while reading the release revision: {BuildRequestError(branchRequest)}");
                branchRequest.Dispose();
                branchRequest = null;
                return;
            }

            string branchJson = branchRequest.downloadHandler.text;
            branchRequest.Dispose();
            branchRequest = null;

            ApexGitHubBranchResponse branch;
            try
            {
                branch = JsonUtility.FromJson<ApexGitHubBranchResponse>(branchJson);
            }
            catch (Exception exception)
            {
                Fail("Apex received an invalid GitHub branch response: " + exception.Message);
                return;
            }

            LatestCommit = branch?.commit?.sha?.Trim() ?? string.Empty;
            if (LatestCommit.Length < 7)
            {
                Fail("The Apex release branch did not return a valid commit revision.");
                return;
            }

            RecordCheckTime();
            int comparison = CompareSemanticVersions(LatestVersion, InstalledVersion);
            if (comparison > 0)
            {
                SetStatus(
                    ApexPackageUpdateStatus.UpdateAvailable,
                    $"Apex Physics Engine {LatestVersion} is available. Installed: {InstalledVersion}.");

                bool shouldInstall = installAfterCheck || AutomaticallyInstall;
                if (!shouldInstall && userInitiatedCheck)
                {
                    shouldInstall = EditorUtility.DisplayDialog(
                        "Apex Update Available",
                        $"Apex Physics Engine {LatestVersion} is available.\n\nInstalled version: {InstalledVersion}\n\nInstall the update now?",
                        "Update Now",
                        "Later");
                }

                if (shouldInstall)
                {
                    InstallLatest();
                }

                return;
            }

            SetStatus(
                ApexPackageUpdateStatus.UpToDate,
                $"Apex Physics Engine {InstalledVersion} is up to date.");

            if (userInitiatedCheck)
            {
                Debug.Log(Message);
            }
        }

        private static void PollAddRequest()
        {
            if (addRequest == null || !addRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= PollAddRequest;
            if (addRequest.Status == StatusCode.Success)
            {
                InstalledVersion = !string.IsNullOrWhiteSpace(addRequest.Result?.version)
                    ? addRequest.Result.version
                    : LatestVersion;
                SetStatus(
                    ApexPackageUpdateStatus.UpToDate,
                    $"Apex Physics Engine {InstalledVersion} was installed. Unity may reload scripts now.");
                Debug.Log(Message);
            }
            else
            {
                SessionState.EraseString(PendingVersionKey);
                string error = addRequest.Error != null
                    ? addRequest.Error.message
                    : "Unity Package Manager returned an unknown error.";
                Fail("Apex update failed: " + error);
            }

            addRequest = null;
        }

        private static void ConfigureGitHubRequest(UnityWebRequest request)
        {
            request.timeout = 20;
            request.SetRequestHeader("User-Agent", "Apex-Physics-Engine-Unity-Updater");
            request.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
        }

        private static bool RequestSucceeded(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success &&
                   request.responseCode >= 200 &&
                   request.responseCode < 300;
        }

        private static string BuildRequestError(UnityWebRequest request)
        {
            if (request == null)
            {
                return "No response was received.";
            }

            string message = string.IsNullOrWhiteSpace(request.error)
                ? "HTTP " + request.responseCode
                : request.error;
            return message + ". Check the internet connection and GitHub access.";
        }

        private static string ResolveInstalledVersion()
        {
            Assembly assembly = typeof(ApexPackageUpdater).Assembly;
            PackageInfo package = PackageInfo.FindForAssembly(assembly);
            return package != null && !string.IsNullOrWhiteSpace(package.version)
                ? package.version
                : "0.0.0";
        }

        private static int CompareSemanticVersions(string first, string second)
        {
            ParseSemanticVersion(first, out int firstMajor, out int firstMinor, out int firstPatch, out bool firstPrerelease);
            ParseSemanticVersion(second, out int secondMajor, out int secondMinor, out int secondPatch, out bool secondPrerelease);

            int comparison = firstMajor.CompareTo(secondMajor);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = firstMinor.CompareTo(secondMinor);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = firstPatch.CompareTo(secondPatch);
            if (comparison != 0)
            {
                return comparison;
            }

            if (firstPrerelease == secondPrerelease)
            {
                return 0;
            }

            return firstPrerelease ? -1 : 1;
        }

        private static void ParseSemanticVersion(
            string value,
            out int major,
            out int minor,
            out int patch,
            out bool prerelease)
        {
            major = 0;
            minor = 0;
            patch = 0;
            prerelease = false;

            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string trimmed = value.Trim();
            int suffixIndex = trimmed.IndexOf('-');
            prerelease = suffixIndex >= 0;
            string numeric = suffixIndex >= 0 ? trimmed.Substring(0, suffixIndex) : trimmed;
            string[] parts = numeric.Split('.');

            if (parts.Length > 0)
            {
                int.TryParse(parts[0], out major);
            }
            if (parts.Length > 1)
            {
                int.TryParse(parts[1], out minor);
            }
            if (parts.Length > 2)
            {
                int.TryParse(parts[2], out patch);
            }
        }

        private static bool AutomaticCheckIsDue()
        {
            string storedTicks = EditorPrefs.GetString(LastCheckTicksKey, string.Empty);
            if (!long.TryParse(storedTicks, out long ticks))
            {
                return true;
            }

            DateTime lastCheck = new DateTime(ticks, DateTimeKind.Utc);
            return DateTime.UtcNow - lastCheck >= TimeSpan.FromHours(AutomaticCheckIntervalHours);
        }

        private static void RecordCheckTime()
        {
            EditorPrefs.SetString(LastCheckTicksKey, DateTime.UtcNow.Ticks.ToString());
        }

        private static void Fail(string message)
        {
            DisposeWebRequests();
            SetStatus(ApexPackageUpdateStatus.Error, message);
            Debug.LogWarning(message);
        }

        private static void DisposeWebRequests()
        {
            EditorApplication.update -= PollManifestRequest;
            EditorApplication.update -= PollBranchRequest;

            manifestRequest?.Dispose();
            branchRequest?.Dispose();
            manifestRequest = null;
            branchRequest = null;
        }

        private static void SetStatus(ApexPackageUpdateStatus status, string message)
        {
            Status = status;
            Message = message;
            NotifyChanged();
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
