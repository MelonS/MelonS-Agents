using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MelonS.GameProto.Naval;

namespace MelonS.GameProto.EditorTools
{
    /// <summary>
    /// 배-바다 이동 프로토타입 씬 생성기. PawnSim 의 SceneSetup.cs 와 같은 패턴
    /// (batchmode -executeMethod 로 씬을 코드에서 재현 가능하게 굽는다) 이지만
    /// 별도 파일·별도 씬이라 PawnSim 쪽 EditorBuildSettings 등록은 건드리지 않는다.
    ///
    /// Invoked from CLI:
    ///   Unity.exe -batchmode -quit -projectPath ... -executeMethod
    ///   MelonS.GameProto.EditorTools.NavalSceneSetup.Generate
    /// </summary>
    public static class NavalSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/OceanPrototype.unity";
        private const string MaterialPath = "Assets/Materials/OceanGerstner.mat";
        private const string MeshPath = "Assets/Meshes/OceanGrid.asset";

        [MenuItem("MelonS/Naval/Generate Ocean Prototype Scene")]
        public static void Generate()
        {
            Debug.Log("[NavalSceneSetup] starting...");
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Materials");
            Directory.CreateDirectory("Assets/Meshes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SetupLightingAndSky();
            GameObject shipGo = SetupShip();
            SetupOcean(shipGo);
            SetupCameras(shipGo);

            GameObject ssGo = new GameObject("AutoScreenshotter");
            ssGo.AddComponent<MelonS.GameProto.AutoScreenshotter>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NavalSceneSetup] Ocean prototype -> {ScenePath}");
        }

        private static void SetupLightingAndSky()
        {
            GameObject sunGo = new GameObject("Sun");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sunGo.transform.rotation = Quaternion.Euler(45, -30, 0);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.25f, 0.30f, 0.35f);
            RenderSettings.fog = false;
        }

        private static GameObject SetupShip()
        {
            // 저폴리 3D 모델은 후속 과제(운영자 지시: 생성형 AI로) — v0은 박스
            // placeholder 로 이동감부터 검증한다.
            GameObject shipGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shipGo.name = "Ship";
            shipGo.transform.localScale = new Vector3(2f, 1.2f, 6f);
            shipGo.transform.position = new Vector3(0, 0.6f, 0);

            Rigidbody rb = shipGo.AddComponent<Rigidbody>();
            rb.mass = 800f;

            shipGo.AddComponent<ShipController>();
            ShipBuoyancy buoyancy = shipGo.AddComponent<ShipBuoyancy>();
            buoyancy.bowOffsetZ = 3f;
            buoyancy.sternOffsetZ = -3f;
            buoyancy.beamOffsetX = 1f;

            return shipGo;
        }

        private static void SetupOcean(GameObject shipGo)
        {
            Shader oceanShader = Shader.Find("MelonS/Naval/OceanGerstner");
            if (oceanShader == null)
                Debug.LogError("[NavalSceneSetup] OceanGerstner shader not found — check Assets/Shaders/OceanGerstner.shader compiled OK");

            Material oceanMat = new Material(oceanShader) { name = "OceanGerstner" };
            // OpenMMO doc/WATER_SYSTEM.md 파장(20/14/9m)을 시작점으로 사용.
            oceanMat.SetFloat("_WaveLength1", 20f);
            oceanMat.SetFloat("_WaveLength2", 14f);
            oceanMat.SetFloat("_WaveLength3", 9f);
            oceanMat.SetFloat("_Amplitude1", 0.45f);
            oceanMat.SetFloat("_Amplitude2", 0.28f);
            oceanMat.SetFloat("_Amplitude3", 0.15f);
            oceanMat.SetFloat("_Dir1", 0f);
            oceanMat.SetFloat("_Dir2", 55f);
            oceanMat.SetFloat("_Dir3", -35f);
            AssetDatabase.CreateAsset(oceanMat, MaterialPath);

            Mesh oceanMesh = BuildOceanGrid(80f, 40);
            AssetDatabase.CreateAsset(oceanMesh, MeshPath);

            GameObject oceanGo = new GameObject("Ocean");
            oceanGo.AddComponent<MeshFilter>().sharedMesh = oceanMesh;
            MeshRenderer renderer = oceanGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = oceanMat;
            oceanGo.AddComponent<OceanWaveSampler>();
        }

        private static void SetupCameras(GameObject shipGo)
        {
            GameObject rig25Go = new GameObject("CameraRig_2Point5D");
            rig25Go.AddComponent<Camera>();
            CameraRig2Point5D rig25 = rig25Go.AddComponent<CameraRig2Point5D>();
            rig25.target = shipGo.transform;
            rig25Go.tag = "MainCamera";
            rig25Go.AddComponent<AudioListener>();

            GameObject rig3Go = new GameObject("CameraRig_3D");
            Camera cam3 = rig3Go.AddComponent<Camera>();
            CameraRig3D rig3 = rig3Go.AddComponent<CameraRig3D>();
            rig3.target = shipGo.transform;
            rig3Go.AddComponent<AudioListener>();
            cam3.enabled = false;
            rig3Go.SetActive(false);

            GameObject switcherGo = new GameObject("CameraModeSwitcher");
            CameraModeSwitcher switcher = switcherGo.AddComponent<CameraModeSwitcher>();
            switcher.rig2Point5D = rig25Go;
            switcher.rig3D = rig3Go;
        }

        private static Mesh BuildOceanGrid(float size, int segments)
        {
            var mesh = new Mesh { name = "OceanGrid" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            int verts = segments + 1;
            var vertices = new Vector3[verts * verts];
            var uvs = new Vector2[verts * verts];
            float half = size * 0.5f;
            for (int z = 0; z < verts; z++)
            {
                for (int x = 0; x < verts; x++)
                {
                    int i = z * verts + x;
                    float fx = (float)x / segments;
                    float fz = (float)z / segments;
                    vertices[i] = new Vector3(fx * size - half, 0, fz * size - half);
                    uvs[i] = new Vector2(fx, fz);
                }
            }
            var triangles = new int[segments * segments * 6];
            int ti = 0;
            for (int z = 0; z < segments; z++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int i = z * verts + x;
                    triangles[ti++] = i;
                    triangles[ti++] = i + verts;
                    triangles[ti++] = i + 1;
                    triangles[ti++] = i + 1;
                    triangles[ti++] = i + verts;
                    triangles[ti++] = i + verts + 1;
                }
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
