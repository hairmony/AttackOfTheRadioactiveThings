using UnityEngine;
using TMPro;

public class ControlLabel : MonoBehaviour
{
    public PlayerMovement player;
    public TextMeshProUGUI uiText;
    public string buttonVariableName;

    void Start()
    {
        string inputName = (string)typeof(PlayerMovement)
            .GetField(buttonVariableName)
            .GetValue(player);
        string key = GetAssignedKey(inputName);
        uiText.text = key;
    }

    public static string GetAssignedKey(string inputName, bool preferController = false)
    {
#if UNITY_EDITOR
        var obj = new UnityEditor.SerializedObject(
            UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);
        var axes = obj.FindProperty("m_Axes");

        for (int i = 0; i < axes.arraySize; i++)
        {
            var axis = axes.GetArrayElementAtIndex(i);
            if (axis.FindPropertyRelative("m_Name").stringValue == inputName)
            {
                string negKey = axis.FindPropertyRelative("negativeButton").stringValue;
                string posKey = axis.FindPropertyRelative("positiveButton").stringValue;

                // For axes (like Horizontal), show both keys
                if (!string.IsNullOrEmpty(negKey) && !string.IsNullOrEmpty(posKey))
                {
                    return negKey + " / " + posKey;
                }

                // For buttons, just show the positive key
                if (!string.IsNullOrEmpty(posKey)) return posKey;

                string alt = axis.FindPropertyRelative("altPositiveButton").stringValue;
                if (!string.IsNullOrEmpty(alt)) return alt;

                return "None";
            }
        }
#endif
        return "None";
    }
}