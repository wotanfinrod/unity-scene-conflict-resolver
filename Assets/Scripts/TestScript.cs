using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Scene newScene = SceneManager.GetSceneByName("SampleScene");
        Scene oldScene = SceneManager.GetSceneByName("SampleScene_old");

        GameObject[] newSceneRoots = newScene.GetRootGameObjects();
        GameObject[] oldSceneRoots = oldScene.GetRootGameObjects();

        for(int i = 0 ; i < newSceneRoots.Length; i++)
        {
            if(oldSceneRoots[i].Equals(newSceneRoots[i]))
            {
                Debug.Log($"{oldSceneRoots[i].name} is equal to {newSceneRoots[i].name}");
            }
            else
            {
                Debug.Log($"{oldSceneRoots[i].name} is not equal to {newSceneRoots[i].name}");
            }
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
