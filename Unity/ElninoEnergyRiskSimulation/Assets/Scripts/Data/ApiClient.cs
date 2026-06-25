using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ApiClient : MonoBehaviour
{
    [Header("서버 설정")]
    public string serverUrl = "http://localhost:5000";

    // 각 엔드포인트별 이벤트 (JSON 문자열 그대로 전달)
    public event Action<string> OnHealthSuccess;
    public event Action<string> OnOniSuccess;
    public event Action<string> OnPredictSuccess;
    public event Action<string> OnOniRangeSuccess;
    public event Action<string> OnBlackoutSuccess;
    public event Action<string> OnError;

    // -----------------------------------------------------------------------
    // 외부 호출 진입점
    // -----------------------------------------------------------------------

    public void FetchHealth()
    {
        StartCoroutine(Get($"{serverUrl}/health", OnHealthSuccess));
    }

    public void FetchOni(int year, int month)
    {
        string url = $"{serverUrl}/oni?year={year}&month={month}";
        StartCoroutine(Get(url, OnOniSuccess));
    }

    public void FetchPredict(int year, int month, float oni)
    {
        string url = $"{serverUrl}/predict?year={year}&month={month}&oni={oni}";
        StartCoroutine(Get(url, OnPredictSuccess));
    }

    public void FetchOniRange(int year, int month)
    {
        string url = $"{serverUrl}/predict/oni_range?year={year}&month={month}";
        StartCoroutine(Get(url, OnOniRangeSuccess));
    }

    public void FetchBlackoutSimulation(int year, int month, float oni)
    {
        string body = $"{{\"year\":{year},\"month\":{month},\"oni\":{oni}}}";
        StartCoroutine(Post($"{serverUrl}/blackout_simulation", body, OnBlackoutSuccess));
    }

    // -----------------------------------------------------------------------
    // 내부 Coroutine
    // -----------------------------------------------------------------------

    private IEnumerator Get(string url, Action<string> onSuccess)
    {
        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            OnError?.Invoke($"[GET {url}] {req.responseCode} {req.error}");
    }

    private IEnumerator Post(string url, string jsonBody, Action<string> onSuccess)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        using UnityWebRequest req = new(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            OnError?.Invoke($"[POST {url}] {req.responseCode} {req.error}");
    }
}
