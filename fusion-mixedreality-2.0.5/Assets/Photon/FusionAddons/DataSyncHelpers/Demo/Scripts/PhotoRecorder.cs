using Fusion;
using UnityEngine;

public class PhotoRecorder : NetworkBehaviour
{
    [SerializeField] NetworkObject picturePrefab;
    [SerializeField] Camera captureCamera;

    private void Awake()
    {
        if (captureCamera == null) captureCamera = GetComponentInChildren<Camera>();
        if (captureCamera == null || captureCamera.targetTexture == null)
        {
            Debug.LogError("A camera with a render texture target should be provided");
        }
    }

    [EditorButton("Shoot picture")]
    public void ShootPicture()
    {
        Debug.Log("OnCameraShoot");
        var pictureGO = Runner.Spawn(picturePrefab, transform.position, transform.rotation, Runner.LocalPlayer);
        var picture = pictureGO.GetComponent<CameraPicture>();
        picture.SetPictureTexture(captureCamera.activeTexture);
    }
}
