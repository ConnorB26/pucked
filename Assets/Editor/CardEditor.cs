using System;
using System.Collections.Generic;
using System.Linq;
using Cards;
using Effects.Base;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(CardDefinition))]
    public class CardEditor : UnityEditor.Editor
    {
        private SerializedProperty _cardNameProp;
        private SerializedProperty _categoryProp;
        private SerializedProperty _artworkProp;
        private SerializedProperty _descriptionProp;
        private SerializedProperty _variationProp;
        private SerializedProperty _effectsProp;

        private ReorderableList _effectList;

        // -------------------------------------------------
        // Init
        // -------------------------------------------------
        private void OnEnable()
        {
            _cardNameProp = serializedObject.FindProperty("cardName");
            _categoryProp = serializedObject.FindProperty("category");
            _artworkProp = serializedObject.FindProperty("artwork");
            _descriptionProp = serializedObject.FindProperty("description");
            _variationProp = serializedObject.FindProperty("variationIndex");
            _effectsProp = serializedObject.FindProperty("effects");

            BuildEffectList();
        }

        // -------------------------------------------------
        // Effect type discovery (robust)
        // -------------------------------------------------
        private static List<Type> GetAllEffectTypes()
        {
            var result = new List<Type>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] typesInAsm;
                try
                {
                    typesInAsm = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var t in typesInAsm)
                {
                    if (t == null) continue;
                    if (t.IsAbstract) continue;
                    if (!typeof(CardEffect).IsAssignableFrom(t)) continue;
                    if (t == typeof(CardEffect)) continue;

                    result.Add(t);
                }
            }

            return result.OrderBy(t => t.Name).ToList();
        }

        // -------------------------------------------------
        // Reorderable effects list
        // -------------------------------------------------
        private void BuildEffectList()
        {
            _effectList = new ReorderableList(serializedObject, _effectsProp, true, true, true, true);

            _effectList.drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Effects (executed in order)"); };

            _effectList.drawElementCallback = (rect, index, _, _) =>
            {
                EditorGUI.PropertyField(rect, _effectsProp.GetArrayElementAtIndex(index), GUIContent.none);
            };

            _effectList.onAddDropdownCallback = (_, _) =>
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Add Existing Effect"), false, () =>
                {
                    ShowExistingEffectSearch(effect =>
                    {
                        _effectsProp.arraySize++;
                        _effectsProp.GetArrayElementAtIndex(_effectsProp.arraySize - 1).objectReferenceValue = effect;
                        serializedObject.ApplyModifiedProperties();
                    });
                });

                menu.AddItem(new GUIContent("Create New Effect"), false, () => { ShowCreateEffectWindow(); });

                menu.ShowAsContext();
            };
        }

        // -------------------------------------------------
        // Search existing effects popup
        // -------------------------------------------------
        private void ShowExistingEffectSearch(Action<CardEffect> onSelect)
        {
            var guids = AssetDatabase.FindAssets("t:CardEffect");
            var items = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<CardEffect>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(e => e != null)
                .ToList();

            EffectSearchWindow.Init("Select Effect", items, onSelect);
        }

        // -------------------------------------------------
        // Create new effect popup
        // -------------------------------------------------
        private void ShowCreateEffectWindow()
        {
            var types = GetAllEffectTypes();
            EffectTypeWindow.Init(types, type =>
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    $"Create {type.Name}",
                    type.Name,
                    "asset",
                    "Save new effect asset"
                );

                if (string.IsNullOrEmpty(path))
                    return;

                var newEffect = (CardEffect)CreateInstance(type);
                AssetDatabase.CreateAsset(newEffect, path);
                AssetDatabase.SaveAssets();

                _effectsProp.arraySize++;
                _effectsProp.GetArrayElementAtIndex(_effectsProp.arraySize - 1).objectReferenceValue = newEffect;
                serializedObject.ApplyModifiedProperties();

                Selection.activeObject = newEffect;
            });
        }

        // -------------------------------------------------
        // Inspector
        // -------------------------------------------------
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();

            GUILayout.Space(8);
            DrawSectionHeader("Card Info");
            EditorGUILayout.PropertyField(_cardNameProp);
            EditorGUILayout.PropertyField(_categoryProp);

            GUILayout.Space(8);
            DrawSectionHeader("Artwork");
            EditorGUILayout.PropertyField(_artworkProp);

            GUILayout.Space(8);
            DrawSectionHeader("Description");
            EditorGUILayout.PropertyField(_descriptionProp, GUILayout.Height(55));

            GUILayout.Space(8);
            DrawSectionHeader("Variation");
            EditorGUILayout.PropertyField(_variationProp);
            if (GUILayout.Button("Clone as Variation"))
            {
                CloneVariation((CardDefinition)target);
            }

            GUILayout.Space(10);
            DrawSectionHeader("Effects");
            _effectList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        // -------------------------------------------------
        // Top header
        // -------------------------------------------------
        private void DrawHeader()
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(_cardNameProp.stringValue) ? "Untitled Card" : _cardNameProp.stringValue,
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 }
            );

            EditorGUILayout.LabelField("Card Definition", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
        }

        // -------------------------------------------------
        // Pro-style section header bar
        // -------------------------------------------------
        private void DrawSectionHeader(string label)
        {
            var rect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.18f)); // dark bar

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.85f, 0.85f, 0.9f) },
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13
            };

            rect.x += 6;
            EditorGUI.LabelField(rect, label, style);
            GUILayout.Space(4);
        }

        // -------------------------------------------------
        // Clone as variation
        // -------------------------------------------------
        private void CloneVariation(CardDefinition card)
        {
            var originalPath = AssetDatabase.GetAssetPath(card);
            var newPath = AssetDatabase.GenerateUniqueAssetPath(originalPath.Replace(".asset", "_Var.asset"));

            var clone = Instantiate(card);
            clone.cardName = card.cardName + " (Var)";

            AssetDatabase.CreateAsset(clone, newPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = clone;
        }

        // -------------------------------------------------
        // SearchWindow for existing effects
        // -------------------------------------------------
        public class EffectSearchWindow : EditorWindow
        {
            private List<CardEffect> _items;
            private Action<CardEffect> _onSelect;
            private string _search = "";

            public static void Init(string title, List<CardEffect> items, Action<CardEffect> onSelect)
            {
                var wnd = CreateInstance<EffectSearchWindow>();
                wnd.titleContent = new GUIContent(title);
                wnd._items = items;
                wnd._onSelect = onSelect;
                wnd.ShowUtility();
            }

            private void OnGUI()
            {
                GUILayout.Label("Search", EditorStyles.boldLabel);
                _search = EditorGUILayout.TextField(_search);
                GUILayout.Space(4);

                foreach (var item in _items)
                {
                    if (!string.IsNullOrEmpty(_search) &&
                        !item.name.ToLower().Contains(_search.ToLower()))
                        continue;

                    if (GUILayout.Button(item.name))
                    {
                        _onSelect?.Invoke(item);
                        Close();
                    }
                }
            }
        }

        // -------------------------------------------------
        // EffectTypeWindow for creating new effect assets
        // -------------------------------------------------
        public class EffectTypeWindow : EditorWindow
        {
            private List<Type> _types;
            private Action<Type> _onSelect;
            private string _search = "";

            public static void Init(List<Type> types, Action<Type> onSelect)
            {
                var wnd = CreateInstance<EffectTypeWindow>();
                wnd.titleContent = new GUIContent("Create Effect");
                wnd._types = types ?? new List<Type>();
                wnd._onSelect = onSelect;
                wnd.ShowUtility();
            }

            private void OnGUI()
            {
                GUILayout.Label("Select Effect Type", EditorStyles.boldLabel);

                if (_types == null || _types.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No concrete CardEffect subclasses were found.\n" +
                        "Make sure your effect scripts inherit CardEffect " +
                        "and compile without errors.",
                        MessageType.Warning);
                    return;
                }

                _search = EditorGUILayout.TextField("Search", _search);
                GUILayout.Space(4);

                foreach (var t in _types)
                {
                    if (!string.IsNullOrEmpty(_search) &&
                        !t.Name.ToLower().Contains(_search.ToLower()))
                        continue;

                    if (GUILayout.Button(t.Name))
                    {
                        _onSelect?.Invoke(t);
                        Close();
                    }
                }
            }
        }
    }
}