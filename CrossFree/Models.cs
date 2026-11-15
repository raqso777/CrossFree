using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfApp1;

public class Crosshair
{
    public string Name { get; set; } = "";
    public string Shape { get; set; } = "Cross";
    public double Size { get; set; } = 20;
    public double Thickness { get; set; } = 2;
    public double Gap { get; set; }
    public bool Outline { get; set; } = true;
    public bool Glow { get; set; }
    public bool CenterDot { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public string Color { get; set; } = "#FFFFFF";
    public bool IsCustom { get; set; }

    // Tylko dla gotowych presetów z zakładki GAME (nie zapisujemy ich do JSON dla customów)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Category { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Note { get; set; }

    [JsonIgnore] public bool IsGame => !string.IsNullOrEmpty(Category);
    [JsonIgnore] public string Group => IsGame ? Category! : IsCustom ? "CUSTOM" : "CLASSIC";
    [JsonIgnore] public string Sub => Note ?? Shape;

    public Crosshair Clone() => (Crosshair)MemberwiseClone();
}

public class AppConfig
{
    public double BgOpacity { get; set; } = 0.4;           // przyciemnienie tła podglądu
    public bool OverlayActive { get; set; } = true;
    public string SelectedCrosshair { get; set; } = "CLASSIC";
    public bool FirstRunDone { get; set; }

    public string? BackgroundPath { get; set; }
    public string? BackgroundName { get; set; }
    public double BackgroundOpacity { get; set; } = 0.55;
    public double BackgroundBlur { get; set; } = 18;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Crosshair? Active { get; set; }                  // ostatnio używany celownik (z Twoimi zmianami)

    public List<Crosshair> Crosshairs { get; set; } = new(); // tylko customy

    static readonly string PathFile = System.IO.Path.Combine(AppContext.BaseDirectory, "crosshairs.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(PathFile))
            {
                var c = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(PathFile), Opt);
                if (c != null)
                {
                    c.Crosshairs = c.Crosshairs.Where(x => x.IsCustom).ToList(); // stare presety (DOT, CIRCLE...) znikają
                    return c;
                }
            }
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        try { File.WriteAllText(PathFile, JsonSerializer.Serialize(this, Opt)); } catch { }
    }
}

public static class Presets
{
    public static Crosshair Classic() => new()
    {
        Name = "CLASSIC", Shape = "Cross", Size = 28, Thickness = 2, Gap = 6,
        Outline = true, Glow = true, Color = "#00E5FF", Note = "Domyślny celownik"
    };

    static Crosshair G(string cat, string name, string shape, double size, double th, double gap,
                       bool outline, bool glow, bool dot, string color, string note) => new()
    {
        Category = cat, Name = name, Shape = shape, Size = size, Thickness = th, Gap = gap,
        Outline = outline, Glow = glow, CenterDot = dot, Color = color, Note = note
    };

    public static readonly string[] Categories = { "CS2", "Valorant", "Fortnite", "FiveM", "Inne" };

    public static readonly List<Crosshair> Games = new()
    {
        // CS2 – pro najczęściej: małe, statyczne, zielone/cyjanowe, ciasna przerwa
        G("CS2", "Pro Green",  "Cross", 12, 1, 2, false, false, false, "#00FF00", "Mały, statyczny, zielony (styl s1mple / sh1ro)"),
        G("CS2", "Pro Cyan",   "Cross", 12, 1, 3, false, false, false, "#00FFFF", "Cyan, ciasna przerwa (styl NiKo / m0NESY)"),
        G("CS2", "Pro Yellow", "Cross", 14, 2, 3, false, false, false, "#FFEB3B", "Żółty, nieco grubszy – dobry na jasnych mapach"),
        G("CS2", "Classic Outline", "Cross", 18, 2, 4, true, false, false, "#00FF00", "Klasyk z obrysem, czytelny na każdym tle"),

        // Valorant
        G("Valorant", "TenZ Plus",   "Cross", 14, 2, 2, false, false, false, "#00FFFF", "Cyan, krótkie linie, mały offset"),
        G("Valorant", "Clean White", "Cross", 12, 2, 1, true,  false, false, "#FFFFFF", "Biały z obrysem – styl streamerów"),
        G("Valorant", "Green + Dot", "Cross", 16, 2, 3, false, false, true,  "#00FF00", "Zielony krzyżyk z kropką"),

        // Fortnite – kropka większa niż w innych grach, "button", statyczny krzyż
        G("Fortnite", "Neon Dot",   "Dot",    14, 2, 0, true,  true,  false, "#39FF14", "Większa kropka – dobra do śledzenia celu"),
        G("Fortnite", "Button",     "Circle", 20, 2, 0, true,  false, true,  "#00E5FF", "Okrąg z kropką – do strzelb i wybuchowych"),
        G("Fortnite", "Static Cross", "Cross", 18, 2, 4, true,  false, false, "#FFD60A", "Statyczny krzyżyk, nie rozprasza ruchem"),

        // FiveM / GTA RP
        G("FiveM", "RP Dot",    "Dot",   8,  2, 0, true, false, false, "#FFFFFF", "Mała kropka – spokojny, immersyjny wygląd"),
        G("FiveM", "Red Dot",   "Dot",   12, 2, 0, true, false, false, "#FF3B30", "Czerwona kropka"),
        G("FiveM", "PvP Cross", "Cross", 16, 2, 3, true, false, false, "#00FF66", "Wyraźny krzyżyk do strzelanek PvP"),

        // Inne
        G("Inne", "Sniper", "Sniper", 48, 2, 8, true, true, true, "#A288FF", "Luneta – okrąg z długimi liniami"),
    };
}
