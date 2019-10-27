using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour
{

    [SerializeField] private ComputeShader _shader;
    private RenderTexture _target;

    // Automatically called by Unity when Camera finished rendering
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        // Make sure we have a render target of appropriate dimension
        InitRenderTexture();

        // Setup the target in the shader
        _shader.SetTexture(0, "Result", _target);

        // We will thread groups of size (threadGroupSizeX x threadGroupSizeY) pizels
        float threadGroupSizeX = 8.0f;
        float threadGroupSizeY = 8.0f;
        int threadGroupsX = Mathf.CeilToInt(Screen.width / threadGroupSizeX);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / threadGroupSizeY);

        // Dispatch the shader
        _shader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Blit the result texture to the screen
        Graphics.Blit(_target, destination);
    }

    // Initializes the render texture with the correct dimensions
    private void InitRenderTexture()
    {
        if (_target == null || _target.height != Screen.height || _target.width != Screen.width)
        {
            // We are either uninitialized or have mismatching dimensions

            // Release the current (mismatching) target if we have one
            if (_target != null)
            {
                _target.Release();
            }

            // Create a new render target
            _target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }
}
