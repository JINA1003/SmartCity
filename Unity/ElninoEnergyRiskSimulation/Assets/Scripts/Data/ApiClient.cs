using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class ApiClient : MonoBehaviour
{
    [Header("서버 설정")]
    public string serverUrl = "http://localhost:5000";

    // 각 엔드포인트별 이벤트 (JSON 문자열 그대로 전달)
    [Header("이벤트")]
    public UnityEvent<string> onHealthSuccess;
    public UnityEvent<string> onOniSuccess;
    public UnityEvent<string> onPredictSuccess;
    public UnityEvent<string> onOniRangeSuccess;
    public UnityEvent<string> onBlackoutSuccess;
    public UnityEvent<string> onError;   // 모든 에러 공용

    // -----------------------------------------------------------------------
    // 외부 호출 진입점
    // -----------------------------------------------------------------------

    public void FetchHealth()
    {
        StartCoroutine(Get($"{serverUrl}/health", onHealthSuccess));
    }

    public void FetchOni(int year, int month)
    {
        string url = $"{serverUrl}/oni?year={year}&month={month}";
        StartCoroutine(Get(url, onOniSuccess));
    }

    public void FetchPredict(int year, int month, float oni)
    {
        string url = $"{serverUrl}/predict?year={year}&month={month}&oni={oni}";
        StartCoroutine(Get(url, onPredictSuccess));
    }

    public void FetchOniRange(int year, int month)
    {
        string url = $"{serverUrl}/predict/oni_range?year={year}&month={month}";
        StartCoroutine(Get(url, onOniRangeSuccess));
    }

    public void FetchBlackoutSimulation(int year, int month, float oni)
    {
        string body = $"{{\"year\":{year},\"month\":{month},\"oni\":{oni}}}";
        StartCoroutine(Post($"{serverUrl}/blackout_simulation", body, onBlackoutSuccess));
    }

    // -----------------------------------------------------------------------
    // 내부 Coroutine
    // -----------------------------------------------------------------------

    private IEnumerator Get(string url, UnityEvent<string> onSuccess)
    {
        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(req.downloadHandler.text);
        else
            onError?.Invoke($"[GET {url}] {req.responseCode} {req.error}");
    }

    private IEnumerator Post(string url, string jsonBody, UnityEvent<string> onSuccess)
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
            onError?.Invoke($"[POST {url}] {req.responseCode} {req.error}");
    }
}
