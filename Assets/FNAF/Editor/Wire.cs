using UnityEditor;
using UnityEngine;

namespace FNAF.EditorTools
{
    /// <summary>场景生成器的序列化字段接线助手（绕过 private [SerializeField] 限制）。</summary>
    internal static class Wire
    {
        internal static void Ref(Component c, string field, Object value)
        {
            SerializedObject so = new SerializedObject(c);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Float(Component c, string field, float v)
        {
            SerializedObject so = new SerializedObject(c);
            so.FindProperty(field).floatValue = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Int(Component c, string field, int v)
        {
            SerializedObject so = new SerializedObject(c);
            so.FindProperty(field).intValue = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Bool(Component c, string field, bool v)
        {
            SerializedObject so = new SerializedObject(c);
            so.FindProperty(field).boolValue = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Str(Component c, string field, string v)
        {
            SerializedObject so = new SerializedObject(c);
            so.FindProperty(field).stringValue = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void ColorField(Component c, string field, Color v)
        {
            SerializedObject so = new SerializedObject(c);
            so.FindProperty(field).colorValue = v;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void KeyCodeField(Component c, string field, KeyCode key)
        {
            SerializedObject so = new SerializedObject(c);
            so.FindProperty(field).intValue = (int)key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>把 Object 数组字段整体写入（如 JumpscareController.bodies）。</summary>
        internal static void RefArray(Component c, string field, Object[] values)
        {
            SerializedObject so = new SerializedObject(c);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void StrArray(Component c, string field, string[] values)
        {
            SerializedObject so = new SerializedObject(c);
            SerializedProperty prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
