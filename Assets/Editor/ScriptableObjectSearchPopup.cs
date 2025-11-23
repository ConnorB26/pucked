using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class ScriptableObjectSearchPopup<T> : EditorWindow where T : ScriptableObject
    {
        private List<T> _assets;
        private Action<T> _onSelect;
        private string _search = "";

        public static void Open(Action<T> callback)
        {
            var wnd = CreateInstance<ScriptableObjectSearchPopup<T>>();
            wnd._onSelect = callback;
            wnd.titleContent = new GUIContent(typeof(T).Name + " Search");
            wnd.ShowUtility();
        }

        private void OnEnable()
        {
            _assets = AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Search", EditorStyles.boldLabel);
            _search = EditorGUILayout.TextField(_search);

            EditorGUILayout.Space();

            foreach (var asset in _assets)
            {
                if (!string.IsNullOrEmpty(_search) && !asset.name.ToLower().Contains(_search.ToLower()))
                    continue;

                if (GUILayout.Button(asset.name))
                    _onSelect?.Invoke(asset);
            }
        }
    }
}