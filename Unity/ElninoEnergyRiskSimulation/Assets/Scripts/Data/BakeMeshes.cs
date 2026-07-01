using System;
using System.Collections;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 구역 메시를 타일 분할 + LOD 포함해서 .mesh 에셋으로 사전 계산하는 에디터 도구.
///
/// 사용 순서:
///   1. Tools > Bake Terrain Heights 먼저 완료 (TerrainHeights_{id}.bytes 생성)
///   2. 플레이 모드 실행
///   3. Tools > Bake District Meshes 열기
///   4. 씬에서 자동 탐색 → 메시 굽기 시작
///   5. 완료 후 Assets/Resources/Districts/{districtId}/Meshes/ 에 .mesh 파일 생성됨
///
/// 런타임에서는 C++ DLL 호출 없이 Resources.Load<Mesh>() 로만 로드된다.
/// </summary>
public class BakeMeshesWindow : EditorWindow
{
    private BuildingManager _buildingManager;
    private bool   _isBaking;
    private string _status   = "대기 중";
    private int    _progress;
    private int    _total;

    // 베이킹 파라미터 (BuildingManager Inspector 값과 일치시킬 것)
    private int _tileN = 3;

    private const string AssetBaseDir = "Assets/Resources/Districts";

    [MenuItem("Tools/Bake District Meshes")]
    static void ShowWindow() =>
        GetWindow<BakeMeshesWindow>("메시 사전 계산");

    void OnGUI()
    {
        EditorGUILayout.LabelField("구역 메시 사전 계산 도구", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "각 구의 건물 메시를 타일 분할 + LOD 포함해 .mesh 에셋으로 미리 저장합니다.\n" +
            "한 번만 실행하면 런타임에서 C++ DLL 호출 없이 즉시 로드됩니다.\n\n" +
            "⚠ 'Tools > Bake Terrain Heights' 가 먼저 완료되어야 합니다.\n\n" +
            $"출력 경로: {AssetBaseDir}/{{districtId}}/Meshes/{{tx}}_{{ty}}_LOD{{0~2}}.mesh",
            MessageType.Info);

        EditorGUILayout.Space(4);

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("⚠ 플레이 모드를 먼저 시작하세요 (건물 데이터가 로드된 상태여야 합니다).", MessageType.Warning);

        EditorGUILayout.Space(4);

        _buildingManager = (BuildingManager)EditorGUILayout.ObjectField(
            "BuildingManager", _buildingManager, typeof(BuildingManager), true);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("베이킹 설정", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("아래 값은 BuildingManager Inspector의 '서브청크 & LOD 설정' 값과 동일하게 맞추세요.", MessageType.None);

        _tileN = EditorGUILayout.IntField(new GUIContent("타일 분할 (N×N)", "구역 메시를 N×N 격자로 분할"), _tileN);

        EditorGUILayout.Space(4);

        if (GUILayout.Button("씬에서 자동 탐색"))
        {
            _buildingManager = FindFirstObjectByType<BuildingManager>();
            if (_buildingManager == null)
                Debug.LogWarning("[MeshBaker] BuildingManager를 씬에서 찾을 수 없습니다.");
        }

        EditorGUILayout.Space(8);

        bool canBake = Application.isPlaying && !_isBaking && _buildingManager != null;
        GUI.enabled = canBake;
        if (GUILayout.Button("메시 굽기 시작", GUILayout.Height(40)))
        {
            _isBaking = true;
            _progress = 0;
            _buildingManager.StartCoroutine(BakeRoutine());
        }
        GUI.enabled = true;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("상태", _status);

        if (_total > 0)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(r, (float)_progress / _total, $"{_progress} / {_total} 구 완료");
        }

        if (_isBaking) Repaint();
    }

