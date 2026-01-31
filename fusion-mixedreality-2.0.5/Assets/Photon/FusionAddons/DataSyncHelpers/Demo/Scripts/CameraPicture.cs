using Fusion;
using TMPro;
using UnityEngine;
using Fusion.Addons.DataSyncHelpers;

public class CameraPicture : StreamSynchedBehaviour
{
    Texture2D texture;
    [SerializeField] MeshRenderer pictureRenderer;
    [SerializeField] Material actualPictureMaterial;
    [SerializeField] Vector2 renderTextureCopyScale = new Vector2(1, 1);
    [SerializeField] Vector2 renderTextureCopyOffset = new Vector2(0, 0);
    [SerializeField] TextMeshPro progressText;

    private void Awake()
    {
        if (pictureRenderer == null) pictureRenderer = GetComponentInChildren<MeshRenderer>();
        if (progressText == null) progressText = GetComponentInChildren<TextMeshPro>();
        if (pictureRenderer == null) Debug.LogError("pictureRenderer not set!");
        if (progressText != null) progressText.gameObject.SetActive(false);
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();
        ResetTexture();
    }

    public void SetPictureTexture(RenderTexture renderTexture)
    {
        if (Object.HasStateAuthority && texture == null)
        {
            SaveRenderTexturePixels(renderTexture);

            if(actualPictureMaterial != null)
            {
                pictureRenderer.material = actualPictureMaterial;
            }
            pictureRenderer.material.mainTexture = texture;

            // Send the texture to all users
            Send(ByteArrayTools.TextureData(renderTexture));
        }
    }

    protected override void OnDataProgress(float progress)
    {
        base.OnDataProgress(progress);
        if (progressText)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = $"{(int)(100 * progress)}%";
        }
    }

    public override void OnDataChunkReceived(byte[] data, PlayerRef source, float time)
    {
        Debug.LogError($"Image received");
        progressText.gameObject.SetActive(false);
        base.OnDataChunkReceived(data, source, time);
        ByteArrayTools.FillTexture(ref texture, data);
        if(actualPictureMaterial != null)
        {
            pictureRenderer.material = actualPictureMaterial;
        }
        pictureRenderer.material.mainTexture = texture;
    }

    #region Texture handling
    void SaveRenderTexturePixels(RenderTexture renderTexture)
    {
        // Ensure that any pre-existing allocated texture is freed
        ResetTexture();

        var temporaryRenderTexture = RenderTexture.GetTemporary(renderTexture.descriptor);
        Graphics.Blit(renderTexture, temporaryRenderTexture, renderTextureCopyScale, renderTextureCopyOffset);
        Graphics.Blit(temporaryRenderTexture, renderTexture);
        RenderTexture.ReleaseTemporary(temporaryRenderTexture);

        texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, true);
        var currentRenderTexture = RenderTexture.active;
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture.Apply();
        RenderTexture.active = currentRenderTexture;
    }

    public void ResetTexture()
    {
        if (texture)
        {
            Destroy(texture);
        }
        texture = null;
    }
    #endregion
}

