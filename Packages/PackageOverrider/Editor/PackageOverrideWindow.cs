using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace PackageOverrider
{
    /// <summary>
    /// manifest.jsonのパッケージソースをローカルパスにオーバーライドするEditorWindow
    /// </summary>
    public class PackageOverriderWindow : EditorWindow
    {
        private const string ManifestPath = "Packages/manifest.json";
        private const string SettingsPath = "UserSettings/PackageOverriderSettings.json";

        private Vector2 _scrollPosition;
        private string _filterText = "";
        private PackageOverrideData _data;
        private Dictionary<string, string> _currentDependencies;
        private bool _hasUnsavedChanges;

        [MenuItem("Window/Package Management/Package Overrider", false, 2100)]
        public static void ShowWindow()
        {
            var window = GetWindow<PackageOverriderWindow>("Package Overrider");
            window.minSize = new Vector2(500, 300);
        }

        private void OnEnable()
        {
            LoadSettings();
            LoadManifest();
        }

        private void OnFocus()
        {
            LoadManifest();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawPackageList();
            DrawFooter();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                LoadManifest();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));
            _filterText = EditorGUILayout.TextField(_filterText, EditorStyles.toolbarSearchField, GUILayout.Width(200));

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPackageList()
        {
            if (_currentDependencies == null)
            {
                EditorGUILayout.HelpBox("manifest.json を読み込めませんでした。", MessageType.Error);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var filteredDependencies = _currentDependencies
                .Where(kvp => !kvp.Key.StartsWith("com.unity.modules."))
                .Where(kvp => string.IsNullOrEmpty(_filterText) ||
                              kvp.Key.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(kvp => kvp.Key);

            foreach (var kvp in filteredDependencies)
            {
                DrawPackageEntry(kvp.Key, kvp.Value);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPackageEntry(string packageName, string currentSource)
        {
            var entry = GetOrCreateEntry(packageName);
            bool wasOverridden = entry.isOverridden;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            entry.isOverridden = EditorGUILayout.ToggleLeft(packageName, entry.isOverridden, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;

            if (entry.isOverridden)
            {
                if (!wasOverridden && string.IsNullOrEmpty(entry.originalSource))
                {
                    entry.originalSource = currentSource;
                    _hasUnsavedChanges = true;
                }

                EditorGUILayout.LabelField("Original:", entry.originalSource, EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Override:", GUILayout.Width(60));
                string newPath = EditorGUILayout.TextField(entry.overridePath);
                if (newPath != entry.overridePath)
                {
                    entry.overridePath = newPath;
                    _hasUnsavedChanges = true;
                }

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string selectedPath = EditorUtility.OpenFolderPanel(
                        "Select Package Folder",
                        string.IsNullOrEmpty(entry.overridePath) ? "" : entry.overridePath,
                        "");

                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        entry.overridePath = selectedPath;
                        _hasUnsavedChanges = true;
                    }
                }
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(entry.overridePath))
                {
                    string packageJsonPath = Path.Combine(entry.overridePath, "package.json");
                    if (!File.Exists(packageJsonPath))
                    {
                        EditorGUILayout.HelpBox("指定されたパスに package.json が見つかりません。", MessageType.Warning);
                    }
                }
            }
            else
            {
                if (wasOverridden)
                {
                    _hasUnsavedChanges = true;
                }
                EditorGUILayout.LabelField("Current:", currentSource, EditorStyles.miniLabel);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!_hasUnsavedChanges);
            if (GUILayout.Button("Apply Changes", GUILayout.Width(120), GUILayout.Height(25)))
            {
                ApplyChanges();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void LoadManifest()
        {
            try
            {
                string content = File.ReadAllText(ManifestPath, Encoding.UTF8);
                _currentDependencies = ParseDependencies(content);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load manifest.json: {ex.Message}");
                _currentDependencies = null;
            }
        }

        private static Dictionary<string, string> ParseDependencies(string jsonContent)
        {
            var result = new Dictionary<string, string>();

            var dependenciesMatch = Regex.Match(jsonContent, @"""dependencies""\s*:\s*\{([^}]+)\}",
                RegexOptions.Singleline);
            if (!dependenciesMatch.Success)
                return result;

            var entryMatches = Regex.Matches(dependenciesMatch.Groups[1].Value,
                @"""([^""]+)""\s*:\s*""([^""]+)""");

            foreach (Match match in entryMatches)
            {
                result[match.Groups[1].Value] = match.Groups[2].Value;
            }

            return result;
        }

        private void ApplyChanges()
        {
            try
            {
                string content = File.ReadAllText(ManifestPath, Encoding.UTF8);

                foreach (var entry in _data.entries)
                {
                    if (!_currentDependencies.ContainsKey(entry.packageName))
                        continue;

                    string newSource;
                    if (entry.isOverridden && !string.IsNullOrEmpty(entry.overridePath))
                    {
                        newSource = "file:" + entry.overridePath.Replace("\\", "/");
                    }
                    else if (!entry.isOverridden && !string.IsNullOrEmpty(entry.originalSource))
                    {
                        newSource = entry.originalSource;
                    }
                    else
                    {
                        continue;
                    }

                    string pattern = $@"(""{Regex.Escape(entry.packageName)}""\s*:\s*"")[^""]+("")";
                    content = Regex.Replace(content, pattern, $"$1{EscapeJsonString(newSource)}$2");
                }

                File.WriteAllText(ManifestPath, content, Encoding.UTF8);
                SaveSettings();

                UnityEditor.PackageManager.Client.Resolve();

                _hasUnsavedChanges = false;
                LoadManifest();

                Debug.Log("Package overrides applied successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to apply changes: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"変更の適用に失敗しました:\n{ex.Message}", "OK");
            }
        }

        private static string EscapeJsonString(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                    _data = JsonUtility.FromJson<PackageOverrideData>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load settings: {ex.Message}");
            }

            _data ??= new PackageOverrideData();
        }

        private void SaveSettings()
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(_data, true);
                File.WriteAllText(SettingsPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save settings: {ex.Message}");
            }
        }

        private PackageOverrideEntry GetOrCreateEntry(string packageName)
        {
            var entry = _data.entries.Find(e => e.packageName == packageName);
            if (entry == null)
            {
                entry = new PackageOverrideEntry { packageName = packageName };
                _data.entries.Add(entry);
            }
            return entry;
        }
    }

    [Serializable]
    internal class PackageOverrideEntry
    {
        public string packageName;
        public string originalSource;
        public string overridePath;
        public bool isOverridden;
    }

    [Serializable]
    internal class PackageOverrideData
    {
        public List<PackageOverrideEntry> entries = new List<PackageOverrideEntry>();
    }
}
