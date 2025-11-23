using System;
using System.Collections.Generic;
using Cards;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(DeckDefinition))]
    public class DeckDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _deckNameProp;
        private SerializedProperty _descriptionProp;
        private SerializedProperty _categoriesProp;

        private void OnEnable()
        {
            _deckNameProp = serializedObject.FindProperty("deckName");
            _descriptionProp = serializedObject.FindProperty("description");
            _categoriesProp = serializedObject.FindProperty("categories");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var deck = (DeckDefinition)target;

            DrawHeader(deck);

            GUILayout.Space(8);
            DrawSectionHeader("Deck Info");
            EditorGUILayout.PropertyField(_deckNameProp);
            EditorGUILayout.PropertyField(_descriptionProp);

            GUILayout.Space(8);
            DrawSectionHeader("Category Breakdown");
            DrawCategoryTools(deck);
            GUILayout.Space(4);
            DrawCategoriesList();

            GUILayout.Space(10);
            DrawTotals(deck);

            serializedObject.ApplyModifiedProperties();
        }

        // ───────────────────────────────────────────────────────────────
        // Header + section headers
        // ───────────────────────────────────────────────────────────────

        private void DrawHeader(DeckDefinition deck)
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(deck.deckName) ? "Deck Definition" : deck.deckName,
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 }
            );

            EditorGUILayout.LabelField("Puck'd Deck / Card Composition", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
        }

        private void DrawSectionHeader(string label)
        {
            var rect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.18f));

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

        // ───────────────────────────────────────────────────────────────
        // Category tools
        // ───────────────────────────────────────────────────────────────

        private void DrawCategoryTools(DeckDefinition deck)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Category"))
            {
                _categoriesProp.arraySize++;
            }

            if (GUILayout.Button("Add Missing Categories"))
            {
                AddMissingCategories(deck);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void AddMissingCategories(DeckDefinition deck)
        {
            var existing = new HashSet<CardCategory>();

            for (var i = 0; i < _categoriesProp.arraySize; i++)
            {
                var catProp = _categoriesProp.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("category");
                existing.Add((CardCategory)catProp.enumValueIndex);
            }

            foreach (CardCategory cat in Enum.GetValues(typeof(CardCategory)))
            {
                if (existing.Contains(cat))
                    continue;

                _categoriesProp.arraySize++;
                var newEntry = _categoriesProp.GetArrayElementAtIndex(_categoriesProp.arraySize - 1);
                newEntry.FindPropertyRelative("category").enumValueIndex = (int)cat;
                newEntry.FindPropertyRelative("cards").ClearArray();
            }
        }

        // ───────────────────────────────────────────────────────────────
        // Categories + card slots UI
        // ───────────────────────────────────────────────────────────────

        private void DrawCategoriesList()
        {
            if (_categoriesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No categories defined. Use 'Add Missing Categories' to scaffold from CardCategory enum.",
                    MessageType.Info);
                return;
            }

            for (var i = 0; i < _categoriesProp.arraySize; i++)
            {
                var entryProp = _categoriesProp.GetArrayElementAtIndex(i);
                DrawCategoryEntry(entryProp, i);
                GUILayout.Space(6);
            }
        }

        private void DrawCategoryEntry(SerializedProperty entryProp, int index)
        {
            var catProp = entryProp.FindPropertyRelative("category");
            var cardsProp = entryProp.FindPropertyRelative("cards");

            // Compute total for this category
            var totalForCategory = 0;
            for (var i = 0; i < cardsProp.arraySize; i++)
            {
                var slot = cardsProp.GetArrayElementAtIndex(i);
                var countProp = slot.FindPropertyRelative("count");
                totalForCategory += Mathf.Max(0, countProp.intValue);
            }

            // Header row
            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.BeginHorizontal();

            // Category dropdown
            EditorGUILayout.PropertyField(catProp, GUIContent.none, GUILayout.Width(140));

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Total: {totalForCategory}", GUILayout.Width(80));

            // Remove category
            GUI.backgroundColor = new Color(0.6f, 0.25f, 0.25f);
            if (GUILayout.Button("X", GUILayout.Width(24)))
            {
                _categoriesProp.DeleteArrayElementAtIndex(index);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Card slots
            DrawCardSlotsList(cardsProp);

            EditorGUILayout.EndVertical();
        }

        private void DrawCardSlotsList(SerializedProperty cardsProp)
        {
            // Each row: CardDefinition | Count | remove
            for (var i = 0; i < cardsProp.arraySize; i++)
            {
                var slotProp = cardsProp.GetArrayElementAtIndex(i);
                var cardProp = slotProp.FindPropertyRelative("card");
                var countProp = slotProp.FindPropertyRelative("count");

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.PropertyField(cardProp, GUIContent.none);

                GUILayout.Space(4);
                countProp.intValue = Mathf.Max(0, EditorGUILayout.IntField(countProp.intValue, GUILayout.Width(50)));

                GUI.backgroundColor = new Color(0.6f, 0.25f, 0.25f);
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    cardsProp.DeleteArrayElementAtIndex(i);
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            // Add button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Card Variant", GUILayout.Width(160)))
            {
                cardsProp.arraySize++;
                var newSlot = cardsProp.GetArrayElementAtIndex(cardsProp.arraySize - 1);
                newSlot.FindPropertyRelative("card").objectReferenceValue = null;
                newSlot.FindPropertyRelative("count").intValue = 1;
            }

            EditorGUILayout.EndHorizontal();
        }

        // ───────────────────────────────────────────────────────────────
        // Totals / summary
        // ───────────────────────────────────────────────────────────────

        private void DrawTotals(DeckDefinition deck)
        {
            var total = deck.TotalCardCount;

            EditorGUILayout.LabelField("Deck Totals", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.LabelField($"Total Cards in Deck: {total}");

            // Optional: show per-category summary
            if (deck.categories != null)
            {
                foreach (var cat in deck.categories)
                {
                    EditorGUILayout.LabelField($"{cat.category}: {cat.TotalCount} cards");
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}