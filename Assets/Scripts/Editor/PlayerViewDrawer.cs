using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(PlayerView))]
public class PlayerViewDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var playerName = property.FindPropertyRelative("playerName");
        var cardCount = property.FindPropertyRelative("cardCount");
        var isEliminated = property.FindPropertyRelative("isEliminated");
        var cardNames = property.FindPropertyRelative("cardNames");

        var labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        // Draw foldout header
        property.isExpanded = EditorGUI.Foldout(labelRect, property.isExpanded,
            $"{playerName.stringValue} ({(isEliminated.boolValue ? "Eliminated" : $"{cardCount.intValue} cards")})");

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            // Draw cards list
            var cardsRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight,
                position.width, EditorGUIUtility.singleLineHeight);

            for (var i = 0; i < cardNames.arraySize; i++)
            {
                EditorGUI.LabelField(cardsRect, cardNames.GetArrayElementAtIndex(i).stringValue);
                cardsRect.y += EditorGUIUtility.singleLineHeight;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded) return height;

        var cardNames = property.FindPropertyRelative("cardNames");
        height += cardNames.arraySize * EditorGUIUtility.singleLineHeight;
        return height;
    }
}