using System.IO;
using UnityEngine;

/// <summary>
/// batchmode에서 결과를 눈으로 확인하기 위한 스크린샷 도구.
/// </summary>
public static class PreviewCapture
{
    public static void Capture(string file, Vector3 position, Quaternion rotation, float fieldOfView = 60f, int width = 960, int height = 540)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file));

        var camGo = new GameObject("PreviewCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(position, rotation);
        cam.fieldOfView = fieldOfView;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.82f, 0.86f, 0.92f);

        var rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());

        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
    }
}
