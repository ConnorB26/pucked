using System;
using System.Collections.Generic;
using Cards;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>Custom inspector for DeckDefinition. Shows deck composition, save card rules, and a live totals summary.</summary>
    [CustomEditor(typeof(DeckDefinition))]
    public class DeckDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _deckNameProp;
        private SerializedProperty _descriptionProp;
        private SerializedProperty _saveCategoryProp;
        private SerializedProperty _extraSaveRatioProp;
        private SerializedProperty _saveVariantsProp;
        private SerializedProperty _categoriesProp;

        private void OnEnable()
        {
            _deckNameProp = serializedObject.FindProperty("deckName");
            _descriptionProp = serializedObject.FindProperty("description");
            _saveCategoryProp = serializedObject.FindProperty("saveCategory");
            _extraSaveRatioProp = serializedObject.FindProperty("extraSavesPerPlayerRatio");
            _saveVariantsProp = serializedObject.FindProperty("saveVariants");
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
            DrawSectionHeader("Save Card Rules (Defuse / Goalie Save)");
            DrawSaveRulesSection(deck);

            GUILayout.Space(8);
            DrawSectionHeader("Non-Save Card Composition");
            DrawCategoryTools(deck);
            GUILayout.Space(4);
            DrawCategoriesList(deck);

            GUILayout.Space(10);
            DrawTotals(deck);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader(DeckDefinition deck)
        {
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(deck.deckName)
                    ? "Deck Definition"
                    : deck.deckName,
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 });

            EditorGUILayout.LabelField("Puck'd Deck Rules & Composition", EditorStyles.miniLabel);
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

        private void DrawSaveRulesSection(DeckDefinition deck)
        {
            EditorGUILayout.PropertyField(_saveCategoryProp);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Extra Saves / Player (ratio)", GUILayout.Width(180));
            _extraSaveRatioProp.floatValue = Mathf.Max(
                0f,
                EditorGUILayout.FloatField(_extraSaveRatioProp.floatValue));
            EditorGUILayout.EndHorizontal();

            // Example save counts for 2-5 players
            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.LabelField("Save Count Examples", EditorStyles.boldLabel);

            for (var players = 2; players <= 5; players++)
            {
                var expected = deck.GetExpectedSaveCount(players);
                var extras = Mathf.Max(0, expected - players);
                EditorGUILayout.LabelField(
                    $"{players} players → {expected} saves " +
                    $"({players} guaranteed + {extras} extra)");
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(4);
            DrawSaveVariantsList(deck);
        }

        private void DrawSaveVariantsList(DeckDefinition deck)
        {
            EditorGUILayout.LabelField("Save Variants", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each variant gets a percentage chance when generating save cards.\n" +
                "All weights should sum to 100% or less.",
                MessageType.Info);

            var totalWeight = 0f;

            // Draw list & accumulate total weight in this frame
            for (var i = 0; i < _saveVariantsProp.arraySize; i++)
            {
                var variantProp = _saveVariantsProp.GetArrayElementAtIndex(i);
                var cardProp = variantProp.FindPropertyRelative("card");
                var weightProp = variantProp.FindPropertyRelative("weight");

                EditorGUILayout.BeginHorizontal("HelpBox");

                EditorGUILayout.PropertyField(cardProp, GUIContent.none);

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Weight %", GUILayout.Width(60));
                weightProp.floatValue = Mathf.Clamp(
                    EditorGUILayout.FloatField(weightProp.floatValue, GUILayout.Width(60)),
                    0f, 100f);

                totalWeight += weightProp.floatValue;

                GUILayout.FlexibleSpace();

                GUI.backgroundColor = new Color(0.6f, 0.25f, 0.25f);
                if (GUILayout.Button("X", GUILayout.Width(24)))
                {
                    _saveVariantsProp.DeleteArrayElementAtIndex(i);
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
            if (GUILayout.Button("+ Add Save Variant", GUILayout.Width(170)))
            {
                _saveVariantsProp.arraySize++;
                var newVariant = _saveVariantsProp.GetArrayElementAtIndex(_saveVariantsProp.arraySize - 1);
                newVariant.FindPropertyRelative("card").objectReferenceValue = null;
                newVariant.FindPropertyRelative("weight").floatValue = 0f;
            }

            EditorGUILayout.EndHorizontal();

            // Validation / summary
            GUILayout.Space(4);

            if (_saveVariantsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No save variants configured. The deck builder will not be able to generate save cards.",
                    MessageType.Warning);
            }
            else
            {
                var msg = $"Total save variant weight: {totalWeight:0.#}%";
                if (totalWeight > 100f)
                {
                    EditorGUILayout.HelpBox(
                        msg + " (must be ≤ 100%)",
                        MessageType.Error);
                }
                else if (Mathf.Approximately(totalWeight, 0f))
                {
                    EditorGUILayout.HelpBox(
                        msg + " (all 0% – no variant will ever be picked).",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox(msg, MessageType.Info);
                }
            }
        }

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

            EditorGUILayout.HelpBox(
                $"Save Category is set to '{deck.saveCategory}'. " +
                "Any card in that category should be configured as a Save Variant above, " +
                "not in this Non-Save section.",
                MessageType.Info);
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

        private void DrawCategoriesList(DeckDefinition deck)
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
                DrawCategoryEntry(deck, entryProp, i);
                GUILayout.Space(6);
            }
        }

        private void DrawCategoryEntry(DeckDefinition deck, SerializedProperty entryProp, int index)
        {
            var catProp = entryProp.FindPropertyRelative("category");
            var cardsProp = entryProp.FindPropertyRelative("cards");

            var categoryEnum = (CardCategory)catProp.enumValueIndex;

            // Compute total for this category
            var totalForCategory = 0;
            for (var i = 0; i < cardsProp.arraySize; i++)
            {
                var slot = cardsProp.GetArrayElementAtIndex(i);
                var countProp = slot.FindPropertyRelative("count");
                totalForCategory += Mathf.Max(0, countProp.intValue);
            }

            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(catProp, GUIContent.none, GUILayout.Width(140));

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Total: {totalForCategory}", GUILayout.Width(80));

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

            // Warning if this category is actually the save category
            if (categoryEnum == deck.saveCategory)
            {
                EditorGUILayout.HelpBox(
                    "This category matches the Save Category and will be ignored by the deck builder.\n" +
                    "Configure save cards in the 'Save Card Rules' section instead.",
                    MessageType.Warning);
            }

            GUILayout.Space(4);
            DrawCardSlotsList(deck, cardsProp);

            EditorGUILayout.EndVertical();
        }

        private void DrawCardSlotsList(DeckDefinition deck, SerializedProperty cardsProp)
        {
            for (var i = 0; i < cardsProp.arraySize; i++)
            {
                var slotProp = cardsProp.GetArrayElementAtIndex(i);
                var cardProp = slotProp.FindPropertyRelative("card");
                var countProp = slotProp.FindPropertyRelative("count");

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.PropertyField(cardProp, GUIContent.none);

                GUILayout.Space(4);
                countProp.intValue = Mathf.Max(
                    0,
                    EditorGUILayout.IntField(countProp.intValue, GUILayout.Width(50)));

                var card = cardProp.objectReferenceValue as CardDefinition;
                if (card != null && card.category == deck.saveCategory)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("(save card – ignored here)", GUILayout.Width(170));
                }

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

        private void DrawTotals(DeckDefinition deck)
        {
            EditorGUILayout.LabelField("Deck Totals / Examples", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("HelpBox");

            EditorGUILayout.LabelField($"Base (non-save) cards: {deck.TotalBaseCardCount}");

            for (var players = 2; players <= 5; players++)
            {
                var saves = deck.GetExpectedSaveCount(players);
                var total = deck.GetExpectedTotalCardCount(players);

                EditorGUILayout.LabelField(
                    $"{players} players → {total} total cards ({saves} saves)");
            }

            EditorGUILayout.EndVertical();
        }
    }
}