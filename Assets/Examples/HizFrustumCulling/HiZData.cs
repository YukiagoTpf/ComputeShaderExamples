using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HiZData
{
    public static Vector2Int HIZMapSize = new Vector2Int(2048, 1024);
    public RenderTexture HIZ_MAP;
    private HiZData()
    {

    }
    private static HiZData _instance;
    public static HiZData GetInstance()
    {
        if(_instance == null)
        {
            _instance = new HiZData();
        }
        return _instance;
    }

}
