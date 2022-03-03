using UnityEngine;
using UnityEditor;
using System.Collections;
using NumSharp;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class MRITextureBuilder : EditorWindow
{
    string inputPath, headerPath, outputPath;
    int width = 320, height = 300, depth = 54;

    [MenuItem("Window/3D Texture Builders/Magnetic Resonance Texture Builder")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(MRITextureBuilder));
    }

    void OnEnable()
    {
        inputPath = "Assets/Resources/VolumeRaw/MRI/";
        outputPath = "Assets/Resources/VolumeTextures/MRI/";
        headerPath = "Assets/Resources/VolumeRaw/MRI/";
    }

    IDictionary<string, int> GetMetadata(string headerPath)
    {
        StreamReader reader = File.OpenText(headerPath);
        IDictionary<string, int> metadata = new Dictionary<string, int>();

        string[] desiredKeys = { "cal_max", "cal_min" };
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            string[] items = line.Split('\t');
            if (desiredKeys.Contains(items[0]))
            {
                metadata.Add(items[0], System.Int32.Parse(items[2]));
            }
        }
        return metadata;
    }

    void GenerateTexture(string inputPath, string outputPath, string headerPath, int width, int height, int depth)
    {
        if (!File.Exists(inputPath))
        {
            Debug.LogError("Error: File not found. The numpy array at " + inputPath + " does not exist");
            return;
        }

        IDictionary<string, int> metadata = GetMetadata(headerPath);

        int textureSize = width * height * depth;

        // From Anders Tasken's project
        Texture3D texture = new Texture3D(width, height, depth, TextureFormat.R8, false);
        // TODO: Confirm if these are good settings
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 0;
        // Done

        // TODO: For larger textures, make a tiled version (i.e. take n rows at a time)
        var array = np.load(inputPath);
        int size = array.shape[0] * array.shape[1] * array.shape[2];

        if (size != textureSize)
        {
            Debug.LogError("Mismatch between desired texture resolution and input resolution!");
        }

        byte[] colors = new byte[textureSize];
        var flatArray = array.flat;

        int calmax = 0, calmin = 0;
        metadata.TryGetValue("cal_max", out calmax);
        metadata.TryGetValue("cal_min", out calmin);

        // Since my data satisfies this, I'll keep it like this for now
        // In reality, values below calmin are black, those above calmax are white, and those between are something else
        // Sometimes a separate file will define colors for different tissue types in a color lookup table
        // Such a file can be parsed in a similar fashion to the metadata file
        if (calmax == 0 && calmin == calmax) // Undefined color mappings
        {
            for (int i = 0; i < textureSize; i++)
            {
                double val = flatArray.GetDouble(i);
                colors[i] = (byte)(val / 4096);
            }
        }
        else
        {
            Debug.Log("I don't support explicit transfer functions specified by the metadata yet");
            return;
        }
        
        texture.SetPixelData(colors, 0);
        texture.Apply();

        AssetDatabase.CreateAsset(texture, outputPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    void OnGUI()
    {
        inputPath = EditorGUILayout.TextField("Input numpy array file", inputPath);
        headerPath = EditorGUILayout.TextField("Nii header file", headerPath);
        outputPath = EditorGUILayout.TextField("Output texture path", outputPath);
        width = EditorGUILayout.IntField("Width:", width);
        height = EditorGUILayout.IntField("Height:", height);
        depth = EditorGUILayout.IntField("Depth:", depth);

        if (GUILayout.Button("Generate texture"))
        {
            GenerateTexture(inputPath, outputPath, headerPath, width, height, depth);
        }
    }
}