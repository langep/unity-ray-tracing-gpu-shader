using UnityEngine;

// RayTracing controller to be attached to the Main Camera
[RequireComponent(typeof(Camera))]
public class RayTracingMaster : MonoBehaviour
{

    [SerializeField] private ComputeShader _shader;
    [SerializeField] private Texture _skybox_texture;


    private Camera _camera;
    private RenderTexture _target;

    // Automatically called by Unity when the object is initialized
    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    // Automatically called by Unity when Camera finished rendering
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    // Set parameters on the shader.
    private void SetShaderParameters()
    {
        // Camera parameters
        _shader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        _shader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);

        // Skybox texture
        _shader.SetTexture(0, "_SkyboxTexture", _skybox_texture);

        // Set ground plane location
        _shader.SetFloat("_GroundPlaneY", 0.0f);
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