    private IEnumerator BakeRoutine()
    {
        DistrictType[] allDistricts = (DistrictType[])Enum.GetValues(typeof(DistrictType));
        _total = allDistricts.Length - 1; // DistrictType.None 제외

        // 출력 루트 폴더 (구별 서브폴더는 루프 안에서 생성)
        string sysBaseDir = Path.Combine(Application.dataPath, "Resources", "Districts");

        // 에셋 임포트를 마지막에 한 번에 처리 (CreateAsset 호출마다 reimport 방지)
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (DistrictType district in allDistricts)
            {
                if (district == DistrictType.None) continue;

                int districtId = (int)district;

                // ── 1. 사전 계산된 지형 높이 로드 ─────────────────────────
                _status = $"[{district}] 지형 높이 로드 중...";
                Repaint();
                yield return null;

                // 구별 메시 출력 폴더 생성
                Directory.CreateDirectory(Path.Combine(sysBaseDir, $"{districtId}", "Meshes"));

                TextAsset heightFile = Resources.Load<TextAsset>($"Districts/{districtId}/TerrainHeights");
                if (heightFile == null)
                {
                    Debug.LogWarning($"[MeshBaker] {district}: TerrainHeights_{districtId}.bytes 없음 — " +
                                     "'Tools > Bake Terrain Heights' 를 먼저 실행하세요. 건너뜁니다.");
                    _progress++;
                    yield return null;
                    continue;
                }

                float[] heights = new float[heightFile.bytes.Length / sizeof(float)];
                Buffer.BlockCopy(heightFile.bytes, 0, heights, 0, heightFile.bytes.Length);

                // ── 2. C++ → Unity Mesh 빌드 ──────────────────────────────
                _status = $"[{district}] C++ 메시 빌드 중...";
                Repaint();
                yield return null;

                Mesh fullMesh = _buildingManager.BuildAndGetDistrictMesh(districtId, heights);
                if (fullMesh == null)
                {
                    Debug.LogWarning($"[MeshBaker] {district}: 메시 빌드 결과 없음 — 건너뜁니다.");
                    _progress++;
                    yield return null;
                    continue;
                }

                // ── 3. 타일 분할 ──────────────────────────────────────────
                _status = $"[{district}] 타일 분할 중 ({_tileN}×{_tileN})...";
                Repaint();
                yield return null;

                double3[] positions   = _buildingManager.GetBuildingPositionsForBaking(districtId);
                int       bufferStart = _buildingManager.GetDistrictBufferStart(districtId);

                var tiles = MeshTileBuilder.SplitIntoTiles(fullMesh, positions, bufferStart, _tileN);

                // fullMesh 는 타일로 쪼갰으므로 해제 (에셋으로 저장하지 않음)
                UnityEngine.Object.DestroyImmediate(fullMesh);

                // ── 4. 타일별 LOD 생성 & .mesh 에셋 저장 ──────────────────
                for (int i = 0; i < tiles.Count; i++)
                {
                    var (tileMesh, tx, ty) = tiles[i];

                    _status = $"[{district}] 타일 ({tx},{ty}) LOD 생성 및 저장 중... ({i + 1}/{tiles.Count})";
                    Repaint();

                    string meshDir = $"{AssetBaseDir}/{districtId}/Meshes";

                    // LOD0 — 원본 타일 메시
                    SaveMeshAsset(tileMesh, meshDir, tx, ty, 0);


                    yield return null; // 프레임 양보 (UI 갱신)
                }

                // 마커 파일 저장 — bytes[0] = tileN (런타임 TrySpawnFromBakedMeshes에서 읽음)
                SaveMarkerFile(districtId, (byte)_tileN);

                Debug.Log($"[MeshBaker] {district} ({districtId}) 완료 — {tiles.Count}개 타일 × 3 LOD 저장");
                _status = $"[{district}] 완료 ({tiles.Count}개 타일)";
                _progress++;
                Repaint();
                yield return null;
            }
        }
        finally
        {
            // 예외 발생 시에도 반드시 해제 (미해제 시 에디터 임포트 파이프라인 잠김)
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _status = $"전체 완료! {_total}개 구 처리됨";
        _isBaking = false;
        Repaint();

        Debug.Log("[MeshBaker] 메시 사전 계산 완료. " +
                  "Assets/Resources/Districts/{districtId}/Meshes/ 폴더를 확인하세요.\n" +
                  "이제 런타임에서 C++ DLL 없이 즉시 로드됩니다.");
    }

    /// <summary>
    /// 런타임이 tileN 을 알 수 있도록 마커 .bytes 파일을 저장한다.
    /// bytes[0] = tileN
    /// </summary>
    private static void SaveMarkerFile(int districtId, byte tileN)
    {
        string assetPath = $"{AssetBaseDir}/{districtId}/Meshes/baked.bytes";
        string sysPath   = Path.Combine(
            Application.dataPath, "Resources", "Districts",
            $"{districtId}", "Meshes", "baked.bytes");

        File.WriteAllBytes(sysPath, new[] { tileN });
        AssetDatabase.ImportAsset(assetPath);
    }

    /// <summary>
    /// Mesh 를 지정 경로에 .mesh 에셋으로 저장한다.
    /// 이미 존재하면 삭제 후 재생성 (덮어쓰기).
    /// CreateAsset 호출 이후 mesh 소유권은 AssetDatabase 로 넘어가므로
    /// 호출자는 이후 해당 mesh 를 DestroyImmediate 해서는 안 된다.
    /// </summary>
    private static void SaveMeshAsset(Mesh mesh, string meshDir, int tx, int ty, int lodLevel)
    {
        string path = $"{meshDir}/{tx}_{ty}_LOD{lodLevel}.mesh";

        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AssetDatabase.CreateAsset(mesh, path);
    }
}
