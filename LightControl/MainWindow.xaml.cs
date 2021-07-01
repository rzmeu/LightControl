using ControlzEx.Theming;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using YeelightAPI;
using YeelightAPI.Models;
using Application = System.Windows.Application;
using System.IO.Ports;
using System.ComponentModel;
using System.Windows.Media.Animation;
using System.Diagnostics;

namespace LightControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private NotifyIcon notifyIcon;
        private bool isOpen;
        private Device device;
        private int? brightness;
        private int? currentBrightness;
        private Timer _timer;
        private Boolean hidden = true;

        public MainWindow()
        {
            InitializeComponent();
            ThemeManager.Current.ChangeTheme(this, "Dark.Blue");

            var primaryScreen = Screen.AllScreens.FirstOrDefault(s => s.Primary);
            if (primaryScreen != null)
            {
                var workingArea = primaryScreen.WorkingArea;

                Left = workingArea.Width - 300 - 8;
                Top = workingArea.Height - 200 - 16;
                Width = 308;
                Height = 204;
            }

            Topmost = true;
            ShowInTaskbar = false;
            Hide();

            notifyIcon = new NotifyIcon();
            notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Application.Current.Shutdown());
            
            notifyIcon.Click += notifyIcon_Click;
            notifyIcon.Icon = Properties.Resources.ico;
            notifyIcon.Visible = true;
            
            _timer = new Timer();
            _timer.Tick += OnTimedEvent;
            _timer.Interval = 500;

            RegisterInStartup(true);
            GetDevicesAsync();

        }

        private void Device_OnNotificationReceived(object sender, NotificationReceivedEventArgs e)
        {
            Trace.WriteLine(e.Result.Params);
        }

        private void ShowAnimation()
        {
            if (!hidden)
                return;
            Storyboard sb = new Storyboard();
            Show();
            Activate();
            ThicknessAnimation ta = new ThicknessAnimation(new Thickness(308, 0, 0, 0), new Thickness(0, 0, 0, 0), new Duration(new TimeSpan(0, 0, 0, 0, 200)));
            sb.Children.Add(ta);
            Storyboard.SetTarget(ta, slidingCanvas);
            Storyboard.SetTargetProperty(ta, new PropertyPath(Canvas.MarginProperty));
            sb.Begin();
            hidden = false;
        }

        private void HideAnimation()
        {
            if (hidden)
                return;
            Storyboard sb = new Storyboard();
            sb.Completed += Hide_Completed;
            ThicknessAnimation ta = new ThicknessAnimation(new Thickness(0, 0, 0, 0), new Thickness(308, 0, 0, 0), new Duration(new TimeSpan(0, 0, 0, 0, 200)));
            sb.Children.Add(ta);
            Storyboard.SetTarget(ta, slidingCanvas);
            Storyboard.SetTargetProperty(ta, new PropertyPath(Canvas.MarginProperty));
            sb.Begin();
            hidden = true;
        }

        private void Hide_Completed(object sender, EventArgs e)
        {
            Hide();
        }

        private BackgroundWorker backgroundWorker1 = new BackgroundWorker();

        private void RegisterInStartup(bool isChecked)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey
                ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (isChecked)
            {
                registryKey.SetValue("LightControl", AppDomain.CurrentDomain.BaseDirectory + "LightControl.exe");
            }
            else
            {
                registryKey.DeleteValue("LightControl");
            }
        }

        // Define the callback for the progress reporter
        private void OnDeviceFound(Device device)
        {
            this.device = device;


            device.OnNotificationReceived += Device_OnNotificationReceived;

            object brightness;
            
            device.Properties.TryGetValue(PROPERTIES.bright.ToString(), out brightness);
            int brght = int.Parse((string)brightness);
            Slider.Value = brght;
            currentBrightness = brght;
            object power;
            
            device.Properties.TryGetValue(PROPERTIES.power.ToString(), out power);
            ToggleSwitch.IsOn = power.ToString().Equals("on");
        }
	
        private	async Task GetDevicesAsync()
        {
            // Initialize the instance of Progress<T> with a callback to handle a discovered device
            var progressReporter = new Progress<Device>(OnDeviceFound);
        
            // Await the asynchronous call to the static API
            await DeviceLocator.DiscoverAsync(progressReporter);
        
            // Alternatively: although each device is handled as soon as it is discovered by the callback registered with the progress reporter, 
            // you still can await the result collection
            IEnumerable<Device> discoveredDevices = await DeviceLocator.DiscoverAsync(progressReporter);
        }
        

        void notifyIcon_Click(object sender, EventArgs e)
        {
            if (e.GetType() == typeof(System.Windows.Forms.MouseEventArgs) && ((System.Windows.Forms.MouseEventArgs)e).Button == MouseButtons.Left)
            {
                if(isOpen)
                    return;

                //Show();
                isOpen = true;
                ShowAnimation();
            }
        }



        private void WindowLostFocus(object sender, EventArgs e)
        {
            isOpen = false;
            HideAnimation();
            //Hide();
        }
        
        private void OnTimedEvent(object source, EventArgs e)
        {
            _timer.Stop();
            if (brightness != currentBrightness)
            {
                SetBrightness();
                _timer.Start(); 
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            brightness = (int)e.NewValue;

            if (_timer == null || !_timer.Enabled)
            {
                SetBrightness();
                _timer.Start();
            }
        }

        private async Task SetBrightness()
        {
            Task.Run(async () =>
            {
                if (brightness != null)
                {
                    currentBrightness = brightness;
                    try
                    {
                        if (!device.IsConnected)
                        {
                            await device.Connect();
                        }
                        await device.SetBrightness(currentBrightness.GetValueOrDefault(0), 500);
                    }
                    catch (Exception)
                    {
                    }
                }
            });
        }

        private void ToggleSwitch_OnToggled(object sender, RoutedEventArgs e)
        {
            SetEnabled(((ToggleSwitch) sender).IsOn);
        }

        private async Task SetEnabled(bool enabled)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (!device.IsConnected)
                    {
                        await device.Connect();
                    }
                    await device.SetPower(enabled);
                    if (enabled)
                    {
                        await device.SetBrightness(brightness.GetValueOrDefault(0), 500);
                    }
                }
                catch (Exception)
                {
                }
            });
        }

        private void GamingButton_OnClick(object sender, RoutedEventArgs e)
        {
            Slider.Value = 50;
        }

        private void MinimumButton_OnClick(object sender, RoutedEventArgs e)
        {
            Slider.Value = 1;
        }

        private void MaxButton_OnClick(object sender, RoutedEventArgs e)
        {
            Slider.Value = 100;
        }

    }
}
