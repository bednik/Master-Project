using UnityEngine;
using UnityEditor;
using System.Collections;
using NumSharp;
using System.IO;


public class USTextureBuilder : EditorWindow
{
    string inputPath, outputPath;
    int width = 186, height = 186, depth = 186, amount = 11;
    bool generate = false, generating = false;

    [MenuItem("Window/3D Texture Builders/Ultrasound Texture Builder")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(USTextureBuilder));
    }

    void OnEnable()
    {
        inputPath = "Assets/Resources/VolumeRaw/US/A6/";
        outputPath = "Assets/Resources/VolumeTextures/US/A6/";
    }

    void GenerateTexture(string inputPath, string outputPath, int width, int height, int depth)
    {
        if (!File.Exists(inputPath))
        {
            Debug.LogError("Error: File not found. The numpy array at " + inputPath + " does not exist");
            return;
        }
        Debug.Log("Generating texture from " + inputPath);

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

        Debug.Log("Finished generating texture at " + outputPath);

        AssetDatabase.CreateAsset(density, outputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    void generateTextures(string inputPath, string outputPath, int width, int height, int depth, int amount)
    {
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        for (int i = 1; i <= amount + 1; i++)
        {
            string volPath = "";
            string outPath = "";
            if (i < 10)
            {
                volPath = inputPath + "vol0" + i + ".npy";
                outPath = outputPath + "vol0" + i + ".asset";
            }
            else
            {
                
                volPath = inputPath + "vol" + i + ".npy";
                outPath = outputPath + "vol0" + i + ".asset";
            }

            

            GenerateTexture(volPath, outPath, width, height, depth);
        }
    }

    void OnGUI()
    {
        inputPath = EditorGUILayout.TextField("Input folder", inputPath);
        outputPath = EditorGUILayout.TextField("Output texture folder", outputPath);
        width = EditorGUILayout.IntField("Width:", width);
        height = EditorGUILayout.IntField("Height:", height);
        depth = EditorGUILayout.IntField("Depth:", depth);
        amount = EditorGUILayout.IntField("Amount of images:", amount);

        if (GUILayout.Button("Generate texture"))
        {
            generate = true;
        }
    }

    private void Update()
    {
        if (!generating && generate)
        {
            generate = false;
            generating = true;
            Debug.Log("Test");
            generateTextures(inputPath, outputPath, width, height, depth, amount);
        }
    }
}