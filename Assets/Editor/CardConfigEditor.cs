#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardConfig))]
public class CardConfigEditor : Editor
{
    SerializedProperty defId, cardName, type, description, artwork, targetMode, canBeCountered;
    SerializedProperty attack, peek;

    void OnEnable()
    {
        defId = serializedObject.FindProperty("defId");
        cardName = serializedObject.FindProperty("cardName");
        type = serializedObject.FindProperty("type");
        description = serializedObject.FindProperty("description");
        artwork = serializedObject.FindProperty("artwork");
        targetMode = serializedObject.FindProperty("targetMode");
        canBeCountered = serializedObject.FindProperty("canBeCountered");
        attack = serializedObject.FindProperty("attack");
        peek = serializedObject.FindProperty("peek");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(defId);
        EditorGUILayout.PropertyField(cardName);
        EditorGUILayout.PropertyField(type);
        EditorGUILayout.PropertyField(description);
        EditorGUILayout.PropertyField(artwork);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Play & Counter", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(targetMode);
        using (new EditorGUI.DisabledScope(((CardType)type.enumValueIndex) == CardType.Puckd ||
                                           ((CardType)type.enumValueIndex) == CardType.Save))
        {
            EditorGUILayout.PropertyField(canBeCountered);
        }

        // Type-specific params
        EditorGUILayout.Space(8);
        DrawTypeParams();

        // Hints & validation
        EditorGUILayout.Space(10);
        DrawValidation();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawTypeParams()
    {
        var t = (CardType)type.enumValueIndex;
        if (t == CardType.Attack)
        {
            EditorGUILayout.LabelField("Attack Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(attack.FindPropertyRelative("extraTurns"), new GUIContent("Extra Turns"));
        }
        else if (t == CardType.Peek)
        {
            EditorGUILayout.LabelField("Peek Parameters", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(peek.FindPropertyRelative("count"), new GUIContent("Count"));
        }
        else
        {
            EditorGUILayout.HelpBox("No extra parameters for this type.", MessageType.None);
        }
    }

    void DrawValidation()
    {
        var t = (CardType)type.enumValueIndex;
        var cfg = (CardConfig)target;

        if (string.IsNullOrWhiteSpace(cfg.cardName))
            EditorGUILayout.HelpBox("Card name is empty.", MessageType.Warning);

        if (!cfg.artwork)
            EditorGUILayout.HelpBox("Artwork is missing.", MessageType.Info);

        if (t == CardType.Puckd && cfg.targetMode != TargetMode.None)
            EditorGUILayout.HelpBox("Puck’d is a draw-trigger card; TargetMode should be None.", MessageType.Warning);

        if (t == CardType.Save && cfg.targetMode != TargetMode.None)
            EditorGUILayout.HelpBox("Save is a reaction card; TargetMode should be None.", MessageType.Warning);

        if (t == CardType.Attack && cfg.attack.extraTurns <= 0)
            EditorGUILayout.HelpBox("Attack extra turns should be >= 1.", MessageType.Warning);

        if (t == CardType.Peek && cfg.peek.count <= 0)
            EditorGUILayout.HelpBox("Peek count should be >= 1.", MessageType.Warning);
    }
}
#endif