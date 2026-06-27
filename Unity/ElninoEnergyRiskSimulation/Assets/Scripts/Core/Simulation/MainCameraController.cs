using System.Collections.Generic;
using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;

public class MainCameraController : MonoBehaviour
{
    // 메인 카메라 붙여넣기
    [Header("Main Camera")]
    public Camera mainCamera;

    // 카메라가 화면을 비출때 방향 조정
    private Vector3 lookDownRotation = new Vector3(65f, 0f, 0f);

    private CesiumGlobeAnchor cameraAnchor;
    private double initialHeight;

    // 구 이름/구 센터 좌표 저장
    // 좌표는 MinimapManager에서 가져옴
    private Dictionary<string, double2> districtLonLatMap =
        new Dictionary<string, double2>();

    public bool IsDistrictClickEnabled { get; private set; } = true;

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

    public void RegisterDistrictPosition(string districtName, double lon, double lat)
    {
        if (string.IsNullOrEmpty(districtName)) return;

        if (!districtLonLatMap.ContainsKey(districtName))
        {
            districtLonLatMap.Add(districtName, new double2(lon, lat));
        }
    }

    // 모드 1. 구 클릭 시 카메라 이동
    public void MoveToDistrictByClick(string districtName)
    {
        // IsDistrictClickEnabled  = true가 아닌 false 일때
        if (!IsDistrictClickEnabled)
        {
            Debug.Log("[MainCameraController] 구 클릭 이동이 비활성화된 상태입니다.");
            return;
        }

        MoveToDistrict(districtName);
    }

    // 모드 2. 시뮬레이션 시작 시 정전 구로 카메라 이동
    // 시뮬레이션 로직에서 아래 함수 실행하면 됨
    public void MoveToBlackoutDistrict(string districtName)
    {
        DisableDistrictClick(); // 구 클릭 비활

        MoveToDistrict(districtName);
    }

    public void EnableDistrictClick()
    {
        IsDistrictClickEnabled = true;
    }

    public void DisableDistrictClick()
    {
        IsDistrictClickEnabled = false;
    }

    // 특정 구로 카메라 이동
    private void MoveToDistrict(string districtName)
    {
        if (cameraAnchor == null)
        {
            Debug.LogWarning("[MainCameraController] 카메라 Anchor가 없습니다.");
            return;
        }

        if (!districtLonLatMap.ContainsKey(districtName))
        {
            Debug.LogWarning("[MainCameraController] 이동 좌표를 찾을 수 없습니다: " + districtName);
            return;
        }

        double2 lonLat = districtLonLatMap[districtName];

        cameraAnchor.longitudeLatitudeHeight =
            new double3(
                lonLat.x,
                lonLat.y,
                initialHeight
            );

        mainCamera.transform.rotation = Quaternion.Euler(lookDownRotation);
    }
}