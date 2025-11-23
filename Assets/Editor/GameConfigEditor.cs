#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameConfig))]
public class GameConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var cfg = (GameConfig)target;

        if (cfg.startingHandSize < 1)
            EditorGUILayout.HelpBox("Starting hand size should be at least 1.", MessageType.Warning);

        if (cfg.reactionSeconds is > 0 and < 2)
            EditorGUILayout.HelpBox("Reaction window below 2 seconds may feel too tight for online play.",
                MessageType.Info);

        if (cfg.useFixedSeed)
            EditorGUILayout.HelpBox($"Using fixed seed: {cfg.seed} (deterministic for tests).", MessageType.None);
    }
}
#endif