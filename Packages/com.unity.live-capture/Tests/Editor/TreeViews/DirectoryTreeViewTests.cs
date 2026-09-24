using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Unity.LiveCapture.Editor;
#if UNITY_6000_5_OR_NEWER
using TreeViewId = UnityEngine.EntityId;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<UnityEngine.EntityId>;
#elif UNITY_6000_3_OR_NEWER
using TreeViewId = System.Int32;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#else
using TreeViewId = System.Int32;
#endif

namespace Unity.LiveCapture.Tests.Editor
{
    public class DirectoryTreeViewTests
    {
        const string k_Folder = "Assets/LiveCaptureDirectoryTreeViewTest";

        [Test]
        public void SelectingFolder_FindsTakeAfterReload()
        {
            AssetDatabase.CreateFolder("Assets", "LiveCaptureDirectoryTreeViewTest");
            var take = ScriptableObject.CreateInstance<Take>();
            AssetDatabase.CreateAsset(take, k_Folder + "/TestTake.asset");
            try
            {
                var state = new TreeViewState();
                var tree = new DirectoryTreeViewImpl(state);
                var assetsFolder = AssetDatabase.LoadMainAssetAtPath("Assets");
#if UNITY_6000_5_OR_NEWER
                TreeViewId assetsId = assetsFolder.GetEntityId();
#else
                TreeViewId assetsId = assetsFolder.GetInstanceID();
#endif
                state.expandedIDs.Add(assetsId);
                tree.Reload();
                var folder = AssetDatabase.LoadMainAssetAtPath(k_Folder);
#if UNITY_6000_5_OR_NEWER
                TreeViewId id = folder.GetEntityId();
#else
                TreeViewId id = folder.GetInstanceID();
#endif
                tree.SetSelection(new List<TreeViewId> { id }, TreeViewSelectionOptions.FireSelectionChanged);
                Assert.That(tree.SelectedTakes, Does.Contain(take));
                var serializedState = EditorJsonUtility.ToJson(state);
                var restoredState = new TreeViewState();
                EditorJsonUtility.FromJsonOverwrite(serializedState, restoredState);
                Assert.That(restoredState.expandedIDs, Does.Contain(assetsId));
            }
            finally
            {
                AssetDatabase.DeleteAsset(k_Folder);
            }
        }
    }
}
