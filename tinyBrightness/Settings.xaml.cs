﻿using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IniParser;
using IniParser.Model;
using Microsoft.Win32;

namespace tinyBrightness
{
    /// <summary>
    /// Логика взаимодействия для Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        public Settings()
        {
            InitializeComponent();
        }

        private FileIniDataParser parser = new FileIniDataParser();

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (key == Key.LeftShift || key == Key.RightShift
                || key == Key.LeftCtrl || key == Key.RightCtrl
                || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            ((TextBox)sender).Text = GetHotkey(key).ToString();
            Keyboard.ClearFocus();
        }

        private StringBuilder GetHotkey(Key key)
        {
            StringBuilder HotkeyText = new StringBuilder();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                HotkeyText.Append("Ctrl+");
            }
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                HotkeyText.Append("Shift+");
            }
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            {
                HotkeyText.Append("Alt+");
            }
            HotkeyText.Append(key.ToString());

            return HotkeyText;
        }

        private void HotkeysSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            IniData data = SettingsController.GetCurrentSettings();

            if (HotkeysSwitch.IsOn)
            {
                data["Hotkeys"]["HotkeysEnable"] = "1";
                ((MainWindow)Owner).SetHotkeysByStrings(BrightnessUpTextbox.Text, BrightnessDownTextbox.Text);
            }
            else
            {
                data["Hotkeys"]["HotkeysEnable"] = "0";
                ((MainWindow)Owner).RemoveAllHotkeys();
            }

            SettingsController.SaveSettings(data);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }

        private void AcrylicWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (rk.GetValue("tinyBrightness", null) != null)
                RunSwitch.IsOn = true;

            IniData data = SettingsController.GetCurrentSettings();

            BrightnessUpTextbox.Text = data["Hotkeys"]["HotkeyUp"];
            BrightnessDownTextbox.Text = data["Hotkeys"]["HotkeyDown"];

            if (data["Hotkeys"]["HotkeysEnable"] == "1")
                HotkeysSwitch.IsOn = true;

            if (data["Misc"]["Blur"] == "1" && System.Environment.OSVersion.Version.Major == 10)
                BlurSwitch.IsOn = true;

            if (System.Environment.OSVersion.Version.Major != 10)
                BlurSwitch.IsEnabled = false;
        }

        private void BrightnessUpTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (((TextBox)sender).Text == "")
                ((TextBox)sender).Text = "Ctrl+Shift+Add";
            else
            {
                IniData data = SettingsController.GetCurrentSettings();

                data["Hotkeys"]["HotkeyUp"] = ((TextBox)sender).Text;

                SettingsController.SaveSettings(data);

                ((MainWindow)Owner).SetHotkeysByStrings(BrightnessUpTextbox.Text, BrightnessDownTextbox.Text);
            }
        }

        private void BrightnessDownTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (((TextBox)sender).Text == "")
                ((TextBox)sender).Text = "Ctrl+Shift+Subtract";
            else
            {
                IniData data = SettingsController.GetCurrentSettings();

                data["Hotkeys"]["HotkeyDown"] = ((TextBox)sender).Text;

                SettingsController.SaveSettings(data);

                ((MainWindow)Owner).SetHotkeysByStrings(BrightnessUpTextbox.Text, BrightnessDownTextbox.Text);
            }
        }

        private void RunSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (RunSwitch.IsOn)
                rk.SetValue("tinyBrightness", Application.ResourceAssembly.Location + " --silent");
            else
                rk.DeleteValue("tinyBrightness", false);
        }

        private void BlurSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            IniData data = SettingsController.GetCurrentSettings();

            if (BlurSwitch.IsOn)
                data["Misc"]["Blur"] = "1";
            else
                data["Misc"]["Blur"] = "0";

            SettingsController.SaveSettings(data);
        }
    }
}
