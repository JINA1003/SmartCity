using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class ApiClient : MonoBehaviour
{
    [Header("서버 설정")]
    public string serverUrl = "http://localhost:5000";

    public event Action<JObject> OnHealthSuccess;
    public event Action<JObject> OnOniSuccess;
    public event Action<JObject> OnPredictSuccess;
    public event Action<JObject> OnOniRangeSuccess;
    public event Action<JObject> OnBlackoutSuccess;
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

    private IEnumerator Get(string url, Action<JObject> onSuccess)
    {
        using UnityWebRequest req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(JObject.Parse(req.downloadHandler.text));
        else
            OnError?.Invoke($"[GET {url}] {req.responseCode} {req.error}");
    }

    private IEnumerator Post(string url, string jsonBody, Action<JObject> onSuccess)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
        using UnityWebRequest req = new(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bodyBytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onSuccess?.Invoke(JObject.Parse(req.downloadHandler.text));
        else
            OnError?.Invoke($"[POST {url}] {req.responseCode} {req.error}");
    }
}
