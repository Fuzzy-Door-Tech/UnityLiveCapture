using System;
using System.Reflection;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace Unity.LiveCapture.Editor
{
    static class TreeViewExtensions
    {
#if UNITY_6000_3_OR_NEWER
        static class TreeViewProperty<TIdentifier> where TIdentifier : struct
        {
            public static readonly PropertyInfo DeselectOnUnhandledMouseDown = typeof(TreeView<TIdentifier>)
                .GetProperty("deselectOnUnhandledMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static void DeselectOnUnhandledMouseDown<TIdentifier>(this TreeView<TIdentifier> treeView, bool value)
            where TIdentifier : struct
        {
            if (treeView == null)
            {
                throw new ArgumentNullException(nameof(treeView));
            }

            Debug.Assert(TreeViewProperty<TIdentifier>.DeselectOnUnhandledMouseDown != null);

            TreeViewProperty<TIdentifier>.DeselectOnUnhandledMouseDown.SetValue(treeView, value);
        }
#else
        static readonly PropertyInfo s_DeselectOnUnhandledMouseDown = typeof(TreeView)
            .GetProperty("deselectOnUnhandledMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void DeselectOnUnhandledMouseDown(this TreeView treeView, bool value)
        {
            if (treeView == null)
            {
                throw new ArgumentNullException(nameof(treeView));
            }

            Debug.Assert(s_DeselectOnUnhandledMouseDown != null);

            s_DeselectOnUnhandledMouseDown.SetValue(treeView, value);
        }
#endif
    }
}
