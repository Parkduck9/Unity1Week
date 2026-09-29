using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// batchmode에서 결과를 눈으로 확인하기 위한 스크린샷 도구.
/// </summary>
public static class PreviewCapture
{
    /// <param name="beforeRender">렌더 직전에 카메라를 받아 추가 설정 (예: UI Canvas를 이 카메라에 연결)</param>
    public static void Capture(string file, Vector3 position, Quaternion rotation, float fieldOfView = 60f, int width = 960, int height = 540,
        Action<Camera> beforeRender = null)
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
        beforeRender?.Invoke(cam);
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
