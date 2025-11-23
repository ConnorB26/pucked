#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DeckList))]
public class DeckListEditor : Editor
{
    SerializedProperty entriesProp;

    void OnEnable() => entriesProp = serializedObject.FindProperty("entries");

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(entriesProp, includeChildren: true);

        var list = (DeckList)target;
        EditorGUILayout.Space(8);
        DrawValidation(list);

        EditorGUILayout.Space(8);
        DrawUtilities(list);

        serializedObject.ApplyModifiedProperties();
    }

    void DrawValidation(DeckList list)
    {
        int total = list.TotalCards;
        int puckd = list.CountByType(CardType.Puckd);
        int save = list.CountByType(CardType.Save);

        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Total Cards: {total}");

        var byType = list.entries.Where(e => e.card && e.count > 0)
            .GroupBy(e => e.card.type)
            .Select(g => $"{g.Key}: {g.Sum(e => e.count)}").ToArray();
        EditorGUILayout.LabelField("By Type: " + (byType.Length == 0 ? "—" : string.Join(", ", byType)));

        if (total == 0)
            EditorGUILayout.HelpBox("Deck is empty.", MessageType.Error);
        if (puckd == 0)
            EditorGUILayout.HelpBox("No Puck’d cards present—game may never end.", MessageType.Warning);
        if (save == 0)
            EditorGUILayout.HelpBox("No Save cards present—first Puck’d eliminates with no counter.",
                MessageType.Warning);
    }

    void DrawUtilities(DeckList list)
    {
        EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

        if (GUILayout.Button("Remove Null/Zero Entries"))
        {
            Undo.RecordObject(list, "Clean Deck");
            list.entries = list.entries.Where(e => e.card && e.count > 0).ToList();
            EditorUtility.SetDirty(list);
        }

        if (GUILayout.Button("Sort by Type then Name"))
        {
            Undo.RecordObject(list, "Sort Deck");
            list.entries = list.entries.Where(e => e.card)
                .OrderBy(e => e.card.type)
                .ThenBy(e => e.card.cardName)
                .ToList();
            EditorUtility.SetDirty(list);
        }
    }
}
#endif