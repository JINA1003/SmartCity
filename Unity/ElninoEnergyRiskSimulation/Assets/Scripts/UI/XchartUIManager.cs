using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XCharts.Runtime;

public class XchartUIManager : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════════════════
    // Inspector 필드
    // ═══════════════════════════════════════════════════════════════════════

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Chart")]
    [SerializeField] private LineChart oniChart;

    [Header("DataManager 연동")]
    [SerializeField] private DataManager dataManager;

    // ═══════════════════════════════════════════════════════════════════════
    // 색상 상수
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly Color ColorTemperature = HexColor("E05C5C"); // 기온     (빨강)
    private static readonly Color ColorReserve     = HexColor("5FA876"); // 공급예비율 (초록)
    private static readonly Color ColorSupply      = HexColor("7A9FBF"); // 공급량   (파랑)
    private static readonly Color ColorConsumption = HexColor("FF9D00"); // 소비량   (주황)

    private static readonly Color32 ColorLanina  = new Color32( 80, 130, 200, 40);
    private static readonly Color32 ColorNeutral = new Color32(160, 160, 160, 25);
    private static readonly Color32 ColorElnino  = new Color32(200,  80,  80, 40);

    private static readonly Color32 ColorCritical = new Color32(220,  50,  50, 200);
    private static readonly Color32 ColorWarn     = new Color32(230, 120,  30, 200);
    private static readonly Color32 ColorCaution  = new Color32(220, 200,  30, 200);
    private static readonly Color32 ColorNormal   = new Color32( 80, 180, 100, 200);

    // ═══════════════════════════════════════════════════════════════════════
    // 상수
    // ═══════════════════════════════════════════════════════════════════════

    private const string DEFAULT_DISTRICT = "종로구";
    private const double Y_MIN = 0.0;
    private const double Y_MAX = 16.0;

    // ═══════════════════════════════════════════════════════════════════════
    // 상태
    // ═══════════════════════════════════════════════════════════════════════

    private readonly List<OniRangeData> _entries = new List<OniRangeData>();
    private string _currentDistrict = DEFAULT_DISTRICT;

    // ═══════════════════════════════════════════════════════════════════════
    // Unity 생명주기
    // ═══════════════════════════════════════════════════════════════════════

    private void OnEnable()
    {
        if (dataManager != null)
            dataManager.OniRangeDataUpdated += OnOniRangeDataUpdated;
    }

    private void OnDisable()
    {
        if (dataManager != null)
            dataManager.OniRangeDataUpdated -= OnOniRangeDataUpdated;
    }

    private void Start()
    {
        // API 데이터가 아직 없으면 차트는 빈 상태로 대기
        // DataManager에서 OniRangeDataUpdated 이벤트가 오면 BuildChart() 호출됨
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 외부 인터페이스
    // ═══════════════════════════════════════════════════════════════════════

    // DataManager 이벤트 핸들러
    public void OnOniRangeDataUpdated(List<OniRangeData> data)
    {
        _entries.Clear();
        _entries.AddRange(data);
        BuildChart();
    }

    // 구 클릭 시 외부에서 호출 — 해당 구 데이터로 차트 갱신
    public void SetDistrict(string districtName)
    {
        _currentDistrict = districtName;
        if (_entries.Count > 0)
            BuildChart();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 차트 빌드
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildChart()
    {
        if (oniChart == null || _entries.Count == 0) return;

        SetTitle($"{_currentDistrict} ONI별 에너지 현황");

        oniChart.RemoveChartComponents<MarkArea>();
        oniChart.RemoveChartComponents<MarkLine>();
        oniChart.RemoveData();

        ConfigureBackground();
        ConfigureAxes();
        ConfigureTooltip();

        AddXAxisLabels();

        bool hasGuTemp = _entries[0].guTemperature != null &&
                         _entries[0].guTemperature.ContainsKey(_currentDistrict);
        bool hasGuCons = _entries[0].guConsumption != null &&
                         _entries[0].guConsumption.ContainsKey(_currentDistrict);

        // 정규화 범위 계산
        (float supplyMin, float supplyMax) = MinMax(e => e.supplyPower);
        (float consMin,   float consMax)   = MinMax(e =>
            hasGuCons ? (float)e.guConsumption.GetValueOrDefault(_currentDistrict, 0.0)
                      : e.seoulTotalConsumption);
        (float tempMin,   float tempMax)   = MinMax(e =>
            hasGuTemp ? e.guTemperature.GetValueOrDefault(_currentDistrict, 0f)
                      : e.seoulTemperature);

        // Serie 0 : 공급예비율 (실제 % 값, y축 직접 매핑)
        AddLineSerie("예비율 (%)", ColorReserve, 2.5f, false,
            e => e.reserveRate);

        // Serie 1 : 기온 (구별 or 서울 전체, 정규화)
        string tempLabel = hasGuTemp ? $"{_currentDistrict} 기온 (정규화)" : "서울 기온 (정규화)";
        AddLineSerie(tempLabel, ColorTemperature, 1.5f, true,
            e => (float)Normalize(
                hasGuTemp ? e.guTemperature.GetValueOrDefault(_currentDistrict, 0f) : e.seoulTemperature,
                tempMin, tempMax));

        // Serie 2 : 공급량 (정규화)
        AddLineSerie("공급량 (정규화)", ColorSupply, 1.5f, true,
            e => (float)Normalize(e.supplyPower, supplyMin, supplyMax));

        // Serie 3 : 소비량 (구별 or 서울 전체, 정규화)
        string consLabel = hasGuCons ? $"{_currentDistrict} 소비량 (정규화)" : "서울 소비량 (정규화)";
        AddLineSerie(consLabel, ColorConsumption, 1.5f, true,
            e => (float)Normalize(
                hasGuCons ? (float)e.guConsumption.GetValueOrDefault(_currentDistrict, 0.0) : e.seoulTotalConsumption,
                consMin, consMax));

        AddBackgroundZones();
        AddThresholdLines();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 차트 설정
    // ═══════════════════════════════════════════════════════════════════════

    private void ConfigureBackground()
    {
        oniChart.theme.transparentBackground = true;

        var bg = oniChart.GetChartComponent<Background>();
        if (bg != null)
        {
            bg.show       = false;
            bg.autoColor  = false;
            bg.imageColor = new Color(0f, 0f, 0f, 0f);
        }

        var grid = oniChart.GetChartComponent<GridCoord>();
        if (grid != null)
        {
            grid.backgroundColor = new Color32(0, 0, 0, 0);
            grid.showBorder      = false;
            grid.borderWidth     = 0;
        }

        // XCharts가 런타임에 자동 생성하는 자식 "background" Image → alpha=0
        var bgChild = oniChart.transform.Find("background");
        if (bgChild != null)
        {
            var img = bgChild.GetComponent<Image>();
            if (img != null)
            {
                img.color         = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = false;
            }
        }
    }

    private void ConfigureAxes()
    {
        var xAxis = oniChart.GetChartComponent<XAxis>();
        if (xAxis != null)
        {
            xAxis.axisLabel.show               = true;
            xAxis.axisLabel.textStyle.color    = Color.white;
            xAxis.axisLabel.textStyle.fontSize = 9;
            xAxis.axisLabel.interval           = 4;
            xAxis.axisLine.lineStyle.color     = new Color32(255, 255, 255, 120);
            xAxis.splitLine.show               = false;
        }

        var yAxis = oniChart.GetChartComponent<YAxis>();
        if (yAxis != null)
        {
            yAxis.axisLabel.show               = true;
            yAxis.axisLabel.textStyle.color    = Color.white;
            yAxis.axisLabel.textStyle.fontSize = 9;
            // y축 레이블: 0~16 범위. 예비율(%) 실제값 기준으로 표시
            yAxis.axisLabel.formatter          = "{value}%";
            yAxis.axisLine.show                = false;
            yAxis.splitLine.show               = true;
            yAxis.splitLine.lineStyle.color    = new Color32(255, 255, 255, 30);
            yAxis.splitLine.lineStyle.width    = 1f;
            yAxis.minMaxType                   = Axis.AxisMinMaxType.Custom;
            yAxis.min                          = Y_MIN;
            yAxis.max                          = Y_MAX;
        }
    }

    private void ConfigureTooltip()
    {
        var tooltip = oniChart.GetChartComponent<Tooltip>();
        if (tooltip == null) return;

        tooltip.type           = Tooltip.Type.Cross;
        tooltip.titleFormatter = "ONI: {b}";
        tooltip.itemFormatter  = "{a}: {c}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 데이터 추가
    // ═══════════════════════════════════════════════════════════════════════

    private void AddXAxisLabels()
    {
        foreach (var e in _entries)
            oniChart.AddXAxisData(e.oni.ToString("F1"));
    }

    private void AddLineSerie(string name, Color color, float width, bool dashed,
                              Func<OniRangeData, float> selector)
    {
        var serie = oniChart.AddSerie<Line>(name);
        serie.lineStyle.color = color;
        serie.lineStyle.width = width;
        serie.lineStyle.type  = dashed ? LineStyle.Type.Dashed : LineStyle.Type.Solid;
        serie.symbol.type     = SymbolType.None;
        serie.show            = true;

        foreach (var e in _entries)
            oniChart.AddData(name, (double)selector(e));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MarkArea / MarkLine
    // ═══════════════════════════════════════════════════════════════════════

    private void AddBackgroundZones()
    {
        AddMarkArea("라니냐",  0, 19, ColorLanina);
        AddMarkArea("중립",   20, 30, ColorNeutral);
        AddMarkArea("엘니뇨", 31, 50, ColorElnino);
    }

    private void AddMarkArea(string label, int startIdx, int endIdx, Color32 color)
    {
        var ma = oniChart.AddChartComponent<MarkArea>();
        if (ma == null) return;

        ma.show                     = true;
        ma.serieIndex               = 0;
        ma.text                     = label;
        ma.start.type               = MarkAreaType.None;
        ma.start.xValue             = startIdx;
        ma.end.type                 = MarkAreaType.None;
        ma.end.xValue               = endIdx;
        ma.itemStyle.color          = color;
        ma.itemStyle.borderWidth    = 0;
        ma.label.show               = true;
        ma.label.textStyle.color    = new Color32(220, 220, 220, 200);
        ma.label.textStyle.fontSize = 11;
    }

    private void AddThresholdLines()
    {
        var ml = oniChart.AddChartComponent<MarkLine>();
        if (ml == null) return;

        ml.show       = true;
        ml.serieIndex = 0;
        ml.data.Clear();
        ml.data.Add(HLine( 5.0, "심각 5%",  ColorCritical));
        ml.data.Add(HLine( 7.0, "경계 7%",  ColorWarn));
        ml.data.Add(HLine(10.0, "주의 10%", ColorCaution));
        ml.data.Add(HLine(15.0, "정상 15%", ColorNormal));
    }

    private static MarkLineData HLine(double yVal, string label, Color32 color)
    {
        var d = new MarkLineData();
        d.type                     = MarkLineType.Custom;
        d.name                     = label;
        d.yValue                   = yVal;
        d.lineStyle.color          = color;
        d.lineStyle.width          = 1.2f;
        d.lineStyle.type           = LineStyle.Type.Dashed;
        d.label.show               = true;
        d.label.formatter          = label;
        d.label.textStyle.color    = color;
        d.label.textStyle.fontSize = 9;
        d.startSymbol.type         = SymbolType.None;
        d.endSymbol.type           = SymbolType.None;
        return d;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 유틸리티
    // ═══════════════════════════════════════════════════════════════════════

    private (float min, float max) MinMax(Func<OniRangeData, float> selector)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var e in _entries)
        {
            float v = selector(e);
            if (v < min) min = v;
            if (v > max) max = v;
        }
        return (min, max);
    }

    private static double Normalize(float val, float min, float max)
    {
        if (Math.Abs(max - min) < 1e-6f) return Y_MIN;
        return Y_MIN + (val - min) / (max - min) * (Y_MAX - Y_MIN);
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    private void SetTitle(string text)
    {
        if (titleText != null) titleText.text = text;
    }
}
