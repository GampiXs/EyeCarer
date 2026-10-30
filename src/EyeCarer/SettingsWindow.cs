using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace EyeCarer;

internal sealed class SettingsWindow : Window
{
    private readonly Settings original;
    private readonly Action<Settings> apply, preview;
    private readonly Func<string> status;
    private readonly StackPanel fields = new();
    private readonly TextBox interval = new(), duration = new(), message = new();
    private readonly CheckBox animations = new(), show = new(), startup = new();
    private readonly Slider blur = new(), grain = new(), coverage = new(), opacity = new();
    private readonly TextBlock error = new(), state = new(), saved = new();
    private readonly DispatcherTimer ticker = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool filling;
    public SettingsWindow(Settings current, Action<Settings> apply, Action<Settings> preview, Func<string> status)
    {
        original = current.Clone(); this.apply = apply; this.preview = preview; this.status = status;
        Title = "EyeCarer — Settings"; Width = 550; Height = 660; MinWidth = 480; MinHeight = 480;
        MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#151820"); Foreground = Brush("#F5F2F4"); FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
        var resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <Style TargetType='TextBox'><Setter Property='Background' Value='#252936'/><Setter Property='Foreground' Value='#F5F2F4'/><Setter Property='BorderBrush' Value='#505665'/><Setter Property='Padding' Value='9,6'/><Setter Property='CaretBrush' Value='White'/><Style.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='BorderBrush' Value='#E26A86'/></Trigger></Style.Triggers></Style>
 <Style TargetType='Button'><Setter Property='Foreground' Value='#F5F2F4'/><Setter Property='Background' Value='#303442'/><Setter Property='Padding' Value='12,9'/><Setter Property='Margin' Value='4,0'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='border' Background='{TemplateBinding Background}' BorderBrush='#555B6B' BorderThickness='1' CornerRadius='6' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='border' Property='BorderBrush' Value='#E26A86'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='border' Property='BorderBrush' Value='White'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType='CheckBox'><Setter Property='Margin' Value='0,7'/><Setter Property='Foreground' Value='#F5F2F4'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><StackPanel Orientation='Horizontal' Background='Transparent'><Border x:Name='track' Width='32' Height='18' CornerRadius='9' Background='#515565' Margin='0,0,10,0'><Ellipse x:Name='knob' Width='12' Height='12' Fill='White' HorizontalAlignment='Left' Margin='3,0'/></Border><ContentPresenter VerticalAlignment='Center'/></StackPanel><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='track' Property='Background' Value='#AD3E5B'/><Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='track' Property='Background' Value='#D9728C'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>");
        Resources = resources;
        var root = new DockPanel(); Content = root;
        var footer = new StackPanel { Margin = new Thickness(20,12,20,16) }; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        error.Foreground = Brush("#FFA3B5"); error.TextWrapping = TextWrapping.Wrap; footer.Children.Add(error);
        saved.Foreground = Brush("#C8ACB5"); saved.Margin = new Thickness(0,0,0,8); footer.Children.Add(saved);
        var actions = new DockPanel(); footer.Children.Add(actions);
        Button AddButton(string text, Action action) { var b = new Button { Content=text }; b.Click += (_,_)=>action(); return b; }
        actions.Children.Add(AddButton("Reset to Defaults",()=>Fill(new Settings())));
        var right = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right }; actions.Children.Add(right);
        right.Children.Add(AddButton("Cancel",Close));
        var save = AddButton("Save & Apply",()=>Run(false)); save.Background = Brush("#96334E"); right.Children.Add(save);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, Padding=new Thickness(24,16,24,8) }; root.Children.Add(scroll); scroll.Content=fields;
        fields.Children.Add(new TextBlock { Text="EyeCarer", FontSize=26, FontWeight=FontWeights.SemiBold });
        fields.Children.Add(new TextBlock { Text="A little room for your eyes.", Foreground=Brush("#ADB3C4"), Margin=new Thickness(0,2,0,12) });
        Section("GENERAL"); Numeric("Rest Interval", "minutes · 0.5–120",interval); Numeric("Rest Duration","seconds · 1–300, shorter than interval",duration);
        Section("VISUAL EFFECTS"); Range("Blur Intensity",blur,50,false); Range("Film Grain",grain,100,true); Range("Red Haze Coverage",coverage,100,true); Range("Red Haze Opacity",opacity,100,true);
        animations.Content="Enable Animations"; fields.Children.Add(animations);
        fields.Children.Add(AddButton("Preview Effect · 5 seconds",()=>Run(true)));
        Section("CENTER MESSAGE"); show.Content="Show Center Text"; fields.Children.Add(show); message.MaxLength=80; fields.Children.Add(message);
        show.Checked += (_,_)=>message.IsEnabled=true; show.Unchecked += (_,_)=>message.IsEnabled=false;
        Section("SYSTEM"); startup.Content="Start with Windows"; fields.Children.Add(startup); state.Foreground=Brush("#ADB3C4"); state.Margin=new Thickness(0,10,0,12); fields.Children.Add(state);
        foreach(var text in new[]{interval,duration,message}) text.TextChanged += (_,_)=>Changed();
        foreach(var toggle in new[]{animations,show,startup}) { toggle.Checked+=(_,_)=>Changed(); toggle.Unchecked+=(_,_)=>Changed(); }
        Fill(current); saved.Text="";
        ticker.Tick+=(_,_)=>state.Text=status(); ticker.Start(); state.Text=status();
        Closed+=(_,_)=>ticker.Stop();
    }
    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    private void Changed() { if(!filling) { saved.Text="Unsaved changes"; error.Text=""; } }
    private void Section(string title) => fields.Children.Add(new TextBlock { Text=title, FontSize=11, FontWeight=FontWeights.SemiBold, Foreground=Brush("#CF7A91"), Margin=new Thickness(0,18,0,10) });
    private void Numeric(string title,string unit,TextBox input)
    {
        var row=new Grid { Margin=new Thickness(0,0,0,10) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(110) });
        var label=new StackPanel(); label.Children.Add(new TextBlock {Text=title}); label.Children.Add(new TextBlock {Text=unit,FontSize=11,Foreground=Brush("#ADB3C4")}); row.Children.Add(label);
        Grid.SetColumn(input,1); row.Children.Add(input); fields.Children.Add(row);
        System.Windows.Automation.AutomationProperties.SetName(input,title);
    }
    private void Range(string title,Slider slider,double max,bool percent)
    {
        var row=new DockPanel(); var value=new TextBlock { Width=52,TextAlignment=TextAlignment.Right }; DockPanel.SetDock(value,Dock.Right); row.Children.Add(value); row.Children.Add(new TextBlock {Text=title}); fields.Children.Add(row);
        slider.Minimum=0; slider.Maximum=max; slider.TickFrequency=1; slider.IsSnapToTickEnabled=true; slider.Margin=new Thickness(0,4,0,10); slider.Foreground=Brush("#CF7A91");
        slider.ValueChanged+=(_,_)=> { value.Text=slider.Value.ToString("0",CultureInfo.InvariantCulture)+(percent?"%":""); Changed(); }; fields.Children.Add(slider);
        System.Windows.Automation.AutomationProperties.SetName(slider,title);
    }
    private void Fill(Settings s)
    {
        filling=true; interval.Text=(s.IntervalSeconds/60).ToString("0.###",CultureInfo.CurrentCulture); duration.Text=s.DurationSeconds.ToString(CultureInfo.CurrentCulture);
        blur.Value=s.BlurRadius; grain.Value=s.GrainStrength*100; coverage.Value=s.HazeCoverage*100; opacity.Value=s.HazeOpacity*100;
        animations.IsChecked=s.EnableAnimations; show.IsChecked=s.ShowCenterText; message.Text=s.CenterText; message.IsEnabled=s.ShowCenterText; startup.IsChecked=s.StartWithWindows;
        filling=false; Changed();
    }
    private void Run(bool isPreview)
    {
        bool Number(string text,out double value) => double.TryParse(text.Replace(',', '.'),NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out value)&&double.IsFinite(value);
        if(!Number(interval.Text,out var minutes)||minutes<.5||minutes>120) { error.Text="Rest Interval: enter 0.5–120 minutes."; interval.Focus(); return; }
        if(!Number(duration.Text,out var seconds)||seconds<1||seconds>300||seconds>=minutes*60) { error.Text="Rest Duration: enter 1–300 seconds, less than the interval."; duration.Focus(); return; }
        if(string.IsNullOrWhiteSpace(message.Text)) { error.Text="Message Text cannot be empty."; message.Focus(); return; }
        var draft=original.Clone(); draft.IntervalSeconds=minutes*60; draft.DurationSeconds=seconds; draft.BlurRadius=blur.Value; draft.GrainStrength=grain.Value/100; draft.HazeCoverage=coverage.Value/100; draft.HazeOpacity=opacity.Value/100;
        draft.EnableAnimations=animations.IsChecked==true; draft.ShowCenterText=show.IsChecked==true; draft.CenterText=message.Text; draft.StartWithWindows=startup.IsChecked==true;
        try { if(isPreview) preview(draft); else { apply(draft); saved.Text="Saved and applied"; } error.Text=""; }
        catch(Exception e) { Log.Error("settings",e); error.Text="Could not apply settings. Check folder permissions and Windows Startup, then retry."; startup.IsChecked=StartupShortcut.Enabled; }
    }
}
