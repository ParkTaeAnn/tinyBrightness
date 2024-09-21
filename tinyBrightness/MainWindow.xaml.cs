﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using ScreenTools;
using IniParser.Model;
using NHotkey.Wpf;
using System.Windows.Input;
using NHotkey;
using SourceChord.FluentWPF;
using Microsoft.Win32;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Media;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using Application = System.Windows.Application;
using System.Globalization;

namespace tinyBrightness
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            AdaptIconToTheme();
            Main_Grid.PreviewMouseWheel += (sender, e)
                                        => Slider_Brightness.Value += Slider_Brightness.SmallChange * e.Delta / 120;
        }

        public void AdaptIconToTheme()
        {
            if (Environment.OSVersion.Version.Major == 10)
            {
                int releaseId = int.Parse(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "").ToString());

                if (releaseId >= 1903)
                {
                    string CurrentTheme = SystemTheme.WindowsTheme.ToString();

                    if (CurrentTheme == "Dark")
                        TrayIcon.Icon = new Icon(Properties.Resources.lightIcon, SystemInformation.SmallIconSize);
                    else if (CurrentTheme == "Light")
                        TrayIcon.Icon = new Icon(Properties.Resources.darkIcon, SystemInformation.SmallIconSize);
                }
                else
                {
                    TrayIcon.Icon = new Icon(Properties.Resources.lightIcon, SystemInformation.SmallIconSize);
                }
            }
            else
            {
                TrayIcon.Icon = new Icon(Properties.Resources.icon, SystemInformation.SmallIconSize);
            }
        }

        class MONITOR
        {
            public string name { get; set; }
            public DisplayConfiguration.PHYSICAL_MONITOR Handle { get; set; }
            public uint Min { get; set; }
            public uint Max { get; set; }

            public MONITOR(string name, DisplayConfiguration.PHYSICAL_MONITOR Handle, uint Min, uint Max)
            {
                this.name = name;
                this.Handle = Handle;
                this.Min = Min;
                this.Max = Max;
            }
        }

        List<MONITOR> MonitorList { get; set; } = new List<MONITOR>();

        MONITOR CurrentMonitor;

        private void Set_Initial_Brightness()
        {
            double Brightness = 0;

            try
            {
                Brightness = DisplayConfiguration.GetMonitorBrightness(CurrentMonitor.Handle) * 100;

                Slider_Brightness.IsEnabled = true;
                Main_Grid.ToolTip = null;
            }
            catch
            {
                Slider_Brightness.IsEnabled = false;
                Main_Grid.ToolTip = "This monitor is not supported. You may need to enable «DDC/CI» option in your monitor settings.";
            }

            Slider_Brightness.Value = Brightness;
            PercentText.Text = Brightness.ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateMonitorList();
            SetWindowPosition();
            Show();
            Activate();
            Set_Initial_Brightness();
            AfterUpdateCheck();

            IniData data = SettingsController.GetCurrentSettings();

            DisplayConfiguration.PHYSICAL_MONITOR Handle = DisplayConfiguration.GetPhysicalMonitors(DisplayConfiguration.GetCurrentMonitor())[0];

            try
            {
                DisplayConfiguration.MonitorExtremums MonExtems = DisplayConfiguration.GetMonitorExtremums(Handle);
                double CurrentBrightness = (double)(MonExtems.Current - MonExtems.Min) / (double)(MonExtems.Max - MonExtems.Min);
                HotkeyPopupWindow.dwMinimumBrightness = MonExtems.Min;
                HotkeyPopupWindow.dwMaximumBrightness = MonExtems.Max;
                HotkeyPopupWindow.dwCurrentBrightness = MonExtems.Current;
                HotkeyPopupWindow.PercentText.Text = (CurrentBrightness * 100).ToString();
            }
            catch { }
            HotkeyPopupWindow.Show();
            HotkeyPopupWindow.ShowMe(data["Misc"]["HotkeyPopupPosition"]);
        }

        public bool IsAnimationsEnabled => SystemParameters.ClientAreaAnimation &&
                                                  RenderCapability.Tier > 0;

        public double TopAnim { get; set; } = 0;
        public double TopAnimMargin { get; set; } = 0;

        private void SetWindowPosition()
        {
            double factor = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;

            var desktopWorkingArea = Screen.GetWorkingArea(System.Windows.Forms.Control.MousePosition);
            Left = desktopWorkingArea.Right / factor - Width;
            /*Top = desktopWorkingArea.Bottom / factor - Height;*/

            int AdditionalPixel = 0;
            if (factor > 1)
                AdditionalPixel = 1;

            TopAnim = desktopWorkingArea.Bottom / factor - Height + AdditionalPixel;
            TopAnimMargin = desktopWorkingArea.Bottom / factor - Height + 30 + AdditionalPixel;

            if (IsAnimationsEnabled)
                (FindResource("showMe") as Storyboard).Begin(this);
            else
                (FindResource("showMeWOAnim") as Storyboard).Begin(this);
        }

        private void UpdateMonitorList()
        {
            MonitorList.Clear();

            foreach (Screen screen in Screen.AllScreens)
            {
                DisplayConfiguration.PHYSICAL_MONITOR mon = DisplayConfiguration.GetPhysicalMonitors(DisplayConfiguration.GetMonitorByBounds(screen.Bounds))[0];

                string Name = screen.DeviceFriendlyName();

                if (string.IsNullOrEmpty(Name))
                {
                    Name = "Generic Monitor";
                }

                DisplayConfiguration.MonitorExtremums MonExtrs;

                try
                {
                    MonExtrs = DisplayConfiguration.GetMonitorExtremums(mon);
                }
                catch
                {
                    MonExtrs = new DisplayConfiguration.MonitorExtremums() { Min = 0, Max = 0 };
                }

                MonitorList.Add(new MONITOR(Name, mon, MonExtrs.Min, MonExtrs.Max));
            }

            Monitor_List_Combobox.ItemsSource = MonitorList;
            Monitor_List_Combobox.SelectedItem = MonitorList[0];
            CurrentMonitor = MonitorList[0];
        }


        private void Window_Deactivated(object sender, EventArgs e)
        {
            /*Hide();*/
            (FindResource("hideMe") as Storyboard).Begin(this);
        }

        private void Monitor_List_Combobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CurrentMonitor = MonitorList[Monitor_List_Combobox.SelectedIndex];
            Set_Initial_Brightness();
        }

        #region Hotkeys

        class Keys
        {
            public ModifierKeys Modifiers;
            public Key MainKey;
        }

        private Keys GetKeys(string HotkeyString)
        {
            string[] HotkeyArray = HotkeyString.Split('+');

            Keys keys = new Keys();

            foreach (string Element in HotkeyArray)
            {
                switch (Element)
                {
                    case "Ctrl":
                        keys.Modifiers |= ModifierKeys.Control;
                        break;
                    case "Alt":
                        keys.Modifiers |= ModifierKeys.Alt;
                        break;
                    case "Shift":
                        keys.Modifiers |= ModifierKeys.Shift;
                        break;
                    default:
                        Enum.TryParse(Element, out keys.MainKey);
                        break;
                }
            }

            return keys;
        }

        public void SetHotkeysByStrings(string UpString, string DownString)
        {
            //unbind current bindings
            RemoveAllHotkeys();

            //brightness up
            Keys BrightnessUpKeys = GetKeys(UpString);
            HotkeyManager.Current.AddOrReplace("BrightnessUp", BrightnessUpKeys.MainKey, BrightnessUpKeys.Modifiers, OnBrightnessUp);

            //brightness down
            Keys BrightnessDownKeys = GetKeys(DownString);
            HotkeyManager.Current.AddOrReplace("BrightnessDown", BrightnessDownKeys.MainKey, BrightnessDownKeys.Modifiers, OnBrightnessDown);
        }

        public void RemoveAllHotkeys()
        {
            HotkeyManager.Current.Remove("BrightnessUp");
            HotkeyManager.Current.Remove("BrightnessDown");
        }

        private HotkeyPopup HotkeyPopupWindow = new HotkeyPopup();

        private void OnBrightnessUp(object sender, HotkeyEventArgs e)
        {
            BrightnessHotkeyEvent(true);
        }

        private void OnBrightnessDown(object sender, HotkeyEventArgs e)
        {
            BrightnessHotkeyEvent(false);
        }

        private void BrightnessHotkeyEvent(bool IsUp)
        {
            (FindResource("hideMe") as Storyboard).Begin(this);

            IniData data = SettingsController.GetCurrentSettings();

            int StepSize = 5;
            if (int.TryParse(data["Hotkeys"]["StepSize"], out int StepSizeValue)) StepSize = StepSizeValue;

            try
            {
                double StepDouble = (double)StepSize / 100;

                DisplayConfiguration.PHYSICAL_MONITOR Handle = DisplayConfiguration.GetPhysicalMonitors(DisplayConfiguration.GetCurrentMonitor())[0];
                /*Task.Run(() => { try { DisplayConfiguration.SetBrightnessOffset(Handle, IsUp ? StepDouble : -StepDouble); } catch { } });*/

                if (HotkeyPopupWindow.IsVisible)
                {
                    int HotkeyPopupBrightness = int.Parse(HotkeyPopupWindow.PercentText.Text);
                    int NewBrightness = HotkeyPopupBrightness + (IsUp ? StepSize : -StepSize);

                    if (NewBrightness > 100) NewBrightness = 100;
                    else if (NewBrightness < 0) NewBrightness = 0;

                    Task.Run(() => { try { DisplayConfiguration.SetMonitorBrightness(Handle, (double)NewBrightness / 100, HotkeyPopupWindow.dwMinimumBrightness, HotkeyPopupWindow.dwMaximumBrightness); } catch { } });

                    HotkeyPopupWindow.PercentText.Text = NewBrightness.ToString();
                    HotkeyPopupWindow.ShowMe(data["Misc"]["HotkeyPopupPosition"]);
                }
                else
                {
                    DisplayConfiguration.MonitorExtremums MonExtems = DisplayConfiguration.GetMonitorExtremums(Handle);
                    double CurrentBrightness = (double)(MonExtems.Current - MonExtems.Min) / (double)(MonExtems.Max - MonExtems.Min);
                    Task.Run(() => { try { DisplayConfiguration.SetBrightnessOffset(Handle, IsUp ? StepDouble : -StepDouble, CurrentBrightness, MonExtems.Min, MonExtems.Max); } catch { } });
                    HotkeyPopupWindow.dwMinimumBrightness = MonExtems.Min;
                    HotkeyPopupWindow.dwMaximumBrightness = MonExtems.Max;
                    HotkeyPopupWindow.dwCurrentBrightness = MonExtems.Current;
                    HotkeyPopupWindow.PercentText.Text = ((CurrentBrightness * 100) + (IsUp ? StepSize : -StepSize)).ToString();
                    HotkeyPopupWindow.ShowMe(data["Misc"]["HotkeyPopupPosition"]);
                }
            }
            catch { }
        }

        #endregion

        #region Settings
        public void LoadSettings()
        {
            SettingsController.LoadSettings();
            IniData data = SettingsController.GetCurrentSettings();

            if (data["Hotkeys"]["HotkeysEnable"] == "1")
                SetHotkeysByStrings(data["Hotkeys"]["HotkeyUp"], data["Hotkeys"]["HotkeyDown"]);

            if (data["Misc"]["Blur"] == "1" && Environment.OSVersion.Version.Major == 10)
            {
                Background = null;
                AcrylicWindow.SetEnabled(this, true);
            }

            UpdateCheckTimer.Tick += (sender, e) =>
            {
                CheckForUpdates(false);
            };

            if (data["Updates"]["DisableCheckEveryDay"] != "1")
                UpdateCheckTimer.Start();

            if (data["Updates"]["DisableCheckOnStartup"] != "1")
                CheckForUpdates(false);

            //AutoBrightness
            if (data["AutoBrightness"]["Enabled"] == "1")
                CheckForSunriset.Start();

            //AutoConnectBrightness
            if (data["AutoConnectBrightness"]["Enabled"] == "1")
                CheckForAutoConnectBrightness.Start();

            SetupAutoBrightnessTimer();
            SetupAutoConnectBrightnessTimer();

            TrayIcon.TrayBalloonTipClicked += (senderB, eB) => new Update().Window_Loaded();
        }

        public void CheckForUpdates(bool IsManual)
        {
            UpdateController UpdContr = new UpdateController();
            UpdContr.CheckForUpdatesAsync();
            UpdContr.CheckingComplete += (sender, IsAvailabe) =>
            {
                double factor = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;

                if (IsAvailabe)
                {
                    TrayIcon.ShowBalloonTip("New Version is Available: " + UpdContr.NewVersionString, UpdContr.Description + " Click here to see more.", new Icon(Properties.Resources.updateIcon, new System.Drawing.Size(Convert.ToInt32(40 * factor), Convert.ToInt32(40 * factor))), true);
                }
                else if (!IsAvailabe && IsManual)
                {
                    TrayIcon.ShowBalloonTip("No Updates Available", "You are using latest version.", new Icon(Properties.Resources.updateIcon, new System.Drawing.Size(Convert.ToInt32(40 * factor), Convert.ToInt32(40 * factor))), true);
                }
            };
        }

        public DispatcherTimer UpdateCheckTimer = new DispatcherTimer()
        {
            Interval = new TimeSpan(1, 0, 0, 0)
        };

        private void AfterUpdateCheck()
        {
            if (File.Exists("tinyBrightness.Old.exe"))
            {
                File.Delete("tinyBrightness.Old.exe");

                double factor = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;

                TrayIcon.ShowBalloonTip("Update installed successfully!", "Enjoy new version :3", new Icon(Properties.Resources.updateIcon, new System.Drawing.Size(Convert.ToInt32(40 * factor), Convert.ToInt32(40 * factor))), true);
            }
        }

        #endregion

        private DebounceDispatcher debounceTimer = new DebounceDispatcher();

        private void Slider_Brightness_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            PercentText.Text = Convert.ToInt32(((Slider)sender).Value).ToString();

            debounceTimer.Throttle(15, (p) =>
            {
                try
                {
                    DisplayConfiguration.PHYSICAL_MONITOR Handle = CurrentMonitor.Handle;
                    uint Min = CurrentMonitor.Min;
                    uint Max = CurrentMonitor.Max;
                    double Value = ((Slider)sender).Value / 100;
                    Task.Run(() => DisplayConfiguration.SetMonitorBrightness(Handle, Value, Min, Max));
                }
                catch { }
            });
        }

        #region Tray

        private void TaskbarIcon_TrayLeftMouseUp(object sender, RoutedEventArgs e)
        {
            SetWindowPosition();
            Set_Initial_Brightness();
            Show();
            Activate();
        }

        private void UpdateMonitors_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Assembly.GetEntryAssembly().Location);
            Application.Current.Shutdown();
            //UpdateMonitorList();
            //Set_Initial_Brightness();
            //SetWindowPosition();
            //Show();
            //Activate();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SetWindowPosition(); // workaround that prevents the settings window from freezing under certain conditions
            var Win = new Settings();
            Win.Owner = this;
            Win.Show();
            (FindResource("hideMe") as Storyboard).Begin(this);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            CheckForUpdates(true);
        }

        #endregion

        #region AutoBrightness

        private void AutoBrightnessOnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            IniData data = SettingsController.GetCurrentSettings();

            switch (e.Mode)
            {
                case PowerModes.Resume:
                    if (data["AutoBrightness"]["Enabled"] == "1")
                    {
                        CheckForSunriset.Start();
                        Task.Run(() =>
                        {
                            System.Threading.Thread.Sleep(4000);
                            SetAutoBrightness(1);
                        });
                    }
                    break;
                case PowerModes.Suspend:
                    CheckForSunriset.Stop();
                    break;
            }
        }

        public DispatcherTimer CheckForSunriset = new DispatcherTimer()
        {
            Interval = new TimeSpan(0, 1, 0)
        };

        public void SetupAutoBrightnessTimer()
        {
            SystemEvents.PowerModeChanged += AutoBrightnessOnPowerChange;
            CheckForSunriset.Tick += (sender, e) =>
            {
                Task.Run(() => SetAutoBrightness(0));
            };
        }

        private void SetAutoBrightness(int Mode)
        {
            TimeSpan CurrentTime = DateTime.UtcNow.TimeOfDay;
            TimeSpan TrimmedCurrentTime = new TimeSpan(CurrentTime.Hours, CurrentTime.Minutes, 0);

            SunrisetTools RisetTools = new SunrisetTools(AutoBrightnessSettings.GetLat(), AutoBrightnessSettings.GetLon());

            foreach (MONITOR mon in MonitorList)
            {
                if (TimeSpan.Compare(TrimmedCurrentTime, RisetTools.GetTodaySunrise()) == Mode)
                {
                    try { DisplayConfiguration.SetMonitorBrightness(mon.Handle, AutoBrightnessSettings.GetSunriseBrightness(), mon.Min, mon.Max); }
                    catch { }
                }
                else if (TimeSpan.Compare(TrimmedCurrentTime, RisetTools.GetTodaySunset()) == Mode)
                {
                    try { DisplayConfiguration.SetMonitorBrightness(mon.Handle, AutoBrightnessSettings.GetSunsetBrightness(), mon.Min, mon.Max); }
                    catch { }
                }
                else if (TimeSpan.Compare(TrimmedCurrentTime, RisetTools.GetTodayDawn()) == Mode)
                {
                    try { DisplayConfiguration.SetMonitorBrightness(mon.Handle, AutoBrightnessSettings.GetAstroSunriseBrightness(), mon.Min, mon.Max); }
                    catch { }
                }
                else if (TimeSpan.Compare(TrimmedCurrentTime, RisetTools.GetTodayDusk()) == Mode)
                {
                    try { DisplayConfiguration.SetMonitorBrightness(mon.Handle, AutoBrightnessSettings.GetAstroSunsetBrightness(), mon.Min, mon.Max); }
                    catch { }
                }
            }
        }

        #endregion

        #region AutoConnectBrightness

        private void AutoConnectOnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            IniData data = SettingsController.GetCurrentSettings();

            switch (e.Mode)
            {
                case PowerModes.Resume:
                    if (data["AutoConnectBrightness"]["Enabled"] == "1")
                    {
                        CheckForAutoConnectBrightness.Start();
                        Task.Run(() =>
                        {
                            System.Threading.Thread.Sleep(4000);
                            SetAutoConnectBrightness(1);
                        });
                    }
                    break;
                case PowerModes.Suspend:
                    CheckForAutoConnectBrightness.Stop();
                    break;
            }
        }

        public DispatcherTimer CheckForAutoConnectBrightness = new DispatcherTimer()
        {
            Interval = new TimeSpan(0, 0, 10)
        };

        public void SetupAutoConnectBrightnessTimer()
        {
            SystemEvents.PowerModeChanged += AutoConnectOnPowerChange;
            CheckForAutoConnectBrightness.Tick += (sender, e) =>
            {
                Task.Run(() => SetAutoConnectBrightness(0));
            };
        }

        private void SetAutoConnectBrightness(int Mode)
        {
            foreach (MONITOR mon in MonitorList)
            {
                try
                {
                    string chkModel = AutoBrightnessSettings.GetAutoConnectModel();
                    double chkBrightness = AutoBrightnessSettings.GetAutoConnectBrightness();
                    if (DisplayConfiguration.GetMonitorBrightness(mon.Handle) != chkBrightness && (mon.name == chkModel || "ALLS" == chkModel))
                    {
                        DisplayConfiguration.SetMonitorBrightness(mon.Handle, chkBrightness, mon.Min, mon.Max);
                        System.Threading.Thread.Sleep(500);
                    }
                }
                catch { }
            }
        }
        #endregion
    }
}
