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
        SerializedProperty autoConnectOnAwakeProperty;

        private void OnEnable() {
            hostProperty = serializedObject.FindProperty("serverHost");
            portProperty = serializedObject.FindProperty("serverPort");
            autoSearchEventHandlerProperty = serializedObject.FindProperty("autoSearchEventHandlers");
            eventKeepingGameObjectsProperty = serializedObject.FindProperty("eventKeepingGameObjects");
            autoConnectOnAwakeProperty = serializedObject.FindProperty("autoConnectOnAwake");
        }

        public override void OnInspectorGUI() {
            EditorGUILayout.PropertyField(hostProperty);
            EditorGUILayout.PropertyField(portProperty);
            EditorGUILayout.PropertyField(autoConnectOnAwakeProperty);
            EditorGUILayout.PropertyField(autoSearchEventHandlerProperty);

            if (!autoSearchEventHandlerProperty.boolValue) {
                EditorGUILayout.PropertyField(eventKeepingGameObjectsProperty, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}