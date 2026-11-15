using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace WpfApp1;

/// <summary>Ikona w zasobniku systemowym (przy zegarze) + czyste ciemne menu.</summary>
public sealed class TrayService : IDisposable
{
    readonly NotifyIcon _ni;
    readonly ToolStripMenuItem _toggle;
    public event Action? OpenRequested, ToggleRequested, ExitRequested;

    public TrayService()
    {
        var open = new ToolStripMenuItem("Otwórz");
        _toggle = new ToolStripMenuItem("Wyłącz celownik");
        var exit = new ToolStripMenuItem("Zamknij");
        open.Click += (_, _) => OpenRequested?.Invoke();
        _toggle.Click += (_, _) => ToggleRequested?.Invoke();
        exit.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            Renderer = new DarkRenderer(),
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Color.FromArgb(21, 24, 33),
            ForeColor = Color.FromArgb(232, 234, 240),
            Padding = new Padding(4)
        };
        foreach (var i in new[] { open, _toggle, exit }) i.Padding = new Padding(8, 6, 8, 6);
        menu.Items.AddRange(new ToolStripItem[] { open, _toggle, new ToolStripSeparator(), exit });

        _ni = new NotifyIcon { Icon = LoadIcon(), Text = "Crosshair", Visible = true, ContextMenuStrip = menu };
        _ni.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) OpenRequested?.Invoke(); };
    }

    public void SetOverlayState(bool on) => _toggle.Text = on ? "Wyłącz celownik" : "Włącz celownik";

    static Icon LoadIcon()
    {
        try
        {
            var p = Path.Combine(AppContext.BaseDirectory, "icon.jpg");
            if (File.Exists(p))
            {
                using var src = new Bitmap(p);
                using var bmp = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, 32, 32);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
        catch { }
        return SystemIcons.Application;
    }

    public void Dispose() { _ni.Visible = false; _ni.Dispose(); }

    sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkTable()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.FromArgb(232, 234, 240);
            base.OnRenderItemText(e);
        }
    }

    sealed class DarkTable : ProfessionalColorTable
    {
        static readonly Color Bg = Color.FromArgb(21, 24, 33), Hov = Color.FromArgb(38, 44, 60), Bd = Color.FromArgb(38, 43, 56);
        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuBorder => Bd;
        public override Color MenuItemBorder => Hov;
        public override Color MenuItemSelected => Hov;
        public override Color MenuItemSelectedGradientBegin => Hov;
        public override Color MenuItemSelectedGradientEnd => Hov;
        public override Color MenuItemPressedGradientBegin => Hov;
        public override Color MenuItemPressedGradientMiddle => Hov;
        public override Color MenuItemPressedGradientEnd => Hov;
        public override Color SeparatorDark => Bd;
        public override Color SeparatorLight => Bd;
    }
}
