using UnityEngine;
using UnityEditor;
using System.Collections;
using System.IO;

public class CTTextureBuilder : EditorWindow
{
    string inputPath, outputPath;
    int width = 512, height = 512, depth = 310;

    [MenuItem("Window/3D Texture Builders/Computed Tomography Texture Builder")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(CTTextureBuilder));
    }

    void OnEnable()
    {
        inputPath = "Assets/VolumeData/CT/";
        outputPath = "Assets/Resources/VolumeTextures/CT/";
    }

    void GenerateTexture(string inputPath, string outputPath, int width, int height, int depth)
    {
        if (!File.Exists(inputPath) || (!inputPath.EndsWith(".raw") && !inputPath.EndsWith(".zraw")))
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

        Texture3D density = new Texture3D(width, height, depth, TextureFormat.R16, false);

        density.wrapMode = TextureWrapMode.Clamp;
        density.filterMode = FilterMode.Bilinear;
        density.anisoLevel = 0;

        int min = short.MaxValue;
        int max = short.MinValue;
        long sum = 0;

        // TODO: For larger textures, make a tiled version (i.e. take n rows at a time)
        using (var stream = new FileStream(inputPath, FileMode.Open))
        {
            var len = stream.Length;

            ushort[] colors = new ushort[textureSize];

            // Hallo, Bendik from May 2022 here. This is tuned for a volume with values between -1000 and ~3000
            // For a volume consisting of only byte values, use an R8 texture and byte values instead of shorts (and only read one byte)
            // The idea that you see here is basically concatenating the bytes to make a 12 bit number, but to make it work with my transfer function I had to multiply by 16 to make it 16 bit
            // This means you probably have to change this function if using it for a later project. It could also probably be a compute shader or at least multicore...
            // *Smoke bomb* *Evil laugh* *Coughing* *Cartoony run-away sound*
            for (int i = 0; i < textureSize; i++)
            {
                byte lower = (byte)stream.ReadByte();
                byte higher = (byte)stream.ReadByte();
                short val = (short)(lower + (higher << 8));
                colors[i] = (ushort)((val + 1024)*16);
                min = (val < min) ? val : min;
                max = (val > max) ? val : max;
            }

            density.SetPixelData(colors, 0);
            density.Apply();
        }

        Debug.Log(min);
        Debug.Log(max);

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
