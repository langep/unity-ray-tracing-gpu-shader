using System.Collections.Generic;
using UnityEngine;

// RayTracing controller to be attached to the Main Camera
[RequireComponent(typeof(Camera))]
public class RayTracingMaster : MonoBehaviour
{

    [SerializeField] private ComputeShader _shader;
    [SerializeField] private Texture _skyboxTexture;
    [SerializeField] private Light _directionalLight;

    [Range(0.0f, 1.9f)] [SerializeField] private float _globalIllumination = 1.0f;
    [Range(0, 9)] [SerializeField] private int _reflections = 7;

    [Header("Spheres")]
    [SerializeField] private int _sphereSeed = 111;
    [SerializeField] private Vector2 _sphereRadius = new Vector2(3.0f, 8.0f);
    [Range(0, 200)] [SerializeField] private int _spheresMax = 146;
    [SerializeField] private float _spherePlacementRadius = 100.0f;

    private Camera _camera;
    private RenderTexture _target;
    private RenderTexture _converged;
    // Progressive sampling variables
    private uint _currentSample = 0;
    private Material _addMaterial;
    // Sphere buffer
    private ComputeBuffer _sphereBuffer;

    // Automatically called by Unity when the object is initialized
    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    // Automatically called by Unity when the object is enabled (after initializaiton)
    private void OnEnable()
    {
        _currentSample = 0;
        SetUpScene();
    }

    // Automatically called by Unity when the object is disabled
    private void OnDisable()
    {
        if (_sphereBuffer != null)
        {
            _sphereBuffer.Release();
        }

    }

    // Automatically called by Unity each frame
    private void Update()
    {
        // Reset _currentSample count if camera has moved
        if (transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
        }

        if (_directionalLight.transform.hasChanged)
        {
            _currentSample = 0;
            _directionalLight.transform.hasChanged = false;
        }
    }

    // Automatically called by Unity when a value in the inspector changes
    private void OnValidate()
    {
        _currentSample = 0;
        SetUpScene();
    }

    // Automatically called by Unity when Camera finished rendering
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        SetShaderParameters();
        Render(destination);
    }

    private void SetUpScene()
    {
        Random.InitState(_sphereSeed);
        List<Sphere> spheres = new List<Sphere>();
        // Add a number of random spheres
        for (int i = 0; i < _spheresMax; i++)
        {
            Sphere sphere = new Sphere();
            // Create random position and radius
            sphere.radius = _sphereRadius.x + Random.value * (_sphereRadius.y - _sphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * _spherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);
            // Reject spheres that are intersecting others
            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                {
                    goto SkipSphere;
                }

            }

            // Albedo, specular color and smoothness and emission
            Color color = Random.ColorHSV();
            float chance = Random.value;
            if (chance < 0.85f)
            {
                bool metal = chance < 0.7f;
                sphere.albedo = metal ? Vector4.zero : new Vector4(color.r, color.g, color.b);
                sphere.specular = metal ? new Vector4(color.r, color.g, color.b) : new Vector4(0.04f, 0.04f, 0.04f);
                sphere.smoothness = Random.value;
            }
            else
            {
                Color emission = Random.ColorHSV(0, 1, 0, 1, 1.0f, 3.0f);
                sphere.emission = new Vector3(emission.r, emission.g, emission.b);
            }

            // Add the sphere to the list
            spheres.Add(sphere);
        SkipSphere:
            continue;
        }


        // Compute size of Sphere struct in bytes
        int strideSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Sphere));
        // Create and ssign to compute buffer
        if (_sphereBuffer != null)
        {
            _sphereBuffer.Release();
        }
        _sphereBuffer = new ComputeBuffer(spheres.Count, strideSize);
        _sphereBuffer.SetData(spheres);
    }

    // Set parameters on the shader.
    private void SetShaderParameters()
    {
        // Camera parameters
        _shader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        _shader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        // Skybox texture
        _shader.SetTexture(0, "_SkyboxTexture", _skyboxTexture);
        // Set ground plane location
        _shader.SetFloat("_GroundPlaneY", 0.0f);
        // Pixel offset for progressive sampling
        _shader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        // Number of reflections
        _shader.SetInt("_Reflections", _reflections);
        // Directional Light information for diffuse reflections
        Vector3 l = _directionalLight.transform.forward;
        _shader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, _directionalLight.intensity));
        // The spheres
        _shader.SetBuffer(0, "_Spheres", _sphereBuffer);
        // Randomness seed
        _shader.SetFloat("_Seed", Random.value);
        // GLobal Illumination
        _shader.SetFloat("_GlobalIllumination", _globalIllumination);

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

        // Blit the result texture to the screen with progressive sampling
        // by using our AddShader
        if (_addMaterial == null)
        {
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        }
        _addMaterial.SetFloat("_Sample", _currentSample);
        Graphics.Blit(_target, _converged, _addMaterial);
        Graphics.Blit(_converged, destination);
        _currentSample++;
    }

    // Initializes the render texture with the correct dimensions
    private void InitRenderTexture()
    {
        // We use the +converged texture for higher precision
        if (_converged == null || _converged.height != Screen.height || _converged.width != Screen.width)
        {
            // Reset _currentSample count to restart progressive sampling
            _currentSample = 0;

            // Release the current (mismatching) texture if we have one
            if (_converged != null)
            {
                _converged.Release();
            }

            // Create a new render texture
            _converged = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();
        }


        if (_target == null || _target.height != Screen.height || _target.width != Screen.width)
        {
            // Reset _currentSample count to restart progressive sampling
            _currentSample = 0;

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
