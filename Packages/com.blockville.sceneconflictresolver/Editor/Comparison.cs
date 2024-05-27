using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using Codice.Client.GameUI.Update;
using Codice.CM.Common;
using GluonGui.WorkspaceWindow.Views.WorkspaceExplorer.Explorer;
using JetBrains.Annotations;
using Unity.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using YamlDotNet.RepresentationModel;


public class Comparison : MonoBehaviour
{
    public static string oldScenePath = "Assets/Scenes/SampleScene_old.unity";
    public static string mainScenePath = "Assets/Scenes/SampleScene.unity";
    
    static MyYamlSceneFile oldSceneFile;
    static MyYamlSceneFile newSceneFile;
    
    static List<EntityDifferences> differences;
    static List<YamlNode> addedNodes;
    static List<YamlNode> deletedNodes;
    
    static List<GameObject> outlinedObjects = new List<GameObject>(); 
    static List<GameObject> oldObjects = new List<GameObject>();
    static List<GameObject> newObjects = new List<GameObject>();
    public static List<List<string>> linkedConflicts;

    [MenuItem("Window/Blockville/TEST")]
    public static void TEST()
    {
        differences = new List<EntityDifferences>();
        linkedConflicts = new List<List<string>>();
        oldSceneFile = new MyYamlSceneFile(oldScenePath);
        newSceneFile = new MyYamlSceneFile(mainScenePath);
        differences = oldSceneFile.FindChangedNodes(newSceneFile);
        addedNodes = oldSceneFile.FindAddedNodes(newSceneFile);
        deletedNodes = oldSceneFile.FindDeletedNodes(newSceneFile);

        HandleHiearchyChanges();
    }

    #region Main Funcs
    [MenuItem("Window/Blockville/ConflictResolver")]
    public static void ShowPathWindow()
    {
        ScenePathWindow.ShowWindow();
    }

    public static void StartVisualizing()
    {
        linkedConflicts = new List<List<string>>();
        oldSceneFile = new MyYamlSceneFile(oldScenePath);
        newSceneFile = new MyYamlSceneFile(mainScenePath);
        differences = oldSceneFile.FindChangedNodes(newSceneFile);
        addedNodes = oldSceneFile.FindAddedNodes(newSceneFile);
        deletedNodes = oldSceneFile.FindDeletedNodes(newSceneFile);

        if(!(differences.Any() || addedNodes.Any() || deletedNodes.Any()))
        {
            Debug.Log("No changes found.");
            return;
        }

        AddOldScene();
        
        ComparisonWindow.ShowWindow();

        HandleAddedNodes();
        HandleDeletedNodes();
        HandleHiearchyChanges();
        HandleDifferences();
    }

