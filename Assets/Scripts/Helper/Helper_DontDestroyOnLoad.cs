using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Helper_DontDestroyOnLoad : MonoBehaviour
{
    private static Helper_DontDestroyOnLoad Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
}
