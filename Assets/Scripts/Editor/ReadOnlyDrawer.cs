using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Save previous GUI enabled value
        var previousGUIState = GUI.enabled;

        // Disable edit for property
        GUI.enabled = false;

        // Draw property
        EditorGUI.PropertyField(position, property, label);

        // Restore previous GUI enabled value
        GUI.enabled = previousGUIState;
    }
}