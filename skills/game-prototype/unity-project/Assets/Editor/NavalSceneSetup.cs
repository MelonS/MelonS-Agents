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

        private const string ShipModelPath = "Assets/Models/Naval/ship-pirate-medium.fbx";

        private static GameObject SetupShip()
        {
            // Kenney Pirate Kit (CC0, kenney.nl/assets/pirate-kit) — 박스
            // placeholder 를 실제 저폴리 배 모델로 교체 (2026-08-12, 운영자
            // "너무 허접한데" 피드백). 유료 3D 생성 API(Meshy 등)는 money
            // firewall 대상이라 안 쓰고, 기존에 PawnSim 도 쓰던 Kenney CC0
            // 경로를 그대로 따름 — game-artist 에이전트 우선순위(Kenney CC0
            // 우선, SDXL 은 최후수단)와 일치.
            GameObject shipGo = new GameObject("Ship");
            shipGo.transform.position = new Vector3(0, 0.6f, 0);

            // 실측 절반-치수 — 모델을 바꿔도 하드코딩 다시 안 하도록 부력 샘플
            // 지점을 여기서 동적으로 계산한다. 못 찾으면(폴백 박스) 옛 고정값.
            float halfLength = 3f, halfBeam = 1f;

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ShipModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"[NavalSceneSetup] {ShipModelPath} 못 찾음 — 박스로 폴백");
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fallback.transform.SetParent(shipGo.transform, false);
                fallback.transform.localScale = new Vector3(2f, 1.2f, 6f);
            }
            else
            {
                GameObject model = Object.Instantiate(modelAsset);
                model.name = "ShipModel";
                model.transform.SetParent(shipGo.transform, false);

                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
                foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
                Debug.Log($"[NavalSceneSetup] ship model bounds size={bounds.size} center={bounds.center}");

                // 여유(0.85배) — 부력 샘플점을 뱃머리/현측 맨 끝보다 살짝 안쪽에
                // 둬서(전체가 파도 아래로 살짝 잠겨도) 안정된다.
                halfLength = bounds.extents.z * 0.85f;
                halfBeam = bounds.extents.x * 0.85f;

                BoxCollider col = shipGo.AddComponent<BoxCollider>();
                Vector3 localCenter = shipGo.transform.InverseTransformPoint(bounds.center);
                col.center = localCenter;
                col.size = bounds.size;
            }

            Rigidbody rb = shipGo.AddComponent<Rigidbody>();
            rb.mass = 800f;

            shipGo.AddComponent<ShipController>();
            ShipBuoyancy buoyancy = shipGo.AddComponent<ShipBuoyancy>();
            buoyancy.bowOffsetZ = halfLength;
            buoyancy.sternOffsetZ = -halfLength;
            buoyancy.beamOffsetX = halfBeam;

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
