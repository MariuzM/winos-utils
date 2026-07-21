using System.Windows.Controls;
using WinUtils.Services;

namespace WinUtils.Views;

public partial class PersonalizationPage : UserControl
{
    private readonly StartMenuTweaker _startMenu = new();

    public PersonalizationPage()
    {
        InitializeComponent();

        StartMenuSection.ScanFunc = () => _startMenu.Scan();
        StartMenuSection.ApplyAction = () => _startMenu.Apply();
        StartMenuSection.StatusChanged += (_, status) => StartMenuCard.Status = status;

        RefreshDarkMode();
        RefreshWindowBorders();
        RefreshWindowShadows();
        RefreshAnimations();
    }

    private async void OnAnimationsToggle(object sender, System.Windows.RoutedEventArgs _)
    {
        bool enabled = AnimationsToggle.IsChecked == true;
        AnimationsToggle.IsEnabled = false;
        AnimationsError.IsOpen = false;

        try
        {
            await Task.Run(() => AnimationManager.SetEnabled(enabled));
        }
        catch (Exception e)
        {
            AnimationsError.Message = e.Message;
            AnimationsError.IsOpen = true;
        }

        RefreshAnimations();
    }

    private void RefreshAnimations()
    {
        try
        {
            bool enabled = AnimationManager.IsEnabled();
            AnimationsToggle.IsChecked = enabled;
            AnimationsToggle.IsEnabled = true;
            AnimationsStateText.Text = enabled
                ? "On — windows and menus animate."
                : "Off — windows and menus snap instantly.";
            AnimationsCard.Status = enabled ? "Animations on" : "Animations off";
        }
        catch (Exception e)
        {
            AnimationsToggle.IsEnabled = false;
            AnimationsStateText.Text = "Unable to read the current setting.";
            AnimationsCard.Status = "State unavailable";
            AnimationsError.Message = e.Message;
            AnimationsError.IsOpen = true;
        }
    }

    private async void OnWindowShadowsToggle(object sender, System.Windows.RoutedEventArgs _)
    {
        bool enabled = WindowShadowsToggle.IsChecked == true;
        WindowShadowsToggle.IsEnabled = false;
        WindowShadowsError.IsOpen = false;

        try
        {
            await Task.Run(() => WindowShadowManager.SetEnabled(enabled));
        }
        catch (Exception e)
        {
            WindowShadowsError.Message = e.Message;
            WindowShadowsError.IsOpen = true;
        }

        RefreshWindowShadows();
    }

    private void RefreshWindowShadows()
    {
        try
        {
            bool enabled = WindowShadowManager.IsEnabled();
            WindowShadowsToggle.IsChecked = enabled;
            WindowShadowsToggle.IsEnabled = true;
            WindowShadowsStateText.Text = enabled
                ? "On — windows cast a drop shadow, visible as a dark edge."
                : "Off — no shadows, so nothing outlines the window edges.";
            WindowShadowsCard.Status = enabled ? "Shadows on" : "Shadows off";
        }
        catch (Exception e)
        {
            WindowShadowsToggle.IsEnabled = false;
            WindowShadowsStateText.Text = "Unable to read the current setting.";
            WindowShadowsCard.Status = "State unavailable";
            WindowShadowsError.Message = e.Message;
            WindowShadowsError.IsOpen = true;
        }
    }

    private async void OnDarkModeToggle(object sender, System.Windows.RoutedEventArgs _)
    {
        bool enabled = DarkModeToggle.IsChecked == true;
        DarkModeToggle.IsEnabled = false;
        DarkModeError.IsOpen = false;

        try
        {
            await Task.Run(() => ColorModeManager.SetDarkMode(enabled));
        }
        catch (Exception e)
        {
            DarkModeError.Message = e.Message;
            DarkModeError.IsOpen = true;
        }

        RefreshDarkMode();
    }

    private void RefreshDarkMode()
    {
        try
        {
            ColorModeState s = ColorModeManager.GetState();
            DarkModeToggle.IsChecked = s.Dark;
            DarkModeToggle.IsEnabled = true;

            if (s.Mixed)
            {
                DarkModeStateText.Text = "Custom — Windows and applications use different modes.";
                DarkModeCard.Status = "Custom mode";
            }
            else if (s.Dark)
            {
                DarkModeStateText.Text = "On — Windows and supported applications use dark mode.";
                DarkModeCard.Status = "Dark mode";
            }
            else
            {
                DarkModeStateText.Text = "Off — Windows and supported applications use light mode.";
                DarkModeCard.Status = "Light mode";
            }
        }
        catch (Exception e)
        {
            DarkModeToggle.IsEnabled = false;
            DarkModeStateText.Text = "Unable to read the current color mode.";
            DarkModeCard.Status = "State unavailable";
            DarkModeError.Message = e.Message;
            DarkModeError.IsOpen = true;
        }
    }

    private async void OnWindowBordersToggle(object sender, System.Windows.RoutedEventArgs _)
    {
        bool enabled = WindowBordersToggle.IsChecked == true;
        WindowBordersToggle.IsEnabled = false;
        WindowBordersError.IsOpen = false;

        try
        {
            await Task.Run(() => WindowBorderManager.SetEnabled(enabled));
        }
        catch (Exception e)
        {
            WindowBordersError.Message = e.Message;
            WindowBordersError.IsOpen = true;
        }

        RefreshWindowBorders();
    }

    private void RefreshWindowBorders()
    {
        try
        {
            WindowBorderState s = WindowBorderManager.GetState();
            WindowBordersToggle.IsChecked = s.Enabled;
            WindowBordersToggle.IsEnabled = s.Supported && (s.HelperAvailable || s.Enabled);

            if (!s.Supported)
            {
                WindowBordersStateText.Text = "Unavailable — requires Windows 11.";
                WindowBordersCard.Status = "Windows 11 required";
            }
            else if (s.Enabled && s.Running)
            {
                WindowBordersStateText.Text = "On — helper running and registered for sign-in.";
                WindowBordersCard.Status = "Borders removed";
            }
            else if (s.Enabled)
            {
                WindowBordersStateText.Text = "On — helper will start at the next sign-in.";
                WindowBordersCard.Status = "Starts at sign-in";
            }
            else if (!s.HelperAvailable)
            {
                WindowBordersStateText.Text =
                    $"Unavailable — {WindowBorderProtocol.Protocol.HelperFileName} is missing.";
                WindowBordersCard.Status = "Helper missing";
            }
            else
            {
                WindowBordersStateText.Text = "Off — Windows draws the default borders.";
                WindowBordersCard.Status = "Windows default";
            }
        }
        catch (Exception e)
        {
            WindowBordersToggle.IsEnabled = false;
            WindowBordersStateText.Text = "Unable to read the current state.";
            WindowBordersCard.Status = "State unavailable";
            WindowBordersError.Message = e.Message;
            WindowBordersError.IsOpen = true;
        }
    }
}
