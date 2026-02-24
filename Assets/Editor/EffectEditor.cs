using System;
using System.Collections.Generic;
using System.Linq;
using Cards;
using Effects.Base;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>Custom inspector for CardEffect subclasses. Draws the effect's description, all serialized fields, and create/duplicate buttons.</summary>
    [CustomEditor(typeof(CardEffect), true)]
    public class EffectEditorFull : UnityEditor.Editor
    {
        private SerializedProperty _descriptionProp;

        private void OnEnable()
        {
            _descriptionProp = serializedObject.FindProperty("description");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var effect = (CardEffect)target;

            DrawHeader(effect);
            DrawDescription();
            DrawEffectFields(effect);
            DrawButtons(effect);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader(CardEffect effect)
        {
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField(effect.name,
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleLeft
                });

            EditorGUILayout.LabelField(
                effect.GetType().Name,
                EditorStyles.miniLabel
            );

            EditorGUILayout.Space(10);
        }

        private void DrawDescription()
        {
            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_descriptionProp, GUIContent.none);
            EditorGUILayout.Space(10);
        }

        private void DrawEffectFields(CardEffect effect)
        {
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;

            EditorGUILayout.LabelField("Effect Properties", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("HelpBox");

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.name == "m_Script" || iterator.name == "description")
                    continue;

                EditorGUILayout.PropertyField(iterator, true);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawButtons(CardEffect effect)
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.75f, 1f, 0.75f);
            if (GUILayout.Button("Create Card Using This Effect", GUILayout.Height(25)))
                CreateCardFromEffect(effect);

            GUI.backgroundColor = new Color(1f, 0.85f, 0.85f);
            if (GUILayout.Button("Duplicate Effect", GUILayout.Height(25)))
                DuplicateEffect(effect);

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Create New Effect...", GUILayout.Height(25)))
                ShowCreateEffectMenu();
        }


        private void CreateCardFromEffect(CardEffect effect)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Card",
                "NewCard",
                "asset",
                "Save new card asset"
            );

            if (string.IsNullOrEmpty(path))
                return;

            var card = CreateInstance<CardDefinition>();
            card.cardName = "New Card";
            card.effects = new List<CardEffect> { effect };

            AssetDatabase.CreateAsset(card, path);
            AssetDatabase.SaveAssets();

            Selection.activeObject = card;
        }

        private void DuplicateEffect(CardEffect effect)
        {
            var originalPath = AssetDatabase.GetAssetPath(effect);
            var newPath = AssetDatabase.GenerateUniqueAssetPath(originalPath);

            var clone = Instantiate(effect);
            AssetDatabase.CreateAsset(clone, newPath);
            AssetDatabase.SaveAssets();

            Selection.activeObject = clone;
        }

        private List<Type> GetEffectTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    typeof(CardEffect).IsAssignableFrom(t) &&
                    !t.IsAbstract &&
                    t != typeof(CardEffect))
                .OrderBy(t => t.Name)
                .ToList();
        }

        private void ShowCreateEffectMenu()
        {
            var menu = new GenericMenu();
            var types = GetEffectTypes();

            foreach (var type in types)
            {
                menu.AddItem(new GUIContent(type.Name), false, () =>
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

                    Selection.activeObject = newEffect;
                });
            }

            menu.ShowAsContext();
        }
    }
}