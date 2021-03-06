using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Reset : MonoBehaviour
{
    public GameObject render;
    public GameObject ui;
    public GameObject speedUI, qualityUI, ambientUI;

    public void Panic()
    {
        VolumeRenderController controller = render.GetComponent<VolumeRenderController>();

        if (controller.emptySpaceSkip && controller.volumeType == VolumeRendering.VolumeType.US)
        {
            controller.outMap.Release();
        }
        if (controller.volumeType == VolumeRendering.VolumeType.US)
        {
            Destroy(speedUI);
        }
        if (controller.emptySpaceSkip)
        {
            Destroy(qualityUI);
        }
        if (controller.shaded)
        {
            Destroy(ambientUI);
        }

        Destroy(render);

        GameObject menu = Instantiate(ui, new Vector3(0, -0.5f, 3f), Quaternion.identity);
        VolumeBuilder x = menu.transform.GetChild(menu.transform.childCount - 1).GetComponent(typeof(VolumeBuilder)) as VolumeBuilder;
        x.panic = Resources.Load<Reset>("Prefabs/Reset button");
        x.speedUI = Resources.Load<GameObject>("Prefabs/Ultrasound delay");
        x.ambientUI = Resources.Load<GameObject>("Prefabs/Ambient light");
        x.qualityUI = Resources.Load<GameObject>("Prefabs/Quality factor");
        Resources.UnloadUnusedAssets();
        Destroy(gameObject);
    }
}
