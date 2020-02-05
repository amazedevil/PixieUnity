using UnityEditor;

namespace Pixie.Unity.Editor
{
    [CustomEditor(typeof(PXUnityClient))]
    public class PXUnityClientEditor : UnityEditor.Editor
    {
        SerializedProperty hostProperty;
        SerializedProperty portProperty;
        SerializedProperty autoSearchEventHandlerProperty;
        SerializedProperty eventKeepingGameObjectsProperty;

        private void OnEnable() {
            hostProperty = serializedObject.FindProperty("serverHost");
            portProperty = serializedObject.FindProperty("serverPort");
            autoSearchEventHandlerProperty = serializedObject.FindProperty("autoSearchEventHandlers");
            eventKeepingGameObjectsProperty = serializedObject.FindProperty("eventKeepingGameObjects");
        }

        public override void OnInspectorGUI() {
            EditorGUILayout.PropertyField(hostProperty);
            EditorGUILayout.PropertyField(portProperty);
            EditorGUILayout.PropertyField(autoSearchEventHandlerProperty);

            if (!autoSearchEventHandlerProperty.boolValue) {
                EditorGUILayout.PropertyField(eventKeepingGameObjectsProperty, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}