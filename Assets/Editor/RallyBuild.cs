using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RallyBuild
{
    static RallyBuild() { EditorApplication.delayCall += RetainRuntimeShaders; }

    // Materials are procedural: retain their shaders even with no serialized material assets.
    static void RetainRuntimeShaders()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets.Length == 0) return;
        var settings = new SerializedObject(assets[0]);
        var shaders = settings.FindProperty("m_AlwaysIncludedShaders");
        if (shaders == null) return;
        var shader = Shader.Find("Standard");
        if (shader == null) { Debug.LogError("Formula Horizon needs the Built-in Standard shader. Open with Unity 2021.3 LTS and the Built-in render pipeline."); return; }
        for (int i = 0; i < shaders.arraySize; i++)
            if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
        int index = shaders.arraySize;
        shaders.InsertArrayElementAtIndex(index);
        shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
        settings.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Formula Horizon/Open Game Scene")]
    public static void Open()
    {
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        EditorSceneManager.OpenScene("Assets/Scenes/RallyMain.unity");
    }
    [MenuItem("Formula Horizon/Build Windows Game")]
    public static void Windows()
    {
        RetainRuntimeShaders();
        string folder=EditorUtility.OpenFolderPanel("Choose Windows build folder","","");
        if(string.IsNullOrEmpty(folder))return;
        var result=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/RallyMain.unity"},folder+"/FormulaHorizon.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
        Debug.Log("Build result: "+result.summary.result);
    }
}
