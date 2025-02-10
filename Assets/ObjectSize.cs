using UnityEngine;

public class ObjectSize : MonoBehaviour
{
    void Start()
    {
        // 例: 同じGameObjectにRendererが付いている場合
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            Vector3 objectSize = rend.bounds.size;

            // Unity上では1ユニット＝1mとみなすため
            float widthInMeters = objectSize.x;
            float heightInMeters = objectSize.y;
            float depthInMeters = objectSize.z;

            Debug.Log("Width (m): " + widthInMeters);
            Debug.Log("Height (m): " + heightInMeters);
            Debug.Log("Depth (m): " + depthInMeters);

            // 必要に応じてcmに変換
            float widthInCentimeters = widthInMeters * 100f;
            float heightInCentimeters = heightInMeters * 100f;
            float depthInCentimeters = depthInMeters * 100f;

            Debug.Log("Width (cm): " + widthInCentimeters);
            Debug.Log("Height (cm): " + heightInCentimeters);
            Debug.Log("Depth (cm): " + depthInCentimeters);
        }
    }
}