    public static void HandleAddedNodes()
    {
        foreach (YamlNode addedNode in addedNodes)
        {
            string componentType = newSceneFile.GetComponentType(addedNode);
            if(newSceneFile.IsStripped(addedNode))
            {
                continue;
            }
            
            if
            (
                componentType == NodeTypes.GAMEOBJECT ||
                componentType == NodeTypes.PREFAB_INSTANCE
            ) 
            {
                string objName = newSceneFile.GetNameOfGameObject(addedNode);
                GameObject obj = GameObject.Find(objName);

                if(PrefabUtility.IsAddedGameObjectOverride(obj)) //Prefab Changes are handled in the later loops.
                {
                    continue;
                }

                string header = $"Added {componentType}: <b>{objName}</b>";
                string description = $"{componentType} <b>{objName}</b> is added to the scene.";
                
                ComparisonWindow.AddConflict
                (
                    header,
                    description,
                    newSceneFile.GetIdByRootNode(addedNode),
                    ConflictType.ADDED
                );
                
                SetAsAdded(obj);
            }
            else //New Component Added - A GameObject is changed.
            {
                YamlNode ownerGameObject = newSceneFile.GetGameObjectOfComponent(addedNode);
                YamlNode ownerPrefab;
                bool isStripped = newSceneFile.IsStripped(ownerGameObject);
                string objName;
                string typeOfOwner;
                if(isStripped)
                {
                    ownerPrefab = newSceneFile.GetPrefabOfStrippedComponent(ownerGameObject);
                    objName = newSceneFile.GetNameOfPrefabInstance(ownerPrefab);
                    typeOfOwner = NodeTypes.PREFAB_INSTANCE;
                }
                else
                {
                    ownerPrefab = null;
                    objName = newSceneFile.GetNameOfGameObject(ownerGameObject);
                    typeOfOwner = NodeTypes.GAMEOBJECT;
                }

                GameObject obj = GameObject.Find(objName);
                GameObject oldObj = GameObject.Find(objName + "___old");
                if(oldObj == null)
                {
                    //Gameobject is also new.
                    continue;
                }
                
                string header = $"Changed {typeOfOwner} : <b>{objName}</b>";
                string description = $"Component <b>{newSceneFile.GetComponentType(addedNode)}</b> is added to the {typeOfOwner} <b>{objName}</b>.";
                string id = isStripped? newSceneFile.GetIdByRootNode(ownerPrefab) : newSceneFile.GetIdByRootNode(addedNode);
                ComparisonWindow.AddConflict
                (
                    header,
                    description,
                    id,
                    ConflictType.CHANGED
                );

                SetAsNewVariant(obj);
                SetAsOldVariant(oldObj);
            }
        }
    }

    public static void HandleHiearchyChanges()
    {
        foreach(EntityDifferences difference in differences)
        {
            if(difference.m_fieldName != "m_Father") continue;

            Regex regex = new Regex(@"\d+");
            string oldFatherId = regex.Match(difference.m_difference.Item1).ToString();
            string newFatherId = regex.Match(difference.m_difference.Item2).ToString();
            List<string> newLink = new List<string>(){difference.m_entityId};
            YamlNode oldFather;
            YamlNode newFather;
            
            if(oldFatherId != "0")
            {
                oldFather = oldSceneFile.IsStripped(oldFatherId) ? //Is a child of prefab instance 
                    oldSceneFile.GetPrefabOfStrippedComponent(oldFatherId) : 
                    oldSceneFile.GetRootNodeById(oldFatherId);
                oldFatherId = oldSceneFile.GetIdByRootNode(oldFather);
                newLink.Add(oldFatherId);
            }

            if(newFatherId != "0")
            {
                newFather = newSceneFile.IsStripped(newFatherId) ? //Is a child of prefab instance
                    newSceneFile.GetPrefabOfStrippedComponent(newFatherId) : 
                    newSceneFile.GetRootNodeById(newFatherId);
                newFatherId = newSceneFile.GetIdByRootNode(newFather);
                newLink.Add(newFatherId);
            }

            linkedConflicts.Add(newLink);
        }
    }

    public static void HandleDeletedNodes()
    {
        foreach (YamlNode deletedNode in deletedNodes)
        {
            string componentType = oldSceneFile.GetComponentType(deletedNode);

            if (oldSceneFile.IsStripped(deletedNode))
            {
                continue;
            }
            
            if // GameObject Deleted
            (
                componentType == NodeTypes.GAMEOBJECT ||
                componentType == NodeTypes.PREFAB_INSTANCE
            ) 
            {
                string objName = oldSceneFile.GetNameOfGameObject(deletedNode);
                GameObject obj = GameObject.Find(objName + "___old");

                if(PrefabUtility.IsAddedGameObjectOverride(obj)) 
                {
                    continue; //Prefab changes are handled in the later loops.
                }

                string header = $"Deleted {componentType}: <b>{objName}</b>";
                string description = $"{componentType} <b>{objName}</b> is deleted from the scene.";
                
                ComparisonWindow.AddConflict
                (
                    header,
                    description, 
                    oldSceneFile.GetIdByRootNode(deletedNode), 
                    ConflictType.DELETED
                );
                
                SetAsDeleted(obj);
            }

            else // Component Deleted - A GameObject is changed.
            {
                YamlNode ownerGameObject = oldSceneFile.GetGameObjectOfComponent(deletedNode);
                string objName = oldSceneFile.GetNameOfGameObject(ownerGameObject);
                GameObject obj = GameObject.Find(objName);
                GameObject oldObj = GameObject.Find(objName + "___old");
                
                if(obj == null) continue;  //Gameobject is also deleted.

                string header = $"Changed GameObject : <b>{objName}</b>";
                string description = $"Component <b>{oldSceneFile.GetComponentType(deletedNode)}</b> is deleted from the GameObject <b>{objName}</b>.";
                
                ComparisonWindow.AddConflict
                (
                    header,
                    description,
                    oldSceneFile.GetIdByRootNode(deletedNode),
                    ConflictType.CHANGED
                );

                SetAsOldVariant(obj);
                SetAsNewVariant(oldObj);
            }
        }
    }

