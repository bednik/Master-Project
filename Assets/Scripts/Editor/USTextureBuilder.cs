using UnityEngine;
using UnityEditor;
using System.Collections;
using NumSharp;


public class USTextureBuilder : MonoBehaviour
{
    string inputPath, outputPath;

    [MenuItem("Window/3D Texture Builders/Ultrasound Texture Builder")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(USTextureBuilder));
    }

    void OnGUI()
    {
        // The actual window code goes here
    }
}