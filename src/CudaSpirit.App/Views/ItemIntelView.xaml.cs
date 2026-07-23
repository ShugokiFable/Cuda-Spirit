using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class ItemIntelView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;

    public ItemIntelView()
    {
        InitializeComponent();
        BindingBox.ItemsSource = Enum.GetValues(typeof(ItemBinding));
        BindingBox.SelectedItem = ItemBinding.Unknown;
        var s = _hub.Settings.Current;
        StorageMaidBox.IsChecked = s.HasStorageMaid;
        TransactionMaidBox.IsChecked = s.HasTransactionMaid;
        MagnusBox.IsChecked = s.HasMagnusStorage;
        Refresh();
    }

    public void Refresh()
    {
        HistoryGrid.ItemsSource = _hub.Db.GetItemDecisionHistory(30).Select(x => new
        {
            x.ItemName, x.Verdict, x.Binding, When = x.CreatedAt.LocalDateTime.ToString("g")
        }).ToList();
    }

    private void OnAnalyze(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ItemNameBox.Text) && string.IsNullOrWhiteSpace(TooltipBox.Text))
        {
            VerdictText.Text = "Enter the exact name or paste the tooltip.";
            return;
        }
        var result = _hub.ItemSafety.Evaluate(new ItemGuidanceRequest
        {
            ItemName = ItemNameBox.Text.Trim(), TooltipText = TooltipBox.Text.Trim(),
            Binding = BindingBox.SelectedItem is ItemBinding b ? b : ItemBinding.Unknown,
            IsSeasonCharacter = SeasonBox.IsChecked == true
        });
        VerdictText.Text = result.Headline;
        ConfidenceText.Text = $"Confidence {result.ConfidencePercent}% · verdict {result.Verdict}";
        ExplanationText.Text = result.Explanation;
        LocationText.Text = result.BestLocation;
        WarningText.Text = $"{result.BindingWarning}\n{result.TransferAdvice}";
        ChecklistList.ItemsSource = result.BeforeYouAct;
        Refresh();
    }

    private async void OnAskAi(object sender, RoutedEventArgs e)
    {
        var name = ItemNameBox.Text.Trim();
        var tooltip = TooltipBox.Text.Trim();
        if (name.Length == 0 && tooltip.Length == 0)
        {
            VerdictText.Text = "Enter the exact name or paste the tooltip first.";
            return;
        }
        await _hub.Conversation.SendAsync($"Audit this exact Black Desert item before I act. Name: {name}. Tooltip: {tooltip}. Binding selected: {BindingBox.SelectedItem}. Tell me whether to open, keep, store, transfer, exchange, use, or sell; what could bind or expire; exact transfer methods; and every irreversible risk. Prefer current stored official knowledge and say when evidence is incomplete.");
        VerdictText.Text = "Detailed audit added to the shared AI Advisor conversation.";
    }

    private void OnTransfer(object sender, RoutedEventArgs e)
    {
        var result = _hub.Transfer.Evaluate(new TransferRequest
        {
            ItemName = ItemNameBox.Text.Trim(),
            Binding = BindingBox.SelectedItem is ItemBinding b ? b : ItemBinding.Unknown,
            IsMarketable = MarketableBox.IsChecked == true,
            IsTradeGood = TradeGoodBox.IsChecked == true,
            IsGuildItem = GuildItemBox.IsChecked == true,
            IsTreasureItem = TreasureItemBox.IsChecked == true,
            FamilyInventoryEligible = FamilyEligibleBox.IsChecked == true,
            HasStorageMaid = StorageMaidBox.IsChecked == true,
            HasTransactionMaid = TransactionMaidBox.IsChecked == true,
            MagnusStorageUnlocked = MagnusBox.IsChecked == true
        });
        TransferSummary.Text = result.Summary + (result.Blockers.Count > 0 ? "\n" + string.Join("\n", result.Blockers.Select(x => "BLOCKER: " + x)) : "");
        TransferSteps.ItemsSource = result.Steps;
    }
}
