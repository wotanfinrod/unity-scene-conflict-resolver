using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class ScenePathWindow : EditorWindow
{
    private const string WINDOW_PATH = "Packages/com.blockville.sceneconflictresolver/Editor/pages/ScenePathWindow.uxml";
    public static ScenePathWindow Instance;
    
    public static void ShowWindow()
    {
        ScenePathWindow scenePathWindow = GetWindow<ScenePathWindow>("Conlict Resolver");
        scenePathWindow.Init();
        scenePathWindow.Show();
        scenePathWindow.minSize = new Vector2(727, 281);
        scenePathWindow.maxSize = new Vector2(727, 281);
    }

    public void Init()
    {
        Instance = this;
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WINDOW_PATH);
        VisualElement root = rootVisualElement;
        visualTree.CloneTree(root);
        SetError("");
        rootVisualElement.Q<Button>("ApplyButton").clicked += () => OnApplyButtonPressed();
    }

    public static void OnApplyButtonPressed()
    {
        string oldInput = Instance.rootVisualElement.Q<TextField>("OldPathInput").value;
        string newInput = Instance.rootVisualElement.Q<TextField>("NewPathInput").value;

        if(File.Exists(oldInput) && File.Exists(newInput))
        {
            Comparison.oldScenePath = oldInput;
            Comparison.mainScenePath = newInput;
            CloseWindow();
            Comparison.StartVisualizing();
        }
        else
        {
            SetError("One or more of the paths are invalid.");
        }
    }

    public static void CloseWindow()
    {
        Instance.Close();
    }

    public static void SetError(string error)
    {
        Instance.rootVisualElement.Q<Label>("ErrorLabel").text = error;
    }
}
