#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieGame.UnityClient.Core;

namespace ZombieGame.UnityClient.Editor
{
  public static class ZombieGameEditorMenus
  {
    private const string ConfigAssetPath = "Assets/ZombieGame/Resources/GameRuntimeConfig.asset";

    [MenuItem("ZombieGame/Create Runtime Config Asset")]
    public static void CreateRuntimeConfigAsset()
    {
      if (File.Exists(ConfigAssetPath))
      {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameRuntimeConfig>(ConfigAssetPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
        return;
      }

      Directory.CreateDirectory(Path.GetDirectoryName(ConfigAssetPath)!);
      var asset = ScriptableObject.CreateInstance<GameRuntimeConfig>();
      AssetDatabase.CreateAsset(asset, ConfigAssetPath);
      AssetDatabase.SaveAssets();
      Selection.activeObject = asset;
      EditorGUIUtility.PingObject(asset);
    }

    [MenuItem("ZombieGame/Open Art/Cards Folder")]
    public static void OpenCardsFolder()
    {
      const string folder = "Assets/ZombieGame/Art/Cards";
      if (!AssetDatabase.IsValidFolder("Assets/ZombieGame/Art/Cards"))
      {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "ZombieGame/Art/Cards"));
        AssetDatabase.Refresh();
      }

      var obj = AssetDatabase.LoadAssetAtPath<Object>(folder);
      Selection.activeObject = obj;
      EditorGUIUtility.PingObject(obj);
    }
  }
}
#endif
