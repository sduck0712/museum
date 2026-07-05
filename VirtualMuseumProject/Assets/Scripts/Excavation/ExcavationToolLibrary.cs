using System;
using System.Collections.Generic;
using VirtualMuseum.Data;

namespace VirtualMuseum.Excavation
{
    [Serializable]
    public struct ExcavationToolStats
    {
        public ExcavationToolType type;
        public string displayName;
        public float strength;    // 마스크 제거 속도 배율
        public float breakRisk;   // 내구도 감소 계수
        public float brushRadius; // UV 공간 브러시 반경
    }

    /// <summary>
    /// 5.2절 도구 시스템 수치 테이블.
    /// 스펙 기준: 에어블로워(위험 없음) &lt; 소프트브러시(낮음) &lt; 조각칼(중간) &lt; 소형 삽/피크(높음)
    /// </summary>
    public static class ExcavationToolLibrary
    {
        public static readonly Dictionary<ExcavationToolType, ExcavationToolStats> Tools = new()
        {
            { ExcavationToolType.AirBlower,  new ExcavationToolStats { type = ExcavationToolType.AirBlower,  displayName = "에어 블로워",   strength = 0.15f, breakRisk = 0f,    brushRadius = 0.045f } },
            { ExcavationToolType.SoftBrush,  new ExcavationToolStats { type = ExcavationToolType.SoftBrush,  displayName = "소프트 브러시", strength = 0.35f, breakRisk = 0.06f, brushRadius = 0.035f } },
            { ExcavationToolType.ChiselTool, new ExcavationToolStats { type = ExcavationToolType.ChiselTool, displayName = "조각칼",        strength = 0.6f,  breakRisk = 0.16f, brushRadius = 0.028f } },
            { ExcavationToolType.PickTool,   new ExcavationToolStats { type = ExcavationToolType.PickTool,   displayName = "소형 삽/피크",  strength = 0.9f,  breakRisk = 0.32f, brushRadius = 0.05f  } },
        };
    }
}
