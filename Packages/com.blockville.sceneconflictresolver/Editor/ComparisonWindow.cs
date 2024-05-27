using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public enum ConflictType
{
    ADDED,
    DELETED,
    CHANGED
}

public struct ConflictResolveChoice
{
    public ConflictResolveChoice(string id, bool isNew, ConflictType conflictType)
    {
        this.id = id;
        this.isNew = isNew;
        this.conflictType = conflictType;
    }
    public string id;
    public bool isNew;
    public ConflictType conflictType;
}

public class ComparisonWindow : EditorWindow
{   
    private const string WINDOW_PATH = "Packages/com.blockville.sceneconflictresolver/Editor/pages/ConflictResolverWindow.uxml";
    private const string CONFLICT_VIEW_PATH = "Packages/com.blockville.sceneconflictresolver/Editor/pages/ConflictView.uxml";
    private const string CONFLICT_VIEW_STYLES = "Packages/com.blockville.sceneconflictresolver/Editor/styles/ConflictViewStyles.uss";
 
    static int m_conflictCount = 0;

    public static ComparisonWindow Instance;

    public static void ShowWindow()
    {
        ComparisonWindow comparisonWindow = GetWindow<ComparisonWindow>("Conflict Resolver");
        comparisonWindow.Init();
        comparisonWindow.Show();
        comparisonWindow.minSize = new Vector2(727, 281);
        comparisonWindow.maxSize = new Vector2(1454, 562);
    }

    public static void CloseWindow()
    {
        Instance.Close();
    }

    void Init()
    {
        Instance = this;
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WINDOW_PATH);
        VisualElement root = rootVisualElement;
        m_conflictCount = 0;
        visualTree.CloneTree(root);
        rootVisualElement.Q<VisualElement>("Conflicts").Clear();
        rootVisualElement.Q<Button>("ApplyButton").clicked += () => Comparison.OnApplyButtonPressed();
        rootVisualElement.Q<Button>("RevertButton").clicked += () => Comparison.OnRevertButtonPressed();
        rootVisualElement.Q<Button>("AllOld").clicked += () => Comparison.OnAllOldButtonPressed();
        rootVisualElement.Q<Button>("AllNew").clicked += () => Comparison.OnAllNewButtonPressed();
        SetErrorMessage("");
    }

    public static void AddConflict(string header, string description, string id, ConflictType conflictType)
    {
        VisualTreeAsset componentTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CONFLICT_VIEW_PATH);
        VisualElement newConflictComponent = componentTree.CloneTree();
        newConflictComponent.name = $"Conflict_{id}_{(int)conflictType}";
        newConflictComponent.Q<Label>("HeaderLabel").text = header;
        newConflictComponent.Q<Label>("DescriptionLabel").text = description;
        Instance.rootVisualElement.Q<VisualElement>("Conflicts").Add(newConflictComponent);
        m_conflictCount++;
        newConflictComponent.Q<Button>("SelectButton").clicked += () => Comparison.OnSelectButtonPressed(id, conflictType);
        Instance.rootVisualElement.Q<Label>("CountLabel").text = $"{m_conflictCount} Conflicts Found";
         newConflictComponent.Q<RadioButtonGroup>("ToggleGroup").value = -1;
        newConflictComponent.Q<RadioButtonGroup>("ToggleGroup").RegisterValueChangedCallback((evt) => OnToggleChanged(id, evt.newValue));
    }

    public static List<ConflictResolveChoice> GetSelections()
    {
        List<ConflictResolveChoice> selections = new List<ConflictResolveChoice>();
        VisualElement conflictsRoot = Instance.rootVisualElement.Q<VisualElement>("Conflicts");
        foreach(VisualElement conflictView in conflictsRoot.Children())
        {
            RadioButtonGroup selection = conflictView.Q<RadioButtonGroup>("ToggleGroup");
            bool toggleOldVal = selection.Q<RadioButton>("ToggleOld").value;
            bool toggleNewVal = selection.Q<RadioButton>("ToggleNew").value;

            if(!toggleNewVal && !toggleOldVal)
            {
                SetErrorMessage("Please select an option for each conflict.");
                selections.Clear();
                return selections;
            }

            ConflictType currentConflictType = (ConflictType)int.Parse(conflictView.name.Split('_')[2]);
            string currentId = conflictView.name.Split('_')[1];

            selections.Add(new ConflictResolveChoice(currentId, toggleNewVal, currentConflictType));
        }

        return selections;
    }

    public static void OnToggleChanged(string id, int newValue)
    {
        Debug.Log($"Toggle changed for {id} to {newValue}");
        foreach(List<string> linkGroup in Comparison.linkedConflicts)
        {
            if(linkGroup.Contains(id))
            {
                foreach(string linkedItemId in linkGroup)
                {
                    VisualElement conflictsRoot = Instance.rootVisualElement.Q<VisualElement>("Conflicts");
                    foreach(VisualElement conflictView in conflictsRoot.Children())
                    {
                        if(conflictView.name.Split('_')[1] == linkedItemId)
                        {
                            RadioButtonGroup selection = conflictView.Q<RadioButtonGroup>("ToggleGroup");
                            selection.value = newValue;
                        }
                    }
                }
            }
        }
    }

    public static void SetErrorMessage(string message)
    {
        Instance.rootVisualElement.Q<Label>("ErrorLabel").text = message;
    }

    void OnDestroy()
    {
        Comparison.RevertChanges();
    }
}
