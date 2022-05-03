using UnityEngine;
using VolumeRendering;
using MathNet.Numerics.Interpolation;
using System.Collections;

public class VolumeRenderController : MonoBehaviour
{
    #region properties
    public VolumeType volumeType;

    public EmptySpaceSkipMethod emptySpaceSkipMethod;

    public Texture3D m_volume;

    public Texture2D transferFunction;

    private Texture2D byteToFloat;

    public int m_blockSize;

    public bool emptySpaceSkip;

    public RenderTexture outMap;
    private Texture3D storeTex;

    private int currentTex = 1;
    private Material material;
    private bool doneESS = false;
    private bool ready = true;
    public ComputeShader cs, cs_occ;

    private int[] dims;

    public float delay = 0.1f;
    #endregion

    IEnumerator PreprocessESS()
    {
        int kernelHandle = cs_occ.FindKernel("CSMain");
        cs_occ.SetTexture(kernelHandle, "transferFunction", transferFunction, 0);
        cs_occ.SetTexture(kernelHandle, "volume", m_volume, 0);
        cs_occ.SetTexture(kernelHandle, "Result", outMap, 0);
        cs_occ.SetInt("blockSize", m_blockSize);

        cs_occ.Dispatch(kernelHandle, Mathf.CeilToInt((float)dims[0] / 8), Mathf.CeilToInt((float)dims[1] / 8), dims[2]);
        yield return null;

        if (emptySpaceSkipMethod != EmptySpaceSkipMethod.CHEBYSHEV)
        {
            Graphics.CopyTexture(outMap, material.GetTexture("_OccupancyMap"));
            yield return null;
        }
        else
        {
            Graphics.CopyTexture(outMap, storeTex);
            yield return null;

            //////// TRANSFORM 1 ////////
            kernelHandle = cs.FindKernel("Trans1");
            cs.SetTexture(kernelHandle, "InMap", storeTex, 0);
            cs.SetTexture(kernelHandle, "OutMap", outMap, 0);
            cs.SetTexture(kernelHandle, "ByteToFloat", byteToFloat, 0);
            cs.Dispatch(kernelHandle, 1, Mathf.CeilToInt((float)dims[1] / 8), Mathf.CeilToInt((float)dims[2] / 8));
            yield return null;

            // Copy to normal texture
            Graphics.CopyTexture(outMap, storeTex);
            yield return null;

            //////// TRANSFORM 2 ////////
            kernelHandle = cs.FindKernel("Trans2");
            cs.SetTexture(kernelHandle, "InMap", storeTex);
            cs.SetTexture(kernelHandle, "OutMap", outMap, 0);
            cs.SetTexture(kernelHandle, "ByteToFloat", byteToFloat, 0);
            cs.Dispatch(kernelHandle, Mathf.CeilToInt((float)dims[0] / 8), 1, Mathf.CeilToInt((float)dims[2] / 8));
            yield return null;

            // Copy to normal texture
            Graphics.CopyTexture(outMap, storeTex);
            yield return null;

            //////// TRANSFORM 3 ////////

            kernelHandle = cs.FindKernel("Trans3");
            cs.SetTexture(kernelHandle, "InMap", storeTex);
            cs.SetTexture(kernelHandle, "OutMap", outMap, 0);
            cs.SetTexture(kernelHandle, "ByteToFloat", byteToFloat, 0);
            cs.Dispatch(kernelHandle, Mathf.CeilToInt((float)dims[0] / 8), Mathf.CeilToInt((float)dims[1] / 8), 1);
            yield return null;

            // Copy to normal texture
            Graphics.CopyTexture(outMap, material.GetTexture("_DistanceMap"));
            yield return null;

            //material.SetTexture("_DistanceMap", storeTex);
        }

        doneESS = true;
    }

    private IEnumerator updateUS()
    {
        if (currentTex >= 11)
        {
            currentTex = 1;
        }
        else
        {
            currentTex++;
        }

        // Fetch the new texture
        ResourceRequest req;
        req = Resources.LoadAsync("VolumeTextures/US/A6/vol0" + currentTex);
        if (!req.isDone)
        {
            yield return null;
        }
        m_volume = (Texture3D)req.asset;

        yield return new WaitForSecondsRealtime(delay);

        // Preprocess empty space skipping
        if (emptySpaceSkip)
        {
            //doneESS = false;
            yield return StartCoroutine(PreprocessESS());
            //if (!doneESS)
            //{
            //    yield return null;
            //}
        }

        material.SetTexture("_Volume", m_volume);
        ready = true;
    }

    private void Start()
    {
        dims = new int[3] { Mathf.CeilToInt((float)m_volume.width / m_blockSize), Mathf.CeilToInt((float)m_volume.height / m_blockSize), Mathf.CeilToInt((float)m_volume.depth / m_blockSize) };
        material = GetComponent<Renderer>().material;

        byteToFloat = new Texture2D(256, 1, TextureFormat.R8, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
            anisoLevel = 0
        };

        byte[] bytes = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            bytes[i] = (byte)i;
        }
        byteToFloat.SetPixelData<byte>(bytes, 0);
        byteToFloat.Apply();

        outMap = new RenderTexture(dims[0], dims[1], 0, RenderTextureFormat.R8)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = dims[2],
            enableRandomWrite = true,
            filterMode = FilterMode.Point
        };
        outMap.Create();

        storeTex = new Texture3D(dims[0], dims[1], dims[2], TextureFormat.R8, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
            anisoLevel = 0
        };
        storeTex.Apply();

        //cs_occ = (ComputeShader)Resources.Load("ComputeShaders/UniformSubdivision");
        //cs = (ComputeShader)Resources.Load("ComputeShaders/Chebyshev");
    }

    private void FixedUpdate()
    {
        if (volumeType == VolumeType.US && ready)
        {
            ready = false;
            StartCoroutine(updateUS());
        }
    }
}
