using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

#if UNITY_6000_5_OR_NEWER
using TreeViewId = UnityEngine.EntityId;
using TreeView = UnityEditor.IMGUI.Controls.TreeView<UnityEngine.EntityId>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<UnityEngine.EntityId>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<UnityEngine.EntityId>;
#elif UNITY_6000_3_OR_NEWER
using TreeViewId = System.Int32;
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
using TreeViewId = System.Int32;
#endif

namespace Unity.LiveCapture.Editor
{
    [Serializable]
    class DirectoryTreeView
    {
        [SerializeField]
        TreeViewState m_TreeViewState = new TreeViewState();

        DirectoryTreeViewImpl m_Impl;

        public Take[] SelectedTakes => m_Impl.SelectedTakes;

        public void OnGUI(Rect rect)
        {
            InitializeIfNeeded();

            Debug.Assert(m_Impl != null);

            m_Impl.OnGUI(rect);
        }

        public void Reload()
        {
            InitializeIfNeeded();

            Debug.Assert(m_Impl != null);

            m_Impl.Reload();
            m_Impl.ReloadSelection();
        }

        void InitializeIfNeeded()
        {
            if (m_Impl == null)
            {
                m_Impl = new DirectoryTreeViewImpl(m_TreeViewState);
            }
        }
    }

    class DirectoryTreeViewImpl : TreeView
    {

        static readonly TreeViewId k_RootId = default(TreeViewId);

        static class Contents
        {
            public static readonly GUIContent FolderIcon = EditorGUIUtility.TrIconContent("Folder Icon");
            public static readonly GUIContent FolderEmptyIcon = EditorGUIUtility.TrIconContent("FolderEmpty Icon");
        }

        HashSet<string> m_AncestorPaths;
        HashSet<string> m_DirectoriesWithAssets;
        HashSet<string> m_DirectoriesWithChildren;
        HashSet<string> m_DirectoryLeafs;

        public Take[] SelectedTakes { get; private set; }

        public DirectoryTreeViewImpl(TreeViewState treeViewState) : base(treeViewState)
        {
            useScrollView = true;
            this.DeselectOnUnhandledMouseDown(true);
            Reload();
            ReloadSelection();
        }

        protected override TreeViewItem BuildRoot()
        {
            var paths = AssetDatabase.FindAssets($"t:{typeof(Take).Name}")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .ToArray();

            m_DirectoriesWithAssets = new HashSet<string>(
                paths
                    .Select(p => Path.GetDirectoryName(p))
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(NormalizePath));
            m_DirectoryLeafs = new HashSet<string>(EnumerateLeafs(m_DirectoriesWithAssets));
            m_AncestorPaths = new HashSet<string>(
                m_DirectoriesWithAssets.SelectMany(EnumerateAncestors));
            m_DirectoriesWithChildren = new HashSet<string>(
                m_AncestorPaths.Select(GetParentDirectory).Where(p => !string.IsNullOrEmpty(p)));

            var root = new TreeViewItem(k_RootId, -1, "Root");

            return root;
        }

        IEnumerable<string> EnumerateLeafs(IEnumerable<string> directories)
        {
            var leaf = string.Empty;

            foreach (var dir in directories.OrderByDescending(d => d))
            {
                if (!leaf.StartsWith(dir))
                {
                    leaf = dir;

                    yield return dir;
                }
            }
        }

        static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        static string GetParentDirectory(string path)
        {
            var separator = path.LastIndexOf('/');

            return separator < 0 ? string.Empty : path.Substring(0, separator);
        }

        static int GetDepth(string path)
        {
            return path.Count(c => c == '/');
        }

        static IEnumerable<string> EnumerateAncestors(string directory)
        {
            var path = NormalizePath(directory);

            while (!string.IsNullOrEmpty(path))
            {
                yield return path;

                if (path == "Assets")
                {
                    yield break;
                }

                path = GetParentDirectory(path);
            }
        }
        static TreeViewId GetTreeViewId(string path)
        {
#if UNITY_6000_5_OR_NEWER
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            return asset != null ? asset.GetEntityId() : default;
#else
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            return asset != null ? asset.GetInstanceID() : default;
#endif
        }

        static string GetAssetPath(TreeViewId id)
        {
#if UNITY_6000_5_OR_NEWER
            return AssetDatabase.GetAssetPath(EditorUtility.EntityIdToObject(id));
#else
            return AssetDatabase.GetAssetPath(id);
#endif
        }

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var expandedPaths = new HashSet<string>(
                state.expandedIDs
                    .Select(id => GetAssetPath(id))
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(NormalizePath));
            var items = new List<TreeViewItem>(m_AncestorPaths.Count);

            foreach (var path in m_AncestorPaths
                .OrderBy(GetDepth)
                .ThenBy(p => p, StringComparer.Ordinal))
            {
                var parent = GetParentDirectory(path);

                if (path != "Assets" && !expandedPaths.Contains(parent))
                {
                    continue;
                }

                var item = new TreeViewItem(
                    GetTreeViewId(path),
                    GetDepth(path),
                    path == "Assets" ? "Assets" : Path.GetFileName(path));

                item.icon = m_DirectoriesWithAssets.Contains(path)
                    ? Contents.FolderIcon.image as Texture2D
                    : Contents.FolderEmptyIcon.image as Texture2D;

                if (m_DirectoriesWithChildren.Contains(path)
                    && !m_DirectoryLeafs.Contains(path))
                {
                    // Add a dummy child so the row shows a collapse arrow before its children are fetched.
                    item.AddChild(null);
                }

                items.Add(item);
            }

            SetupParentsAndChildrenFromDepths(root, items);

            return items;
        }

        public void ReloadSelection()
        {
            SelectionChanged(GetSelection());
        }

        protected override void SelectionChanged(IList<TreeViewId> selectedIds)
        {
            SelectedTakes = null;

            if (selectedIds.Count > 0)
            {
                var items = FindRows(SortItemIDsInRowOrder(selectedIds));

                SelectedTakes = items
                    .Select(i => GetAssetPath(i.id))
                    .SelectMany(p => AssetDatabaseUtility.GetAssetsAtPath<Take>(p, false))
                    .ToArray();
            }
        }
    }
}
