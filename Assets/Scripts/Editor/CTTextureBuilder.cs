using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;

public class CTTextureBuilder : EditorWindow
{
    string inputPath, outputPath;
    int width = 154, height = 154, depth = 441;
    bool color = false;

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

        // From Anders Tasken's project
        Texture3D texture = new Texture3D(width, height, depth, TextureFormat.ARGB32, false);
        // TODO: Confirm if these are good settings
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 0;
        // Done

        // TODO: For larger textures, make a tiled version (i.e. take n rows at a time)

        using(var stream = new FileStream(inputPath, FileMode.Open))
        {
            
            var len = stream.Length;

            if (len != textureSize)
            {
                Debug.LogError("Mismatch between desired texture resolution and input resolution!");
                return;
            }

            Color32[] colors = new Color32[textureSize];

            if (!color)
            {
                for (int i = 0; i < textureSize; i++)
                {
                    byte b = (byte)stream.ReadByte();
                    colors[i] = new Color32(b, b, b, b);
                }
            }
            else // Early coloring test based on CT value. TODO: Make it smarter. That's for later though. Not important yet.
            {
                for (int i = 0; i < textureSize; i++)
                {
                    byte b = (byte)stream.ReadByte();
                    if (b <= 85)
                    {
                        colors[i] = new Color32(255, 0, 0, b);
                    }
                    else if (b > 170)
                    {
                        colors[i] = new Color32(0, 0, 255, b);
                    }
                    else
                    {
                        colors[i] = new Color32(0, 255, 0, b);
                    }
                }
            }
            texture.SetPixels32(colors);
            texture.Apply();
        }
        

        AssetDatabase.CreateAsset(texture, outputPath);
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
        color = EditorGUILayout.Toggle("Generate colored volume", color);

        if (GUILayout.Button("Generate texture"))
        {
            GenerateTexture(inputPath, outputPath, width, height, depth);
        }
    }
}