    public static void HandleDifferences()
    {
        foreach (EntityDifferences difference in differences)
        {
            string[] ignoredIds = new string[]{"1", "2", "3", "4"};
            
            if(ignoredIds.Contains(difference.m_entityId)) 
                continue; //Scene Settings are ignored.

            string componentType = newSceneFile.GetComponentType(difference.m_entityId);

            if //Gameobject itself changed
            (
                componentType == NodeTypes.GAMEOBJECT ||
                componentType == NodeTypes.PREFAB_INSTANCE
            ) 
            {
                string objName = newSceneFile.GetNameOfGameObject(difference.m_entityId);
                string oldObjName = oldSceneFile.GetNameOfGameObject(difference.m_entityId) + "___old";
                GameObject newObj = GameObject.Find(objName);
                GameObject oldObj = GameObject.Find(oldObjName);
                
                if(outlinedObjects.Contains(newObj)) 
                    continue; //This object has another conflict. Do not show it twice for the sake of simplicity.

                string header = $"Changed {componentType} : <b>{objName}</b>";
                string description = $"Changed field : <b>{difference.m_fieldName}</b>.";
                ComparisonWindow.AddConflict
                (
                    header,
                    description,
                    difference.m_entityId,
                    ConflictType.CHANGED
                );

                SetAsNewVariant(newObj);
                SetAsOldVariant(oldObj);
            }

            else //A component of a GameObject changed
            {
                YamlNode ownerGameObject = newSceneFile.GetGameObjectOfComponent(difference.m_entityId);
                string objName = newSceneFile.GetNameOfGameObject(ownerGameObject);
                string oldObjName = objName + "___old";
                GameObject newObj = GameObject.Find(objName);
                GameObject oldObj = GameObject.Find(oldObjName);
                
                if(outlinedObjects.Contains(newObj)) 
                    continue; //This object has another conflict. Do not show it twice for the sake of simplicity.

                string header = $"Changed GameObject : <b>{objName}</b>";
                string description = $"Component: <b>{newSceneFile.GetComponentType(difference.m_entityId)}</b> has change on field: <b>{difference.m_fieldName}</b>.";

                ComparisonWindow.AddConflict
                (
                    header,
                    description,
                    difference.m_entityId,
                    ConflictType.CHANGED
                );

                SetAsNewVariant(newObj);
                SetAsOldVariant(oldObj);
            }
        }
    }

