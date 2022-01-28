using UnityEngine;
using UnityEditor;
using System.Collections;
using NumSharp;
using System.IO;


public class USTextureBuilder : EditorWindow
{
    string inputPath, outputPath;
    int width = 186, height = 186, depth = 186;

    [MenuItem("Window/3D Texture Builders/Ultrasound Texture Builder")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(USTextureBuilder));
    }

    void OnEnable()
    {
        inputPath = "Assets/Resources/VolumeRaw/US/";
        outputPath = "Assets/Resources/VolumeTextures/US/";
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
        Texture3D density = new Texture3D(width, height, depth, TextureFormat.R8, false);
        // TODO: Confirm if these are good settings
        density.wrapMode = TextureWrapMode.Clamp;
        density.filterMode = FilterMode.Bilinear;
        density.anisoLevel = 0;
        // Done

        // TODO: For larger textures, make a tiled version (i.e. take n rows at a time)
        var array = np.load(inputPath);
        int size = array.shape[0] * array.shape[1] * array.shape[2];

        if (size != textureSize)
        {
            Debug.LogError("Mismatch between desired texture resolution and input resolution!");
        }

        byte[] densityVals = new byte[textureSize];
        var flatArray = array.flat;

        for (int i = 0; i < textureSize ; i++)
        {
            densityVals[i] = flatArray[i];
        }

        density.SetPixelData(densityVals, 0);
        density.Apply();

        AssetDatabase.CreateAsset(density, outputPath);
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

        if (GUILayout.Button("Generate texture"))
        {
            GenerateTexture(inputPath, outputPath, width, height, depth);
        }
    }
}