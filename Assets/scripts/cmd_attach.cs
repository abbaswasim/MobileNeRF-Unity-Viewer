using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class cmd_attach : MonoBehaviour
{
	public Material m_Material;
	CommandBuffer buf = null;
	CommandBuffer finalbuf = null;
	Mesh fullscreen_quad_mesh;

	// Start is called before the first frame update
	void Start()
    {
		if (!m_Material)
		{
			Debug.Log("There is no material assigned");
		}

		buf = new CommandBuffer();
		buf.name = "Deferred gbuffer";

		finalbuf = new CommandBuffer();
		finalbuf.name = "After Final pass";

		var cam = Camera.main;
		if (!cam)
        {
			Debug.Log("There is no camera available");
			return;
		}

		//cam.RemoveAllCommandBuffers();
		cam.RemoveCommandBuffer(CameraEvent.AfterGBuffer, buf);
		// cam.AddCommandBuffer(CameraEvent.BeforeLighting, buf);
		// cam.AddCommandBuffer(CameraEvent.AfterSkybox, buf); // This has stopped working for some reason
		cam.AddCommandBuffer(CameraEvent.AfterGBuffer, buf);

		cam.RemoveCommandBuffer(CameraEvent.AfterFinalPass, finalbuf);
		cam.AddCommandBuffer(CameraEvent.AfterFinalPass, finalbuf);

		if (fullscreen_quad_mesh == null)
			fullscreen_quad_mesh = create_fullscreen_mesh();
	}

	// From URP source code
	static Mesh create_fullscreen_mesh()
	{
		// Simple full-screen triangle.
		Vector3[] positions =
			{
				new Vector3(-1.0f,  1.0f, 0.0f),
				new Vector3(-1.0f, -3.0f, 0.0f),
				new Vector3(3.0f,  1.0f, 0.0f)
			};

		Vector2[] uvs =
			{
				new Vector2(0.0f,  0.0f),
				new Vector2(0.0f, 2.0f),
				new Vector2(2.0f,  0.0f)
			};

		int[] indices = { 0, 1, 2 };

		Mesh mesh = new Mesh();
		mesh.indexFormat = IndexFormat.UInt16;
		mesh.vertices = positions;
		mesh.triangles = indices;
		mesh.uv = uvs;

		return mesh;
	}

	// Update is called once per frame
	void Update()
    {}

    public void OnWillRenderObject()
    {
		Matrix4x4 matrix = Matrix4x4.identity;

        buf.Clear();

		int _g_buffer_0_copy = Shader.PropertyToID("_GBuffer0");
		buf.GetTemporaryRT(_g_buffer_0_copy, -1, -1, 0, FilterMode.Bilinear);
		buf.Blit(BuiltinRenderTextureType.GBuffer0, _g_buffer_0_copy);

		int _g_buffer_1_copy = Shader.PropertyToID("_GBuffer1");
		buf.GetTemporaryRT(_g_buffer_1_copy, -1, -1, 0, FilterMode.Bilinear);
		buf.Blit(BuiltinRenderTextureType.GBuffer1, _g_buffer_1_copy);

		int _g_buffer_2_copy = Shader.PropertyToID("_GBuffer2");
		buf.GetTemporaryRT(_g_buffer_2_copy, -1, -1, 0, FilterMode.Bilinear);
		buf.Blit(BuiltinRenderTextureType.GBuffer2, _g_buffer_2_copy);

		int _g_buffer_3_copy = Shader.PropertyToID("_GBuffer3");
		buf.GetTemporaryRT(_g_buffer_3_copy, -1, -1, 0, FilterMode.Bilinear);
		buf.Blit(BuiltinRenderTextureType.GBuffer3, _g_buffer_3_copy);

		int depth = Shader.PropertyToID("_DepthTemp");
		buf.GetTemporaryRT(depth, -1, -1, 0, FilterMode.Bilinear);

		RenderTargetIdentifier[] mrt = { BuiltinRenderTextureType.GBuffer0, BuiltinRenderTextureType.GBuffer1, BuiltinRenderTextureType.GBuffer2 , BuiltinRenderTextureType.GBuffer3 };
		buf.SetRenderTarget(mrt, depth);

		buf.ClearRenderTarget(RTClearFlags.All, Color.clear, 1.0f, 0xF0);

		buf.SetGlobalTexture("_GBuffer0", _g_buffer_0_copy);
		buf.SetGlobalTexture("_GBuffer1", _g_buffer_1_copy);
		buf.SetGlobalTexture("_GBuffer2", _g_buffer_2_copy);
		buf.SetGlobalTexture("_GBuffer3", _g_buffer_3_copy);

		buf.DrawMesh(fullscreen_quad_mesh, matrix, m_Material, 0, 0);

		buf.ReleaseTemporaryRT(_g_buffer_0_copy);
		buf.ReleaseTemporaryRT(_g_buffer_1_copy);
		buf.ReleaseTemporaryRT(_g_buffer_2_copy);
		buf.ReleaseTemporaryRT(_g_buffer_3_copy);

		// Draw onto the second buffer that will be used after final pass
		// Big hack but can't find another way to by pass Unity's built-in deferred renderer's FinalPass
        finalbuf.Clear();
		finalbuf.Blit(BuiltinRenderTextureType.GBuffer3, BuiltinRenderTextureType.CameraTarget);
	}
}
