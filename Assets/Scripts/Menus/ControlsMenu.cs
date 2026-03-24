using UnityEngine;
using TMPro;
using System.Reflection;

public class ControlsMenu : MonoBehaviour
{
    [Header("References")]
    public PlayerControls player1Controls;
    public PlayerControls player2Controls;
    public GameObject controlLabelPrefab; // Now has 3 text fields
    public Transform contentParent;
    
    [Header("Input Type")]
    public bool showController = false;

    void Start()
    {
        GenerateControlsMenu();
    }

    void GenerateControlsMenu()
    {
        FieldInfo[] fields = typeof(PlayerControls).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (FieldInfo field in fields)
        {
            if (field.FieldType == typeof(string))
            {
                string fieldName = field.Name;
                
                if (!fieldName.Contains("Button") && !fieldName.Contains("Axis")) continue;

                string inputName1 = (string)field.GetValue(player1Controls);
                string inputName2 = (string)field.GetValue(player2Controls);
                string displayName = FormatFieldName(fieldName);
                string key1 = ControlLabel.GetAssignedKey(inputName1, showController);
                string key2 = ControlLabel.GetAssignedKey(inputName2, showController);

                GameObject labelObj = Instantiate(controlLabelPrefab, contentParent);
                TextMeshProUGUI[] texts = labelObj.GetComponentsInChildren<TextMeshProUGUI>();
                
                if (texts.Length >= 3)
                {
                    texts[0].text = displayName + ":"; // Action
                    texts[1].text = key1;              // Player 1
                    texts[2].text = key2;              // Player 2
                }
            }
        }
    }

    string FormatFieldName(string fieldName)
    {
        fieldName = fieldName.Replace("Button", "").Replace("Axis", "");
        
        string result = "";
        foreach (char c in fieldName)
        {
            if (char.IsUpper(c) && result.Length > 0)
                result += " ";
            result += c;
        }
        
        if (result.Length > 0)
            result = char.ToUpper(result[0]) + result.Substring(1);
            
        return result;
    }
}