using System;
using System.Reflection;
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

            var property = TreeViewProperty<TIdentifier>.DeselectOnUnhandledMouseDown;
            if (property == null)
            {
                throw new MissingMemberException(
                    typeof(TreeView<TIdentifier>).FullName,
                    "deselectOnUnhandledMouseDown");
            }

            property.SetValue(treeView, value);
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

            if (s_DeselectOnUnhandledMouseDown == null)
            {
                throw new MissingMemberException(
                    typeof(TreeView).FullName,
                    "deselectOnUnhandledMouseDown");
            }

            s_DeselectOnUnhandledMouseDown.SetValue(treeView, value);
        }
#endif
    }
}