    public static bool AddOldScene()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            Scene scene = EditorSceneManager.GetSceneAt(i);
            if (scene.path == oldScenePath) 
            {
                return false; //Old scene already added.
            }
        }

        Scene oldScene = EditorSceneManager.OpenScene(oldScenePath, OpenSceneMode.Additive);
        
        for(int i = 0 ; i < oldScene.rootCount ; i++)
        {
            StampGameObjectAndChildren(oldScene.GetRootGameObjects()[i]);
        }
        return true;
    }

    public static void RevertChanges()
    {
        Scene oldScene = SceneManager.GetSceneByPath(oldScenePath);
        if (oldScene.IsValid())
        {
            EditorSceneManager.CloseScene(oldScene, true);
        }
        foreach (GameObject outlinedObject in outlinedObjects)
        {
            if(outlinedObject == null) 
                continue;
            RemoveOutlineRecursive(outlinedObject);
        }
        outlinedObjects.Clear();
        oldObjects.Clear();
        newObjects.Clear();
    }
    
    #endregion

    #region Outline
    static void SetAsAdded(GameObject obj)
    {
        Outline outline = obj.GetComponent<Outline>();
        if(!outline)
        {
            outline = obj.AddComponent<Outline>();
        }
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = Color.green;
        outline.OutlineWidth = 10f;
        outlinedObjects.Add(obj);
        newObjects.Add(obj);
    }

    static void SetAsDeleted(GameObject obj)
    {
        Outline outline = obj.GetComponent<Outline>();
        if(!outline)
        {
            outline = obj.AddComponent<Outline>();
        }        
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = Color.red;
        outline.OutlineWidth = 10f;
        oldObjects.Add(obj);
    }

    static void SetAsOldVariant(GameObject obj)
    {
        Outline outline = obj.GetComponent<Outline>();
        if(!outline)
        {
            outline = obj.AddComponent<Outline>();
        }        
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = Color.blue;
        outline.OutlineWidth = 10f;
        oldObjects.Add(obj);
    }

    static void SetAsNewVariant(GameObject obj)
    {
        Outline outline = obj.GetComponent<Outline>();
        if(!outline)
        {
            outline = obj.AddComponent<Outline>();
        }        
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = Color.yellow;
        outline.OutlineWidth = 10f;
        outlinedObjects.Add(obj);
        newObjects.Add(obj);
    }

    static void RemoveOutlineRecursive(GameObject obj)
    {
        var outline = obj.GetComponent<Outline>();
        if(outline != null) DestroyImmediate(outline);
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            RemoveOutlineRecursive(obj.transform.GetChild(i).gameObject);
        }
    }
    #endregion

    #region ButtonCallbacks

    public static void OnSelectButtonPressed(string id, ConflictType conflictType)
    {
        MyYamlSceneFile sceneToBeUsed = conflictType == ConflictType.DELETED ? oldSceneFile : newSceneFile;
        
        string componentType = sceneToBeUsed.GetComponentType(id);
        
        string subjectName = (componentType == NodeTypes.GAMEOBJECT) || (componentType == NodeTypes.PREFAB_INSTANCE) ? 
            sceneToBeUsed.GetNameOfGameObject(id) : sceneToBeUsed.GetNameOfGameObject(sceneToBeUsed.GetGameObjectOfComponent(id));

        GameObject oldObj = GameObject.Find(subjectName + "___old");
        
        if(oldObj == null && !(conflictType == ConflictType.ADDED)) // Name change
        {
            string oldSubjectName = (oldSceneFile.GetComponentType(id) == NodeTypes.GAMEOBJECT) || (oldSceneFile.GetComponentType(id) == NodeTypes.PREFAB_INSTANCE) ? 
                oldSceneFile.GetNameOfGameObject(id) : oldSceneFile.GetNameOfGameObject(oldSceneFile.GetGameObjectOfComponent(id));
            oldObj = GameObject.Find(oldSubjectName + "___old");
        }


        GameObject newObj = GameObject.Find(subjectName);
        List<GameObject> selections = new List<GameObject>();
        if(oldObj) selections.Add(oldObj);
        if(newObj) selections.Add(newObj);

        foreach(GameObject selection in selections) 
        {
            EditorGUIUtility.PingObject(selection);
        }
        Selection.objects = selections.ToArray();
    }

    public static void OnDetailsButtonPressed(string id)
    {
        Debug.Log("Details Button : " + id);  //NOT IMPLEMENTED
    }

    public static void OnApplyButtonPressed()
    {
        List<ConflictResolveChoice> selections = ComparisonWindow.GetSelections();
        if(!selections.Any()) return;

        foreach(var selection in selections)
        {
            MyYamlSceneFile sceneToBeUsed = selection.conflictType == ConflictType.DELETED ? oldSceneFile : newSceneFile;
            string componentType = sceneToBeUsed.GetComponentType(selection.id);
            
            string subjectName = componentType == NodeTypes.GAMEOBJECT || componentType == NodeTypes.PREFAB_INSTANCE ? 
                sceneToBeUsed.GetNameOfGameObject(selection.id) :
                sceneToBeUsed.GetNameOfGameObject(sceneToBeUsed.GetGameObjectOfComponent(selection.id));

            GameObject oldObj = GameObject.Find(subjectName + "___old");
            GameObject newObj = GameObject.Find(subjectName);

            if(selection.isNew)
                continue;

            if
            (
                (
                    selection.conflictType == ConflictType.ADDED ||
                    selection.conflictType == ConflictType.CHANGED
                ) && 
                newObj != null
            )
            {
                DestroyImmediate(newObj);
            }

            if(selection.conflictType == ConflictType.CHANGED || selection.conflictType == ConflictType.DELETED)
            {
                if(!oldObj) //Name change
                {
                    string oldSceneComponentType = oldSceneFile.GetComponentType(selection.id); 

                    string oldSubjectName = oldSceneComponentType == NodeTypes.GAMEOBJECT || oldSceneComponentType == NodeTypes.PREFAB_INSTANCE ?
                        oldSceneFile.GetNameOfGameObject(selection.id) :
                        oldSceneFile.GetNameOfGameObject(oldSceneFile.GetGameObjectOfComponent(selection.id));
                    
                    oldObj = GameObject.Find(oldSubjectName + "___old");
                    subjectName = oldSubjectName;
                }

                if(!oldObj) continue; //Old object is moved on another conflict.

                DestroyImmediate(oldObj.GetComponent<Outline>());

                if(oldObj.transform.parent == null) //Root Object
                {
                    SceneManager.MoveGameObjectToScene(oldObj, SceneManager.GetSceneByPath(mainScenePath));
                }
                else //Child Object
                {
                    string parentName = oldObj.transform.parent.name.Replace("___old", "");
                    oldObj.transform.parent = null;
                    SceneManager.MoveGameObjectToScene(oldObj, SceneManager.GetSceneByPath(mainScenePath));
                    Transform newParentTf = GameObject.Find(parentName).transform;
                    oldObj.transform.parent = newParentTf;
                }

                DestampGameObjectAndChildren(oldObj);
                oldObj.name = subjectName;
                outlinedObjects.Add(oldObj);
            }
        }
        EditorSceneManager.CloseScene(SceneManager.GetSceneByPath(oldScenePath), true);
        ComparisonWindow.CloseWindow();
    }

    public static void OnRevertButtonPressed()
    {
        ComparisonWindow.CloseWindow();
    }

    public static void OnAllOldButtonPressed()
    {   
        Selection.objects = oldObjects.ToArray();
    }

    public static void OnAllNewButtonPressed()
    {
        Selection.objects = newObjects.ToArray();
    }
    
    #endregion

    #region Utils

    public static void StampGameObjectAndChildren(GameObject gameObject)
    {
        gameObject.name += "___old";
        for (int i = 0; i < gameObject.transform.childCount; i++)
        {
            StampGameObjectAndChildren(gameObject.transform.GetChild(i).gameObject);
        }
    }

    public static void DestampGameObjectAndChildren(GameObject gameObject)
    {
        if(gameObject.name.Contains("___old"))
        {
            gameObject.name = gameObject.name.Replace("___old", "");
        }
        for (int i = 0; i < gameObject.transform.childCount; i++)
        {
            DestampGameObjectAndChildren(gameObject.transform.GetChild(i).gameObject);
        }
    }

    #endregion
}
