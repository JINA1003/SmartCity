// XCharts 패키지 설치 후 Project Settings > Player > Scripting Define Symbols에
// XCHART 를 추가하면 실제 차트 API가 활성화됩니다.
#if XCHART
using XCharts.Runtime;
#endif

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ONI 범위별 에너지 지표 그래프.
/// DataManager.OnOniRangeDataUpdated 이벤트 구현 후 연결 예정.
/// ShowSeoulView()   : 서울 전체 (Start 시 자동 호출)
/// ShowDistrictView(): 미니맵 구 클릭 시 호출
/// </summary>
public class XchartUIManager : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI titleText;

#if XCHART
    [Header("Charts")]
    public LineChart temperatureChart;
    public LineChart consumptionChart;
    public LineChart supplyChart;
    public LineChart reserveRateChart;

    // 범례 색상 — 소비량 강조, 나머지 원색 채도 낮춤
    private static readonly Color ColorTemperature = HexColor("C47070"); // 기온     저채도 빨강
    private static readonly Color ColorConsumption = HexColor("FF9D00"); // 소비량   원색 주황 (강조)
    private static readonly Color ColorSupply      = HexColor("7A9FBF"); // 공급량   저채도 파랑
    private static readonly Color ColorReserve     = HexColor("5FA876"); // 공급예비율 저채도 초록
#endif

    // TODO: OniRangeData 구현 후 아래 내부 클래스 제거하고 OniRangeData로 교체
    private class TempOniData
    {
        public float oni;
        public float seoulTemperature;
        public float supplyPower;
        public float reserveRate;
        public float seoulTotalConsumption;
        public Dictionary<string, float>  guTemperature = new();
        public Dictionary<string, double> guConsumption = new();
    }

    private readonly List<TempOniData> _entries = new();

    // -----------------------------------------------------------------------
    // Unity 생명주기
    // -----------------------------------------------------------------------

    // DataManager 연동 시 구독할 기본 구 이름
    private const string DefaultDistrict = "종로구";

    private void Start()
    {
        // DataManager 구현 완료 후 아래 주석 해제
        // DataManager.Instance.OnOniRangeDataUpdated += OnOniRangeDataUpdated;

#if XCHART
        InitChartBackground(temperatureChart);
        InitChartBackground(consumptionChart);
        InitChartBackground(supplyChart);
        InitChartBackground(reserveRateChart);
#endif

        // 임시: 정적 더미 데이터로 종로구 그래프 표시
        // DataManager 연동 후 제거
        LoadStaticData();
        ShowDistrictView(DefaultDistrict);
    }

    // OnDestroy: DataManager 구현 완료 후 추가
    // private void OnDestroy() =>
    //     DataManager.Instance.OnOniRangeDataUpdated -= OnOniRangeDataUpdated;

    // -----------------------------------------------------------------------
    // DataManager 이벤트 핸들러
    // -----------------------------------------------------------------------

    // DataManager가 /predict/oni_range 파싱 완료 후 발행하는 이벤트 구독
    // /predict 완료와 같은 타이밍에 호출되어 다른 패널과 동시에 업데이트됨
    // public void OnOniRangeDataUpdated(List<OniRangeData> data)
    // {
    //     _entries.Clear();
    //     foreach (var d in data)
    //         _entries.Add(new TempOniData
    //         {
    //             oni                   = d.oni,
    //             seoulTemperature      = d.seoulTemperature,
    //             supplyPower           = d.supplyPower,
    //             reserveRate           = d.reserveRate,
    //             seoulTotalConsumption = d.seoulTotalConsumption,
    //             guTemperature         = d.guTemperature,
    //             guConsumption         = d.guConsumption,
    //         });
    //     ShowDistrictView(DefaultDistrict);
    // }

    // -----------------------------------------------------------------------
    // 공개 진입점
    // -----------------------------------------------------------------------

    public void ShowSeoulView()
    {
        SetTitle("서울시 ONI별 에너지 현황");

        if (_entries.Count == 0) return;

        var onis         = new List<string>();
        var temps        = new List<double>();
        var consumptions = new List<double>();
        var supplies     = new List<double>();
        var reserves     = new List<double>();

        foreach (var e in _entries)
        {
            onis.Add(e.oni.ToString("F1"));
            temps.Add(Math.Round(e.seoulTemperature, 2));
            consumptions.Add(Math.Round(e.seoulTotalConsumption / 1000.0, 2));
            supplies.Add(Math.Round(e.supplyPower, 1));
            reserves.Add(Math.Round(e.reserveRate, 2));
        }

#if XCHART
        ApplyToChart(temperatureChart, "기온 (°C)",    onis, temps,        27.0,     29.0,     ColorTemperature);
        ApplyToChart(consumptionChart, "소비량 (GWh)",  onis, consumptions, 5200.0,   5700.0,   ColorConsumption, showTooltip: true);
        ApplyToChart(supplyChart,      "공급량 (MW)",    onis, supplies,    110000.0, 123000.0, ColorSupply);
        ApplyToChart(reserveRateChart, "공급예비율 (%)", onis, reserves,    0.0,      16.0,     ColorReserve);
#endif
    }

    public void ShowDistrictView(string guName)
    {
        SetTitle($"{guName} ONI별 에너지 현황");

        if (_entries.Count == 0) return;

        var onis         = new List<string>();
        var temps        = new List<double>();
        var consumptions = new List<double>();

        foreach (var e in _entries)
        {
            if (!e.guTemperature.ContainsKey(guName)) continue;

            onis.Add(e.oni.ToString("F1"));
            temps.Add(Math.Round(e.guTemperature[guName], 2));
            consumptions.Add(Math.Round(e.guConsumption[guName] / 1000.0, 2));
        }

        if (onis.Count == 0) return;

        // 공급/예비율은 구별 데이터 없음 → 서울 전체값 사용
        var supplies = new List<double>();
        var reserves = new List<double>();
        foreach (var e in _entries)
        {
            supplies.Add(Math.Round(e.supplyPower, 1));
            reserves.Add(Math.Round(e.reserveRate, 2));
        }

#if XCHART
        ApplyToChart(temperatureChart, $"{guName} 기온 (°C)",   onis, temps,        28.8,  29.8,  ColorTemperature);
        ApplyToChart(consumptionChart, $"{guName} 소비량 (GWh)", onis, consumptions, 178.0, 205.0, ColorConsumption, showTooltip: true);
        ApplyToChart(supplyChart,      "공급량 (MW)",             onis, supplies,    110000.0, 123000.0, ColorSupply);
        ApplyToChart(reserveRateChart, "공급예비율 (%)",           onis, reserves,    0.0,      16.0,     ColorReserve);
#endif
    }

    // -----------------------------------------------------------------------
    // 차트 스타일
    // -----------------------------------------------------------------------

