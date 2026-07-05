using System;
using System.Collections.Generic;
using VirtualMuseum.Data;

namespace VirtualMuseum.Excavation
{
    [Serializable]
    public struct ExcavationToolStats
    {
        public ExcavationToolType type;
        public float strength;    // 마스크 제거 속도 배율
        public float breakRisk;   // 내구도 감소 계수
        public float brushRadius; // UV 공간 브러시 반경
    }

    /// <summary>
    /// 5.2절 도구 시스템: 에어 블로워/소프트 브러시/조각칼/삽 4종의 수치 테이블.
    /// </summary>
    public static class ExcavationToolLibrary
    {
        public static readonly Dictionary<ExcavationToolType, ExcavationToolStats> Tools = new()
        {
            { ExcavationToolType.AirBlower, new ExcavationToolStats { type = ExcavationToolType.AirBlower, strength = 0.15f, breakRisk = 0f,    brushRadius = 0.03f  } },
            { ExcavationToolType.SoftBrush, new ExcavationToolStats { type = ExcavationToolType.SoftBrush, strength = 0.35f, breakRisk = 0.05f, brushRadius = 0.025f } },
            { ExcavationToolType.PickTool,  new ExcavationToolStats { type = ExcavationToolType.PickTool,  strength = 0.6f,  breakRisk = 0.15f, brushRadius = 0.02f  } },
            { ExcavationToolType.ChiselTool,new ExcavationToolStats { type = ExcavationToolType.ChiselTool,strength = 0.85f, breakRisk = 0.3f,  brushRadius = 0.018f } },
        };
    }
}
