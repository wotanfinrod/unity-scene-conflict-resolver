using UnityEditor;

public static class AssetLoader
{
    public static UnityEngine.Object LoadAssetByFileIDAndGUID(string fileid, string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (path == null)
        {
            return null;
        }

        long fileIdLong = long.Parse(fileid);

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (UnityEngine.Object asset in assets)
        {
            if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier<UnityEngine.Object>(asset.GetInstanceID(), out string assetGuid, out long assetLocalId)) 
            {
                if(assetLocalId == fileIdLong)
                {
                    return asset;
                }
            }
        }
        return null;
    }

}