#if XCHART
    /// <summary>
    /// XCharts 배경 투명 설정.
    /// LineChart 루트에는 Image가 없지만, XCharts가 자식 "background"에 Image를 생성한다.
    /// </summary>
    private static void InitChartBackground(LineChart chart)
    {
        if (chart == null) return;

        chart.theme.transparentBackground = true;

        var bg = chart.GetChartComponent<Background>();
        if (bg != null)
        {
            bg.show = false;
            bg.autoColor = false;
            bg.imageColor = new Color(1f, 1f, 1f, 0f);
        }

        var grid = chart.GetChartComponent<GridCoord>();
        if (grid != null)
        {
            grid.backgroundColor = new Color32(0, 0, 0, 0);
            grid.showBorder = false;
            grid.borderWidth = 0;
        }

        // XCharts 내부 자식 background (Unity Image) — 스크립트 비활성화해도 남을 수 있음
        var backgroundChild = chart.transform.Find("background");
        if (backgroundChild != null)
        {
            var image = backgroundChild.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(1f, 1f, 1f, 0f);
                image.raycastTarget = false;
            }
        }
    }

    // showTooltip=true인 차트만 툴팁 표시 (소비량 차트), 나머지는 숨김
    private static void ApplyToChart(LineChart chart, string seriesName,
                                     List<string> xLabels, List<double> values,
                                     double yMin, double yMax, Color lineColor,
                                     bool showTooltip = false)
    {
        if (chart == null) return;

        chart.RemoveData();
        InitChartBackground(chart);

        // x축 — ONI 레이블 검정, 크기 10, 격자선 제거
        var xAxis = chart.GetChartComponent<XAxis>();
        if (xAxis != null)
        {
            xAxis.axisLabel.show              = true;
            xAxis.axisLabel.textStyle.color   = Color.black;
            xAxis.axisLabel.textStyle.fontSize = 10;
            xAxis.axisLine.lineStyle.color    = new Color32(0, 0, 0, 120);
            xAxis.splitLine.show              = false;
        }

        // y축 — 레이블 숨김, 수평 격자선 유지, 범위 고정
        var yAxis = chart.GetChartComponent<YAxis>();
        if (yAxis != null)
        {
            yAxis.axisLabel.show            = false; // tick 레이블 숨김
            yAxis.axisLine.show             = false;
            yAxis.splitLine.show            = true;
            yAxis.splitLine.lineStyle.color = new Color32(100, 100, 100, 60);
            yAxis.splitLine.lineStyle.width = 1f;
            yAxis.minMaxType                = Axis.AxisMinMaxType.Custom;
            yAxis.min                       = yMin;
            yAxis.max                       = yMax;
        }

        // 소비량 차트만 formatter 설정, 나머지는 기본 툴팁 유지
        var tooltip = chart.GetChartComponent<Tooltip>();
        if (tooltip != null && showTooltip)
        {
            tooltip.type           = Tooltip.Type.Cross;
            tooltip.titleFormatter = "ONI: {b}";
            tooltip.itemFormatter  = "소비량: {c} GWh";
        }

        // x축 ONI 데이터
        foreach (var label in xLabels)
            chart.AddXAxisData(label);

        // 시리즈
        var serie = chart.GetSerie(0);
        if (serie == null)
            serie = chart.AddSerie<Line>(seriesName);
        else
            serie.serieName = seriesName;

        serie.lineStyle.color = lineColor;
        serie.lineStyle.width = 2f;
        serie.symbol.type     = SymbolType.None;

        foreach (var v in values)
            chart.AddData(0, v);

        // TODO: ONI 슬라이더 연동 세로선 (MarkLine) — 추후 구현
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }
#endif

    // -----------------------------------------------------------------------
    // 헬퍼
    // -----------------------------------------------------------------------

    private void SetTitle(string text)
    {
        if (titleText != null)
            titleText.text = text;
    }

    // -----------------------------------------------------------------------
    // 정적 더미 데이터 (DataManager 연동 후 제거)
    // -----------------------------------------------------------------------

    private void LoadStaticData()
    {
        _entries.Clear();

        // 실제 API 응답 데이터 (2030년 8월 기준, oni -2.5 ~ 2.5, 51개)
        float[] sampleOnis = {
            -2.5f,-2.4f,-2.3f,-2.2f,-2.1f,-2.0f,-1.9f,-1.8f,-1.7f,-1.6f,
            -1.5f,-1.4f,-1.3f,-1.2f,-1.1f,-1.0f,-0.9f,-0.8f,-0.7f,-0.6f,
            -0.5f,-0.4f,-0.3f,-0.2f,-0.1f, 0.0f, 0.1f, 0.2f, 0.3f, 0.4f,
             0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f, 1.1f, 1.2f, 1.3f, 1.4f,
             1.5f, 1.6f, 1.7f, 1.8f, 1.9f, 2.0f, 2.1f, 2.2f, 2.3f, 2.4f, 2.5f
        };
        float[] sampleTemps = {
            27.58f,27.59f,27.61f,27.62f,27.64f,27.65f,27.67f,27.68f,27.70f,27.71f,
            27.73f,27.74f,27.76f,27.77f,27.79f,27.80f,27.82f,27.83f,27.85f,27.86f,
            27.88f,27.89f,27.91f,27.92f,27.94f,27.95f,27.97f,27.98f,28.00f,28.01f,
            28.03f,28.04f,28.06f,28.07f,28.09f,28.10f,28.12f,28.13f,28.15f,28.16f,
            28.18f,28.19f,28.21f,28.22f,28.24f,28.25f,28.27f,28.28f,28.30f,28.31f,28.33f
        };
        float[] sampleSupply = {
            112730.2f,112762.1f,112799.9f,112843.9f,112894.2f,112950.4f,113013.0f,113081.5f,113156.4f,113237.2f,
            113324.3f,113417.4f,113516.8f,113622.1f,113733.9f,113851.4f,113975.4f,114105.3f,114241.6f,114383.7f,
            114532.2f,114686.6f,114847.5f,115014.1f,115187.3f,115366.2f,115551.6f,115742.8f,115940.1f,116144.0f,
            116353.6f,116569.7f,116791.5f,117019.9f,117254.1f,117494.7f,117741.1f,117994.1f,118252.8f,118518.0f,
            118788.9f,119066.4f,119349.6f,119639.4f,119934.9f,120237.0f,120544.7f,120859.1f,121179.1f,121505.7f,121838.0f
        };
        float[] sampleReserve = {
            4.44f,4.70f,4.96f,5.22f,5.47f,5.73f,5.98f,6.23f,6.48f,6.72f,
            6.97f,7.21f,7.45f,7.69f,7.92f,8.15f,8.39f,8.61f,8.84f,9.07f,
            9.29f,9.51f,9.73f,9.94f,10.16f,10.37f,10.58f,10.78f,10.99f,11.19f,
            11.39f,11.59f,11.79f,11.98f,12.18f,12.37f,12.55f,12.74f,12.92f,13.11f,
            13.29f,13.46f,13.64f,13.81f,13.98f,14.15f,14.32f,14.48f,14.65f,14.81f,14.97f
        };
        float[] sampleConsume = {
            5453214f,5447620f,5443600f,5448112f,5446104f,5446716f,5442396f,5445153f,5443114f,5481018f,
            5429187f,5431452f,5430254f,5429111f,5410456f,5410265f,5439941f,5410026f,5410943f,5383389f,
            5413995f,5406213f,5339596f,5346536f,5330051f,5250754f,5269616f,5307112f,5348975f,5358474f,
            5502354f,5485127f,5494573f,5510919f,5500277f,5520242f,5521140f,5526839f,5577137f,5582413f,
            5569158f,5566722f,5556047f,5565715f,5600224f,5603072f,5578152f,5585491f,5603090f,5612425f,5621367f
        };

        // 종로구 실제 데이터 (기본 district)
        float[] gangnamTemps = {
            28.92f,28.93f,28.95f,28.96f,28.98f,28.99f,29.01f,29.02f,29.04f,29.05f,
            29.07f,29.08f,29.10f,29.11f,29.13f,29.14f,29.16f,29.17f,29.19f,29.20f,
            29.22f,29.23f,29.25f,29.26f,29.28f,29.29f,29.31f,29.32f,29.34f,29.35f,
            29.37f,29.38f,29.40f,29.41f,29.43f,29.44f,29.46f,29.47f,29.49f,29.50f,
            29.52f,29.53f,29.55f,29.56f,29.58f,29.59f,29.61f,29.62f,29.64f,29.65f,29.67f
        };
        double[] gangnamConsumptions = {
            192230.11,192230.11,192230.11,192230.11,190222.12,190222.12,190222.12,190222.12,190222.12,191710.92,
            189780.08,189780.08,190788.92,190788.92,190788.92,189150.25,189975.31,190239.03,188835.50,187711.67,
            188234.22,189181.38,186431.95,186530.20,186310.05,183004.84,183669.47,185558.41,187108.72,186888.25,
            193460.47,193728.83,194116.94,194652.08,194170.06,194551.27,194551.27,194846.94,196908.38,197095.95,
            196398.44,195896.73,195709.83,195629.41,197361.25,197605.91,197117.11,197117.11,196926.70,196926.70,200847.22
        };

        for (int i = 0; i < sampleOnis.Length; i++)
        {
            _entries.Add(new TempOniData
            {
                oni                   = sampleOnis[i],
                seoulTemperature      = sampleTemps[i],
                supplyPower           = sampleSupply[i],
                reserveRate           = sampleReserve[i],
                seoulTotalConsumption = sampleConsume[i],
                guTemperature = new Dictionary<string, float>
                {
                    { "종로구", gangnamTemps[i] },
                },
                guConsumption = new Dictionary<string, double>
                {
                    { "종로구", gangnamConsumptions[i] },
                },
            });
        }
    }
}
