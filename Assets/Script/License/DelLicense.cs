using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DelLicense : MonoBehaviour
{
    // Start is called before the first frame update
    public void dellicense()
    {      
    PlayerPrefs.DeleteKey("isLicensed");
    PlayerPrefs.DeleteKey("license_key");
    PlayerPrefs.Save();  
    }

}
