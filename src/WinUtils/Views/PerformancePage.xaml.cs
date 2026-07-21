using System.Collections.ObjectModel;
using System.Windows.Controls;
using WinUtils.Services;

namespace WinUtils.Views;

public partial class PerformancePage : UserControl
{
    private readonly GamingOptimizer _optimizer = new();
    private readonly ObservableCollection<ProcessSummary> _processes = new();

    public PerformancePage()
    {
        InitializeComponent();
        ProcessList.ItemsSource = _processes;

        // The process list is context-only, so it piggybacks on the same scan pass.
        // ScanFunc runs on a background thread, hence the marshal back to the UI thread.
        GamingSection.ScanFunc = () =>
        {
            var procs = _optimizer.ScanProcesses();
            Dispatcher.Invoke(() =>
            {
                _processes.Clear();
                foreach (var p in procs)
                    _processes.Add(p);
            });
            return _optimizer.Scan();
        };
        GamingSection.ApplyAction = () => _optimizer.Apply();
        GamingSection.StatusChanged += (_, status) => GamingCard.Status = status;

        RefreshPowerPlan();
        RefreshIndexing();
    }

    private async void OnPowerPlanToggle(object sender, System.Windows.RoutedEventArgs _)
    {
        bool enabled = PowerPlanToggle.IsChecked == true;
        PowerPlanToggle.IsEnabled = false;
        PowerPlanError.IsOpen = false;

        try
        {
            await Task.Run(() => PowerPlanService.SetHighPerformance(enabled));
        }
        catch (Exception e)
        {
            PowerPlanError.Message = e.Message;
            PowerPlanError.IsOpen = true;
        }

        RefreshPowerPlan();
    }

    private void RefreshPowerPlan()
    {
        try
        {
            bool high = PowerPlanService.IsHighPerformance();
            PowerPlanToggle.IsChecked = high;
            PowerPlanToggle.IsEnabled = true;
            PowerPlanStateText.Text = high
                ? "On — the High performance plan is active."
                : "Off — Windows is using another plan (usually Balanced).";
            PowerPlanCard.Status = high ? "High performance" : "Balanced";
        }
        catch (Exception e)
        {
            PowerPlanToggle.IsEnabled = false;
            PowerPlanStateText.Text = "Unable to read the active power plan.";
            PowerPlanCard.Status = "State unavailable";
            PowerPlanError.Message = e.Message;
            PowerPlanError.IsOpen = true;
        }
    }

    private async void OnIndexingToggle(object sender, System.Windows.RoutedEventArgs _)
    {
        bool enabled = IndexingToggle.IsChecked == true;
        IndexingToggle.IsEnabled = false;
        IndexingError.IsOpen = false;

        try
        {
            await Task.Run(() => SearchIndexService.SetEnabled(enabled));
        }
        catch (Exception e)
        {
            IndexingError.Message = e.Message;
            IndexingError.IsOpen = true;
        }

        RefreshIndexing();
    }

    private void RefreshIndexing()
    {
        try
        {
            bool enabled = SearchIndexService.IsEnabled();
            IndexingToggle.IsChecked = enabled;
            IndexingToggle.IsEnabled = true;
            IndexingStateText.Text = enabled
                ? "On — WSearch indexes files in the background."
                : "Off — the indexer service is disabled.";
            IndexingCard.Status = enabled ? "Indexing on" : "Indexing off";
        }
        catch (Exception e)
        {
            IndexingToggle.IsEnabled = false;
            IndexingStateText.Text = "Unable to read the service state.";
            IndexingCard.Status = "State unavailable";
            IndexingError.Message = e.Message;
            IndexingError.IsOpen = true;
        }
    }
}
