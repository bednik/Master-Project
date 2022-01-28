using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;

public class CTTextureBuilder : EditorWindow
{
    string inputPath, outputPath;
    int width = 154, height = 154, depth = 441;

    [MenuItem("Window/3D Texture Builders/Computed Tomography Texture Builder")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(CTTextureBuilder));
    }

    void OnEnable()
    {
        inputPath = "Assets/Resources/VolumeRaw/CT/";
        outputPath = "Assets/Resources/VolumeTextures/CT/";
    }

    void GenerateTexture(string inputPath, string outputPath, int width, int height, int depth)
    {
        if (!File.Exists(inputPath) || !inputPath.EndsWith(".raw"))
        {
            Debug.LogError("Error: File not found. The RAW file at " + inputPath + " does not exist");
            return;
        }

        if (!outputPath.EndsWith(".asset"))
        {
            Debug.LogError("Error: Invalid output path. The output path should end with '.asset'");
            return;
        }

        int textureSize = width * height * depth;

        Texture3D density = new Texture3D(width, height, depth, TextureFormat.R8, false);

        density.wrapMode = TextureWrapMode.Clamp;
        density.filterMode = FilterMode.Bilinear;
        density.anisoLevel = 0;

        // TODO: For larger textures, make a tiled version (i.e. take n rows at a time)
        using (var stream = new FileStream(inputPath, FileMode.Open))
        {
            var len = stream.Length;

            if (len != textureSize)
            {
                Debug.LogError("Mismatch between desired texture resolution and input resolution!");
                return;
            }

            byte[] colors = new byte[textureSize];
            for (int i = 0; i < textureSize; i++)
            {
                colors[i] = (byte)stream.ReadByte();
            }

            density.SetPixelData(colors, 0);
            density.Apply();
        }
        

        AssetDatabase.CreateAsset(density, outputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    void OnGUI()
    {
        inputPath = EditorGUILayout.TextField("Input RAW file", inputPath);
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
