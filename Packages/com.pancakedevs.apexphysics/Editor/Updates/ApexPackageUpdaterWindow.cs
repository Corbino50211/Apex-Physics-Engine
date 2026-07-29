using UnityEditor;
using UnityEngine;

namespace PancakeDevs.ApexPhysics.Editor
{
    public sealed class ApexPackageUpdaterWindow : EditorWindow
    {
        private const string MenuPath =
            "Apex Physics Engine/Updates/Check for Updates...";

        [MenuItem(MenuPath, false, 900)]
        private static void OpenFromMenu()
        {
            ApexPackageUpdaterWindow window = GetWindow<ApexPackageUpdaterWindow>();
            window.titleContent = new GUIContent("Apex Updates");
            window.minSize = new Vector2(430f, 300f);
            window.Show();
            window.Focus();

            if (!ApexPackageUpdater.IsBusy)
            {
                ApexPackageUpdater.CheckForUpdates(true);
            }
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Apex Updates");
            minSize = new Vector2(430f, 300f);
            ApexPackageUpdater.Changed += Repaint;
        }

        private void OnDisable()
        {
            ApexPackageUpdater.Changed -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Apex Physics Engine Updater", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Checks the PancakeDevs release branch and installs the newest exact revision through Unity Package Manager.",
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(12f);
            DrawVersionPanel();

            EditorGUILayout.Space(10f);
            DrawAutomaticSettings();

            EditorGUILayout.Space(10f);
            DrawStatus();

            GUILayout.FlexibleSpace();
            DrawActions();
            EditorGUILayout.Space(10f);
        }

        private static void DrawVersionPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Package", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Installed Version",
                    string.IsNullOrWhiteSpace(ApexPackageUpdater.InstalledVersion)
                        ? "Unknown"
                        : ApexPackageUpdater.InstalledVersion);
                EditorGUILayout.LabelField(
                    "Latest Version",
                    string.IsNullOrWhiteSpace(ApexPackageUpdater.LatestVersion)
                        ? "Not checked"
                        : ApexPackageUpdater.LatestVersion);
                EditorGUILayout.LabelField("Release Branch", ApexPackageUpdater.UpdateBranch);
            }
        }

        private static void DrawAutomaticSettings()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Automatic Updates", EditorStyles.boldLabel);

                bool autoCheck = EditorGUILayout.ToggleLeft(
                    "Check for updates when Unity opens",
                    ApexPackageUpdater.AutomaticallyCheck);
                if (autoCheck != ApexPackageUpdater.AutomaticallyCheck)
                {
                    ApexPackageUpdater.AutomaticallyCheck = autoCheck;
                }

                using (new EditorGUI.DisabledScope(!autoCheck))
                {
                    bool autoInstall = EditorGUILayout.ToggleLeft(
                        "Install a newer version automatically after checking",
                        ApexPackageUpdater.AutomaticallyInstall);
                    if (autoInstall != ApexPackageUpdater.AutomaticallyInstall)
                    {
                        ApexPackageUpdater.AutomaticallyInstall = autoInstall;
                    }
                }

                EditorGUILayout.HelpBox(
                    "Installing an update can trigger a script reload. Save open scenes and scripts before enabling automatic installation.",
                    MessageType.None);
            }
        }

        private static void DrawStatus()
        {
            MessageType messageType;
            switch (ApexPackageUpdater.Status)
            {
                case ApexPackageUpdateStatus.UpdateAvailable:
                    messageType = MessageType.Warning;
                    break;
                case ApexPackageUpdateStatus.Error:
                    messageType = MessageType.Error;
                    break;
                case ApexPackageUpdateStatus.UpToDate:
                    messageType = MessageType.Info;
                    break;
                default:
                    messageType = MessageType.None;
                    break;
            }

            EditorGUILayout.HelpBox(ApexPackageUpdater.Message, messageType);

            if (ApexPackageUpdater.Status == ApexPackageUpdateStatus.Checking ||
                ApexPackageUpdater.Status == ApexPackageUpdateStatus.Installing)
            {
                Rect progressRect = GUILayoutUtility.GetRect(1f, 20f, GUILayout.ExpandWidth(true));
                float animatedProgress = Mathf.PingPong((float)EditorApplication.timeSinceStartup * 0.45f, 1f);
                string label = ApexPackageUpdater.Status == ApexPackageUpdateStatus.Checking
                    ? "Checking GitHub..."
                    : "Updating package...";
                EditorGUI.ProgressBar(progressRect, animatedProgress, label);
            }
        }

        private static void DrawActions()
        {
            using (new EditorGUI.DisabledScope(ApexPackageUpdater.IsBusy))
            {
                if (ApexPackageUpdater.Status == ApexPackageUpdateStatus.UpdateAvailable)
                {
                    string version = string.IsNullOrWhiteSpace(ApexPackageUpdater.LatestVersion)
                        ? "Latest"
                        : ApexPackageUpdater.LatestVersion;
                    if (GUILayout.Button("Update to " + version, GUILayout.Height(38f)))
                    {
                        ApexPackageUpdater.InstallLatest();
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Check Again"))
                        {
                            ApexPackageUpdater.CheckForUpdates(true);
                        }

                        if (GUILayout.Button("Copy Package URL"))
                        {
                            EditorGUIUtility.systemCopyBuffer = ApexPackageUpdater.LatestPackageUrl;
                        }
                    }

                    return;
                }

                string buttonLabel = ApexPackageUpdater.Status == ApexPackageUpdateStatus.Error
                    ? "Retry Update Check"
                    : "Check for Updates";
                if (GUILayout.Button(buttonLabel, GUILayout.Height(38f)))
                {
                    ApexPackageUpdater.CheckForUpdates(true);
                }

                if (ApexPackageUpdater.Status == ApexPackageUpdateStatus.UpToDate &&
                    !string.IsNullOrWhiteSpace(ApexPackageUpdater.LatestVersion))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Reinstall Latest Revision"))
                        {
                            ApexPackageUpdater.ForceReinstallLatest();
                        }

                        if (GUILayout.Button("Copy Package URL"))
                        {
                            EditorGUIUtility.systemCopyBuffer = ApexPackageUpdater.LatestPackageUrl;
                        }
                    }
                }
            }
        }
    }
}
