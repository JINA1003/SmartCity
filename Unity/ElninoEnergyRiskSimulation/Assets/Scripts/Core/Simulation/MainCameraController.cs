using System.Collections.Generic;
using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine.UI;
using TMPro;

public class MainCameraController : MonoBehaviour
{
    // 메인 카메라 붙여넣기
    [Header("Main Camera")]
    [SerializeField] private Camera mainCamera;

    // 시뮬레이션 ing 알려줄 토클 연결
    // 이거에 따라 카메라 이동 설정 변환
    [Header("Simulation Toggle")]
    [SerializeField] private Toggle simulationToggle;

    [Header("CilkedGuName")]
    [SerializeField] private TMP_Text cilkedGuName;

    // 카메라가 화면을 비출때 방향 조정
    private Vector3 lookDownRotation = new Vector3(65f, 0f, 0f);

    private CesiumGlobeAnchor cameraAnchor;
    private double initialHeight;

    // 구 이름/구 센터 좌표 저장
    // 좌표는 MinimapManager에서 가져옴
    private Dictionary<string, double2> districtLonLatMap =
        new Dictionary<string, double2>();

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        // 메인 카메라 설정 안되어 있으면 자동으로 메인카메라 찾아서 넣음
        if (mainCamera != null)
        {
            cameraAnchor = mainCamera.GetComponent<CesiumGlobeAnchor>();

            if (cameraAnchor != null)
            {
                initialHeight = cameraAnchor.longitudeLatitudeHeight.z;
            }
            else
            {
                Debug.LogError("[MainCameraController] MainCamera에 CesiumGlobeAnchor가 없습니다.");
            }
        }
    }

    // 구 클릭 이벤트 구독
    // 클릭시 해당 구로 카메라 이동
    private void OnEnable()
    {
        MinimapManager.OnDistrictSelected += MoveToClickedDistrict;
        BlackoutSimulationController.OnBlackoutDistrictChanged += MoveToBlackoutDistrict;
    }

    private void OnDisable()
    {
        MinimapManager.OnDistrictSelected -= MoveToClickedDistrict;
        BlackoutSimulationController.OnBlackoutDistrictChanged -= MoveToBlackoutDistrict;
    }

    public void RegisterDistrictPosition(string districtName, double lon, double lat)
    {
        if (string.IsNullOrEmpty(districtName)) return;

        if (!districtLonLatMap.ContainsKey(districtName))
        {
            districtLonLatMap.Add(districtName, new double2(lon, lat));
        }
    }

    // 모드 1. 시뮬레이션 ❌: 구 클릭 가능 -> 클릭된 구로 카메라 이동
    public void MoveToClickedDistrict(string districtName)
    {
        // 토글 ON이면 구 클릭으로 카메라 이동 금지
        if (simulationToggle != null && simulationToggle.isOn)
        {
            Debug.Log("[MainCameraController] 시뮬레이션 ON 상태이므로 구 클릭 카메라 이동 비활성화");
            return;
        }

        MoveToDistrict(districtName);
    }

    // 모드 2. 시뮬레이션 ⭕️: 구 클릭 이동 안됨 / 정전 순회 중인 구로 카메라 이동
    // TODO: 시뮬레이션 on 될때 제대로 실행되는지 확인하기
    public void MoveToBlackoutDistrict(string districtName)
    {
        MoveToDistrict(districtName);
    }

    // 특정 구로 카메라 이동
    public void MoveToDistrict(string districtName)
    {
        if (cameraAnchor == null)
        {
            Debug.LogWarning("[MainCameraController] 카메라 Anchor가 없습니다.");
            return;
        }

        // 구 이름(key)으로 좌표 찾고 없으면 Warning
        if (!districtLonLatMap.TryGetValue(districtName, out double2 lonLat))
        {
            Debug.LogWarning("[MainCameraController] 이동 좌표를 찾을 수 없습니다: " + districtName);
            return;
        }

        // 카메라 좌표값 설정
        cameraAnchor.longitudeLatitudeHeight =
            new double3(
                lonLat.x,
                lonLat.y,
                initialHeight
            );

        // 카메라가 아래를 보도록
        mainCamera.transform.rotation = Quaternion.Euler(lookDownRotation);

        cilkedGuName.text = "현재 위치: " + districtName;
    }
}