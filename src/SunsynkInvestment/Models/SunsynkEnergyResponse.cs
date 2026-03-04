namespace SunsynkInvestment.Models;

public class SunsynkEnergyResponse
{
    public int Code { get; set; }
    public string Msg { get; set; } = "";
    public bool Success { get; set; }
    public SunsynkEnergyData Data { get; set; } = new();
}

public class SunsynkEnergyData
{
    public List<SunsynkEnergyInfo> Infos { get; set; } = [];
}

public class SunsynkEnergyInfo
{
    public string Label { get; set; } = "";
    public string Unit { get; set; } = "";
    public List<SunsynkTimeValue> Records { get; set; } = [];
}

public class SunsynkTimeValue
{
    public string Time { get; set; } = "";
    public string Value { get; set; } = "0";
}
