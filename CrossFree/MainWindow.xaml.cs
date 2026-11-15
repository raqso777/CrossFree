using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WpfApp1;

public partial class MainWindow : Window
{
    readonly AppConfig _cfg = AppConfig.Load();
    readonly ObservableCollection<Crosshair> _mine = new();
    readonly ListCollectionView _mineView;
    readonly ListCollectionView _gameView;
    readonly OverlayWindow _overlay = new();
    readonly TrayService _tray = new();
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly DispatcherTimer _blurTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };

    Crosshair _w = new();
    Crosshair? _selected;
    BitmapSource? _bgSrc;
    bool _loading = true, _suppress;
    string _cat = "CS2";
    int _step;

    // ---------- animacje (krótkie, z łagodnym wyhamowaniem) ----------
    static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };
    static DoubleAnimation Anim(double from, double to, int ms) =>
        new(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Ease };

    static readonly (string Title, string Body)[] Steps =
    {
        ("Witaj w Crosshair!",
         "Dziękujemy za pobranie programu. W kilku krótkich krokach pokażę Ci, jak ustawić własny celownik. Zajmie to mniej niż minutę."),
        ("Wybierz celownik",
         "Po lewej znajdziesz CLASSIC oraz swoje własne celowniki (CUSTOM). Przycisk GAME na dole otwiera gotowe zestawy do CS2, Valorant, Fortnite, FiveM i innych gier."),
        ("Dopasuj po swojemu",
         "Zmieniaj kształt, rozmiar, grubość, przerwę i kolor. Podgląd oraz celownik na ekranie reagują od razu. Gdy będzie idealny, kliknij „Zapisz jako custom”."),
        ("Włącz i graj",
         "Przełącznik „Celownik” włącza i wyłącza nakładkę. Środkowy przycisk w rogu wygasza panel do zasobnika, a celownik dalej działa. Wrócisz do panelu klikając ikonę przy zegarze, a prawy przycisk pokaże menu."),
        ("Ustawienia i gotowe",
         "W Ustawieniach (lewy dolny róg) ustawisz własne tło aplikacji, jego krycie i rozmycie. Program pamięta ostatnio używany celownik. Miłej gry!"),
    };

    public MainWindow()
    {
        InitializeComponent();
        Opacity = 0; // płynne pojawienie się okna (animacja w Loaded)
        try
        {
            var p = Path.Combine(AppContext.BaseDirectory, "icon.jpg");
            if (File.Exists(p)) Icon = BitmapFrame.Create(new Uri(p));
        }
        catch { }

        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Persist(); };
        _blurTimer.Tick += (_, _) => { _blurTimer.Stop(); RebuildBlur(); };

        _mine.Add(Presets.Classic());
        foreach (var c in _cfg.Crosshairs) _mine.Add(c);
        _mineView = new ListCollectionView(_mine);
        _mineView.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
        ListMine.ItemsSource = _mineView;

        _gameView = new ListCollectionView(Presets.Games) { Filter = o => o is Crosshair g && g.Category == _cat };
        ListGame.ItemsSource = _gameView;

        BgS.Value = _cfg.BgOpacity;
        BgImgS.Value = _cfg.BackgroundOpacity;
        BlurS.Value = _cfg.BackgroundBlur;

        _tray.OpenRequested += () => Dispatcher.Invoke(ShowPanel);
        _tray.ToggleRequested += () => Dispatcher.Invoke(() => SetOverlay(!_cfg.OverlayActive));
        _tray.ExitRequested += () => Dispatcher.Invoke(Close);

        // ostatnio używany celownik
        var sel = _mine.FirstOrDefault(c => c.Name == _cfg.SelectedCrosshair)
                  ?? Presets.Games.FirstOrDefault(c => c.Name == _cfg.SelectedCrosshair)
                  ?? _mine[0];
        _selected = sel;
        _suppress = true;
        if (sel.IsGame)
        {
            _cat = sel.Category!;
            foreach (var r in CatPanel.Children.OfType<RadioButton>()) r.IsChecked = (string)r.Tag == _cat;
            _gameView.Refresh();
            ShowGame(true);
            ListGame.SelectedItem = sel;
        }
        else ListMine.SelectedItem = sel;
        _suppress = false;

        _w = (_cfg.Active is { } a && a.Name == sel.Name ? a : sel).Clone();
        _loading = false;
        LoadEditor();
        SetOverlay(_cfg.OverlayActive);

        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, Anim(0, 1, 280));
            ApplyBackground();
            if (!_cfg.FirstRunDone) StartTutorial();
        };
    }

    // ---------- okno ----------
    void TitleBar_MouseDown(object s, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    void Minimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object s, RoutedEventArgs e) => Close();
    void Dim_Click(object s, RoutedEventArgs e) => HideToTray();

    void HideToTray()
    {
        var a = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        a.Completed += (_, _) => { Hide(); BeginAnimation(OpacityProperty, null); Opacity = 1; };
        BeginAnimation(OpacityProperty, a);
    }

    void ShowPanel()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    protected override void OnClosed(EventArgs e)
    {
        _saveTimer.Stop();
        Persist();
        _tray.Dispose();
        _overlay.Close();
        base.OnClosed(e);
        Application.Current.Shutdown();
    }

    static void FadeIn(UIElement el, TranslateTransform? slide = null)
    {
        el.Visibility = Visibility.Visible;
        el.BeginAnimation(OpacityProperty, Anim(0, 1, 220));
        slide?.BeginAnimation(TranslateTransform.YProperty, Anim(14, 0, 300));
    }

    static void FadeOut(UIElement el)
    {
        if (el.Visibility != Visibility.Visible) return;
        var a = Anim(1, 0, 160);
        a.Completed += (_, _) => { el.Visibility = Visibility.Collapsed; el.BeginAnimation(OpacityProperty, null); el.Opacity = 1; };
        el.BeginAnimation(OpacityProperty, a);
    }

    // ---------- overlay ----------
    void Overlay_Click(object s, RoutedEventArgs e) => SetOverlay(ChkOverlay.IsChecked == true);

    void SetOverlay(bool on)
    {
        _cfg.OverlayActive = on;
        ChkOverlay.IsChecked = on;
        _tray.SetOverlayState(on);
        _overlay.Update(_w);
        if (on) _overlay.Show(); else _overlay.Hide();
        Persist();
    }

    // ---------- lista / GAME ----------
    void List_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (_suppress || e.AddedItems.Count == 0 || e.AddedItems[0] is not Crosshair c) return;
        _suppress = true;
        if (ReferenceEquals(s, ListMine)) ListGame.SelectedItem = null; else ListMine.SelectedItem = null;
        _suppress = false;
        _selected = c;
        _w = c.Clone();
        LoadEditor();
        Preview.BeginAnimation(OpacityProperty, Anim(0.25, 1, 200));
    }

    void Game_Click(object s, RoutedEventArgs e) => ShowGame(PanelGame.Visibility != Visibility.Visible);

    void ShowGame(bool on)
    {
        var show = on ? PanelGame : PanelMine;
        var hide = on ? PanelMine : PanelGame;
        hide.Visibility = Visibility.Collapsed;
        if (show.Visibility != Visibility.Visible)
        {
            show.Visibility = Visibility.Visible;
            show.BeginAnimation(OpacityProperty, Anim(0, 1, 220));
        }
        BtnGame.Content = on ? "‹   MOJE CELOWNIKI" : "GAME   ›";
    }

    void Cat_Checked(object s, RoutedEventArgs e)
    {
        _cat = (string)((RadioButton)s).Tag;
        _gameView?.Refresh();
        ListGame?.BeginAnimation(OpacityProperty, Anim(0.3, 1, 180));
    }

    // ---------- edytor ----------
    void LoadEditor()
    {
        _loading = true;
        SizeS.Value = _w.Size; ThickS.Value = _w.Thickness; GapS.Value = _w.Gap;
        OxS.Value = _w.OffsetX; OyS.Value = _w.OffsetY;
        SwOutline.IsChecked = _w.Outline; SwGlow.IsChecked = _w.Glow; SwDot.IsChecked = _w.CenterDot;
        TxtColor.Text = _w.Color;
        TxtName.Text = _selected is { IsCustom: true } ? _w.Name : _w.Name + " – moje";
        if (TryColor(_w.Color, out var col)) ColorDot.Fill = new SolidColorBrush(col);
        foreach (var r in ShapePanel.Children.OfType<RadioButton>()) r.IsChecked = (string)r.Tag == _w.Shape;
        BtnDel.IsEnabled = BtnOver.IsEnabled = _selected is { IsCustom: true };
        _loading = false;
        Apply();
    }

    void Read()
    {
        if (_loading) return;
        _w.Size = SizeS.Value; _w.Thickness = ThickS.Value; _w.Gap = GapS.Value;
        _w.OffsetX = OxS.Value; _w.OffsetY = OyS.Value;
        _w.Outline = SwOutline.IsChecked == true; _w.Glow = SwGlow.IsChecked == true; _w.CenterDot = SwDot.IsChecked == true;
        Apply();
    }

    void Apply()
    {
        if (Preview.ActualWidth > 0)
            CrosshairRenderer.Draw(Preview, _w, Preview.ActualWidth / 2 + _w.OffsetX, Preview.ActualHeight / 2 + _w.OffsetY);
        _overlay.Update(_w);
        ScheduleSave();
    }

    void ScheduleSave() { _saveTimer.Stop(); _saveTimer.Start(); }

    void Preview_SizeChanged(object s, SizeChangedEventArgs e) => Apply();
    void Slider_Changed(object s, RoutedPropertyChangedEventArgs<double> e) => Read();
    void Switch_Click(object s, RoutedEventArgs e) => Read();
    void Bg_Changed(object s, RoutedPropertyChangedEventArgs<double> e) { if (!_loading) { _cfg.BgOpacity = BgS.Value; ScheduleSave(); } }

    void Shape_Checked(object s, RoutedEventArgs e)
    {
        if (_loading) return;
        _w.Shape = (string)((RadioButton)s).Tag;
        Apply();
    }

    void TxtColor_TextChanged(object s, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (TryColor(TxtColor.Text, out var col))
        {
            _w.Color = TxtColor.Text;
            ColorDot.Fill = new SolidColorBrush(col);
            Apply();
        }
    }

    void Swatch_Click(object s, RoutedEventArgs e) => TxtColor.Text = (string)((Button)s).Tag;

    static bool TryColor(string s, out Color c)
    {
        try
        {
            if (s.StartsWith('#')) { c = (Color)ColorConverter.ConvertFromString(s); return true; }
        }
        catch { }
        c = default; return false;
    }

    // ---------- zapis ----------
    bool NameTaken(string name, Crosshair? ignore = null) =>
        Presets.Games.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
        _mine.Any(c => !ReferenceEquals(c, ignore) && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Jeśli nazwa jest zajęta: "nazwa(2)", "nazwa(3)"... (istniejący numer na końcu jest zastępowany).</summary>
    string UniqueName(string name)
    {
        if (!NameTaken(name)) return name;
        var m = Regex.Match(name, @"^(.*?)\s*\(\d+\)$");
        var baseName = m.Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : name;
        for (int i = 2; ; i++)
        {
            var cand = $"{baseName}({i})";
            if (!NameTaken(cand)) return cand;
        }
    }

    Crosshair MakeCustom(string name)
    {
        var n = _w.Clone(); n.Name = name; n.IsCustom = true; n.Category = null; n.Note = null;
        return n;
    }

    void Save_Click(object s, RoutedEventArgs e)
    {
        var name = TxtName.Text.Trim();
        if (name.Length == 0) { ShowToast("Wpisz nazwę celownika"); return; }

        var final = UniqueName(name);          // nigdy nie nadpisuje istniejących
        var n2 = MakeCustom(final);
        _mine.Add(n2);

        ShowGame(false);
        ListMine.SelectedItem = n2;
        _selected = n2;
        Persist();
        ShowToast(final == name ? $"Zapisano „{final}”" : $"Nazwa zajęta – zapisano jako „{final}”");
    }

    void Overwrite_Click(object s, RoutedEventArgs e)
    {
        if (_selected is not { IsCustom: true } old) { ShowToast("Nadpisać można tylko własny celownik"); return; }
        var name = TxtName.Text.Trim();
        if (name.Length == 0) name = old.Name;
        if (NameTaken(name, old)) { ShowToast("Ta nazwa jest już zajęta"); return; }

        var n2 = MakeCustom(name);
        _mine[_mine.IndexOf(old)] = n2;
        ListMine.SelectedItem = n2;
        _selected = n2;
        Persist();
        ShowToast($"Nadpisano „{name}”");
    }

    void Delete_Click(object s, RoutedEventArgs e)
    {
        if (_selected is { IsCustom: true } c)
        {
            _mine.Remove(c);
            ListMine.SelectedItem = _mine[0];
            Persist();
            ShowToast("Usunięto");
        }
    }

    void Reset_Click(object s, RoutedEventArgs e)
    {
        if (_selected != null) { _w = _selected.Clone(); LoadEditor(); ShowToast("Przywrócono ustawienia presetu"); }
    }

    void Persist()
    {
        _cfg.Crosshairs = _mine.Where(c => c.IsCustom).ToList();
        _cfg.Active = _w.Clone();
        _cfg.SelectedCrosshair = _selected?.Name ?? "CLASSIC";
        _cfg.Save();
    }

    void ShowToast(string msg)
    {
        Toast.Text = msg;
        var a = new DoubleAnimationUsingKeyFrames();
        a.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.Zero));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(1, TimeSpan.FromSeconds(1.6)));
        a.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromSeconds(2.2)));
        Toast.BeginAnimation(OpacityProperty, a);
    }

    // ---------- ustawienia / tło ----------
    void Settings_Click(object s, RoutedEventArgs e) { UpdateBgName(); FadeIn(SettingsLayer, SetTr); }
    void SettingsClose_Click(object s, RoutedEventArgs e) { FadeOut(SettingsLayer); Persist(); }

    void PickBg_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Wybierz obraz tła",
            Filter = "Obrazy|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Wszystkie pliki|*.*"
        };
        if (dlg.ShowDialog(this) != true) return;

        if (LoadBitmap(dlg.FileName) == null) { BgNameTxt.Text = "Nie udało się wczytać tego pliku"; return; }

        var old = _cfg.BackgroundPath;
        string stored = dlg.FileName;
        try
        {
            var dest = System.IO.Path.Combine(AppContext.BaseDirectory, "background" + System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant());
            if (!string.Equals(System.IO.Path.GetFullPath(dest), System.IO.Path.GetFullPath(dlg.FileName), StringComparison.OrdinalIgnoreCase))
                File.Copy(dlg.FileName, dest, true);
            stored = dest;
        }
        catch { }

        if (old != null && !string.Equals(old, stored, StringComparison.OrdinalIgnoreCase)) DeleteStored(old);
        _cfg.BackgroundPath = stored;
        _cfg.BackgroundName = System.IO.Path.GetFileName(dlg.FileName);
        ApplyBackground();
        UpdateBgName();
        Persist();
    }

    void RemoveBg_Click(object s, RoutedEventArgs e)
    {
        DeleteStored(_cfg.BackgroundPath);
        _cfg.BackgroundPath = null; _cfg.BackgroundName = null;
        ApplyBackground();
        UpdateBgName();
        Persist();
    }

    static void DeleteStored(string? p)
    {
        try
        {
            if (p == null) return;
            var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(p))?.TrimEnd('\\', '/');
            var baseDir = System.IO.Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\', '/');
            if (string.Equals(dir, baseDir, StringComparison.OrdinalIgnoreCase) &&
                System.IO.Path.GetFileName(p).StartsWith("background", StringComparison.OrdinalIgnoreCase))
                File.Delete(p);
        }
        catch { }
    }

    void UpdateBgName() =>
        BgNameTxt.Text = _bgSrc == null ? "Brak własnego tła" : (_cfg.BackgroundName ?? System.IO.Path.GetFileName(_cfg.BackgroundPath ?? ""));

    void BgImg_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _cfg.BackgroundOpacity = BgImgS.Value;
        BgSharp.Opacity = BgImgS.Value;
        BgBlur.Opacity = BgImgS.Value;
        ScheduleSave();
    }

    void Blur_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        _cfg.BackgroundBlur = BlurS.Value;
        _blurTimer.Stop(); _blurTimer.Start();
        ScheduleSave();
    }

    void ApplyBackground()
    {
        _bgSrc = LoadBitmap(_cfg.BackgroundPath);
        if (_bgSrc == null)
        {
            BgSharp.Visibility = Visibility.Collapsed;
            BgBlur.Visibility = Visibility.Collapsed;
            return;
        }
        BgSharp.Background = new ImageBrush(_bgSrc) { Stretch = Stretch.UniformToFill };
        BgSharp.Opacity = _cfg.BackgroundOpacity;
        BgSharp.Visibility = Visibility.Visible;
        RebuildBlur();
    }

    /// <summary>Efekt "matowego szkła": rozmyta kopia tła widoczna tylko pod panelami (maska = wygląd panelu).</summary>
    void RebuildBlur()
    {
        if (_bgSrc == null || _cfg.BackgroundBlur < 0.5) { BgBlur.Visibility = Visibility.Collapsed; return; }
        double w = Stage.ActualWidth, h = Stage.ActualHeight;
        if (w < 10 || h < 10) return;
        try
        {
            var blurred = BlurBitmap(_bgSrc, w, h, _cfg.BackgroundBlur);
            BgBlur.Background = new ImageBrush(blurred) { Stretch = Stretch.Fill };
            BgBlur.Opacity = _cfg.BackgroundOpacity;
            BgBlur.OpacityMask = new VisualBrush(ContentRoot)
            {
                Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, ContentRoot.ActualWidth, ContentRoot.ActualHeight)
            };
            BgBlur.Visibility = Visibility.Visible;
        }
        catch { BgBlur.Visibility = Visibility.Collapsed; }
    }

    static BitmapSource BlurBitmap(BitmapSource src, double w, double h, double r)
    {
        var img = new Image
        {
            Source = src, Stretch = Stretch.UniformToFill,
            Width = w + 2 * r, Height = h + 2 * r,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Effect = new BlurEffect { Radius = r, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance }
        };
        var host = new Grid { Width = w, Height = h, ClipToBounds = true, Background = Brushes.Black };
        host.Children.Add(img);
        host.Measure(new Size(w, h));
        host.Arrange(new Rect(0, 0, w, h));
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(w), (int)Math.Ceiling(h), 96, 96, PixelFormats.Pbgra32);
        rtb.Render(host);
        rtb.Freeze();
        return rtb;
    }

    static BitmapSource? LoadBitmap(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            int pw;
            using (var fs = File.OpenRead(path))
                pw = BitmapFrame.Create(fs, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).PixelWidth;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.UriSource = new Uri(path);
            if (pw > 1920) bi.DecodePixelWidth = 1920;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    }

    // ---------- samouczek (tylko przy pierwszym uruchomieniu) ----------
    void StartTutorial()
    {
        SettingsLayer.Visibility = Visibility.Collapsed;
        _step = 0;
        ShowStep();
        FadeIn(Tutorial, TutTr);
    }

    void ShowStep()
    {
        var (t, b) = Steps[_step];
        bool last = _step == Steps.Length - 1;
        TutStep.Text = $"KROK {_step + 1} / {Steps.Length}";
        TutTitle.Text = t;
        TutBody.Text = b;
        TutBack.Visibility = _step == 0 ? Visibility.Collapsed : Visibility.Visible;
        TutSkip.Visibility = last ? Visibility.Collapsed : Visibility.Visible;
        TutNext.Content = last ? "Zaczynamy" : "Dalej";
        TutTitle.BeginAnimation(OpacityProperty, Anim(0, 1, 220));
        TutBody.BeginAnimation(OpacityProperty, Anim(0, 1, 260));

        TutDots.Children.Clear();
        for (int i = 0; i < Steps.Length; i++)
            TutDots.Children.Add(new Border
            {
                Height = 6, Width = i == _step ? 22 : 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 6, 0),
                Background = i == _step ? (Brush)FindResource("Accent") : new SolidColorBrush(Color.FromRgb(0x3A, 0x40, 0x54))
            });
    }

    void TutNext_Click(object s, RoutedEventArgs e) { if (_step < Steps.Length - 1) { _step++; ShowStep(); } else FinishTutorial(); }
    void TutBack_Click(object s, RoutedEventArgs e) { if (_step > 0) { _step--; ShowStep(); } }
    void TutSkip_Click(object s, RoutedEventArgs e) => FinishTutorial();
    void TutorialAgain_Click(object s, RoutedEventArgs e) => StartTutorial();

    void FinishTutorial()
    {
        FadeOut(Tutorial);
        _cfg.FirstRunDone = true;
        Persist();
    }
}
