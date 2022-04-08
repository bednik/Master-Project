using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Reset : MonoBehaviour
{
    public GameObject render;
    public GameObject ui;

    public void Panic()
    {
        Destroy(render);
        GameObject menu = Instantiate(ui, new Vector3(0, -0.2f, 1f), Quaternion.identity);
        VolumeBuilder x = menu.transform.GetChild(menu.transform.childCount - 1).GetComponent(typeof(VolumeBuilder)) as VolumeBuilder;
        x.panic = Resources.Load<Reset>("Prefabs/Reset button");
        Destroy(gameObject);
    }
}
