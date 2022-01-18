using UnityEngine;
using UnityEditor;
using System.Collections;
using NumSharp;
using System.IO;


public class MRITextureBuilder : EditorWindow
{
    string inputPath, outputPath;
    int width = 320, height = 300, depth = 54;
    bool color = false;

    [MenuItem("Window/3D Texture Builders/Magnetic Resonance Texture Builder")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(MRITextureBuilder));
    }

    void OnEnable()
    {
        inputPath = "Assets/Resources/VolumeRaw/MRI/";
        outputPath = "Assets/Resources/VolumeTextures/MRI/";
    }

    void GenerateTexture(string inputPath, string outputPath, int width, int height, int depth)
    {
        if (!File.Exists(inputPath))
        {
            Debug.LogError("Error: File not found. The numpy array at " + inputPath + " does not exist");
            return;
        }

        int textureSize = width * height * depth;

        // From Anders Tasken's project
        Texture3D texture = new Texture3D(width, height, depth, TextureFormat.ARGB32, false);
        // TODO: Confirm if these are good settings
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 0;
        // Done

        // TODO: For larger textures, make a tiled version (i.e. take n rows at a time)
        Debug.Log(inputPath);
        var array = np.load(inputPath);
        int size = array.shape[0] * array.shape[1] * array.shape[2];

        if (size != textureSize)
        {
            Debug.LogError("Mismatch between desired texture resolution and input resolution!");
        }

        Color32[] colors = new Color32[textureSize];
        var flatArray = array.flat;

        for (int i = 0; i < textureSize; i++)
        {
            byte b = flatArray[i];
            colors[i] = new Color32(b, b, b, b);
        }

        texture.SetPixels32(colors);
        texture.Apply();

        AssetDatabase.CreateAsset(texture, outputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    void OnGUI()
    {
        inputPath = EditorGUILayout.TextField("Input numpy array file", inputPath);
        outputPath = EditorGUILayout.TextField("Output texture path", outputPath);
        width = EditorGUILayout.IntField("Width:", width);
        height = EditorGUILayout.IntField("Height:", height);
        depth = EditorGUILayout.IntField("Depth:", depth);
        color = EditorGUILayout.Toggle("Generate colored volume", color);

        if (GUILayout.Button("Generate texture"))
        {
            GenerateTexture(inputPath, outputPath, width, height, depth);
        }
    }
}