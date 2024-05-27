using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using YamlDotNet.RepresentationModel;

    public static class NodeTypes
    {
        public const string GAMEOBJECT = "GameObject";
        public const string TRANSFORM  = "Transform";
        public const string MESH_RENDERER = "MeshRenderer";
        public const string SCENE_ROOTS = "SceneRoots";
        public const string MESH_FILTER = "MeshFilter";
        public const string PREFAB_INSTANCE = "PrefabInstance";
    }

    public struct TransformValues
    {
        public TransformValues(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }

        public bool Equals(TransformValues other)
        {
            return position.Equals(other.position) && rotation.Equals(other.rotation) && scale.Equals(other.scale);
        }

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    public struct EntityDifferences
    {
        public string m_entityId;
        public string m_fieldName;
        public Tuple<string, string> m_difference; //(OldValue, NewValue)

        public EntityDifferences(string entityId, string fieldName, Tuple<string, string> difference)
        {
            m_entityId = entityId;
            m_fieldName = fieldName;
            m_difference = difference;
        }
    }

    public class MyYamlSceneFile
    {
        public YamlStream m_yamlStream = null;
        public YamlMappingNode m_sceneRoots = null;
        public List<string> m_strippedIds;
        bool m_isLoaded = false;

        #region Essentials
        //Loads Yaml File

        public MyYamlSceneFile() {}

        public MyYamlSceneFile(string filePathInProj)
        {
            LoadYamlFile(filePathInProj);
        }

        public YamlStream LoadYamlFile(string filePathInProj)
        {
            if(!File.Exists(filePathInProj))
            {
                return null;
            }

            m_strippedIds = new List<string>();
            string sceneStringStripped = HandleStrippedObjects(filePathInProj);
            StringReader input = new StringReader(sceneStringStripped);
            YamlStream yamlStream = new YamlStream();
            yamlStream.Load(input);
            m_yamlStream = yamlStream;
            
            LoadSceneRoots();
            m_isLoaded = true;

            return yamlStream;
        }

        private void LoadSceneRoots()
        {
            if(m_yamlStream == null)
            {
                return;
            }

            int lastElementIndex = m_yamlStream.Documents.Count - 1;
            m_sceneRoots = (YamlMappingNode)m_yamlStream.Documents[lastElementIndex].RootNode;
        }

        //Get the node by it's Id.
        public YamlNode GetRootNodeById(string id)
        {
            foreach(YamlDocument doc in m_yamlStream.Documents)
            {
                if(doc.RootNode.Anchor.Value.Equals(id))
                {
                    return doc.RootNode;
                }  
            }
            return null;
        }

        //Get the Id of given Node
        public string GetIdByRootNode(YamlNode rootNode)
        {
            return rootNode.Anchor.Value;
        }

        //Get the Node Name of given Node
        public string GetComponentType(string id)
        {
            return GetComponentType(GetRootNodeById(id));
        }

        public string GetComponentType(YamlNode rootNode)
        {
            if(rootNode.NodeType == YamlNodeType.Mapping)
            {
                foreach (var entry in ((YamlMappingNode)rootNode).Children)
                {
                    var keyNode = entry.Key as YamlScalarNode;
                    if (keyNode != null)
                    {
                        string key = keyNode.Value;
                        return key;
                    }
                }
            }
            return "";
        }

        public bool IsLoaded()
        {
            return m_isLoaded;
        }
        #endregion
    
        #region SceneRoots

        public List<string> GetSceneRoots()
        {
            List<string> sceneRoots = new List<string>();
            foreach (var items in (YamlSequenceNode)m_sceneRoots.Children[0].Value["m_Roots"])
            {
                sceneRoots.Add(items["fileID"].ToString());
            }
            return sceneRoots;
        }

        public List<string> FindAddedSceneRoots(MyYamlSceneFile newFile)
        {
            List<string> addedSceneRoots = new List<string>();
            List<string> oldSceneRoots = GetSceneRoots();
            List<string> newSceneRoots = newFile.GetSceneRoots();
            foreach(string id in newSceneRoots)
            {
                if(oldSceneRoots.Contains(id)) continue;
                addedSceneRoots.Add(id);
            }
            return addedSceneRoots;
        }

        public List<string> FindRemovedSceneRoots(MyYamlSceneFile newFile)
        {
            List<string> removedSceneRoots = new List<string>();
            List<string> oldSceneRoots = GetSceneRoots();
            List<string> newSceneRoots = newFile.GetSceneRoots();
            foreach(string id in oldSceneRoots)
            {
                if(newSceneRoots.Contains(id)) continue;
                removedSceneRoots.Add(id);
            }
            return removedSceneRoots;
        }

        #endregion

        #region GameObject

        //Return the name of component if it's a gameObject. 
        
        public string GetNameOfGameObject(string nodeId)
        {
            return GetNameOfGameObject(GetRootNodeById(nodeId));
        }
        
        public string GetNameOfGameObject(YamlNode rootNode)
        {
            string componentType = GetComponentType(rootNode);
            if(componentType == NodeTypes.GAMEOBJECT)
            {
                return rootNode["GameObject"]["m_Name"].ToString();   
            }
            else if (componentType == NodeTypes.PREFAB_INSTANCE)
            {
                return GetNameOfPrefabInstance(rootNode);
            }
            
            return "";
        }

        public YamlNode GetGameObjectOfComponent(string id)
        {
            return GetGameObjectOfComponent(GetRootNodeById(id));
        }

        public YamlNode GetGameObjectOfComponent(YamlNode rootNode)
        {
            // Search for the m_GameObject field in the root node
            if(rootNode.NodeType != YamlNodeType.Mapping)
            {
                return null;
            }

            foreach (var childNode in ((YamlMappingNode)rootNode).Children)
            {
                if (childNode.Value is YamlMappingNode mappingNode)
                {
                    if (mappingNode.Children.TryGetValue(new YamlScalarNode("m_GameObject"), out var gameObjectNode))
                    {
                        string gameObjectFileID = gameObjectNode["fileID"].ToString();
                        return GetRootNodeById(gameObjectFileID);
                    }
                }
            }

            return null;
        }

        public List<YamlNode> GetComponentNodesOfGameObject(YamlNode gameObjectNode)
        {
            List<YamlNode> componentNodes = new List<YamlNode>();
            if (gameObjectNode is YamlMappingNode mappingNode && GetComponentType(mappingNode) == "GameObject")
            {
                foreach (YamlNode componentNode in (YamlSequenceNode)gameObjectNode["GameObject"]["m_Component"])
                {
                    string fileId = componentNode["component"]["fileID"].ToString();
                    componentNodes.Add(GetRootNodeById(fileId));
                }
            }
            return componentNodes;
        }

        public bool IsRootGameObject(YamlNode rootNode)
        {
            if(GetComponentType(rootNode) != NodeTypes.GAMEOBJECT)
            {
                return false;
            }

            return IsRootTransform(FindTransformOfGameObject(rootNode));
        }

        #endregion

        #region PrefabInstance

        public string GetNameOfPrefabInstance(string id)
        {
            return GetNameOfPrefabInstance(GetRootNodeById(id));
        }

        public string GetNameOfPrefabInstance(YamlNode rootNode)
        {
            if(GetComponentType(rootNode) != NodeTypes.PREFAB_INSTANCE) return "";

            foreach (var modification in ((YamlSequenceNode)rootNode["PrefabInstance"]["m_Modification"]["m_Modifications"]).Reverse())
            {
                if(modification["propertyPath"].ToString() == "m_Name")
                {
                    return modification["value"].ToString();
                }
            }
            return "";
        }

        public int GetAddedObjectCountOfPrefabInstance(string id)
        {
            return GetAddedObjectCountOfPrefabInstance(GetRootNodeById(id));
        }

        public int GetAddedObjectCountOfPrefabInstance(YamlNode prefabInstanceNode)
        {
            if(GetComponentType(prefabInstanceNode) != NodeTypes.PREFAB_INSTANCE) return -1;

            int addedObjectCount = 0;
            foreach (var added in (YamlSequenceNode)prefabInstanceNode["PrefabInstance"]["m_Modification"]["m_AddedGameObjects"])
            {
                addedObjectCount++;
            }
            return addedObjectCount;
        }

        public int GetDeletedObjectCountOfPrefabInstance(string id)
        {
            return GetDeletedObjectCountOfPrefabInstance(GetRootNodeById(id));
        }

        public int GetDeletedObjectCountOfPrefabInstance(YamlNode prefabInstanceNode)
        {
            if(GetComponentType(prefabInstanceNode) != NodeTypes.PREFAB_INSTANCE) return -1;

            int deletedObjectCount = 0;
            foreach (var deleted in (YamlSequenceNode)prefabInstanceNode["PrefabInstance"]["m_Modification"]["m_RemovedGameObjects"])
            {
                deletedObjectCount++;
            }
            return deletedObjectCount;
        }

        public YamlNode GetPrefabOfStrippedComponent(string id)
        {
            return GetPrefabOfStrippedComponent(GetRootNodeById(id));
        }

        public YamlNode GetPrefabOfStrippedComponent(YamlNode strippedComponent)
        {
          if(!IsStripped(strippedComponent)) return null;
          string id = ((YamlMappingNode)strippedComponent).Children[0].Value["m_PrefabInstance"]["fileID"].ToString();
          return GetRootNodeById(id);
        }


        #endregion

        #region Transform
        public YamlNode FindTransformOfGameObject(YamlNode gameObjectNode)
        {
            if(GetComponentType(gameObjectNode) != NodeTypes.GAMEOBJECT)
            {
                return null;
            }

            string fileId = gameObjectNode["GameObject"]["m_Component"][0]["component"]["fileID"].ToString();
            return GetRootNodeById(fileId);
        }

        public YamlNode FindRootTransform(YamlNode transformNode)
        {
            if(GetComponentType(transformNode) != NodeTypes.TRANSFORM)
            {
                return null;
            }

            string fileId = transformNode["Transform"]["m_Father"]["fileID"].ToString();
            return fileId == "0" ? null : GetRootNodeById(fileId);
        }
        
        public bool IsRootTransform(YamlNode rootNode)
        {
            if(GetComponentType(rootNode) != NodeTypes.TRANSFORM)
            {
                return false;
            }
            
            string nodeId = GetIdByRootNode(rootNode);
            
            foreach (var items in (YamlSequenceNode)m_sceneRoots.Children[0].Value["m_Roots"])
            {
                if(items["fileID"].ToString() == nodeId)
                {
                    return true;
                }   
            }
            return false;
        }
        
        #endregion

        #region MeshRenderer

        public List<Material> GetMaterialsOfMeshRenderer(string nodeId)
        {
            return GetMaterialsOfMeshRenderer(GetRootNodeById(nodeId));
        }

        public List<Material> GetMaterialsOfMeshRenderer(YamlNode meshRendererNode)
        {
            List<Material> materials = new List<Material>();
            if(GetComponentType(meshRendererNode) != NodeTypes.MESH_RENDERER)
            {
                return materials;
            }

            foreach (YamlNode materialNode in (YamlSequenceNode)meshRendererNode["MeshRenderer"]["m_Materials"])
            {
                string fileId = materialNode["fileID"].ToString();
                string guid = materialNode["guid"].ToString();
                Material material = AssetLoader.LoadAssetByFileIDAndGUID(fileId, guid) as Material;
                if(material != null)
                {
                    materials.Add(material);
                }
            }
            return materials;
        }

        #endregion

        #region MeshFilter

        public Mesh GetMeshOfMeshFilter(string nodeId)
        {
            return GetMeshOfMeshFilter(GetRootNodeById(nodeId));
        }

        public Mesh GetMeshOfMeshFilter(YamlNode meshFilterNode)
        {
            if(GetComponentType(meshFilterNode) != NodeTypes.MESH_FILTER)
            {
                return null;
            }
            string fileId = meshFilterNode["MeshFilter"]["m_Mesh"]["fileID"].ToString();
            string guid = meshFilterNode["MeshFilter"]["m_Mesh"]["guid"].ToString();
            return AssetLoader.LoadAssetByFileIDAndGUID(fileId, guid) as Mesh;
        }

        #endregion

        #region Changes
        public List<YamlNode> FindAddedNodes(MyYamlSceneFile newFile)
        {
            List<YamlNode> addedNodes = new List<YamlNode>();
            foreach(YamlDocument doc in newFile.m_yamlStream.Documents)
            {
                if(GetRootNodeById(newFile.GetIdByRootNode(doc.RootNode)) == null) //Node not found in old file.
                {
                    addedNodes.Add(doc.RootNode);
                }
            }
            return addedNodes;
        }

        public List<YamlNode> FindDeletedNodes(MyYamlSceneFile newFile)
        {
            List<YamlNode> deletedNodes = new List<YamlNode>();
            foreach(YamlDocument doc in m_yamlStream.Documents)
            {
                if(newFile.GetRootNodeById(GetIdByRootNode(doc.RootNode)) == null)
                {
                    deletedNodes.Add(doc.RootNode);
                }
            }
            return deletedNodes;
        }

        public List<EntityDifferences> FindChangedNodes(MyYamlSceneFile newFile)
        {
            List<EntityDifferences> changedNodes = new List<EntityDifferences>();
            foreach(YamlDocument doc in m_yamlStream.Documents)
            {
                if(GetComponentType(doc.RootNode) == NodeTypes.SCENE_ROOTS) continue; //Ignore SceneRoots for now

                YamlNode oldNode = doc.RootNode;
                YamlNode newNode = newFile.GetRootNodeById(GetIdByRootNode(oldNode));
                if(newNode != null && !oldNode.Equals(newNode))
                {
                    foreach(var item in (YamlMappingNode)((YamlMappingNode)oldNode).Children[0].Value) 
                    {
                        var newItemValue = ((YamlMappingNode)newNode).Children[0].Value[item.Key];
                        if(!item.Value.Equals(newItemValue))
                        {
                            EntityDifferences entityDifferences = new EntityDifferences
                            (
                                GetIdByRootNode(oldNode),
                                item.Key.ToString(),
                                new Tuple<string,string>
                                (
                                    item.Value.ToString(),
                                    newItemValue.ToString() 
                                )
                            );
                            changedNodes.Add(entityDifferences);
                            continue;
                        }
                    }
                }
            }
            return changedNodes;
        }
        #endregion

        #region Utils

        //Removes all "stripped" tags and stores stripped ids.
        public string HandleStrippedObjects(string filePathInProj)
        {
            string[] allLines  = File.ReadAllLines(filePathInProj);
            List<string> clearedLines = new List<string>();
            foreach (string line in allLines)
            {
                if(line.Contains("stripped"))
                {
                    int ampersandIndex = line.IndexOf('&');
                    string id = line.Substring(ampersandIndex + 1).Split(' ')[0];
                    m_strippedIds.Add(id);
                    clearedLines.Add(line.Replace(" stripped", ""));
                }
                else
                {
                    clearedLines.Add(line);
                }
            }
            return string.Join(Environment.NewLine, clearedLines);
        }

        public bool IsStripped(string id)
        {
            return m_strippedIds.Contains(id);
        }

        public bool IsStripped(YamlNode rootNode)
        {
            return IsStripped(GetIdByRootNode(rootNode));
        }

        public Vector3 GetLocalPosition(YamlNode transformNode)
        {
            if(transformNode is YamlMappingNode mappingNode && GetComponentType(mappingNode) == NodeTypes.TRANSFORM)
            {
                return new Vector3(
                    float.Parse(transformNode["Transform"]["m_LocalPosition"]["x"].ToString(), CultureInfo.InvariantCulture),
                    float.Parse(transformNode["Transform"]["m_LocalPosition"]["y"].ToString(), CultureInfo.InvariantCulture),
                    float.Parse(transformNode["Transform"]["m_LocalPosition"]["z"].ToString(), CultureInfo.InvariantCulture)
                );
            }
            return new Vector3();
        }

        public Quaternion GetLocalRotation(YamlNode transformNode)
        {
            if(transformNode is YamlMappingNode mappingNode && GetComponentType(mappingNode) == NodeTypes.TRANSFORM)
            {
                return new Quaternion(
                    float.Parse(transformNode["Transform"]["m_LocalRotation"]["x"].ToString(), CultureInfo.InvariantCulture),
                    float.Parse(transformNode["Transform"]["m_LocalRotation"]["y"].ToString(), CultureInfo.InvariantCulture),
                    float.Parse(transformNode["Transform"]["m_LocalRotation"]["z"].ToString(), CultureInfo.InvariantCulture),
                    float.Parse(transformNode["Transform"]["m_LocalRotation"]["w"].ToString(), CultureInfo.InvariantCulture)
                );
            }
            return new Quaternion();
        }

        public Vector3 GetLocalScale(YamlNode transformNode)
        {
            if(transformNode is YamlMappingNode mappingNode && GetComponentType(mappingNode) == NodeTypes.TRANSFORM)
            {
                return new Vector3(
                    float.Parse(transformNode["Transform"]["m_LocalScale"]["x"].ToString(), CultureInfo.InvariantCulture),
                    float.Parse(transformNode["Transform"]["m_LocalScale"]["y"].ToString(), CultureInfo.InvariantCulture),
                    float.Parse(transformNode["Transform"]["m_LocalScale"]["z"].ToString(), CultureInfo.InvariantCulture)
                );
            }
            return new Vector3();
        }

        public TransformValues LocalToWorldTransform(string nodeId)
        {
            return LocalToWorldTransform(GetRootNodeById(nodeId));
        }

        public TransformValues LocalToWorldTransform(YamlNode transformNode)
        {
            TransformValues localToWorld = new TransformValues();
            if(transformNode is YamlMappingNode mappingNode && GetComponentType(mappingNode) == NodeTypes.TRANSFORM)
            {
                localToWorld.rotation = GetLocalRotation(transformNode);
                localToWorld.position = GetLocalPosition(transformNode);
                localToWorld.scale = GetLocalScale(transformNode);
            }

            if(IsRootTransform(transformNode))
            {
                return localToWorld;
            }
            else
            {
                TransformValues parentLocalToWorld = LocalToWorldTransform(FindRootTransform(transformNode));
                return new TransformValues
                (
                    parentLocalToWorld.position + localToWorld.position,
                    parentLocalToWorld.rotation * localToWorld.rotation,
                    new Vector3
                    (
                        parentLocalToWorld.scale.x * localToWorld.scale.x,
                        parentLocalToWorld.scale.y * localToWorld.scale.y,
                        parentLocalToWorld.scale.z * localToWorld.scale.z
                    )
                );
            }
        }
        #endregion
    }

